using System.Text.Json;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using DevStation.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DevStation.Application.Shared.Persistence;

namespace DevStation.Infrastructure.Persistence;

public class DevStationDbContext(DbContextOptions<DevStationDbContext> options)
    : DbContext(options), IDevStationDb
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<AppEnvironment> Environments => Set<AppEnvironment>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<DatabaseConnection> DatabaseConnections => Set<DatabaseConnection>();
    public DbSet<JobDefinition> JobDefinitions => Set<JobDefinition>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<HealthCheck> HealthChecks => Set<HealthCheck>();
    public DbSet<HealthCheckResult> HealthCheckResults => Set<HealthCheckResult>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SavedQuery> SavedQueries => Set<SavedQuery>();
    public DbSet<QueryHistoryEntry> QueryHistory => Set<QueryHistoryEntry>();

    // snake_case lives here (not in DI options) so ad-hoc DbContextOptions match the migration model
    protected override void OnConfiguring(DbContextOptionsBuilder ob) =>
        ob.UseSnakeCaseNamingConvention();

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        // enums as text for readable rows
        foreach (var t in new[] { typeof(HealthStatus), typeof(JobStatus), typeof(DeploymentStatus), typeof(IntegrationStatus) })
            cb.Properties(t).HaveConversion<string>().HaveMaxLength(32);

        // SecretRef stores only the key — column named per spec
        cb.Properties<SecretRef>().HaveConversion<SecretRefConverter>().HaveMaxLength(200);
    }

    private sealed class SecretRefConverter()
        : ValueConverter<SecretRef, string>(r => r.Key, k => new SecretRef(k));

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // secret refs → single `secret_ref` column (spec §5)
        mb.Entity<DatabaseConnection>().Property(c => c.PasswordRef).HasColumnName("secret_ref");
        mb.Entity<Integration>().Property(i => i.CredentialsRef).HasColumnName("secret_ref");
        mb.Entity<AppEnvironment>()
            .Property(e => e.Variables)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                s => JsonSerializer.Deserialize<List<EnvironmentVariable>>(s,
                    (JsonSerializerOptions?)null) ?? new List<EnvironmentVariable>(),
                new ValueComparer<List<EnvironmentVariable>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())),
                    v => v.ToList()));
        mb.Entity<AuditEvent>().Property(a => a.Metadata).HasColumnType("jsonb");
        mb.Entity<Notification>().Property(n => n.Metadata).HasColumnType("jsonb");
        mb.Entity<Integration>().Property(i => i.Config).HasColumnType("jsonb");
    }
}
