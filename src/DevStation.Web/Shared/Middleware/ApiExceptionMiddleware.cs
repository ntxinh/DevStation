using DevStation.Application.Shared.Results;

namespace DevStation.Web.Shared.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            var (code, status, message) = ex switch
            {
                AppException a => (a.Code, a.Status, a.Message),
                _ => ("INTERNAL_ERROR", 500, "An unexpected error occurred"),
            };
            var dev = ctx.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
            if (dev)
                message = $"{message} ({ex.GetType().Name}: {ex.Message})";
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new ApiEnvelope<object>(null, new ApiError(code, message)));
        }
    }
}
