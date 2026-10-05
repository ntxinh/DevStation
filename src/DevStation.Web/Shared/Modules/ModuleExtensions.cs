namespace DevStation.Web.Shared.Modules;

public static class ModuleExtensions
{
    /// <summary>The /api group every module hangs endpoints on, gated by the Read tier.</summary>
    public static RouteGroupBuilder MapModuleEndpoints(this WebApplication app)
        => app.MapGroup("/api").RequireAuthorization("Read");
}
