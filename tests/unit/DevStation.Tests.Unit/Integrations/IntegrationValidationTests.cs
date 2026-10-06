using System.Text.Json;
using DevStation.Application.Modules.Integrations;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using DevStation.Domain.ValueObjects;
using DevStation.Infrastructure.Integrations;

namespace DevStation.Tests.Unit.Integrations;

public class IntegrationValidationTests
{
    private sealed class FakeSecrets : ISecretStore
    {
        private readonly Dictionary<string, string> _map = new();
        public Task SetAsync(string k, string v, CancellationToken ct = default)
        { _map[k] = v; return Task.CompletedTask; }
        public Task<string?> GetAsync(string k, CancellationToken ct = default)
            => Task.FromResult(_map.GetValueOrDefault(k));
        public Task DeleteAsync(string k, CancellationToken ct = default)
        { _map.Remove(k); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Jira_configure_without_baseUrl_throws_validation()
    {
        var i = new JiraIntegration(new FakeSecrets());
        await Assert.ThrowsAsync<AppException>(
            () => i.ConfigureAsync([], CancellationToken.None));
    }

    [Fact]
    public async Task Jira_configure_with_baseUrl_succeeds()
    {
        var i = new JiraIntegration(new FakeSecrets());
        await i.ConfigureAsync(new() { ["baseUrl"] = "https://x.atlassian.net" },
            CancellationToken.None);
        Assert.Equal(IntegrationStatus.Configured, i.Status);
    }

    [Fact]
    public async Task Unconfigured_integration_test_returns_false()
    {
        var i = new CloudflareIntegration(new FakeSecrets());
        Assert.False(await i.TestAsync(CancellationToken.None));
        Assert.Equal(IntegrationStatus.NotConfigured, i.Status);
    }

    [Fact]
    public void DTO_serialization_never_contains_secret_value()
    {
        var i = new Integration
        {
            Name = "GitHub", Type = "github", Status = IntegrationStatus.Connected,
            Config = """{"org":"me"}""",
            CredentialsRef = new SecretRef("intg-github-token"),
        };
        var json = JsonSerializer.Serialize(
            IntegrationDto.Of(i, hasCredentials: true, [], []),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"hasCredentials\":true", json);
        Assert.DoesNotContain("secret-token-value", json);
        Assert.DoesNotContain("intg-github-token", json); // ref key stays server-side
    }
}
