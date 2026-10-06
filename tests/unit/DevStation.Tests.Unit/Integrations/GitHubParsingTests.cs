using System.Text.Json;
using DevStation.Infrastructure.GitHub;

namespace DevStation.Tests.Unit.Integrations;

public class GitHubParsingTests
{
    // recorded fixtures (trimmed shapes of real api.github.com payloads)
    private const string ReposJson = """
        [
          {
            "id": 123, "name": "devstation", "full_name": "me/devstation",
            "private": true, "description": "control plane",
            "html_url": "https://github.com/me/devstation",
            "pushed_at": "2026-10-01T12:34:56Z"
          },
          {
            "id": 124, "name": "other", "full_name": "me/other",
            "private": false, "description": null,
            "html_url": "https://github.com/me/other",
            "pushed_at": null
          }
        ]
        """;

    private const string PullsJson = """
        [
          {
            "number": 42, "title": "Add integrations", "state": "open",
            "draft": false, "html_url": "https://github.com/me/devstation/pull/42",
            "created_at": "2026-10-02T08:00:00Z",
            "user": { "login": "octocat" }
          }
        ]
        """;

    private const string RunsJson = """
        {
          "total_count": 1,
          "workflow_runs": [
            {
              "id": 99, "name": "CI", "status": "completed",
              "conclusion": "success", "head_branch": "main",
              "html_url": "https://github.com/me/devstation/actions/runs/99",
              "created_at": "2026-10-03T10:00:00Z"
            }
          ]
        }
        """;

    [Fact]
    public void ParseRepos_maps_recorded_fields()
    {
        var repos = GitHubApiClient.ParseRepos(ReposJson);
        Assert.Equal(2, repos.Count);
        var r = repos[0];
        Assert.Equal(123, r.Id);
        Assert.Equal("devstation", r.Name);
        Assert.Equal("me/devstation", r.FullName);
        Assert.True(r.Private);
        Assert.Equal("control plane", r.Description);
        Assert.Equal("https://github.com/me/devstation", r.HtmlUrl);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T12:34:56Z"), r.PushedAt);
        Assert.Null(repos[1].Description);
        Assert.Null(repos[1].PushedAt);
    }

    [Fact]
    public void ParsePulls_maps_number_title_user()
    {
        var pulls = GitHubApiClient.ParsePulls(PullsJson);
        var p = Assert.Single(pulls);
        Assert.Equal(42, p.Number);
        Assert.Equal("Add integrations", p.Title);
        Assert.Equal("octocat", p.User);
        Assert.Equal("open", p.State);
        Assert.False(p.Draft);
        Assert.Equal("https://github.com/me/devstation/pull/42", p.HtmlUrl);
    }

    [Fact]
    public void ParseRuns_maps_workflow_runs_envelope()
    {
        var runs = GitHubApiClient.ParseRuns(RunsJson);
        var r = Assert.Single(runs);
        Assert.Equal(99, r.Id);
        Assert.Equal("CI", r.Name);
        Assert.Equal("completed", r.Status);
        Assert.Equal("success", r.Conclusion);
        Assert.Equal("main", r.HeadBranch);
    }

    [Fact]
    public void ParseRuns_empty_envelope_yields_empty_list()
        => Assert.Empty(GitHubApiClient.ParseRuns("""{"total_count":0,"workflow_runs":[]}"""));

    [Fact]
    public void Parse_throws_on_malformed_json()
        => Assert.Throws<JsonException>(() => GitHubApiClient.ParseRepos("{not json"));
}
