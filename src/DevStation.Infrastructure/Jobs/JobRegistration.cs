using Quartz;

namespace DevStation.Infrastructure.Jobs;

/// <summary>One entry of the `Jobs:` config array — name + cron + optional timezone
/// + a type key into <see cref="BuiltInJobs.Registry"/>.</summary>
public sealed class JobRegistration
{
    public string Name { get; set; } = "";
    public string Cron { get; set; } = "";
    public string? Timezone { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } = "";

    public JobKey Key => new(Name);

    public Type JobType => BuiltInJobs.Registry.TryGetValue(Type, out var t)
        ? t
        : throw new InvalidOperationException(
            $"jobs: '{Name}' has unknown type '{Type}' — expected one of: " +
            string.Join(", ", BuiltInJobs.Registry.Keys.OrderBy(k => k)));

    public CronScheduleBuilder Schedule => string.IsNullOrEmpty(Timezone)
        ? CronScheduleBuilder.Create(Cron)
        : CronScheduleBuilder.Create(Cron).InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(Timezone));
}
