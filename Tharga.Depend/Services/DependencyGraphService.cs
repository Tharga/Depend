using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public class DependencyGraphService
{
    public Dictionary<ProjectInfo, int> CalculateDependencyLevels(List<GitRepositoryInfo> repositories)
    {
        // Step 1: Flatten all packable projects (deduplicate)
        var allProjects = repositories
            .SelectMany(r => r.Projects)
            .Where(p => p.IsPackable)
            .GroupBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        // Step 2: Build lookup (by package id and by project file path)
        var projectLookup = allProjects.ToDictionary(p => p.PackageId!, StringComparer.OrdinalIgnoreCase);
        var projectPathLookup = allProjects.ToDictionary(p => Path.GetFullPath(p.Path), p => p);

        // Step 3: Build dependency map (PackageRef + ProjectRef)
        var dependencyMap = allProjects.ToDictionary(
            proj => proj,
            proj =>
            {
                var deps = new List<ProjectInfo>();

                // Add NuGet package dependencies that are local
                foreach (var pkg in proj.PackageReferences)
                {
                    if (projectLookup.TryGetValue(pkg.PackageId, out var depProject))
                        deps.Add(depProject);
                }

                // Add project references that point to other packable projects
                foreach (var projRef in proj.ProjectReferences)
                {
                    var fullRefPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(proj.Path)!, projRef.RelativePath));
                    if (projectPathLookup.TryGetValue(fullRefPath, out var depProject))
                        deps.Add(depProject);
                }

                return deps.Distinct().ToList();
            });

        // Step 4: Level calculation (topological order)
        var levelMap = new Dictionary<ProjectInfo, int>();
        var remaining = new HashSet<ProjectInfo>(allProjects);

        while (remaining.Any())
        {
            var ready = remaining.Where(p => dependencyMap[p].All(d => levelMap.ContainsKey(d))).ToList();

            if (!ready.Any())
                throw new InvalidOperationException("Circular dependency detected among NuGet projects.");

            foreach (var proj in ready)
            {
                int level = dependencyMap[proj].Select(d => levelMap[d]).DefaultIfEmpty(-1).Max() + 1;
                levelMap[proj] = level;
                remaining.Remove(proj);
            }
        }

        return levelMap;
    }
}
