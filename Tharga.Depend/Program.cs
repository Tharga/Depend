using Microsoft.Extensions.DependencyInjection;
using Tharga.Depend.Services;

var services = new ServiceCollection();
services.AddTransient<IOutputService, OutputService>();
services.AddTransient<IProjectService, ProjectService>();
services.AddTransient<ICommandService, CommandService>();
services.AddTransient<IPathService, PathService>();
services.AddTransient<IGitRepositoryService, GitRepositoryService>();

var provider = services.BuildServiceProvider();

var commandService = provider.GetRequiredService<ICommandService>();
return await commandService.ExecuteAsync(args);

