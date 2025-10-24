using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public interface ICommandService
{
    Task<int> ExecuteAsync(string[] args);
}

public class CommandService : ICommandService
{
    private const int ExitSuccess = 0;
    private const int ExitInvalidPath = 1;
    private const int ExitUnknownOutputType = 2;
    private const int ExitUnknownViewMode = 3;
    private const int ExitUnhandledError = 99;

    private readonly IOutputService _output;
    private readonly IGitRepositoryService _gitRepoService;
    private readonly IPathService _pathService;

    public CommandService(IOutputService output, IGitRepositoryService gitRepoService, IPathService pathService)
    {
        _output = output;
        _gitRepoService = gitRepoService;
        _pathService = pathService;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            var argsList = args.ToList();
            if (argsList.Contains("--help") || argsList.Contains("-h"))
            {
                _output.PrintHelp();
                return ExitWithCode(ExitSuccess);
            }

            _pathService.EnsureInUserPath();

            var rootPath = _pathService.GetRootPath(argsList);
            if (rootPath == null)
            {
                _output.Error("Error: Please provide a valid folder path.");
                _output.PrintHelp();
                return ExitWithCode(ExitInvalidPath);
            }

            // Load all repositories
            var allRepos = await _gitRepoService.GetAsync(rootPath).ToArrayAsync();

            // Compute latest known package versions from all repos
            var latestPackageVersions = GetLatestPackageVersions(allRepos);

            WarnIfDuplicateProjectNames(allRepos);

            // Now parse CLI options
            var outputType = GetOptionValue(argsList, "dependency", "--output", "-o");
            var viewMode = ParseViewMode(GetOptionValue(argsList, "default", "--view", "-v"));
            if (!viewMode.HasValue) return ExitWithCode(ExitUnknownViewMode);
            var projectName = GetOptionValue(argsList, "", "--project", "-p");
            var excludePattern = GetOptionValue(argsList, "", "--exclude", "-x");
            var onlyPackable = argsList.Contains("--only-packable") || argsList.Contains("-n");
            var showRepoDeps = argsList.Contains("--repo-deps") || argsList.Contains("-rd");
            var showProjectDeps = argsList.Contains("--project-deps") || argsList.Contains("-pd");
            var showRepoUsages = argsList.Contains("--repo-usages") || argsList.Contains("-ru");
            var showProjectUsages = argsList.Contains("--project-usages") || argsList.Contains("-pu");

            // Filter for display only if -p is specified
            var displayRepos = string.IsNullOrWhiteSpace(projectName)
                ? allRepos
                : FilterReposByProject(allRepos, projectName);

            switch (outputType)
            {
                case "l":
                case "list":
                    PrintRepositoryList(displayRepos, projectName, excludePattern, onlyPackable, viewMode.Value, showRepoDeps, showProjectDeps, showRepoUsages, showProjectUsages, latestPackageVersions);
                    break;

                case "d":
                case "dependency":
                    PrintDependencyList(displayRepos, projectName, excludePattern, onlyPackable, viewMode.Value, showRepoDeps, showProjectDeps, showRepoUsages, showProjectUsages, latestPackageVersions);
                    break;

                default:
                    _output.Error($"Unknown output type: {outputType}");
                    return ExitWithCode(ExitUnknownOutputType);
            }

            return ExitWithCode(ExitSuccess);
        }
        catch (Exception e)
        {
            _output.Error($"{e.Message} @{e.StackTrace}");
            return ExitWithCode(ExitUnhandledError);
        }
    }

    private GitRepositoryInfo[] FilterReposByProject(GitRepositoryInfo[] allRepos, string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            return allRepos;

        var matched = allRepos
            .Where(r => r.Projects.Any(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (!matched.Any())
        {
            _output.Warning($"Warning: Project '{projectName}' not found. Displaying all repositories.");
            return allRepos;
        }

        return matched;
    }

    private void PrintRepositoryList(IEnumerable<GitRepositoryInfo> repositories, string projectName, string excludePattern, bool onlyPackable, ViewMode viewMode, bool showRepoDeps, bool showProjectDeps, bool showRepoUsages, bool showProjectUsages, Dictionary<string, string> latestVersions)
    {
        var repos = repositories.ToArray();

        var showPackages = viewMode == ViewMode.Full;
        var showProjects = viewMode is ViewMode.Default or ViewMode.Full;
        var showRepos = viewMode != ViewMode.ProjectOnly;

        var allProjects = repos.SelectMany(r => r.Projects).ToList();

        //NOTE: Only show project level.

        if (viewMode == ViewMode.ProjectOnly)
        {
            foreach (var project in allProjects.Where(p => ShouldInclude(p, excludePattern, onlyPackable)).OrderBy(p => p.Name))
            {
                PrintProjectLine(repos, project, 0);
                PrintProjectDeps(repos, excludePattern, onlyPackable, showProjectDeps, project, 2);
                PrintProjectUsages(project, repos, showProjectUsages, excludePattern, onlyPackable, 2);
            }

            return;
        }

        //NOTE: Show repository level, with different options of projects and packages.
        foreach (var repo in repos.OrderBy(r => r.Name))
        {
            if (!string.IsNullOrEmpty(projectName) && repo.Projects.All(p => p.Name != projectName)) continue;

            var filteredProjects = repo.Projects.Where(p => ShouldInclude(p, excludePattern, onlyPackable)).ToArray();

            if (!filteredProjects.Any() && showProjects) continue;

            if (showRepos)
            {
                PrintRepositoryLine(repos, repo);
                PrintRepoDeps(repos, showRepoDeps, repo, 2);
                PrintRepoUsages(repo, repos, showRepoUsages, 2);
            }

            if (!showProjects) continue;

            foreach (var project in filteredProjects.OrderBy(p => p.Name))
            {
                PrintProjectLine(repos, project, 2);
                PrintProjectDeps(repos, excludePattern, onlyPackable, showProjectDeps, project, 4);
                PrintProjectUsages(project, repos, showProjectUsages, excludePattern, onlyPackable, 4);
                PrintProjectPackages(project, latestVersions, excludePattern, onlyPackable, showPackages, 4);
            }
        }
    }

    private static string FormatId(string id) => string.IsNullOrEmpty(id) ? string.Empty : $" [{id}]";

    private static string GetOptionValue(List<string> argsList, string defaultValue, params string[] keys)
    {
        for (var i = 0; i < argsList.Count - 1; i++)
        {
            if (keys.Contains(argsList[i], StringComparer.OrdinalIgnoreCase))
                return argsList[i + 1];
        }
        return defaultValue;
    }

    private Dictionary<ProjectInfo, int> GetLevelMap(GitRepositoryInfo[] gitRepositoryInfos, string targetProject, string excludePattern, bool onlyPackable, bool includeAllProjects)
    {
        var allProjects = gitRepositoryInfos.SelectMany(r => r.Projects).ToArray();

        var packageIdToProject = allProjects
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var projectDependencies = allProjects.ToDictionary(
            project => project,
            project => project.Packages
                .Where(p => packageIdToProject.ContainsKey(p.Name))
                .Select(p => packageIdToProject[p.Name])
                .ToArray()
        );

        HashSet<ProjectInfo> relevantProjects;

        if (!string.IsNullOrWhiteSpace(targetProject) && !includeAllProjects)
        {
            var root = allProjects.FirstOrDefault(p =>
                string.Equals(p.Name, targetProject, StringComparison.OrdinalIgnoreCase));
            if (root == null)
            {
                _output.Warning($"Warning: Project not found: {targetProject}");
                return new Dictionary<ProjectInfo, int>();
            }

            // Warn if root project is excluded from output
            if (!ShouldInclude(root, excludePattern, onlyPackable))
            {
                _output.Warning($"Warning: Project '{targetProject}' is filtered from output but still used for dependency resolution.");
            }

            relevantProjects = new HashSet<ProjectInfo>();
            var stack = new Stack<ProjectInfo>();
            stack.Push(root);
            relevantProjects.Add(root);

            while (stack.Any())
            {
                var current = stack.Pop();
                if (!projectDependencies.TryGetValue(current, out var deps)) continue;

                foreach (var dep in deps)
                {
                    if (relevantProjects.Add(dep)) stack.Push(dep);
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
                _output.Error("Error: Circular dependency detected.");
                break;
            }

            foreach (var project in ready)
            {
                var level = projectDependencies[project]
                    .Select(dep => dictionary[dep])
                    .DefaultIfEmpty(-1)
                    .Max() + 1;

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

    private void WarnIfDuplicateProjectNames(GitRepositoryInfo[] repos)
    {
        var allProjects = repos.SelectMany(r => r.Projects).ToList();

        var duplicates = allProjects
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        if (!duplicates.Any()) return;

        _output.Warning("Duplicate project names detected (same name in multiple locations):");
        foreach (var group in duplicates)
        {
            _output.WriteLine($"  - Project: {group.Key}", ConsoleColor.Yellow);
            foreach (var project in group)
            {
                _output.WriteLine($"    - {project.Path}", ConsoleColor.DarkGray);
            }
        }

        _output.WriteLine("");
    }

    private void PrintDependencyList(GitRepositoryInfo[] repos, string projectName, string excludePattern, bool onlyPackable, ViewMode viewMode, bool showRepoDeps, bool showProjectDeps, bool showRepoUsages, bool showProjectUsages, Dictionary<string, string> latestVersions)
    {
        var showPackages = viewMode == ViewMode.Full;
        var showProjects = viewMode is ViewMode.Default or ViewMode.Full;
        var showRepos = viewMode != ViewMode.ProjectOnly;

        var levelMap = GetLevelMap(repos, projectName, excludePattern, onlyPackable, includeAllProjects: true);

        var orderedProjects = levelMap
            .Where(kv => ShouldInclude(kv.Key, excludePattern, onlyPackable))
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => kv.Key.Name)
            .Select(kv => kv.Key)
            .ToList();

        var repoLevelMap = GetRepositoryLevelMap(repos, out var hasRepoCycle);
        if (hasRepoCycle) _output.Warning("Circular Git repository dependency detected. Git-level ordering may be partial.");

        //NOTE: Only show project level.
        if (viewMode == ViewMode.ProjectOnly)
        {
            foreach (var project in orderedProjects)
            {
                var level = levelMap.GetValueOrDefault(project, -1);
                PrintProjectLine(repos, project, 0, level);
                PrintProjectDeps(repos, excludePattern, onlyPackable, showProjectDeps, project, 4, levelMap);
                PrintProjectUsages(project, repos, showProjectUsages, excludePattern, onlyPackable, 4);
            }

            return;
        }

        //NOTE: Show repository level, with different options of projects and packages.
        foreach (var repo in repos
                     .Where(r => orderedProjects.Any(p => r.Projects.Any(rp => rp.Path == p.Path)))
                     .OrderBy(r => repoLevelMap.GetValueOrDefault(r, int.MaxValue))
                     .ThenBy(r => r.Name))
        {
            var repoProjects = orderedProjects
                .Where(p => repo.Projects.Any(rp => rp.Name == p.Name && rp.Path == p.Path))
                .ToList();

            if (!repoProjects.Any() && showProjects)
                continue;

            if (showRepos)
            {
                var repoLevel = repoLevelMap.GetValueOrDefault(repo, -1);
                PrintRepositoryLine(repos, repo, repoLevel);
                PrintRepoDeps(repos, showRepoDeps, repo, 2, repoLevelMap);
                PrintRepoUsages(repo, repos, showRepoUsages, 2);
            }

            if (!showProjects) continue;

            foreach (var project in repoProjects)
            {
                var level = levelMap.GetValueOrDefault(project, -1);
                PrintProjectLine(repos, project, 2, level);
                PrintProjectDeps(repos, excludePattern, onlyPackable, showProjectDeps, project, 4, levelMap);
                PrintProjectUsages(project, repos, showProjectUsages, excludePattern, onlyPackable, 4);
                PrintProjectPackages(project, latestVersions, excludePattern, onlyPackable, showPackages, 4);
            }
        }
    }

    private void PrintRepoDeps(GitRepositoryInfo[] repos, bool showRepoDeps, GitRepositoryInfo repo, int indentLevel = 2, Dictionary<GitRepositoryInfo, int> repoLevelMap = null)
    {
        if (!showRepoDeps) return;

        var graph = BuildRepositoryDependencyGraph(repos);

        if (graph.TryGetValue(repo, out var deps) && deps.Any())
        {
            var indent = new string(' ', indentLevel);
            _output.WriteLine($"{indent}Referenced by:", ConsoleColor.Gray);

            foreach (var dep in deps
                         .OrderBy(r => repoLevelMap?.GetValueOrDefault(r, int.MaxValue))
                         .ThenBy(r => r.Name))
            {
                _output.WriteLine($"{indent}- {dep.Name}", ConsoleColor.DarkGreen);
            }
        }
    }

    private void PrintRepoUsages(GitRepositoryInfo repo, GitRepositoryInfo[] repos, bool showRepoUsages, int indentLevel = 2)
    {
        if (!showRepoUsages) return;

        var usageGraph = BuildRepositoryUsageGraph(repos);

        if (!usageGraph.TryGetValue(repo, out var users) || !users.Any())
            return;

        var indent = new string(' ', indentLevel);
        _output.WriteLine($"{indent}Used by:", ConsoleColor.Gray);

        foreach (var userRepo in users.OrderBy(r => r.Name))
        {
            _output.WriteLine($"{indent}- {userRepo.Name}", ConsoleColor.DarkCyan);
        }
    }

    private void PrintProjectDeps(GitRepositoryInfo[] repos, string excludePattern, bool onlyPackable, bool showProjectDeps, ProjectInfo project, int indentLevel = 4, Dictionary<ProjectInfo, int> levelMap = null)
    {
        if (!showProjectDeps) return;

        var projectDependencyGraph = BuildProjectDependencyGraph(repos);
        if (projectDependencyGraph.TryGetValue(project, out var deps) && deps.Any())
        {
            var indent = new string(' ', indentLevel);
            _output.WriteLine($"{indent}Referenced by:", ConsoleColor.Gray);

            foreach (var dep in deps
                         .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
                         .OrderBy(p => levelMap?.GetValueOrDefault(p, int.MaxValue))
                         .ThenBy(p => p.Name))
            {
                _output.WriteLine($"{indent}- {dep.Name}{FormatId(dep.PackageId)}", ConsoleColor.DarkYellow);
            }
        }
    }

    private void PrintProjectUsages(ProjectInfo project, GitRepositoryInfo[] repos, bool showProjectUsages, string excludePattern, bool onlyPackable, int indentLevel = 4)
    {
        if (!showProjectUsages) return;

        var usageGraph = BuildProjectUsageGraph(repos);

        if (!usageGraph.TryGetValue(project, out var users) || !users.Any())
            return;

        var indent = new string(' ', indentLevel);
        _output.WriteLine($"{indent}Used by:", ConsoleColor.Gray);

        foreach (var user in users
                     .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
                     .OrderBy(p => p.Name))
        {
            _output.WriteLine($"{indent}- {user.Name}{FormatId(user.PackageId)}", ConsoleColor.DarkCyan);
        }
    }

    private Dictionary<GitRepositoryInfo, int> GetRepositoryLevelMap(GitRepositoryInfo[] repos, out bool hasCycle)
    {
        hasCycle = false;

        // Map each project file path -> its Git repository
        var repoByProjectPath = repos
            .SelectMany(r => r.Projects.Select(p => (Project: p, Repo: r)))
            .ToDictionary(x => x.Project.Path, x => x.Repo, StringComparer.OrdinalIgnoreCase);

        // Build repo dependency graph (ignore intra-repo deps)
        var repoDependencies = new Dictionary<GitRepositoryInfo, HashSet<GitRepositoryInfo>>();

        foreach (var repo in repos)
        {
            var dependencies = new HashSet<GitRepositoryInfo>();

            foreach (var project in repo.Projects)
            {
                foreach (var package in project.Packages)
                {
                    // Prefer path-based lookup (project references)
                    if (!string.IsNullOrWhiteSpace(package.Path)
                        && repoByProjectPath.TryGetValue(package.Path, out var depRepo)
                        && depRepo != repo)
                    {
                        dependencies.Add(depRepo);
                        continue;
                    }

                    // Fallback: resolve by PackageId (for internally referenced packages)
                    if (!string.IsNullOrWhiteSpace(package.PackageId))
                    {
                        depRepo = repos.FirstOrDefault(r =>
                            r.Projects.Any(p =>
                                string.Equals(p.PackageId, package.PackageId, StringComparison.OrdinalIgnoreCase)
                                && p.Path != project.Path)); // avoid self

                        if (depRepo != null && depRepo != repo)
                            dependencies.Add(depRepo);
                    }
                }
            }

            repoDependencies[repo] = dependencies;
        }

        // Detect cycles before computing levels
        var visited = new HashSet<GitRepositoryInfo>();
        var stack = new HashSet<GitRepositoryInfo>();
        var cycles = new List<List<GitRepositoryInfo>>();

        void Dfs(GitRepositoryInfo current, List<GitRepositoryInfo> path)
        {
            if (stack.Contains(current))
            {
                var cycleStart = path.FindIndex(p => p == current);
                if (cycleStart >= 0)
                    cycles.Add(path.Skip(cycleStart).Append(current).ToList());
                return;
            }

            if (!visited.Add(current))
                return;

            stack.Add(current);
            path.Add(current);

            foreach (var dep in repoDependencies[current])
                Dfs(dep, path);

            stack.Remove(current);
            path.RemoveAt(path.Count - 1);
        }

        foreach (var repo in repos)
            if (!visited.Contains(repo))
                Dfs(repo, new List<GitRepositoryInfo>());

        if (cycles.Any())
        {
            hasCycle = true;
            _output.Warning("Circular Git repository dependency detected:");
            foreach (var cycle in cycles)
            {
                var pathString = string.Join(" -> ", cycle.Select(r => r.Name));
                _output.WriteLine($"   {pathString}", ConsoleColor.Yellow);
            }
            //return ExitWithCode(ExitCircularDependency);
        }

        // Compute repo dependency levels (Kahn’s algorithm)
        var levelMap = new Dictionary<GitRepositoryInfo, int>();
        var remaining = new HashSet<GitRepositoryInfo>(repos);

        while (remaining.Any())
        {
            var ready = remaining
                .Where(r => repoDependencies[r].All(dep => levelMap.ContainsKey(dep)))
                .ToList();

            if (!ready.Any())
            {
                hasCycle = true;
                break;
            }

            foreach (var repo in ready)
            {
                var level = repoDependencies[repo].Select(dep => levelMap[dep]).DefaultIfEmpty(-1).Max() + 1;
                levelMap[repo] = level;
                remaining.Remove(repo);
            }
        }

        return levelMap;
    }

    private Dictionary<GitRepositoryInfo, HashSet<GitRepositoryInfo>> BuildRepositoryDependencyGraph(GitRepositoryInfo[] repos)
    {
        var repoByProjectPath = repos
            .SelectMany(r => r.Projects.Select(p => (Project: p, Repo: r)))
            .ToDictionary(x => x.Project.Path, x => x.Repo, StringComparer.OrdinalIgnoreCase);

        var graph = new Dictionary<GitRepositoryInfo, HashSet<GitRepositoryInfo>>();

        foreach (var repo in repos)
        {
            var dependencies = new HashSet<GitRepositoryInfo>();

            foreach (var project in repo.Projects)
            {
                foreach (var package in project.Packages)
                {
                    if (!string.IsNullOrWhiteSpace(package.Path)
                        && repoByProjectPath.TryGetValue(package.Path, out var depRepo)
                        && depRepo != repo)
                    {
                        dependencies.Add(depRepo);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(package.PackageId))
                    {
                        depRepo = repos.FirstOrDefault(r =>
                            r.Projects.Any(p =>
                                string.Equals(p.PackageId, package.PackageId, StringComparison.OrdinalIgnoreCase)
                                && p.Path != project.Path));

                        if (depRepo != null && depRepo != repo)
                            dependencies.Add(depRepo);
                    }
                }
            }

            graph[repo] = dependencies;
        }

        return graph;
    }

    private Dictionary<ProjectInfo, List<ProjectInfo>> BuildProjectDependencyGraph(GitRepositoryInfo[] repos)
    {
        var allProjects = repos.SelectMany(r => r.Projects).ToList();

        var projectByName = allProjects
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var graph = new Dictionary<ProjectInfo, List<ProjectInfo>>();

        foreach (var project in allProjects)
        {
            var deps = project.Packages
                .Where(p => projectByName.ContainsKey(p.Name))
                .Select(p => projectByName[p.Name])
                .Where(dep => dep.Path != project.Path) // avoid self
                .ToList();

            graph[project] = deps;
        }

        return graph;
    }

    private void PrintPackageLine(PackageInfo package, Dictionary<string, string> latestVersions, int indentLevel = 4)
    {
        var indent = new string(' ', indentLevel);
        var versionText = package.Version ?? "Project";
        var line = $"{indent}- {package.Name}{FormatId(package.PackageId)} ({versionText})";

        if (!string.IsNullOrWhiteSpace(package.Version) &&
            latestVersions.TryGetValue(package.Name, out var latest))
        {
            try
            {
                // Parse both versions using NuGetVersion
                var currentVersion = NuGet.Versioning.NuGetVersion.Parse(package.Version);
                var latestVersion = NuGet.Versioning.NuGetVersion.Parse(latest);

                if (currentVersion < latestVersion)
                {
                    // Show upgrade notice
                    _output.WriteLine($"{indent}- {package.Name}{FormatId(package.PackageId)} ({versionText} -> {latest})", ConsoleColor.Red);
                    return;
                }
            }
            catch
            {
                // Fallback: in case version parsing fails (non-standard)
                if (string.Compare(package.Version, latest, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    _output.WriteLine($"{indent}- {package.Name}{FormatId(package.PackageId)} ({versionText} -> {latest})", ConsoleColor.Red);
                    return;
                }
            }
        }

        _output.WriteLine(line, ConsoleColor.DarkGray);
    }

    private Dictionary<string, string> GetLatestPackageVersions(GitRepositoryInfo[] repos)
    {
        return repos
            .SelectMany(r => r.Projects)
            .SelectMany(p => p.Packages)
            .Where(pkg => !string.IsNullOrWhiteSpace(pkg.Name) && !string.IsNullOrWhiteSpace(pkg.Version))
            .GroupBy(pkg => pkg.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(pkg => pkg.Version).Max(),
                StringComparer.OrdinalIgnoreCase
            );
    }

    private void PrintProjectLine(GitRepositoryInfo[] repos, ProjectInfo project, int indentLevel = 0, int? level = null)
    {
        var indent = new string(' ', indentLevel);

        // Build or reuse dependency graph
        var dependencyGraph = BuildProjectDependencyGraph(repos);
        var usageGraph = BuildProjectUsageGraph(repos);

        var referencedCount = dependencyGraph.TryGetValue(project, out var deps)
            ? deps.Count
            : 0;

        var usedByCount = usageGraph.TryGetValue(project, out var usedBy)
            ? usedBy.Count
            : 0;

        // Combine counts
        var usageParts = new List<string>();
        if (referencedCount > 0)
            usageParts.Add($"Referenced by {referencedCount}");
        if (usedByCount > 0)
            usageParts.Add($"used by {usedByCount}");

        var usageText = usageParts.Any()
            ? $" ({string.Join(", ", usageParts)} project{(referencedCount + usedByCount > 1 ? "s" : "")})"
            : "";

        var levelText = level != null ? $"[{level}] " : "";

        _output.WriteLine($"{indent}- {levelText}{project.Name}{FormatId(project.PackageId)}{usageText}", ConsoleColor.Yellow);
    }

    private void PrintRepositoryLine(GitRepositoryInfo[] repos, GitRepositoryInfo repo, int? level = null)
    {
        var dependencyGraph = BuildRepositoryDependencyGraph(repos);
        var usageGraph = BuildRepositoryUsageGraph(repos);

        var referencesCount = dependencyGraph.TryGetValue(repo, out var deps)
            ? deps.Count
            : 0;

        var usedByCount = usageGraph.TryGetValue(repo, out var users)
            ? users.Count
            : 0;

        var usageParts = new List<string>();
        if (referencesCount > 0)
            usageParts.Add($"Referenced by {referencesCount}");
        if (usedByCount > 0)
            usageParts.Add($"used by {usedByCount}");

        var usageText = usageParts.Any()
            ? $" ({string.Join(", ", usageParts)} repo{(referencesCount + usedByCount > 1 ? "s" : "")})"
            : "";

        var prefix = level != null ? $"- [{level}] " : "- ";
        _output.WriteLine($"{prefix}{repo.Name}{usageText}", ConsoleColor.Green);
    }

    private void PrintProjectPackages(ProjectInfo project, Dictionary<string, string> latestVersions, string excludePattern, bool onlyPackable, bool showPackages, int indentLevel = 2)
    {
        if (!showPackages) return;

        var filteredPackages = project.Packages
            .Where(p => ShouldInclude(
                new ProjectInfo
                {
                    Name = p.Name,
                    PackageId = p.PackageId,
                    Packages = [],
                    Path = p.Path
                },
                excludePattern,
                onlyPackable))
            .OrderBy(p => p.Name);

        foreach (var package in filteredPackages)
        {
            PrintPackageLine(package, latestVersions, indentLevel);
        }
    }

    private Dictionary<ProjectInfo, List<ProjectInfo>> BuildProjectUsageGraph(GitRepositoryInfo[] repos)
    {
        var deps = BuildProjectDependencyGraph(repos);
        var usage = new Dictionary<ProjectInfo, List<ProjectInfo>>();

        foreach (var kvp in deps)
        {
            var project = kvp.Key;
            foreach (var dep in kvp.Value)
            {
                if (!usage.ContainsKey(dep))
                    usage[dep] = new List<ProjectInfo>();

                usage[dep].Add(project);
            }
        }

        return usage;
    }

    private Dictionary<GitRepositoryInfo, List<GitRepositoryInfo>> BuildRepositoryUsageGraph(GitRepositoryInfo[] repos)
    {
        var deps = BuildRepositoryDependencyGraph(repos);
        var usage = new Dictionary<GitRepositoryInfo, List<GitRepositoryInfo>>();

        foreach (var kvp in deps)
        {
            var repo = kvp.Key;
            foreach (var dep in kvp.Value)
            {
                if (!usage.ContainsKey(dep))
                    usage[dep] = new List<GitRepositoryInfo>();

                usage[dep].Add(repo);
            }
        }

        return usage;
    }

    private int ExitWithCode(int code)
    {
        var message = code switch
        {
            ExitSuccess => "", //Exit code 0: Success.
            ExitInvalidPath => "Exit code 1: Invalid or missing path.",
            ExitUnknownOutputType => "Exit code 2: Unknown output type specified.",
            ExitUnknownViewMode => "Exit code 3: Unknown view mode specified.",
            ExitUnhandledError => "Exit code 99: Unhandled error occurred.",
            _ => $"Exit code {code}: Unknown result."
        };

        // Use consistent coloring for terminal readability
        var color = code == 0
            ? ConsoleColor.Green
            : code == ExitUnhandledError
                ? ConsoleColor.Red
                : ConsoleColor.Yellow;

        _output.WriteLine(message, color);
        return code;
    }

    private static ViewMode? ParseViewMode(string value)
    {
        return value?.ToLowerInvariant() switch
        {
            "full" or "f" => ViewMode.Full,
            "repo-only" or "repoonly" or "r" => ViewMode.RepoOnly,
            "project-only" or "projectonly" or "p" => ViewMode.ProjectOnly,
            "default" or "d" or null => ViewMode.Default,
            _ => null
        };
    }
}