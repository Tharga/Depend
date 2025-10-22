using System.Runtime.Intrinsics.Arm;
using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public interface ICommandService
{
    Task<int> ExecuteAsync(string[] args);
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

    public async Task<int> ExecuteAsync(string[] args)
    {
        var argsList = args.ToList();
        if (argsList.Contains("--help") || argsList.Contains("-h"))
        {
            _output.PrintHelp();
            return 0;
        }

        _pathService.EnsureInUserPath();

        var rootPath = _pathService.GetRootPath(argsList);
        if (rootPath == null)
        {
            _output.Error("Error: Please provide a valid folder path.");
            _output.PrintHelp();
            return 1;
        }

        // Load all repositories
        var allRepos = await _gitRepoService.GetAsync(rootPath).ToArrayAsync();

        // Compute latest known package versions from all repos
        var latestPackageVersions = GetLatestPackageVersions(allRepos);

        WarnIfDuplicateProjectNames(allRepos);

        // Now parse CLI options
        var outputType = GetOptionValue(argsList, "dependency", "--output", "-o");
        var viewMode = GetOptionValue(argsList, "default", "--view", "-v");
        var projectName = GetOptionValue(argsList, "", "--project", "-p");
        var excludePattern = GetOptionValue(argsList, "", "--exclude", "-x");
        var onlyPackable = argsList.Contains("--only-packable") || argsList.Contains("-n");
        var showRepoDeps = argsList.Contains("--repo-deps") || argsList.Contains("-rd");
        var showProjectDeps = argsList.Contains("--project-deps") || argsList.Contains("-pd");

        // Filter for display only if -p is specified
        var displayRepos = string.IsNullOrWhiteSpace(projectName)
            ? allRepos
            : FilterReposByProject(allRepos, projectName);

        switch (outputType)
        {
            case "list":
                PrintRepositoryList(displayRepos, rootPath, projectName, excludePattern, onlyPackable, viewMode, latestPackageVersions);
                break;

            case "dependency":
                PrintDependencyList(displayRepos, projectName, excludePattern, onlyPackable, viewMode, showRepoDeps, showProjectDeps, latestPackageVersions);
                break;

            default:
                _output.Error($"Unknown output mode: {outputType}");
                return 2;
        }

        return 0;
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

    private void PrintRepositoryList(IEnumerable<GitRepositoryInfo> repositories,
        string rootPath,
        string projectName,
        string excludePattern,
        bool onlyPackable,
        string viewMode, Dictionary<string, string> latestVersions)
    {
        var repos = repositories.ToArray();

        var showPackages = viewMode == "full";
        var showProjects = viewMode is "default" or "full";
        var showRepos = viewMode is not "project-only";

        var allProjects = repos.SelectMany(r => r.Projects).ToList();

        if (viewMode == "project-only")
        {
            foreach (var project in allProjects
                         .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
                         .OrderBy(p => p.Name))
            {
                _output.WriteLine($"- {project.Name}{FormatId(project.PackageId)}", ConsoleColor.Yellow);
            }

            return;
        }

        foreach (var repo in repos.OrderBy(r => r.Name))
        {
            if (!string.IsNullOrEmpty(projectName) && repo.Projects.All(p => p.Name != projectName)) continue;

            var filteredProjects = repo.Projects
                .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
                .ToList();

            if (!filteredProjects.Any() && showProjects)
                continue;

            if (showRepos)
                _output.WriteLine($"- {repo.Name} ({Path.GetRelativePath(rootPath, repo.Path)})", ConsoleColor.Green);

            if (!showProjects) continue;

            foreach (var project in filteredProjects.OrderBy(p => p.Name))
            {
                _output.WriteLine($"  - {project.Name}{FormatId(project.PackageId)}", ConsoleColor.Yellow);

                if (showPackages)
                {
                    var filteredPackages = project.Packages
                        .Where(p => ShouldInclude(new ProjectInfo
                        {
                            Name = p.Name,
                            PackageId = p.PackageId,
                            Packages = [],
                            Path = p.Path
                        }, excludePattern, onlyPackable))
                        .OrderBy(p => p.Name);

                    foreach (var package in filteredPackages)
                        PrintPackageLine(package, latestVersions, 4);
                }
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

    private Dictionary<ProjectInfo, int> GetLevelMap(
        GitRepositoryInfo[] gitRepositoryInfos,
        string targetProject,
        string excludePattern,
        bool onlyPackable,
        bool includeAllProjects)
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
                _output.WriteLine($"    - {project.Path}", ConsoleColor.DarkGray);
        }

        _output.WriteLine(""); // Add space after warning block
    }

    private void PrintDependencyList(
        GitRepositoryInfo[] repos,
        string projectName,
        string excludePattern,
        bool onlyPackable,
        string viewMode,
        bool showRepoDeps,
        bool showProjectDeps,
        Dictionary<string, string> latestVersions)
    {
        var showPackages = viewMode == "full";
        var showProjects = viewMode is "default" or "full";
        var showRepos = viewMode is not "project-only";

        var levelMap = GetLevelMap(repos, projectName, excludePattern, onlyPackable, includeAllProjects: true);

        var orderedProjects = levelMap
            .Where(kv => ShouldInclude(kv.Key, excludePattern, onlyPackable))
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => kv.Key.Name)
            .Select(kv => kv.Key)
            .ToList();

        var repoLevelMap = GetRepositoryLevelMap(repos, out var hasRepoCycle);
        if (hasRepoCycle) _output.Warning("Circular Git repository dependency detected. Git-level ordering may be partial.");

        if (viewMode == "project-only")
        {
            foreach (var project in orderedProjects)
            {
                var level = levelMap.GetValueOrDefault(project, -1);
                _output.WriteLine($"- [{level}] {project.Name}{FormatId(project.PackageId)}", ConsoleColor.Yellow);

                if (showPackages)
                {
                    foreach (var package in project.Packages
                                 .Where(p => ShouldInclude(new ProjectInfo { Name = p.Name, PackageId = p.PackageId, Packages = [], Path = p.Path }, excludePattern, onlyPackable))
                                 .OrderBy(p => p.Name))
                    {
                        PrintPackageLine(package, latestVersions, 2);
                    }
                }
            }

            if (showProjectDeps)
            {
                var projectDependencyGraph = BuildProjectDependencyGraph(repos);

                foreach (var project in orderedProjects)
                {
                    var level = levelMap.GetValueOrDefault(project, -1);
                    _output.WriteLine($"- [{level}] {project.Name}{FormatId(project.PackageId)}", ConsoleColor.Yellow);

                    if (projectDependencyGraph.TryGetValue(project, out var deps) && deps.Any())
                    {
                        foreach (var dep in deps
                                     .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
                                     .OrderBy(p => levelMap.GetValueOrDefault(p, int.MaxValue))
                                     .ThenBy(p => p.Name))
                        {
                            //var depLevel = levelMap.GetValueOrDefault(dep, -1);
                            //_output.WriteLine($"  - [{depLevel}] {dep.Name}{FormatId(dep.PackageId)}", ConsoleColor.DarkYellow);
                            _output.WriteLine($"  - {dep.Name}{FormatId(dep.PackageId)}", ConsoleColor.DarkYellow);
                        }
                    }

                    if (showPackages)
                    {
                        foreach (var package in project.Packages
                                     .Where(p => ShouldInclude(new ProjectInfo
                                     {
                                         Name = p.Name,
                                         PackageId = p.PackageId,
                                         Packages = [],
                                         Path = p.Path
                                     }, excludePattern, onlyPackable))
                                     .OrderBy(p => p.Name))
                        {
                            PrintPackageLine(package, latestVersions, 2);
                        }
                    }
                }
            }

            return;
        }

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
                _output.WriteLine($"- [{repoLevel}] {repo.Name}", ConsoleColor.Green);

                if (showRepoDeps)
                {
                    var graph = BuildRepositoryDependencyGraph(repos);

                    if (graph.TryGetValue(repo, out var deps) && deps.Any())
                    {
                        foreach (var dep in deps
                                     .OrderBy(r => repoLevelMap.GetValueOrDefault(r, int.MaxValue))
                                     .ThenBy(r => r.Name))
                        {
                            //var depLevel = repoLevelMap.GetValueOrDefault(dep, -1);
                            //_output.WriteLine($"  - [{depLevel}] {dep.Name}", ConsoleColor.DarkGreen);
                            _output.WriteLine($"  - {dep.Name}", ConsoleColor.DarkGreen);
                        }
                    }
                }
            }

            if (!showProjects) continue;

            foreach (var project in repoProjects)
            {
                var level = levelMap.GetValueOrDefault(project, -1);
                _output.WriteLine($"  - [{level}] {project.Name}{FormatId(project.PackageId)}", ConsoleColor.Yellow);

                if (showProjectDeps)
                {
                    var projectDependencyGraph = BuildProjectDependencyGraph(repos);
                    if (projectDependencyGraph.TryGetValue(project, out var deps) && deps.Any())
                    {
                        foreach (var dep in deps
                                     .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
                                     .OrderBy(p => levelMap.GetValueOrDefault(p, int.MaxValue))
                                     .ThenBy(p => p.Name))
                        {
                            //var depLevel = levelMap.GetValueOrDefault(dep, -1);
                            //_output.WriteLine($"    - [{depLevel}] {dep.Name}{FormatId(dep.PackageId)}", ConsoleColor.DarkYellow);
                            _output.WriteLine($"    - {dep.Name}{FormatId(dep.PackageId)}", ConsoleColor.DarkYellow);
                        }
                    }
                }

                if (showPackages)
                {
                    foreach (var package in project.Packages
                                 .Where(p => ShouldInclude(new ProjectInfo { Name = p.Name, PackageId = p.PackageId, Packages = [], Path = p.Path }, excludePattern, onlyPackable))
                                 .OrderBy(p => p.Name))
                    {
                        PrintPackageLine(package, latestVersions, 4);
                    }
                }
            }
        }
    }

    //private void WarnIfDuplicateGitRepositories(GitRepositoryInfo[] repos)
    //{
    //    var duplicates = repos
    //        .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
    //        .Where(g => g.Count() > 1)
    //        .ToList();

    //    if (!duplicates.Any()) return;

    //    _output.Warning("⚠️ Duplicate Git repository names detected (likely cloned copies in different paths):");

    //    foreach (var group in duplicates)
    //    {
    //        _output.WriteLine($"  - Repo: {group.Key}", ConsoleColor.Yellow);
    //        foreach (var repo in group.OrderBy(r => r.Path))
    //            _output.WriteLine($"    - {repo.Path}", ConsoleColor.DarkGray);
    //    }

    //    _output.WriteLine("");
    //}

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

    //private void ShowRepositoryDependencies(GitRepositoryInfo[] repos, string targetRepoName)
    //{
    //    var graph = BuildRepositoryDependencyGraph(repos);

    //    var targetRepo = repos.FirstOrDefault(r =>
    //        string.Equals(r.Name, targetRepoName, StringComparison.OrdinalIgnoreCase));

    //    if (targetRepo == null)
    //    {
    //        _output.Warning($"⚠️ Repository not found: {targetRepoName}");
    //        return;
    //    }

    //    var deps = graph.TryGetValue(targetRepo, out var set) ? set : null;
    //    if (deps == null || deps.Count == 0)
    //    {
    //        _output.WriteLine($"{targetRepoName} has no Git repository dependencies.", ConsoleColor.Gray);
    //        return;
    //    }

    //    _output.WriteLine($"{targetRepoName} depends on:", ConsoleColor.Cyan);
    //    foreach (var dep in deps.OrderBy(x => x.Name))
    //    {
    //        _output.WriteLine($"  - {dep.Name}", ConsoleColor.Yellow);
    //    }
    //}

    private Dictionary<ProjectInfo, List<ProjectInfo>> BuildProjectDependencyGraph(
        GitRepositoryInfo[] repos)
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

    //private void WarnIfProjectsUseOldPackageVersions(GitRepositoryInfo[] repos)
    //{
    //    var allProjects = repos.SelectMany(r => r.Projects).ToList();

    //    // Group by package name, collecting project + version info
    //    var packageUsages = allProjects
    //        .SelectMany(p => p.Packages.Select(pkg => (Project: p, Package: pkg)))
    //        .Where(x => !string.IsNullOrWhiteSpace(x.Package.Name) && !string.IsNullOrWhiteSpace(x.Package.Version))
    //        .GroupBy(x => x.Package.Name, StringComparer.OrdinalIgnoreCase);

    //    foreach (var packageGroup in packageUsages)
    //    {
    //        var packageName = packageGroup.Key;

    //        // Parse version objects and group by version
    //        var versionGroups = packageGroup
    //            .GroupBy(x => x.Package.Version)
    //            .OrderByDescending(g => g.Key)
    //            .ToList();

    //        if (versionGroups.Count <= 1)
    //            continue; // No version conflict

    //        var latestVersion = versionGroups.First().Key;

    //        foreach (var oldGroup in versionGroups.Skip(1))
    //        {
    //            var oldVersion = oldGroup.Key;
    //            foreach (var usage in oldGroup)
    //            {
    //                _output.Warning($"- Project '{usage.Project.Name}' uses {packageName} {oldVersion} (latest known is {latestVersion}).");
    //            }
    //        }
    //    }
    //}

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

}
