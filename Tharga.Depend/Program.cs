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

switch (output)
{
    case "list":
        foreach (var repo in repos.OrderBy(x => x.Name))
        {
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
        var response = repos
            .SelectMany(repo => repo.Projects
                .SelectMany(project => project.Packages
                    //.Where(z => !string.IsNullOrWhiteSpace(z.PackageId))
                    .Select(package => (Repo: repo, Project: project, Package: package))
                )
            ).ToArray();

        var allProjects = repos.SelectMany(r => r.Projects).ToList();

        var packageIdToProject = allProjects
            .Where(p => !string.IsNullOrWhiteSpace(p.PackageId))
            .GroupBy(p => p.PackageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase); // warning: handles duplicates naively

        var projectDependencies = allProjects.ToDictionary(
            project => project,
            project =>
            {
                var deps = new List<ProjectInfo>();

                foreach (var packageRef in project.Packages)
                {
                    if (string.IsNullOrWhiteSpace(packageRef.PackageId))
                        continue;

                    if (packageIdToProject.TryGetValue(packageRef.PackageId, out var depProject))
                    {
                        deps.Add(depProject);
                    }
                }

                return deps;
            });

        var levelMap = new Dictionary<ProjectInfo, int>();
        var remaining = new HashSet<ProjectInfo>(allProjects);

        while (remaining.Any())
        {
            var ready = remaining
                .Where(p => projectDependencies[p].All(dep => levelMap.ContainsKey(dep)))
                .ToList();

            if (!ready.Any())
            {
                Console.WriteLine("❌ Circular dependency detected.");
                break;
            }

            foreach (var project in ready)
            {
                int level = projectDependencies[project].Select(dep => levelMap[dep]).DefaultIfEmpty(-1).Max() + 1;
                levelMap[project] = level;
                remaining.Remove(project);
            }
        }

        foreach (var kv in levelMap
                     .Where(x => !string.IsNullOrEmpty(x.Key.PackageId))
                     .OrderBy(kv => kv.Value).ThenBy(kv => kv.Key.Name))
        {
            Console.WriteLine($"[{kv.Value}] {kv.Key.PackageId} ({kv.Key.Path})");
        }

        //var packages = response
        //    .Where(x => !x.Project.Name.EndsWith("Tests"))
        //    //.Where(x => !string.IsNullOrEmpty(x.Package.PackageId))
        //    .GroupBy(x => x.Package.PackageId);
        ////foreach (var package in packages.OrderBy(x => x.Key))
        //foreach (var package in packages.OrderBy(x => x.Count()))
        //{
        //    Console.WriteLine($"[{package.Count()}] {package.Key} is used by: {string.Join(", ", package.Select(y => y.Project.Name).Distinct())}");
        //}


        ////Lista alla paket som är projekt
        //var packageIds = response
        //    .Where(x => !string.IsNullOrEmpty(x.Package.PackageId))
        //    .Select(x => x.Package.PackageId)
        //    .Distinct()
        //    .Order();

        ////foreach (var packageId in packageIds)
        ////{
        ////    Console.WriteLine(packageId);
        ////}
        //var related = response; //.Where(x => packageIds.Contains(x.Project.PackageId));
        //////foreach (var item in response.OrderBy(x => x.Package.PackageId).ThenBy(x => x.Project.Name))
        //foreach (var item in related.OrderBy(x => x.Package.PackageId).ThenBy(x => x.Project.Name))
        //{
        //    Console.WriteLine($"{item.Package.Name}\t{item.Project.Name}\t{item.Repo.Name}");
        //}
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


//bool isOrder = argsList.Contains("--order");
//bool isGitMode = argsList.Contains("--git");

//var projectIdIndex = argsList.IndexOf("--project");
//string? targetProjectId = null;
//if (projectIdIndex >= 0 && projectIdIndex + 1 < argsList.Count)
//{
//    targetProjectId = argsList[projectIdIndex + 1];
//}

//if (!isList && !isOrder)
//{
//    PrintHelp();
//    return;
//}

//// Initialize services
//var projectService = new VisualStudioProjectService();
//var fileService = new FileListingService(projectService);
//var graphService = new DependencyGraphService();
//var printerService = new RepositoryPrinterService();
//var orderService = new DependencyOrderService();

//// Scan all repositories
//var repos = fileService.GetGitReposWithProjects(rootPath);

//if (isList)
//{
//    if (!string.IsNullOrEmpty(targetProjectId))
//    {
//        var match = repos
//            .SelectMany(r => r.Projects)
//            .FirstOrDefault(p => string.Equals(p.PackageId, targetProjectId, StringComparison.OrdinalIgnoreCase));

//        if (match == null)
//        {
//            Console.WriteLine($"⚠️ Project not found: {targetProjectId}");
//            return;
//        }

//        printerService.PrintSingle(match);
//    }
//    else
//    {
//        printerService.Print(repos, groupByGit: isGitMode);
//    }
//}
//else if (isOrder)
//{
//    var levels = graphService.CalculateDependencyLevels(repos);

//    if (isGitMode)
//    {
//        var gitDepOrderService = new GitDependencyOrderService();
//        gitDepOrderService.Print(repos, graphService);
//    }
//    else
//    {
//        if (!string.IsNullOrEmpty(targetProjectId))
//        {
//            var target = levels.Keys.FirstOrDefault(p =>
//                string.Equals(p.PackageId, targetProjectId, StringComparison.OrdinalIgnoreCase));

//            if (target == null)
//            {
//                Console.WriteLine($"⚠️ Target project '{targetProjectId}' not found in scanned repos.");
//                return;
//            }

//            // Filter only dependencies needed to build this project
//            var required = levels
//                .Where(kv => kv.Key.PackageId != target.PackageId)
//                .Where(kv => IsTransitiveDependency(kv.Key, target, graphService, levels))
//                .OrderBy(kv => kv.Value)
//                .ToList();

//            Console.WriteLine($"[?] Build order dependencies for: {target.PackageId}\n");
//            foreach (var (dep, level) in required)
//            {
//                Console.WriteLine($"[{level}] {dep.PackageId} ({dep.Path})");
//            }
//        }
//        else
//        {
//            orderService.Print(levels);
//        }
//    }
//}

//void PrintHelp()
//{
//    Console.WriteLine("""
//Usage:
//  Tharga.Depend.exe <folder> [--list | --order] [--project <PackageId>]

//Arguments:
//  <folder>        Root folder containing Git repositories and projects.

//Options:
//  --list          Show projects and dependencies (default: full list).
//  --order         Show NuGet-packable build order by dependency.
//  --project <id>  Filter to show output related to a specific NuGet project.
//  --help, -h      Show this help message.

//Examples:
//  Tharga.Depend.exe C:\dev --list
//  Tharga.Depend.exe C:\dev --order
//  Tharga.Depend.exe C:\dev --order --project Tharga.MongoDB
//""");
//}

//bool IsTransitiveDependency(ProjectInfo candidate, ProjectInfo target, DependencyGraphService graph, Dictionary<ProjectInfo, int> levels)
//{
//    var visited = new HashSet<ProjectInfo>();
//    var stack = new Stack<ProjectInfo>();
//    stack.Push(target);

//    while (stack.Any())
//    {
//        var current = stack.Pop();
//        if (!levels.ContainsKey(current))
//            continue;

//        var deps = graph.GetProjectDependencies(current, levels.Keys.ToList());

//        foreach (var dep in deps)
//        {
//            if (dep == candidate)
//                return true;

//            if (visited.Add(dep))
//                stack.Push(dep);
//        }
//    }

//    return false;
//}
