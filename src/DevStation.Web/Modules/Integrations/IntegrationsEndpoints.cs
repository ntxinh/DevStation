using System.Security.Claims;
using DevStation.Application.Modules.Integrations.ConnectIntegration;
using DevStation.Application.Modules.Integrations.DisconnectIntegration;
using DevStation.Application.Modules.Integrations.ListIntegrations;
using DevStation.Application.Modules.Integrations.TestIntegration;
using DevStation.Application.Shared.Results;
using DevStation.Infrastructure.GitHub;
using DevStation.Web.Shared.Modules;

namespace DevStation.Web.Modules.Integrations;

public static class IntegrationsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/integrations", async (ListIntegrationsHandler h,
                CancellationToken ct) => ApiResult.Ok(await h.Handle(ct)));

        // connect/test/disconnect mutate config + secrets → Admin per spec
        api.MapPost("/integrations/{type}/connect", async (string type,
                ConnectIntegrationRequest r, ConnectIntegrationHandler h,
                ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(type, r, u, ct)))
            .RequireAuthorization("Admin");
        api.MapPost("/integrations/{type}/test", async (string type,
                TestIntegrationHandler h, ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(type, u, ct)))
            .RequireAuthorization("Admin");
        api.MapPost("/integrations/{type}/disconnect", async (string type,
                ConfirmRequest r, DisconnectIntegrationHandler h,
                ClaimsPrincipal u, CancellationToken ct)
            => { ConfirmGate.Require(r.Confirm);
                 await h.Handle(type, u, ct);
                 return ApiResult.Ok(new { disconnected = true }); })
            .RequireAuthorization("Admin");

        // GitHub proxy reads — upstream failures surface as 502 INTEGRATION_ERROR
        api.MapGet("/integrations/github/repos", async (GitHubApiClient gh,
                CancellationToken ct)
            => ApiResult.Ok(await gh.ListReposAsync(ct)));
        api.MapGet("/integrations/github/{owner}/{repo}/pulls", async (
                string owner, string repo, GitHubApiClient gh, CancellationToken ct)
            => ApiResult.Ok(await gh.ListPullRequestsAsync(owner, repo, ct)));
        api.MapGet("/integrations/github/{owner}/{repo}/workflow-runs", async (
                string owner, string repo, GitHubApiClient gh, CancellationToken ct)
            => ApiResult.Ok(await gh.ListWorkflowRunsAsync(owner, repo, ct)));
    }
}
