using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Enums;
using DevStation.Infrastructure.GitHub;

namespace DevStation.Infrastructure.Integrations;

/// <summary>GitHub integration — real calls via GitHubApiClient; TestAsync = GET /user.</summary>
public sealed class GitHubIntegration(GitHubApiClient api, ISecretStore secrets)
    : IntegrationBase("GitHub", "github", secrets,
        configKeys: ["org", "defaultRepo"],
        secretKeys: ["token"])
{
    public override async Task<bool> TestAsync(CancellationToken ct)
    {
        try
        {
            var ok = await api.PingAsync(ct);
            Status = ok ? IntegrationStatus.Connected : IntegrationStatus.Error;
            return ok;
        }
        catch (AppException ex) when (ex.Code == "NOT_CONFIGURED")
        {
            Status = IntegrationStatus.NotConfigured;
            return false;
        }
        catch (AppException)
        {
            Status = IntegrationStatus.Error;
            return false;
        }
    }
}
