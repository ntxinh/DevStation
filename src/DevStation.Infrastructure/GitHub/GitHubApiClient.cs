using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;

namespace DevStation.Infrastructure.GitHub;

public sealed record GitHubRepo(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("private")] bool Private,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("pushed_at")] DateTimeOffset? PushedAt);

public sealed record GitHubPullRequest(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("user")] GitHubUser? User);

public sealed record GitHubUser([property: JsonPropertyName("login")] string Login);

public sealed record GitHubWorkflowRun(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("conclusion")] string? Conclusion,
    [property: JsonPropertyName("head_branch")] string HeadBranch,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

/// <summary>
/// Thin proxy over api.github.com. Token comes from ISecretStore
/// (<c>intg-github-token</c>) at call time; the Authorization header is set
/// per-request so it is never captured in stored requests or logs. Upstream
/// failures surface as 502 INTEGRATION_ERROR with status only — response
/// bodies are never echoed (they can contain credentials).
/// </summary>
public sealed class GitHubApiClient(HttpClient http, ISecretStore secrets)
{
    private static string SecretKey => IntegrationCatalog.SecretKey("github", "token");

    /// <summary>Builds an authed request — headers live on the request, never
    /// on the shared HttpClient (no races, nothing to leak into logs).</summary>
    private async Task<HttpRequestMessage> Request(string path,
        CancellationToken ct)
    {
        var token = await secrets.GetAsync(SecretKey, ct)
            ?? throw new AppException("NOT_CONFIGURED", 400,
                "github integration has no stored token");
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com{path}");
        req.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("devstation"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private async Task<string> GetJson(string path, CancellationToken ct)
    {
        using var req = await Request(path, ct);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw new AppException("INTEGRATION_ERROR", 502,
                $"github api returned {(int)res.StatusCode} {res.ReasonPhrase}");
        return await res.Content.ReadAsStringAsync(ct);
    }

    public async Task<List<GitHubRepo>> ListReposAsync(CancellationToken ct)
        => ParseRepos(await GetJson(
            "/user/repos?sort=pushed&direction=desc&per_page=30", ct));

    public async Task<List<GitHubPullRequest>> ListPullRequestsAsync(
        string owner, string repo, CancellationToken ct)
        => ParsePulls(await GetJson(
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/pulls?state=open&per_page=20",
            ct));

    public async Task<List<GitHubWorkflowRun>> ListWorkflowRunsAsync(
        string owner, string repo, CancellationToken ct)
        => ParseRuns(await GetJson(
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/actions/runs?per_page=20",
            ct));

    /// <summary>TestAsync probe: GET /user must succeed.</summary>
    public async Task<bool> PingAsync(CancellationToken ct)
    {
        try
        {
            using var req = await Request("/user", ct);
            using var res = await http.SendAsync(req, ct);
            return res.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        { return false; } // timeout → probe fails; caller cancellation propagates
    }

    // --- pure parsers (unit-tested against recorded fixtures) ---

    public static List<GitHubRepo> ParseRepos(string json)
        => JsonSerializer.Deserialize<List<GitHubRepo>>(json) ?? [];

    public static List<GitHubPullRequest> ParsePulls(string json)
        => JsonSerializer.Deserialize<List<GitHubPullRequest>>(json) ?? [];

    public static List<GitHubWorkflowRun> ParseRuns(string json)
        => JsonSerializer.Deserialize<WorkflowRunEnvelope>(json)?.Runs ?? [];

    private sealed record WorkflowRunEnvelope(
        [property: JsonPropertyName("workflow_runs")] List<GitHubWorkflowRun> Runs);
}
