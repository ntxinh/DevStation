using System.Net.Http.Headers;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Enums;

namespace DevStation.Infrastructure.Integrations;

/// <summary>Cloudflare integration: secret "token" (API token).
/// TestAsync → GET /client/v4/user/tokens/verify — the canonical
/// least-privilege probe. No fake success.</summary>
public sealed class CloudflareIntegration(ISecretStore secrets,
    IHttpClientFactory? httpFactory = null)
    : IntegrationBase("Cloudflare", "cloudflare", secrets,
        configKeys: ["accountId"],
        secretKeys: ["token"])
{
    private static readonly HttpClient Fallback = new();

    public override async Task<bool> TestAsync(CancellationToken ct)
    {
        var token = await Secret("token", ct);
        if (token is null) return false; // Secret() already set NotConfigured

        try
        {
            var http = httpFactory?.CreateClient() ?? Fallback;
            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://api.cloudflare.com/client/v4/user/tokens/verify");
            req.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            var res = await http.SendAsync(req, ct);
            Status = res.IsSuccessStatusCode
                ? IntegrationStatus.Connected : IntegrationStatus.Error;
            return res.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return Fail(); }
        catch (TaskCanceledException) { return Fail(); }
    }
}
