using System.Net.Http.Json;
using System.Text.Json;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Enums;

namespace DevStation.Infrastructure.Integrations;

/// <summary>
/// Azure integration: client-credentials (tenantId/clientId/clientSecret secrets)
/// + config subscriptionId. TestAsync gets a management token then lists
/// subscriptions — a real ARM round-trip. No fake success.
/// </summary>
public sealed class AzureIntegration(ISecretStore secrets,
    IHttpClientFactory? httpFactory = null)
    : IntegrationBase("Azure", "azure", secrets,
        configKeys: ["subscriptionId"],
        secretKeys: ["tenantId", "clientId", "clientSecret"])
{
    private static readonly HttpClient Fallback = new();

    public override async Task<bool> TestAsync(CancellationToken ct)
    {
        var tenantId = await Secret("tenantId", ct);
        var clientId = await Secret("clientId", ct);
        var clientSecret = await Secret("clientSecret", ct);
        if (tenantId is null || clientId is null || clientSecret is null)
            return Fail(IntegrationStatus.NotConfigured);

        var http = httpFactory?.CreateClient() ?? Fallback;
        try
        {
            using var tokenRes = await http.PostAsync(
                $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "https://management.azure.com/.default",
                }), ct);
            if (!tokenRes.IsSuccessStatusCode) return Fail();
            var token = (await tokenRes.Content
                    .ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
                .GetProperty("access_token").GetString();
            if (token is null) return Fail();

            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://management.azure.com/subscriptions?api-version=2020-01-01");
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var res = await http.SendAsync(req, ct);
            Status = res.IsSuccessStatusCode
                ? IntegrationStatus.Connected : IntegrationStatus.Error;
            return res.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return Fail(); }
        catch (TaskCanceledException) { return Fail(); }
        catch (JsonException) { return Fail(); }
        catch (KeyNotFoundException) { return Fail(); }
    }
}
