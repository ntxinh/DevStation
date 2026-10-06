using DevStation.Application.Modules.Jobs.GetJob;
using DevStation.Application.Modules.Jobs.ListExecutions;
using DevStation.Application.Modules.Jobs.ListJobs;
using DevStation.Application.Modules.Jobs.PauseJob;
using DevStation.Application.Modules.Jobs.ResumeJob;
using DevStation.Application.Modules.Jobs.TriggerJob;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Jobs;

public static class JobsModule
{
    public static IServiceCollection AddJobsModule(this IServiceCollection s) => s
        .AddScoped<ListJobsHandler>()
        .AddScoped<GetJobHandler>()
        .AddScoped<ListExecutionsHandler>()
        .AddScoped<TriggerJobHandler>()
        .AddScoped<PauseJobHandler>()
        .AddScoped<ResumeJobHandler>();
}
