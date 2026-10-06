using DevStation.Application.Shared.Logging;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;

namespace DevStation.Application.Modules.Deployments.GetDeployment;

public sealed class GetDeploymentHandler(IDevStationDb db,
    IDeploymentProvider provider, ILogQuery? logQuery = null)
{
    /// <summary>Detail + log lines — from the ILogSink buffer (tagged
    /// Meta["deploymentId"]) with provider logs as fallback.</summary>
    public async Task<DeploymentDto> Handle(Guid id, CancellationToken ct)
    {
        var sinkLines = logQuery?.ForDeployment(id);
        var logs = sinkLines is { Count: > 0 }
            ? sinkLines.OrderBy(e => e.Ts).Select(e => e.Message).ToList()
            : (await provider.GetStatusAsync(id, ct)).Logs.ToList();

        var dto = await DeploymentsModule.Detail(db, id, logs, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Deployment not found");
        return dto;
    }
}
