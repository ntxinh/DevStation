using System.Net;
using System.Net.Sockets;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Enums;
using DevStation.Infrastructure.Health;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Tests.Unit.Health;

public class HealthCheckExecutorTests
{
    private static HealthCheckExecutor Exec(IServiceProvider? sp = null)
        => new(sp ?? new ServiceCollection().BuildServiceProvider());

    private static HealthCheckDef Def(string kind, string target, int timeout = 2)
        => new(Guid.NewGuid(), kind, target, timeout);

    private static int FreeTcpPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static async Task<(string url, Task run)> HttpServer(
        int status = 200, int delayMs = 0, CancellationToken ct = default)
    {
        var port = FreeTcpPort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        var run = Task.Run(async () =>
        {
            try
            {
                var ctx = await listener.GetContextAsync();
                if (delayMs > 0) await Task.Delay(delayMs);
                ctx.Response.StatusCode = status;
                ctx.Response.Close();
            }
            catch { /* listener stopped */ }
            listener.Stop();
        }, ct);
        return ($"http://localhost:{port}/", run);
    }

    [Fact]
    public async Task Http_2xx_is_healthy()
    {
        var (url, run) = await HttpServer(200);
        var r = await Exec().Execute(Def("http", url), CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, r.Status);
        Assert.NotNull(r.LatencyMs);
        await run;
    }

    [Fact]
    public async Task Http_3xx_is_healthy()
    {
        var (url, run) = await HttpServer(302);
        var r = await Exec().Execute(Def("http", url), CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, r.Status);
        await run;
    }

    [Fact]
    public async Task Http_500_is_unhealthy()
    {
        var (url, run) = await HttpServer(500);
        var r = await Exec().Execute(Def("http", url), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, r.Status);
        Assert.NotNull(r.Error);
        await run;
    }

    [Fact]
    public async Task Http_unreachable_is_unhealthy()
    {
        var r = await Exec().Execute(Def("http", $"http://localhost:{FreeTcpPort()}/"), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, r.Status);
        Assert.NotNull(r.Error);
    }

    [Fact]
    public async Task Http_timeout_is_unhealthy()
    {
        var (url, run) = await HttpServer(200, delayMs: 3000);
        var r = await Exec().Execute(Def("http", url, timeout: 1), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, r.Status);
        await run;
    }

    [Fact]
    public async Task Http_bad_url_is_unhealthy()
    {
        var r = await Exec().Execute(Def("http", "not a url"), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, r.Status);
    }

    [Fact]
    public async Task Tcp_open_port_is_healthy()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        try
        {
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            var r = await Exec().Execute(Def("tcp", $"127.0.0.1:{port}"), CancellationToken.None);
            Assert.Equal(HealthStatus.Healthy, r.Status);
        }
        finally { l.Stop(); }
    }

    [Fact]
    public async Task Tcp_closed_port_is_unhealthy()
    {
        var r = await Exec().Execute(Def("tcp", $"127.0.0.1:{FreeTcpPort()}"), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, r.Status);
    }

    [Fact]
    public async Task Tcp_bad_target_is_unhealthy()
    {
        var r = await Exec().Execute(Def("tcp", "no-port-here"), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, r.Status);
        Assert.NotNull(r.Error);
    }

    [Fact]
    public async Task Database_without_provider_is_unknown()
    {
        var r = await Exec().Execute(Def("database", "pg-main"), CancellationToken.None);
        Assert.Equal(HealthStatus.Unknown, r.Status);
    }

    [Fact]
    public async Task Container_without_provider_is_unknown()
    {
        var r = await Exec().Execute(Def("container", "abc123"), CancellationToken.None);
        Assert.Equal(HealthStatus.Unknown, r.Status);
    }

    [Fact]
    public async Task Custom_is_always_unknown()
    {
        var r = await Exec().Execute(Def("custom", "anything"), CancellationToken.None);
        Assert.Equal(HealthStatus.Unknown, r.Status);
    }

    [Fact]
    public async Task Checked_at_is_set()
    {
        var r = await Exec().Execute(Def("custom", "x"), CancellationToken.None);
        Assert.True(DateTimeOffset.UtcNow - r.CheckedAt < TimeSpan.FromMinutes(1));
    }
}
