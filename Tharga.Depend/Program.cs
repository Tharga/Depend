using Microsoft.Extensions.DependencyInjection;
using Tharga.Depend.Services;

var services = new ServiceCollection();
services.AddTransient<IOutputService, OutputService>();
services.AddTransient<IGitRepositoryService, GitRepositoryService>();
services.AddTransient<IProjectService, ProjectService>();
services.AddTransient<ICommandService, CommandService>();
services.AddTransient<IPathService, PathService>();

var provider = services.BuildServiceProvider();

PathService.EnsureInUserPath();

var commandService = provider.GetRequiredService<ICommandService>();
await commandService.ExecuteAsync(args);

