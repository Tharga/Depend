using System.Diagnostics;
using Tharga.Depend.Features.Project;
using Tharga.Depend.Features.Repo;

namespace Tharga.Depend.Features.Output;

public abstract class OutputBase
{
    protected readonly IOutputService _output;

    protected OutputBase(IOutputService output)
    {
        _output = output;
    }

    protected bool ShouldInclude(ProjectInfo project, string excludePattern, bool onlyPackable)
    {
        if (!string.IsNullOrWhiteSpace(excludePattern) && project.Name.Contains(excludePattern, StringComparison.OrdinalIgnoreCase))
            return false;

        if (onlyPackable && string.IsNullOrWhiteSpace(project.PackageId))
            return false;

        return true;
    }

    //protected static string FormatId(string id) => string.IsNullOrEmpty(id) ? string.Empty : $" [{id}]";
    protected static string FormatId(string id, IEnumerable<ProjectInfo> allProjects)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        // If any known project matches this ID or Name, it's an internal project
        var isKnownProject = allProjects.Any(p =>
            string.Equals(p.PackageId, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, id, StringComparison.OrdinalIgnoreCase));

        return isKnownProject ? $" [{id}]" : string.Empty;
    }

    protected Dictionary<GitRepositoryInfo, HashSet<GitRepositoryInfo>> BuildRepositoryDependencyGraph(GitRepositoryInfo[] repos)
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

    protected Dictionary<ProjectInfo, List<ProjectInfo>> BuildProjectDependencyGraph(GitRepositoryInfo[] repos)
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

    protected void PrintProjectLine(GitRepositoryInfo[] repos, ProjectInfo project, int indentLevel = 0, int? level = null)
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

        var allProjects = repos.SelectMany(r => r.Projects).ToList();
        _output.WriteLine($"{indent}- {levelText}{project.Name}{FormatId(project.PackageId, allProjects)}{usageText}", ConsoleColor.Yellow);
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

    protected void PrintProjectDeps(GitRepositoryInfo[] repos, string excludePattern, bool onlyPackable, bool showProjectDeps, ProjectInfo project, int indentLevel = 4, Dictionary<ProjectInfo, int> levelMap = null)
    {
        if (!showProjectDeps) return;

        var projectDependencyGraph = BuildProjectDependencyGraph(repos);
        if (projectDependencyGraph.TryGetValue(project, out var deps) && deps.Any())
        {
            var indent = new string(' ', indentLevel);
            _output.WriteLine($"{indent}Referenced by:", ConsoleColor.Gray);

            var allProjects = repos.SelectMany(r => r.Projects).ToList();
            foreach (var dep in deps
                         .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
                         .OrderBy(p => levelMap?.GetValueOrDefault(p, int.MaxValue))
                         .ThenBy(p => p.Name))
            {
                _output.WriteLine($"{indent}- {dep.Name}{FormatId(dep.PackageId, allProjects)}", ConsoleColor.DarkYellow);
            }
        }
    }

    protected void PrintProjectUsages(ProjectInfo project, GitRepositoryInfo[] repos, bool showProjectUsages, string excludePattern, bool onlyPackable, int indentLevel = 4)
    {
        if (!showProjectUsages) return;

        var usageGraph = BuildProjectUsageGraph(repos);

        if (!usageGraph.TryGetValue(project, out var users) || !users.Any())
            return;

        var indent = new string(' ', indentLevel);
        _output.WriteLine($"{indent}Used by:", ConsoleColor.Gray);

        var allProjects = repos.SelectMany(r => r.Projects).ToList();

        foreach (var user in users
                     .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
                     .OrderBy(p => p.Name))
        {
            _output.WriteLine($"{indent}- {user.Name}{FormatId(user.PackageId, allProjects)}", ConsoleColor.DarkCyan);
        }
    }

    protected void PrintRepositoryLine(GitRepositoryInfo[] repos, GitRepositoryInfo repo, int? level = null)
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

    protected void PrintRepoDeps(GitRepositoryInfo[] repos, bool showRepoDeps, GitRepositoryInfo repo, int indentLevel = 2, Dictionary<GitRepositoryInfo, int> repoLevelMap = null)
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

    protected void PrintRepoUsages(GitRepositoryInfo repo, GitRepositoryInfo[] repos, bool showRepoUsages, int indentLevel = 2)
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

    protected void PrintProjectPackages(ProjectInfo project, Dictionary<string, string> latestVersions, string excludePattern, bool onlyPackable, bool showPackages, int indentLevel, List<ProjectInfo> allProjects)
    {
        if (!showPackages) return;

        var filteredPackages = project.Packages
            .Where(p => ShouldInclude(
                new ProjectInfo
                {
                    TargetFramework = project.TargetFramework,
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
            PrintPackageLine(package, latestVersions, indentLevel, allProjects);
        }
    }

    private void PrintPackageLine(PackageInfo package, Dictionary<string, string> latestVersions, int indentLevel, List<ProjectInfo> allProjects)
    {
        var indent = new string(' ', indentLevel);
        var versionText = package.Version ?? "Project";
        var line = $"{indent}- {package.Name}{FormatId(package.PackageId, allProjects)} ({versionText})";

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
                    _output.WriteLine($"{indent}- {package.Name}{FormatId(package.PackageId, allProjects)} ({versionText} -> {latest})", ConsoleColor.Red);
                    return;
                }
            }
            catch
            {
                // Fallback: in case version parsing fails (non-standard)
                if (string.Compare(package.Version, latest, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    _output.WriteLine($"{indent}- {package.Name}{FormatId(package.PackageId, allProjects)} ({versionText} -> {latest})", ConsoleColor.Red);
                    return;
                }
            }
        }

        _output.WriteLine(line, ConsoleColor.DarkGray);
    }
}