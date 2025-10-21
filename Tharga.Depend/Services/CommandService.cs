using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public interface ICommandService
{
    Task ExecuteAsync(string[] args);
}

public class CommandService : ICommandService
{
    private readonly IOutputService _output;
    private readonly IGitRepositoryService _gitRepoService;
    private readonly IPathService _pathService;

    public CommandService(IOutputService output, IGitRepositoryService gitRepoService, IPathService pathService)
    {
        _output = output;
        _gitRepoService = gitRepoService;
        _pathService = pathService;
    }

    public async Task ExecuteAsync(string[] args)
    {
        var argsList = args.ToList();
        if (argsList.Contains("--help") || argsList.Contains("-h"))
        {
            _output.PrintHelp();
            return;
        }

        var rootPath = _pathService.GetRootPath(argsList);
        if (rootPath == null)
        {
            _output.Error("❌ Please provide a valid folder path.");
            _output.PrintHelp();
            return;
        }

        var repos = await _gitRepoService.GetAsync(rootPath).ToArrayAsync();

        var outputType = GetOptionValue(argsList, "dependency", "--output", "-o");
        var projectName = GetOptionValue(argsList, "", "--project", "-p");
        var excludePattern = GetOptionValue(argsList, "", "--exclude", "-x");
        var onlyPackable = argsList.Contains("--only-packable") || argsList.Contains("-n");

        switch (outputType)
        {
            case "list":
                PrintRepositoryList(repos, rootPath, projectName, excludePattern, onlyPackable);
                break;

            case "dependency":
                var levelMap = GetLevelMap(repos, projectName, excludePattern, onlyPackable);

                foreach (var kv in levelMap
                             .Where(kv => ShouldInclude(kv.Key, excludePattern, onlyPackable))
                             .OrderBy(kv => kv.Value).ThenBy(kv => kv.Key.Name))
                {
                    _output.WriteLine($"[{kv.Value}] {kv.Key.Name}{FormatId(kv.Key.PackageId)}", ConsoleColor.Yellow);
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(outputType, $"Unknown {nameof(outputType)} {outputType}.");
        }
    }

    private void PrintRepositoryList(IEnumerable<GitRepositoryInfo> repos, string rootPath, string projectName, string excludePattern, bool onlyPackable)
    {
        foreach (var repo in repos.OrderBy(x => x.Name))
        {
            if (!string.IsNullOrEmpty(projectName) && repo.Projects.All(x => x.Name != projectName)) continue;

            var filteredProjects = repo.Projects.Where(p => ShouldInclude(p, excludePattern, onlyPackable)).ToArray();
            if (!filteredProjects.Any()) continue;

            _output.WriteLine($"- {repo.Name} ({Path.GetRelativePath(rootPath, repo.Path)})", ConsoleColor.Green);

            foreach (var project in filteredProjects)
            {
                _output.WriteLine($"  - {project.Name}{FormatId(project.PackageId)}", ConsoleColor.Yellow);

                var filteredPackages = project.Packages
                    .Where(p => ShouldInclude(new ProjectInfo { Name = p.Name, PackageId = p.PackageId, Packages = [], Path = p.Path }, excludePattern, onlyPackable));

                foreach (var package in filteredPackages)
                    _output.WriteLine($"    - {package.Name}{FormatId(package.PackageId)}", ConsoleColor.DarkGray);
            }
        }
    }

    private static string FormatId(string? id) => string.IsNullOrEmpty(id) ? string.Empty : $" [{id}]";

    private static string GetOptionValue(List<string> argsList, string defaultValue, params string[] keys)
    {
        for (var i = 0; i < argsList.Count - 1; i++)
        {
            if (keys.Contains(argsList[i], StringComparer.OrdinalIgnoreCase))
                return argsList[i + 1];
        }
        return defaultValue;
    }

    private Dictionary<ProjectInfo, int> GetLevelMap(GitRepositoryInfo[] gitRepositoryInfos, string targetProject, string excludePattern, bool onlyPackable)
    {
        var allProjects = gitRepositoryInfos.SelectMany(r => r.Projects).ToList();

        var packageIdToProject = allProjects
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var projectDependencies = allProjects.ToDictionary(
            project => project,
            project => project.Packages
                .Where(p => packageIdToProject.ContainsKey(p.Name))
                .Select(p => packageIdToProject[p.Name])
                .ToList()
        );

        HashSet<ProjectInfo> relevantProjects;

        if (!string.IsNullOrWhiteSpace(targetProject))
        {
            var root = allProjects.FirstOrDefault(p => string.Equals(p.Name, targetProject, StringComparison.OrdinalIgnoreCase));
            if (root == null)
            {
                _output.Warning($"⚠️ Project not found: {targetProject}");
                return new Dictionary<ProjectInfo, int>();
            }

            // Check if root project would be excluded from output
            if (!ShouldInclude(root, excludePattern, onlyPackable))
                _output.Warning($"⚠️ Project '{targetProject}' is filtered from output but still used for dependency resolution.");

            relevantProjects = new HashSet<ProjectInfo>();
            var stack = new Stack<ProjectInfo>();
            stack.Push(root);
            relevantProjects.Add(root);

            while (stack.Any())
            {
                var current = stack.Pop();
                if (!projectDependencies.TryGetValue(current, out var deps)) continue;

                foreach (var dep in deps)
                    if (relevantProjects.Add(dep)) stack.Push(dep);
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
            var ready = remaining.Where(p => projectDependencies[p].All(dep => dictionary.ContainsKey(dep))).ToList();
            if (!ready.Any())
            {
                _output.Error("❌ Circular dependency detected.");
                break;
            }

            foreach (var project in ready)
            {
                var level = projectDependencies[project].Select(dep => dictionary[dep]).DefaultIfEmpty(-1).Max() + 1;
                dictionary[project] = level;
                remaining.Remove(project);
            }
        }

        return dictionary;
    }

    private bool ShouldInclude(ProjectInfo project, string excludePattern, bool onlyPackable)
    {
        if (!string.IsNullOrWhiteSpace(excludePattern) && project.Name.Contains(excludePattern, StringComparison.OrdinalIgnoreCase))
            return false;

        if (onlyPackable && string.IsNullOrWhiteSpace(project.PackageId))
            return false;

        return true;
    }
}
