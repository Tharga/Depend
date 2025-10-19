using System.Xml.Linq;
using Tharga.Depend.Models;

public interface IProjectService
{
    Task<ProjectInfo> ParseProject(string projectFilePath);
}

internal class ProjectService : IProjectService
{
    public async Task<ProjectInfo> ParseProject(string projectFilePath)
    {
        var doc = XDocument.Load(projectFilePath);
        var ns = doc.Root?.Name.Namespace ?? string.Empty;

        var name = Path.GetFileNameWithoutExtension(projectFilePath);

        // Package references
        var packageReferences = doc.Descendants(ns + "PackageReference")
            .Select(x => new PackageReferenceInfo
            {
                PackageId = x.Attribute("Include")?.Value ?? string.Empty,
                Version = x.Attribute("Version")?.Value ?? x.Element(ns + "Version")?.Value ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.PackageId))
            .ToList();

        // Project references
        var projectReferences = doc.Descendants(ns + "ProjectReference")
            .Select(x => new ProjectReferenceInfo
            {
                RelativePath = x.Attribute("Include")?.Value ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.RelativePath))
            .ToList();

        //var packages = packageReferences
        //    .Select(x => new PackageInfo
        //    {
        //        Name = x.PackageId,
        //        PackageId = x.PackageId,
        //        Version = x.Version,
        //        Type = PackageType.Reference
        //    })
        //    .Union(projectReferences.Select(x =>
        //    {
        //        var result = await GetProjectName(projectFilePath, x.RelativePath);

        //        return new PackageInfo
        //        {
        //            Name = result.Name,
        //            PackageId = result.PackageId,
        //            Path = result.Path,
        //            Type = PackageType.Project
        //        };
        //    }))
        //    .ToArray();
        var packages = await BuildPackageInfos(projectFilePath, packageReferences).ToArrayAsync();
        var projects = await BuildProjectInfos(projectFilePath, projectReferences).ToArrayAsync();

        return new ProjectInfo
        {
            Path = projectFilePath,
            Name = name,
            PackageId = GetPackageId(doc, name),
            Packages = projects.ToArray()
        };
    }

    private async IAsyncEnumerable<PackageInfo> BuildPackageInfos(string projectFilePath, IEnumerable<PackageReferenceInfo> projectReferences)
    {
        foreach (var x in projectReferences)
        {
            var packageInfo = new PackageInfo
                {
                    Name = x.PackageId,
                    PackageId = x.PackageId,
                    Version = x.Version,
                    Type = PackageType.Reference
                };

            yield return packageInfo;
        }
    }

    private async IAsyncEnumerable<PackageInfo> BuildProjectInfos(string projectFilePath, IEnumerable<ProjectReferenceInfo> projectReferences)
    {
        foreach (var x in projectReferences)
        {
            var result = await GetProjectName(projectFilePath, x.RelativePath);
            var packageInfo = new PackageInfo
            {
                Name = result.Name,
                PackageId = result.PackageId,
                Path = result.Path,
                Type = PackageType.Project
            };
            yield return packageInfo;
        }
    }

    private async Task<(string Name, string Path, bool Exists, string PackageId)> GetProjectName(string projectFilePath, string argRelativePath)
    {
        var dir = Path.GetDirectoryName(projectFilePath);
        var fullProjectPath = Path.Combine(dir, argRelativePath);
        var exists = File.Exists(fullProjectPath);

        string packageId = null;
        if (exists)
        {
            //TODO: Open to get the packageId
            var parsed = await ParseProject(fullProjectPath);
            packageId = parsed.PackageId;
        }

        var fn = Path.GetFileName(fullProjectPath).Replace(".csproj", "");
        return (fn, fullProjectPath, exists, packageId);
    }

