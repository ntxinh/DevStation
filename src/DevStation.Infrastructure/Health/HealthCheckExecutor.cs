using System.Diagnostics;
using System.Net.Sockets;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Infrastructure.Health;

public sealed class HealthCheckExecutor(IServiceProvider services) : IHealthCheckExecutor
{
    // singleton — one shared HttpClient, per-request timeout via linked CTS
    private static readonly HttpClient Http = new();

    public async Task<HealthResult> Execute(HealthCheckDef check, CancellationToken ct)
    {
        var timeout = check.TimeoutSeconds is > 0 ? check.TimeoutSeconds : 5;
        var sw = Stopwatch.StartNew();
        var (status, error) = check.Kind switch
        {
            "http" => await CheckHttp(check.Target, timeout, ct),
            "tcp" => await CheckTcp(check.Target, timeout, ct),
            "database" => await CheckDatabase(check.Target, timeout, ct),
            "container" => await CheckContainer(check.Target, ct),
            // ponytail: custom checks need user-supplied probe code — sandbox/scripting not built
            _ => (HealthStatus.Unknown, "custom checks not evaluated"),
        };
        return new HealthResult(status, (int?)sw.ElapsedMilliseconds, error,
            DateTimeOffset.UtcNow);
    }

    private static async Task<(HealthStatus, string?)> CheckHttp(
        string target, int timeout, CancellationToken ct)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            return (HealthStatus.Unhealthy, $"invalid http target '{target}'");
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));
            using var res = await Http.GetAsync(uri, cts.Token);
            return (int)res.StatusCode < 400
                ? (HealthStatus.Healthy, null)
                : (HealthStatus.Unhealthy, $"HTTP {(int)res.StatusCode}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (HealthStatus.Unhealthy, $"timeout after {timeout}s");
        }
        catch (Exception ex)
        {
            return (HealthStatus.Unhealthy, ex.Message);
        }
    }

    private static async Task<(HealthStatus, string?)> CheckTcp(
        string target, int timeout, CancellationToken ct)
    {
        var i = target.LastIndexOf(':');
        if (i <= 0 || !int.TryParse(target[(i + 1)..], out var port) || port is < 1 or > 65535)
            return (HealthStatus.Unhealthy, $"invalid tcp target '{target}' (want host:port)");
        var host = target[..i];
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cts.Token);
            return (HealthStatus.Healthy, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (HealthStatus.Unhealthy, $"timeout after {timeout}s");
        }
        catch (Exception ex)
        {
            return (HealthStatus.Unhealthy, ex.Message);
        }
    }

    private async Task<(HealthStatus, string?)> CheckContainer(
        string target, CancellationToken ct)
    {
        var provider = services.GetService<IContainerProvider>();
        if (provider is null)
            return (HealthStatus.Unknown, "no IContainerProvider registered");
        try
        {
            var c = await provider.InspectAsync(target, ct);
            return c.State is "running"
                ? (HealthStatus.Healthy, null)
                : (HealthStatus.Unhealthy, $"container {c.State}");
        }
        catch (AppException e) { return (HealthStatus.Unknown, e.Message); }
    }

    private async Task<(HealthStatus, string?)> CheckDatabase(
        string target, int timeout, CancellationToken ct)
    {
        // target = connection name or id — resolved against the conn registry;
        // scoped deps need a scope (executor is a singleton)
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<IDevStationDb>();
        var providers = scope.ServiceProvider
            .GetServices<IDatabaseProvider>().ToList();
        var secrets = scope.ServiceProvider.GetService<ISecretStore>();
        if (db is null || secrets is null)
            return (HealthStatus.Unknown, "database probe deps not registered");
        var conn = Guid.TryParse(target, out var gid)
            ? await db.DatabaseConnections
                .FirstOrDefaultAsync(x => x.Id == gid, ct)
            : await db.DatabaseConnections
                .FirstOrDefaultAsync(x => x.Name == target, ct);
        if (conn is null)
            return (HealthStatus.Unhealthy, $"no database connection '{target}'");
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));
            var pw = conn.PasswordRef is null
                ? null
                : await secrets.GetAsync(conn.PasswordRef.Key, cts.Token);
            var ok = await providers.For(conn.Kind)
                .TestConnectionAsync(conn, pw, cts.Token);
            return ok ? (HealthStatus.Healthy, null)
                      : (HealthStatus.Unhealthy, "probe returned false");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (HealthStatus.Unhealthy, $"timeout after {timeout}s");
        }
        catch (Exception ex)
        {
            return (HealthStatus.Unhealthy, ex.Message);
        }
    }
}
