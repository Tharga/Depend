using Tharga.Console;
using Tharga.Console.Commands;
using Tharga.Console.Consoles;
using Tharga.Depend.ConsoleCommands;
using Tharga.Depend.Services;
using Microsoft.Extensions.DependencyInjection;
using Tharga.Depend.Models;

//using var console = new ClientConsole();
//var command = new RootCommandIoc(console);
////var command = new RootCommand(console);
//command.RegisterCommand<DependencyCommands>();
//var engine = new CommandEngine(command);
//engine.Start(args);

var services = new ServiceCollection();
services.AddTransient<IHelpOutputService, HelpOutputService>();
services.AddTransient<IGitRepositoryService, GitRepositoryService>();
services.AddTransient<IProjectService, ProjectService>();

var provider = services.BuildServiceProvider();

var argsList = args.ToList();

if (argsList.Contains("--help") || argsList.Contains("-h"))
{
    provider.GetService<IHelpOutputService>().PrintHelp();
    return;
}

// Extract path (first non-option argument)
var rootPath = argsList.FirstOrDefault(a => !a.StartsWith("-"));
if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
{
    Console.Error.WriteLine("❌ Please provide a valid folder path.");
    provider.GetService<IHelpOutputService>().PrintHelp();
    return;
}

var gitRepositoryService = provider.GetRequiredService<IGitRepositoryService>();
var repos = await gitRepositoryService.GetAsync(rootPath).ToArrayAsync();

var output = GetOptionValue(argsList, "list", "--output", "-o");
var projectName = GetOptionValue(argsList, "list", "--project", "-p");

switch (output)
{
    case "list":
        foreach (var repo in repos.OrderBy(x => x.Name))
        {
            if (!string.IsNullOrEmpty(projectName) && repo.Projects.All(x => x.Name != projectName)) continue;

            Console.WriteLine($"- {repo.Name} ({Path.GetRelativePath(rootPath, repo.Path)})");
            foreach (var project in repo.Projects)
            {
                Console.WriteLine($" - {project.Name}{(string.IsNullOrEmpty(project.PackageId) ? null : $" [{project.PackageId}]")}");
                foreach (var package in project.Packages)
                {
                    Console.WriteLine($"  - {package.Name}{(string.IsNullOrEmpty(package.PackageId) ? null : $" [{package.PackageId}]")}");
                }
            }
        }
        break;
    case "dependency":
        //var response = repos
        //    .SelectMany(repo => repo.Projects
        //        .SelectMany(project => project.Packages
        //            //.Where(z => !string.IsNullOrWhiteSpace(z.PackageId))
        //            .Select(package => (Repo: repo, Project: project, Package: package))
        //        )
        //    ).ToArray();

        var levelMap = GetLevelMap(repos, null); //, projectName);

        foreach (var kv in levelMap
                     //.Where(x => !string.IsNullOrEmpty(x.Key.PackageId))
                     .OrderBy(kv => kv.Value).ThenBy(kv => kv.Key.Name))
        {
            //Console.WriteLine($"[{kv.Value}] {kv.Key.PackageId} ({kv.Key.Path})");
            Console.WriteLine($"[{kv.Value}] {kv.Key.Name}{(string.IsNullOrEmpty(kv.Key.PackageId) ? null : $" [{kv.Key.PackageId}]")} ({kv.Key.Path})");
            //Console.WriteLine($"  - {package.Name}{(string.IsNullOrEmpty(package.PackageId) ? null : $" [{package.PackageId}]")}");
        }

        break;
    default:
        throw new ArgumentOutOfRangeException(output, $"Unknown {nameof(output)} {output}.");
}

static string GetOptionValue(List<string> argsList, string defaultValue, params string[] keys)
{
    for (var i = 0; i < argsList.Count - 1; i++)
    {
        if (keys.Contains(argsList[i], StringComparer.OrdinalIgnoreCase))
        {
            return argsList[i + 1];
        }
    }

    return defaultValue;
}

Dictionary<ProjectInfo, int> GetLevelMap(GitRepositoryInfo[] gitRepositoryInfos, string targetProjectId)
{
    var allProjects = gitRepositoryInfos.SelectMany(r => r.Projects).ToList();

    var packageIdToProject = allProjects
        //.Where(p => !string.IsNullOrWhiteSpace(p.PackageId))
        .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    var projectDependencies = allProjects.ToDictionary(
        project => project,
        project =>
        {
            var deps = new List<ProjectInfo>();

            foreach (var packageRef in project.Packages)
            {
                //if (string.IsNullOrWhiteSpace(packageRef.PackageId))
                //    continue;

                if (packageIdToProject.TryGetValue(packageRef.Name, out var depProject))
                {
                    deps.Add(depProject);
                }
            }

            return deps;
        });

    // ✅ Step 1: Resolve only dependencies for a target project if specified
    HashSet<ProjectInfo> relevantProjects;

    if (!string.IsNullOrWhiteSpace(targetProjectId))
    {
        var root = allProjects
            .FirstOrDefault(p => string.Equals(p.Name, targetProjectId, StringComparison.OrdinalIgnoreCase));

        if (root == null)
        {
            Console.WriteLine($"⚠️ Project not found: {targetProjectId}");
            return new Dictionary<ProjectInfo, int>();
        }

        relevantProjects = new HashSet<ProjectInfo>();
        var stack = new Stack<ProjectInfo>();
        stack.Push(root);
        relevantProjects.Add(root);

        while (stack.Any())
        {
            var current = stack.Pop();

            if (projectDependencies.TryGetValue(current, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (relevantProjects.Add(dep))
                        stack.Push(dep);
                }
            }
        }
    }
    else
    {
        // No filter – use all projects
        relevantProjects = new HashSet<ProjectInfo>(allProjects);
    }

    // ✅ Step 2: Topological sort with level assignment
    var dictionary = new Dictionary<ProjectInfo, int>();
    var remaining = new HashSet<ProjectInfo>(relevantProjects);

    while (remaining.Any())
    {
        var ready = remaining
            .Where(p => projectDependencies[p].All(dep => dictionary.ContainsKey(dep)))
            .ToList();

        if (!ready.Any())
        {
            Console.WriteLine("❌ Circular dependency detected.");
            break;
        }

        foreach (var project in ready)
        {
            int level = projectDependencies[project].Select(dep => dictionary[dep]).DefaultIfEmpty(-1).Max() + 1;
            dictionary[project] = level;
            remaining.Remove(project);
        }
    }

    return dictionary;
}