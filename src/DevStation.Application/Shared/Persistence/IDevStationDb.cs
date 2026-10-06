using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Shared.Persistence;

/// <summary>Seam so Application handlers don't reference Infrastructure.</summary>
public interface IDevStationDb : IAsyncDisposable
{
    DbSet<Project> Projects { get; }
    DbSet<AppEnvironment> Environments { get; }
    DbSet<Service> Services { get; }
    DbSet<DatabaseConnection> DatabaseConnections { get; }
    DbSet<JobDefinition> JobDefinitions { get; }
    DbSet<JobExecution> JobExecutions { get; }
    DbSet<Deployment> Deployments { get; }
    DbSet<HealthCheck> HealthChecks { get; }
    DbSet<HealthCheckResult> HealthCheckResults { get; }
    DbSet<Integration> Integrations { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<SavedQuery> SavedQueries { get; }
    DbSet<QueryHistoryEntry> QueryHistory { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
