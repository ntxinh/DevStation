using DevStation.Application.Modules.Logs.GetLogs;
using DevStation.Application.Shared.Logging;
using DevStation.Application.Shared.Results;

namespace DevStation.Web.Modules.Logs;

public static class LogsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/logs", async (string? source, string? severity,
                Guid? environmentId, string? q, int? take,
                GetLogsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(
                new LogFilter(source, severity, environmentId, q,
                    take ?? 100), ct)));

        api.MapGet("/logs/export", async (string? source, string? severity,
                Guid? environmentId, string? q, int? take,
                GetLogsHandler h, CancellationToken ct) =>
        {
            var lines = await h.Handle(new LogFilter(source, severity,
                environmentId, q, take ?? 500), ct);
            var body = string.Join("\n",
                lines.Select(l =>
                    $"{l.Ts:O} {l.Level,-5} {l.Source,-10} {l.Message}")) + "\n";
            return Results.File(System.Text.Encoding.UTF8.GetBytes(body),
                "text/plain", "logs.txt");
        });
    }
}
