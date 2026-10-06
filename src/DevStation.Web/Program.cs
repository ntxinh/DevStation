using DevStation.Application.Modules.Containers;
using DevStation.Application.Modules.Dashboard.GetDashboardSummary;
using DevStation.Application.Modules.Deployments;
using DevStation.Application.Modules.Databases;
using DevStation.Application.Modules.Environments;
using DevStation.Application.Modules.Health;
using DevStation.Application.Modules.Jobs;
using DevStation.Application.Modules.Projects;
using DevStation.Application.Modules.Logs;
using DevStation.Application.Modules.Services;
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
using DevStation.Web.Modules.Projects;
using DevStation.Web.Modules.Services;
using DevStation.Web.Shared.Auth;
using DevStation.Web.Shared.Middleware;
using DevStation.Web.Shared.Modules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var b = WebApplication.CreateBuilder(args);

// env DEVSTATION_TOKEN -> DevStation:Token
b.Configuration["DevStation:Token"] ??= b.Configuration["DEVSTATION_TOKEN"];

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
b.Services.AddInfrastructure(b.Configuration);
b.Services.AddProjectsModule().AddDashboardModule()
    .AddEnvironmentsModule().AddHealthModule()
    .AddServicesModule().AddContainersModule()
    .AddDatabasesModule()
    .AddJobsModule()
    .AddDeploymentsModule()
    .AddLogsModule();

var app = b.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
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
app.MapHub<HealthHub>("/hubs/health").RequireAuthorization("Read");
app.MapHub<JobsHub>("/hubs/jobs").RequireAuthorization("Read");
app.MapHub<DeploymentsHub>("/hubs/deployments").RequireAuthorization("Read");
app.MapHub<LogsHub>("/hubs/logs").RequireAuthorization("Read");

app.Run();

public partial class Program { } // for WebApplicationFactory
