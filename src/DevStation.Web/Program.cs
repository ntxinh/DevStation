using DevStation.Application.Modules.Containers;
using DevStation.Application.Modules.Ai;
using DevStation.Application.Modules.Mcp;
using DevStation.Application.Modules.Dashboard.GetDashboardSummary;
using DevStation.Application.Modules.Deployments;
using DevStation.Application.Modules.Databases;
using DevStation.Application.Modules.Environments;
using DevStation.Application.Modules.Health;
using DevStation.Application.Modules.Jobs;
using DevStation.Application.Modules.Projects;
using DevStation.Application.Modules.Logs;
using DevStation.Application.Modules.Services;
using DevStation.Application.Modules.Commands;
using DevStation.Application.Modules.Notifications;
using DevStation.Application.Modules.Integrations;
using DevStation.Application.Shared.Notifications;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Logging;
using DevStation.Infrastructure;

using DevStation.Web.Hubs;
using DevStation.Web.Modules.Containers;
using DevStation.Web.Modules.Dashboard;
using DevStation.Web.Modules.Databases;
using DevStation.Web.Modules.Deployments;
using DevStation.Web.Modules.Environments;
using DevStation.Web.Modules.Health;
using DevStation.Web.Modules.Jobs;
using DevStation.Web.Modules.Logs;
using DevStation.Web.Modules.Audit;
using DevStation.Web.Modules.Search;
using DevStation.Web.Modules.Commands;
using DevStation.Web.Modules.Notifications;
using DevStation.Web.Modules.Integrations;
using DevStation.Web.Modules.Projects;
using DevStation.Web.Modules.Services;
using DevStation.Web.Modules.Ai;
using DevStation.Web.Modules.Mcp;
using DevStation.Web.Shared.Auth;
using DevStation.Web.Shared.Middleware;
using DevStation.Web.Shared.Modules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var b = WebApplication.CreateBuilder(args);

// env bridges: DEVSTATION_TOKEN -> DevStation:Token,
// DEVSTATION_CONNECTIONSTRING -> ConnectionStrings:Default
b.Configuration["DevStation:Token"] ??= b.Configuration["DEVSTATION_TOKEN"];
b.Configuration["ConnectionStrings:Default"] ??= b.Configuration["DEVSTATION_CONNECTIONSTRING"];

// opt-in OpenTelemetry (console exporter only — no OTLP dependency).
// Observability:OpenTelemetry:Enabled in config, or OTEL_ENABLED=true env
var otelEnabled = b.Configuration.GetValue("Observability:OpenTelemetry:Enabled", false)
    || string.Equals(b.Configuration["OTEL_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
if (otelEnabled)
{
    b.Services.AddOpenTelemetry()
        .WithTracing(t => t.AddAspNetCoreInstrumentation().AddConsoleExporter())
        .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddConsoleExporter());
}

// web UI runs on a different origin (localhost:3000 → :5080); allow loopback
// origins only — auth is still enforced by the token on every request
b.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(origin =>
        Uri.TryCreate(origin, UriKind.Absolute, out var u) && u.IsLoopback)
    .AllowAnyHeader()
    .AllowAnyMethod()));

b.Services.AddAuthentication("DevToken")
    .AddScheme<AuthenticationSchemeOptions, DevTokenAuthenticationHandler>("DevToken", _ => { });
b.Services.AddAuthorization(o =>
{
    foreach (var t in new[] { "Read", "Execute", "Deploy", "Admin" })
        o.AddPolicy(t, p => p.Requirements.Add(new TierRequirement(t.ToLowerInvariant())));
});
b.Services.AddSingleton<IAuthorizationHandler, TierHandler>();
b.Services.AddSignalR();
b.Services.AddSingleton<IHealthCheckNotifier, SignalRHealthNotifier>();
b.Services.AddSingleton<IDeploymentNotifier, SignalRDeploymentNotifier>();
b.Services.AddSingleton<IJobNotifier, SignalRJobNotifier>();
b.Services.AddSingleton<ILogNotifier, SignalRLogNotifier>();
b.Services.AddSingleton<INotificationNotifier, SignalRNotificationNotifier>();
b.Services.AddInfrastructure(b.Configuration);
b.Services.AddProjectsModule().AddDashboardModule()
    .AddEnvironmentsModule().AddHealthModule()
    .AddServicesModule().AddContainersModule()
    .AddDatabasesModule()
    .AddJobsModule()
    .AddDeploymentsModule()
    .AddCommandsModule()
    .AddNotificationsModule()
    .AddIntegrationsModule()
    .AddLogsModule()
    .AddAiModule()
    .AddMcpModule();

var app = b.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseMiddleware<RequestTimingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", uptime = Environment.TickCount64 }))
    .AllowAnonymous();

var api = app.MapModuleEndpoints();
ProjectsEndpoints.Map(api);
EnvironmentsEndpoints.Map(api);
HealthEndpoints.Map(api);
DashboardEndpoints.Map(api);
ServicesEndpoints.Map(api);
ContainersEndpoints.Map(api);
DatabasesEndpoints.Map(api);
JobsEndpoints.Map(api);
DeploymentsEndpoints.Map(api);
LogsEndpoints.Map(api);
AuditEndpoints.Map(api);
SearchEndpoints.Map(api);
CommandsEndpoints.Map(api);
NotificationsEndpoints.Map(api);
IntegrationsEndpoints.Map(api);
AiEndpoints.Map(api);
McpEndpoints.Map(api);
app.MapHub<HealthHub>("/hubs/health").RequireAuthorization("Read");
app.MapHub<JobsHub>("/hubs/jobs").RequireAuthorization("Read");
app.MapHub<DeploymentsHub>("/hubs/deployments").RequireAuthorization("Read");
app.MapHub<LogsHub>("/hubs/logs").RequireAuthorization("Read");
app.MapHub<NotificationsHub>("/hubs/notifications").RequireAuthorization("Read");

app.Run();

public partial class Program { } // for WebApplicationFactory
