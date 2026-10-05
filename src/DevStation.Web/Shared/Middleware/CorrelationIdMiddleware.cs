namespace DevStation.Web.Shared.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        var id = ctx.Request.Headers["X-Correlation-Id"] is { Count: > 0 } v && !string.IsNullOrEmpty(v[0])
            ? v[0]!
            : Guid.NewGuid().ToString("N");
        ctx.Items["CorrelationId"] = id;
        ctx.Response.Headers["X-Correlation-Id"] = id;
        await next(ctx);
    }
}
