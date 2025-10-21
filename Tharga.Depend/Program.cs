using Microsoft.Extensions.DependencyInjection;
using Tharga.Depend.Services;

var services = new ServiceCollection();
services.AddTransient<IOutputService, OutputService>();
services.AddTransient<IProjectService, ProjectService>();
services.AddTransient<ICommandService, CommandService>();
services.AddTransient<IPathService, PathService>();
services.AddTransient<IGitRepositoryService>(sp =>
{
    var projectService = sp.GetRequiredService<IProjectService>();
    var rootPath = Environment.CurrentDirectory; // or however you resolve the path
    return new GitRepositoryService(projectService, rootPath);
});

var provider = services.BuildServiceProvider();

PathService.EnsureInUserPath();

var commandService = provider.GetRequiredService<ICommandService>();
return await commandService.ExecuteAsync(args);

