using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using DevStation.Application.Shared.Results;
using DevStation.Infrastructure.Containers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Api;

/// <summary>End-to-end: real podman container (busybox httpd) bound as a service —
/// restart must re-check health and persist the post-restart status.
/// Skips (early-return) when podman is absent or busybox can't be pulled.</summary>
public class ServicesEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public ServicesEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _pg.GetConnectionString(),
                    ["DevStation:Token"] = "test",
                })));
    }

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer test");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private static async Task<JsonElement> Data(HttpResponseMessage res)
    {
        var env = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(env.RootElement.GetProperty("data").ValueKind
            != JsonValueKind.Null, $"empty data: {env.RootElement}");
        return env.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task Restart_rechecks_and_persists_post_restart_health()
    {
        if (ContainerProviderDetector.Find("podman") is null) return; // no runtime
        var runner = new ContainerCliRunner();
        try { await runner.RunAsync("podman",
            ["pull", "-q", "docker.io/library/busybox:latest"], CancellationToken.None); }
        catch (AppException) { return; } // offline — skip

        var name = $"ds-svc-it-{Guid.NewGuid():N}"[..20];
        var hostPort = FreePort();
        await runner.RunAsync("podman", ["run", "-d", "--name", name,
            "-p", $"127.0.0.1:{hostPort}:8080",
            "docker.io/library/busybox:latest",
            "httpd", "-f", "-p", "8080"], CancellationToken.None);
        try
        {
            await WaitForTcp(hostPort);

            var created = await _client.PostAsJsonAsync("/api/services", new
            {
                name = "it-httpd",
                url = $"127.0.0.1:{hostPort}",
                containerId = name,
            });
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            var svcId = (await Data(created)).GetProperty("id").GetString()!;

            // restart deflaked: the probe can race busybox's first bind
            string status = "";
            for (var i = 0; i < 3 && status != "healthy"; i++)
            {
                var res = await _client.PostAsJsonAsync(
                    $"/api/services/{svcId}/restart?confirm=true", new { });
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                status = (await Data(res)).GetProperty("status").GetString()!;
                if (status != "healthy") await Task.Delay(500);
            }
            Assert.Equal("healthy", status);

            // persisted, not just returned
            var list = await Data(await _client.GetAsync("/api/services"));
            var row = list.EnumerateArray()
                .Single(s => s.GetProperty("id").GetString() == svcId);
            Assert.Equal("healthy", row.GetProperty("status").GetString());
        }
        finally
        {
            await runner.RunAsync("podman", ["rm", "-f", name], CancellationToken.None);
        }
    }

    [Fact]
    public async Task Restart_without_container_is_422_and_confirm_gate_applies()
    {
        var created = await _client.PostAsJsonAsync("/api/services", new
        {
            name = "it-no-container",
            url = "127.0.0.1:1",
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var svcId = (await Data(created)).GetProperty("id").GetString()!;

        var noConfirm = await _client.PostAsJsonAsync(
            $"/api/services/{svcId}/restart", new { });
        Assert.Equal(HttpStatusCode.BadRequest, noConfirm.StatusCode);

        var res = await _client.PostAsJsonAsync(
            $"/api/services/{svcId}/restart?confirm=true", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        var env = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("SERVICE_NOT_RESTARTABLE",
            env.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private static async Task WaitForTcp(int port)
    {
        for (var i = 0; i < 40; i++)
        {
            try
            {
                using var c = new TcpClient();
                await c.ConnectAsync(IPAddress.Loopback, port);
                return;
            }
            catch { await Task.Delay(250); }
        }
    }
}
