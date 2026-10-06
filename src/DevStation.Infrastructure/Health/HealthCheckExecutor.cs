using System.Diagnostics;
using System.Net.Sockets;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Enums;

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
            "database" => ProviderCheck("IDatabaseProvider", check.Target),
            "container" => ProviderCheck("IContainerProvider", check.Target),
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

    // ponytail: IDatabaseProvider/IContainerProvider land in Tasks 7/8 — resolved by name so
    // this code compiles before they exist. Registered but unprobed → Unknown either way.
    private (HealthStatus, string?) ProviderCheck(string providerName, string target)
    {
        var t = Type.GetType(
            $"DevStation.Application.Shared.Providers.{providerName}, DevStation.Application");
        var provider = t is null ? null : services.GetService(t);
        return (HealthStatus.Unknown, provider is null
            ? $"no {providerName} registered"
            : $"{providerName} registered but probe not wired");
    }
}
