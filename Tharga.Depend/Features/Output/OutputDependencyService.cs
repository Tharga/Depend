using Tharga.Depend.Features.Repo;

namespace Tharga.Depend.Features.Output;

internal class OutputDependencyService : OutputBase, IOutputDependencyService
{
    public OutputDependencyService(IOutputService output)
        : base(output)
    {
    }

    public void PrintDependencyList(GitRepositoryInfo[] repos, string projectName, string excludePattern, bool onlyPackable, ViewMode viewMode, bool showRepoDeps, bool showProjectDeps, bool showRepoUsages, bool showProjectUsages, Dictionary<string, string> latestVersions)
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
        var allProjects = repos.SelectMany(x => x.Projects).ToList();
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
                PrintProjectPackages(project, latestVersions, excludePattern, onlyPackable, showPackages, 4, allProjects);
            }
        }
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
}