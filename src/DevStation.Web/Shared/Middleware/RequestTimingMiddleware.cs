using System.Diagnostics;

namespace DevStation.Web.Shared.Middleware;

/// <summary>
/// Logs "method path -> status in Nms" at Information for every request
/// except /health (it gets polled constantly; logging it is pure noise).
/// </summary>
public sealed class RequestTimingMiddleware(
    RequestDelegate next,
    ILogger<RequestTimingMiddleware> logger)
{
    public async Task Invoke(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments("/health"))
        {
            await next(ctx);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await next(ctx);
        }
        finally
        {
            sw.Stop();
            logger.LogInformation(
                "{Method} {Path} -> {Status} in {ElapsedMs} ms",
                ctx.Request.Method,
                ctx.Request.Path,
                ctx.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
    }
}
