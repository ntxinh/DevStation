using System.Net.Http.Headers;
using System.Text;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Enums;

namespace DevStation.Infrastructure.Integrations;

/// <summary>
/// Jira integration: config baseUrl (+ optional email); secret "token".
/// TestAsync → GET {baseUrl}/rest/api/2/myself. Basic auth (email:token) when
/// email is set — cloud — else Bearer — data-center PATs. No fake success.
/// </summary>
public sealed class JiraIntegration(ISecretStore secrets,
    IHttpClientFactory? httpFactory = null)
    : IntegrationBase("Jira", "jira", secrets,
        configKeys: ["baseUrl", "email"],
        secretKeys: ["token"])
{
    private static readonly HttpClient Fallback = new();

    protected override IReadOnlyList<string> RequiredConfigKeys => ["baseUrl"];

    public override Task ConfigureAsync(IntegrationConfig cfg,
        CancellationToken ct)
    {
        if (!cfg.TryGetValue("baseUrl", out var url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var u)
            || u.Scheme is not ("http" or "https"))
            throw new AppException("VALIDATION_ERROR", 400,
                "jira requires config baseUrl (absolute http(s) URL)");
        return base.ConfigureAsync(cfg, ct);
    }

    public override async Task<bool> TestAsync(CancellationToken ct)
    {
        var token = await Secret("token", ct);
        if (token is null || !Config.TryGetValue("baseUrl", out var baseUrl)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return Fail(IntegrationStatus.NotConfigured);

        try
        {
            var http = httpFactory?.CreateClient() ?? Fallback;
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{uri.ToString().TrimEnd('/')}/rest/api/2/myself");
            req.Headers.Authorization = Config.TryGetValue("email", out var email)
                && !string.IsNullOrWhiteSpace(email)
                ? new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}")))
                : new AuthenticationHeaderValue("Bearer", token);
            using var res = await http.SendAsync(req, ct);
            Status = res.IsSuccessStatusCode
                ? IntegrationStatus.Connected : IntegrationStatus.Error;
            return res.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return Fail(); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        { return Fail(); } // timeout → probe fails; caller cancellation propagates
    }
}
