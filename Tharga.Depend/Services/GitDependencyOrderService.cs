using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public class GitDependencyOrderService
{
    public void Print(List<GitRepositoryInfo> repos, DependencyGraphService graphService)
    {
        // Use all projects for dependency resolution (fixes missing edges)
        var allProjects = repos
            .SelectMany(r => r.Projects)
            .Where(x => x.IsPackable)
            .ToList();

        // Map: Project → GitRepoPath
        var projectToRepo = allProjects.ToDictionary(p => p, p =>
        {
            var repo = repos.First(r => r.Projects.Contains(p));
            return repo.Path;
        });

        // Build: Repo → Repos it depends on
        var repoDeps = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var repo in repos)
        {
            var thisRepoPath = repo.Path;

            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in repo.Projects)
            {
                var deps = graphService.GetProjectDependencies(project, allProjects);

                foreach (var dep in deps)
                {
                    if (projectToRepo.TryGetValue(dep, out var depRepoPath))
                    {
                        if (!string.Equals(depRepoPath, thisRepoPath, StringComparison.OrdinalIgnoreCase))
                            dependencies.Add(depRepoPath);
                    }
                }
            }

            repoDeps[thisRepoPath] = dependencies.ToList();
        }

        // 🧪 Debug output: Git Repo Dependencies
        Console.WriteLine("\n📊 Git Repo Dependencies:");
        foreach (var kvp in repoDeps)
        {
            if (kvp.Value.Count == 0) continue;

            Console.WriteLine($"- {kvp.Key}");
            foreach (var dep in kvp.Value)
            {
                Console.WriteLine($"    → {dep}");
            }
        }

        // Assign levels using topological sort
        var levelMap = new Dictionary<string, int>();
        var remaining = new HashSet<string>(repoDeps.Keys);

        while (remaining.Any())
        {
            var ready = remaining
                .Where(repo => repoDeps[repo].All(dep => levelMap.ContainsKey(dep)))
                .ToList();

            if (!ready.Any())
            {
                Console.WriteLine("❌ Circular dependency detected between Git repositories.\n");
                PrintCycle(repoDeps);
                return;
            }

            foreach (var repo in ready)
            {
                int level = repoDeps[repo].Select(dep => levelMap[dep]).DefaultIfEmpty(-1).Max() + 1;
                levelMap[repo] = level;
                remaining.Remove(repo);
            }
        }

        Console.WriteLine("\n✅ Resolved Git Repo Dependencies:\n");

        foreach (var repo in levelMap.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key))
        {
            Console.WriteLine($"[{repo.Value}] {repo.Key}");
        }
    }

    private void PrintCycle(Dictionary<string, List<string>> graph)
    {
        foreach (var kv in graph)
        {
            var from = kv.Key;
            foreach (var to in kv.Value)
            {
                if (graph.TryGetValue(to, out var deps) && deps.Contains(from))
                {
                    Console.WriteLine($"🔁 Cycle: {from} ↔ {to}");
                }
            }
        }
    }

}