using Tharga.Depend.Services;
using Microsoft.Extensions.DependencyInjection;
using Tharga.Depend.Models;

var services = new ServiceCollection();
services.AddTransient<IOutputService, OutputService>();
services.AddTransient<IGitRepositoryService, GitRepositoryService>();
services.AddTransient<IProjectService, ProjectService>();

var provider = services.BuildServiceProvider();

var argsList = args.ToList();

if (argsList.Contains("--help") || argsList.Contains("-h"))
{
    provider.GetService<IOutputService>().PrintHelp();
    return;
}

var rootPath = argsList.FirstOrDefault(a => !a.StartsWith("-"));
if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
{
    Console.Error.WriteLine("❌ Please provide a valid folder path.");
    provider.GetService<IOutputService>().PrintHelp();
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
        var levelMap = GetLevelMap(repos, projectName);

        foreach (var kv in levelMap.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key.Name))
        {
            Console.WriteLine($"[{kv.Value}] {kv.Key.Name}{(string.IsNullOrEmpty(kv.Key.PackageId) ? null : $" [{kv.Key.PackageId}]")} ({kv.Key.Path})");
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

Dictionary<ProjectInfo, int> GetLevelMap(GitRepositoryInfo[] gitRepositoryInfos, string targetProject)
{
    var allProjects = gitRepositoryInfos.SelectMany(r => r.Projects).ToList();

    var packageIdToProject = allProjects
        .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    var projectDependencies = allProjects.ToDictionary(
        project => project,
        project =>
        {
            var deps = new List<ProjectInfo>();

            foreach (var packageRef in project.Packages)
            {
                if (packageIdToProject.TryGetValue(packageRef.Name, out var depProject))
                {
                    deps.Add(depProject);
                }
            }

            return deps;
        });

    HashSet<ProjectInfo> relevantProjects;

    if (!string.IsNullOrWhiteSpace(targetProject))
    {
        var root = allProjects.FirstOrDefault(p => string.Equals(p.Name, targetProject, StringComparison.OrdinalIgnoreCase));

        if (root == null)
        {
            Console.WriteLine($"⚠️ Project not found: {targetProject}");
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
        relevantProjects = new HashSet<ProjectInfo>(allProjects);
    }

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