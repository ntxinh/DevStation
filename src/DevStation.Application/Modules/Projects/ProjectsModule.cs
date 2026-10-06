using DevStation.Application.Modules.Projects.CreateProject;
using DevStation.Application.Modules.Projects.DeleteProject;
using DevStation.Application.Modules.Projects.GetGitStatus;
using DevStation.Application.Modules.Projects.GetProject;
using DevStation.Application.Modules.Projects.GetRecentCommits;
using DevStation.Application.Modules.Projects.ListProjects;
using DevStation.Application.Modules.Projects.UpdateProject;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Projects;

public static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection s) => s
        .AddScoped<CreateProjectHandler>()
        .AddScoped<ListProjectsHandler>()
        .AddScoped<GetProjectHandler>()
        .AddScoped<UpdateProjectHandler>()
        .AddScoped<DeleteProjectHandler>()
        .AddScoped<GetGitStatusHandler>()
        .AddScoped<GetRecentCommitsHandler>();
}
