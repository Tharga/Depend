using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public class DependencyGraphService
{
    /// <summary>
    /// Calculates dependency levels for all packable projects.
    /// Level 0 = no dependencies on other local NuGet-packable projects.
    /// </summary>
    public Dictionary<ProjectInfo, int> CalculateDependencyLevels(List<GitRepositoryInfo> repositories)
    {
        // Step 1: Get all packable projects
        var allProjects = repositories
            .SelectMany(r => r.Projects)
            .Where(p => p.IsPackable)
            .ToList();

        // Step 2: Build lookup by PackageId (with warnings for duplicates)
        var lookupById = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new Dictionary<string, List<ProjectInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in allProjects)
        {
            if (string.IsNullOrWhiteSpace(project.PackageId))
                continue;

            if (lookupById.ContainsKey(project.PackageId))
            {
                if (!duplicates.ContainsKey(project.PackageId))
                    duplicates[project.PackageId] = new List<ProjectInfo> { lookupById[project.PackageId] };

                duplicates[project.PackageId].Add(project);
            }
            else
            {
                lookupById[project.PackageId] = project;
            }
        }

        if (duplicates.Any())
        {
            Console.WriteLine("⚠️  Duplicate PackageIds detected! These may indicate duplicate repos or misconfigurations:\n");

            foreach (var group in duplicates)
            {
                Console.WriteLine($"PackageId: {group.Key}");
                foreach (var proj in group.Value)
                {
                    Console.WriteLine($"  - {proj.Path}");
                }

                Console.WriteLine();
            }

            // Optionally halt:
            // throw new InvalidOperationException("Duplicate PackageIds must be resolved.");
        }

        // Step 3: Build dependency map (local package/project references only)
        var projectDependencies = allProjects.ToDictionary(
            project => project,
            project =>
            {
                var deps = new List<ProjectInfo>();

                // NuGet package references to other local projects
                foreach (var pkgRef in project.PackageReferences)
                {
                    if (lookupById.TryGetValue(pkgRef.PackageId, out var depProj))
                    {
                        deps.Add(depProj);
                    }
                }

                // Project references to other packable projects
                foreach (var projRef in project.ProjectReferences)
                {
                    var refPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.Path)!, projRef.RelativePath));
                    var match = allProjects.FirstOrDefault(p =>
                        string.Equals(Path.GetFullPath(p.Path), refPath, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                        deps.Add(match);
                }

                return deps.Distinct().ToList();
            });

        // Step 4: Topological sort with level assignment
        var levelMap = new Dictionary<ProjectInfo, int>();
        var remaining = new HashSet<ProjectInfo>(allProjects);

        while (remaining.Any())
        {
            // Find all projects whose dependencies are already resolved
            var ready = remaining.Where(p => projectDependencies[p].All(d => levelMap.ContainsKey(d))).ToList();

            if (!ready.Any())
            {
                Console.WriteLine("❌ Circular dependency detected between projects.");
                Console.WriteLine("   Cannot determine safe build order.\n");
                PrintProjectCycle(projectDependencies);
                break;
            }

            foreach (var proj in ready)
            {
                int level = projectDependencies[proj].Select(d => levelMap[d]).DefaultIfEmpty(-1).Max() + 1;
                levelMap[proj] = level;
                remaining.Remove(proj);
            }
        }

        return levelMap;
    }

    /// <summary>
    /// Returns the local project dependencies (via ProjectReference and PackageReference).
    /// </summary>
    public List<ProjectInfo> GetProjectDependencies(ProjectInfo project, List<ProjectInfo> allProjects)
    {
        var deps = new List<ProjectInfo>();

        // Build PackageId → List<ProjectInfo>
        var lookupById = allProjects
            .Where(p => !string.IsNullOrWhiteSpace(p.PackageId))
            .GroupBy(p => p.PackageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Log duplicates
        foreach (var group in lookupById.Where(g => g.Value.Count > 1))
        {
            Console.WriteLine($"⚠️  Multiple projects found with PackageId '{group.Key}':");
            foreach (var proj in group.Value)
            {
                Console.WriteLine($"   - {proj.Path}");
            }
            Console.WriteLine();
        }

        // Resolve by PackageReference
        foreach (var pkg in project.PackageReferences)
        {
            if (lookupById.TryGetValue(pkg.PackageId, out var matchingProjects))
            {
                foreach (var match in matchingProjects)
                {
                    deps.Add(match);
                }
            }
        }

        // Resolve by ProjectReference (path)
        var lookupByPath = allProjects
            .GroupBy(p => Path.GetFullPath(p.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var projRef in project.ProjectReferences)
        {
            var refPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.Path)!, projRef.RelativePath));
            if (lookupByPath.TryGetValue(refPath, out var dep))
            {
                deps.Add(dep);
            }
        }

        return deps.Distinct().ToList();
    }

    private void PrintProjectCycle(Dictionary<ProjectInfo, List<ProjectInfo>> graph)
    {
        Console.WriteLine("🔁 Potential circular dependencies:");
        foreach (var kv in graph)
        {
            var from = kv.Key;
            foreach (var to in kv.Value)
            {
                if (graph.TryGetValue(to, out var deps) && deps.Contains(from))
                {
                    Console.WriteLine($"  {from.PackageId} ↔ {to.PackageId}");
                }
            }
        }
    }
}