    private static string GetPackageId(XDocument doc, string name)
    {
        var ns = doc.Root?.Name.Namespace ?? string.Empty;

        // Step 1: Explicit IsPackable
        var rawIsPackable = doc.Descendants(ns + "IsPackable")
            .Select(x => x.Value?.Trim().ToLowerInvariant())
            .FirstOrDefault();

        var hasExplicitIsPackable = rawIsPackable is "true" or "false";
        var isExplicitlyPackable = rawIsPackable == "true";

        // Step 2: Heuristic detection based on NuGet-related elements
        var hasNugetMetadata = doc.Descendants()
            .Any(x =>
                x.Name.LocalName is "PackageId" or "Version" or "Authors" or "Company" or "Product" or "Description" or "PackageIconUrl" or "PackageProjectUrl" or "PackageReadmeFile");

        // Step 3: Combine logic
        var isPackable = hasExplicitIsPackable
            ? isExplicitlyPackable
            : hasNugetMetadata;
        var packageId = isPackable ? (doc.Descendants(ns + "PackageId").FirstOrDefault()?.Value ?? name) : null;
        return packageId;
    }
}

public interface IGitRepositoryService
{
    IAsyncEnumerable<GitRepositoryInfo> GetAsync(string rootPath);
}

internal class GitRepositoryService : IGitRepositoryService
{
    private readonly IProjectService _projectService;

    public GitRepositoryService(IProjectService projectService)
    {
        _projectService = projectService;
    }

