using Microsoft.Extensions.DependencyInjection;
using Tharga.Depend.Features.Command;
using Tharga.Depend.Features.Output;
using Tharga.Depend.Features.Project;
using Tharga.Depend.Features.Repo;
using Tharga.Depend.Framework;

var services = new ServiceCollection();
services.AddTransient<IOutputService, OutputService>();
services.AddTransient<IOutputTreeService, OutputTreeService>();
services.AddTransient<IOutoutListService, OutoutListService>();
services.AddTransient<IOutputDependencyService, OutputDependencyService>();
services.AddTransient<IProjectService, ProjectService>();
services.AddTransient<ICommandService, CommandService>();
services.AddTransient<IPathService, PathService>();
services.AddTransient<IGitRepositoryService, GitRepositoryService>();

var provider = services.BuildServiceProvider();

var commandService = provider.GetRequiredService<ICommandService>();
return await commandService.ExecuteAsync(args);
