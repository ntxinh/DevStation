using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Logging;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Notifications;
using DevStation.Application.Shared.Secrets;
using DevStation.Infrastructure.Auditing;
using DevStation.Infrastructure.Containers;
using DevStation.Infrastructure.Databases;
using DevStation.Infrastructure.Deployments;
using DevStation.Infrastructure.Git;
using DevStation.Infrastructure.GitHub;
using DevStation.Infrastructure.Health;
using DevStation.Infrastructure.Integrations;
using DevStation.Infrastructure.Jobs;
using DevStation.Infrastructure.Logging;
using DevStation.Infrastructure.Logs;
using DevStation.Infrastructure.Notifications;
using DevStation.Infrastructure.Persistence;
using DevStation.Infrastructure.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DevStation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // UseSnakeCaseNamingConvention is applied in DevStationDbContext.OnConfiguring
        services.AddDbContext<DevStationDbContext>(o =>
            o.UseNpgsql(config.GetConnectionString("Default")));
        services.AddHostedService<MigrationHostedService>();
        services.AddScoped<IDevStationDb>(sp => sp.GetRequiredService<DevStationDbContext>());
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IGitProvider, LibGit2SharpGitProvider>();
        services.AddSingleton<IHealthCheckExecutor, HealthCheckExecutor>();
        services.AddHostedService<HealthCheckPoller>();
        services.AddSingleton<ContainerCliRunner>();
        services.AddSingleton<IContainerProvider>(sp =>
            ContainerProviderDetector.Detect(sp.GetRequiredService<ContainerCliRunner>()));
        services.AddSingleton<IDatabaseProvider, PostgreSqlProvider>();
        services.AddSingleton<IDatabaseProvider, SqlServerProvider>();
        services.AddSingleton<ISecretStore, EnvSecretStore>();

        services.AddSingleton<LogBuffer>();
        services.AddSingleton<ILogSink>(sp => sp.GetRequiredService<LogBuffer>());
        services.AddSingleton<ILogQuery>(sp => sp.GetRequiredService<LogBuffer>());
        services.AddSingleton<ILoggerProvider>(sp => new BufferedLoggerProvider(
            sp.GetRequiredService<ILogSink>(),
            Enum.TryParse<LogLevel>(config["Logging:Buffered:MinLevel"],
                true, out var lvl) ? lvl : LogLevel.Information));
        services.AddHttpClient();

        // Deployments:Provider = mock|github (default mock)
        var mockOptions = new MockDeploymentOptions();
        config.GetSection("Deployments:Mock").Bind(mockOptions);
        services.AddSingleton(mockOptions);
        services.AddSingleton<IDeploymentProvider>(sp =>
        {
            // github only when configured AND a token exists — else mock
            var useGithub =
                config["Deployments:Provider"]?.Equals("github",
                    StringComparison.OrdinalIgnoreCase) == true
                && !string.IsNullOrEmpty(sp.GetRequiredService<ISecretStore>()
                    .GetAsync("github.token").GetAwaiter().GetResult());
            IDeploymentProvider provider = useGithub
                ? new GitHubActionsDeploymentProvider(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<ISecretStore>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    config,
                    sp.GetRequiredService<ILogSink>(),
                    sp.GetService<IDeploymentNotifier>(),
                    sp.GetService<ILogger<GitHubActionsDeploymentProvider>>())
                : new MockDeploymentProvider(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<ILogSink>(),
                    sp.GetService<IDeploymentNotifier>(),
                    mockOptions,
                    sp.GetService<ILogger<MockDeploymentProvider>>());
            return provider;
        });

        // integrations — GitHubApiClient is a typed HttpClient; the providers
        // wrap it (singletons own in-memory configured state)
        services.AddHttpClient<GitHubApiClient>();
        services.AddSingleton<GitHubIntegration>();
        services.AddSingleton<JiraIntegration>();
        services.AddSingleton<AzureIntegration>();
        services.AddSingleton<CloudflareIntegration>();
        services.AddSingleton<IIntegration>(sp => sp.GetRequiredService<GitHubIntegration>());
        services.AddSingleton<IIntegration>(sp => sp.GetRequiredService<JiraIntegration>());
        services.AddSingleton<IIntegration>(sp => sp.GetRequiredService<AzureIntegration>());
        services.AddSingleton<IIntegration>(sp => sp.GetRequiredService<CloudflareIntegration>());

        // jobs — Quartz in-memory store; job_definitions/executions are the DB view
        IReadOnlyList<JobRegistration> jobs =
            config.GetSection("Jobs").Get<List<JobRegistration>>() ?? [];
        services.AddSingleton(jobs);
        services.AddSingleton<IJobProvider, QuartzJobProvider>();
        services.AddQuartz(q =>
        {
            q.AddJobListener(sp => new AuditingJobListener(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AuditingJobListener>>()),
                []);
            foreach (var reg in jobs)
            {
                q.AddJob(reg.JobType, j => j
                    .WithIdentity(reg.Name)
                    .WithDescription(reg.Description ?? ""));
                q.AddTrigger(t => t
                    .WithIdentity($"{reg.Name}-trigger")
                    .ForJob(reg.Name)
                    .WithSchedule(reg.Schedule));
            }
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        services.AddHostedService<JobDefinitionSync>(); // after migrations (order matters)
        return services;
    }
}