    public async IAsyncEnumerable<GitRepositoryInfo> GetAsync(string rootPath)
    {
        if (!Directory.Exists(rootPath)) throw new InvalidOperationException($"Path {rootPath} does not exist.");

        //TODO: Protect from nested repos.
        //TODO: Protect from duplicate repos and projects.

        foreach (var repoPath in Directory
                     .EnumerateDirectories(rootPath, ".git", SearchOption.AllDirectories)
                     .Select(Path.GetDirectoryName)
                     .Where(path => path != null))
        {
            var subRepos = Directory
                .EnumerateDirectories(repoPath, ".git", SearchOption.AllDirectories).Select(Path.GetDirectoryName)
                .Where(path => path != null)
                .Where(x => x != repoPath)
                .ToArray();

            var projects = Directory.EnumerateFiles(repoPath, "*.csproj", SearchOption.AllDirectories);

            var parsedProjects = await GetParsedProjects(projects)
                .Where(x => subRepos.All(y => !x.Path.StartsWith(y)))
                .ToArrayAsync();

            yield return new GitRepositoryInfo
            {
                Path = repoPath,
                Name = GetRepositoryName(repoPath),
                Projects = parsedProjects.ToArray(),
            };
        }
         //Directory
         //   .EnumerateDirectories(rootPath, ".git", SearchOption.AllDirectories)
         //   .Select(Path.GetDirectoryName)
         //   .Where(path => path != null)
         //   .Distinct()
         //   .OrderByDescending(path => path!.Count(c => c == Path.DirectorySeparatorChar));

        //var assignedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private async IAsyncEnumerable<ProjectInfo> GetParsedProjects(IEnumerable<string> projects)
    {
        foreach (var project in projects)
        {
            var parsedProject = await _projectService.ParseProject(project);
            yield return parsedProject;
        }
    }

    private string GetRepositoryName(string repoPath)
    {
        try
        {
            //var gitConfigPath = Path.Combine(repoPath, ".git", "config");
            //if (File.Exists(gitConfigPath))
            //{
            //    var lines = File.ReadAllLines(gitConfigPath);

            //    // Find a line that starts with "url ="
            //    var urlLine = lines
            //        .FirstOrDefault(l => l.TrimStart().StartsWith("url =", StringComparison.OrdinalIgnoreCase));

            //    if (!string.IsNullOrWhiteSpace(urlLine))
            //    {
            //        var url = urlLine.Split('=')[1].Trim();

            //        // Handle both HTTPS and SSH forms
            //        // e.g. "https://github.com/Tharga/Toolkit.git"
            //        // or    "git@github.com:Tharga/Toolkit.git"
            //        var lastPart = url
            //            .Replace('\\', '/')
            //            .Split(['/', ':'], StringSplitOptions.RemoveEmptyEntries)
            //            .LastOrDefault();

            //        if (!string.IsNullOrEmpty(lastPart))
            //        {
            //            // Remove trailing .git if present
            //            return Path.GetFileNameWithoutExtension(lastPart);
            //        }
            //    }
            //}

            // Fallback to folder name
            return new DirectoryInfo(repoPath).Name;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not resolve repository name for '{repoPath}': {ex.Message}");
            return new DirectoryInfo(repoPath).Name;
        }
    }
}

//public class GitDependencyOrderService
//{
//    public void Print(List<GitRepositoryInfo> repos, DependencyGraphService graphService)
//    {
//        // Use all projects for dependency resolution (fixes missing edges)
//        var allProjects = repos
//            .SelectMany(r => r.Projects)
//            .Where(x => x.IsPackable)
//            .ToList();

//        // Map: Project → GitRepoPath
//        var projectToRepo = allProjects.ToDictionary(p => p, p =>
//        {
//            var repo = repos.First(r => r.Projects.Contains(p));
//            return repo.Path;
//        });

//        // Build: Repo → Repos it depends on
//        var repoDeps = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

//        foreach (var repo in repos)
//        {
//            var thisRepoPath = repo.Path;

//            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//            foreach (var project in repo.Projects)
//            {
//                var deps = graphService.GetProjectDependencies(project, allProjects);

//                foreach (var dep in deps)
//                {
//                    if (projectToRepo.TryGetValue(dep, out var depRepoPath))
//                    {
//                        if (!string.Equals(depRepoPath, thisRepoPath, StringComparison.OrdinalIgnoreCase))
//                            dependencies.Add(depRepoPath);
//                    }
//                }
//            }

//            repoDeps[thisRepoPath] = dependencies.ToList();
//        }

//        // 🧪 Debug output: Git Repo Dependencies
//        Console.WriteLine("\n📊 Git Repo Dependencies:");
//        foreach (var kvp in repoDeps)
//        {
//            if (kvp.Value.Count == 0) continue;

//            Console.WriteLine($"- {kvp.Key}");
//            foreach (var dep in kvp.Value)
//            {
//                Console.WriteLine($"    → {dep}");
//            }
//        }

//        // Assign levels using topological sort
//        var levelMap = new Dictionary<string, int>();
//        var remaining = new HashSet<string>(repoDeps.Keys);

//        while (remaining.Any())
//        {
//            var ready = remaining
//                .Where(repo => repoDeps[repo].All(dep => levelMap.ContainsKey(dep)))
//                .ToList();

//            if (!ready.Any())
//            {
//                Console.WriteLine("❌ Circular dependency detected between Git repositories.\n");
//                PrintCycle(repoDeps);
//                return;
//            }

//            foreach (var repo in ready)
//            {
//                int level = repoDeps[repo].Select(dep => levelMap[dep]).DefaultIfEmpty(-1).Max() + 1;
//                levelMap[repo] = level;
//                remaining.Remove(repo);
//            }
//        }

//        Console.WriteLine("\n✅ Resolved Git Repo Dependencies:\n");

//        foreach (var repo in levelMap.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key))
//        {
//            Console.WriteLine($"[{repo.Value}] {repo.Key}");
//        }
//    }

//    private void PrintCycle(Dictionary<string, List<string>> graph)
//    {
//        foreach (var kv in graph)
//        {
//            var from = kv.Key;
//            foreach (var to in kv.Value)
//            {
//                if (graph.TryGetValue(to, out var deps) && deps.Contains(from))
//                {
//                    Console.WriteLine($"🔁 Cycle: {from} ↔ {to}");
//                }
//            }
//        }
//    }

//}