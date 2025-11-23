using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Tharga.Depend.Features.Repo;

namespace Tharga.Depend.Features.Output;

internal class OutputTreeService : OutputBase, IOutputTreeService
{
    private static readonly Dictionary<string, List<(string Id, string Version)>> _nugetCache
        = new(StringComparer.OrdinalIgnoreCase);

    public OutputTreeService(IOutputService output)
        : base(output)
    {
    }

    public void PrintTree(GitRepositoryInfo[] repos, ViewMode viewMode, string excludePattern, bool onlyPackable)
    {
        switch (viewMode)
        {
            case ViewMode.RepoOnly:
                PrintRepoTreeDependencies(repos, excludePattern, onlyPackable);
                break;

            case ViewMode.ProjectOnly:
                PrintProjectTreeDependencies(repos, excludePattern, onlyPackable);
                break;

            case ViewMode.Full:
                PrintFullTree(repos, excludePattern, onlyPackable);
                break;

            default:
                PrintMixedTree(repos, excludePattern, onlyPackable);
                break;
        }
    }

    // ---------------------------
    // REPO-ONLY TREE
    // ---------------------------
    private void PrintRepoTreeDependencies(GitRepositoryInfo[] repos, string excludePattern, bool onlyPackable)
    {
        // BuildRepositoryDependencyGraph returns Dictionary<GitRepositoryInfo, HashSet<GitRepositoryInfo>>
        var graph = BuildRepositoryDependencyGraph(repos);

        var roots = FindRepoRoots(repos, graph);

        _output.WriteLine(".", ConsoleColor.White);
        foreach (var root in roots)
        {
            PrintRepoNode(root, graph, "", root == roots.Last());
        }
    }

    private List<GitRepositoryInfo> FindRepoRoots(
        GitRepositoryInfo[] repos,
        Dictionary<GitRepositoryInfo, HashSet<GitRepositoryInfo>> graph)
    {
        var allDeps = graph.Values.SelectMany(x => x).ToHashSet();
        return repos
            .Where(r => !allDeps.Contains(r))
            .OrderBy(r => r.Name)
            .ToList();
    }

    private void PrintRepoNode(
        GitRepositoryInfo repo,
        Dictionary<GitRepositoryInfo, HashSet<GitRepositoryInfo>> graph,
        string prefix,
        bool isLast)
    {
        var branch = isLast ? "└── " : "├── ";
        var repoName = GetDisplayName(repo);
        _output.WriteLine($"{prefix}{branch}{repoName}", ConsoleColor.Green);

        if (!graph.TryGetValue(repo, out var deps) || deps.Count == 0)
        {
            return;
        }

        var subPrefix = prefix + (isLast ? "    " : "│   ");

        // Order deterministically when iterating a HashSet
        var ordered = deps.OrderBy(x => x.Name).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            PrintRepoNode(ordered[i], graph, subPrefix, i == ordered.Count - 1);
        }
    }

    // ---------------------------
    // PROJECT-ONLY TREE
    // ---------------------------
    private void PrintProjectTreeDependencies(GitRepositoryInfo[] repos, string excludePattern, bool onlyPackable)
    {
        var graph = BuildProjectDependencyGraph(repos);

        var allDeps = graph.Values.SelectMany(x => x).ToHashSet();
        var allProjects = repos.SelectMany(x => x.Projects).ToList();
        var roots = graph.Keys
            .Where(p => !allDeps.Contains(p))
            .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
            .OrderBy(p => p.Name)
            .ToList();

        _output.WriteLine(".", ConsoleColor.White);
        foreach (var root in roots)
        {
            PrintProjectNode(root, graph, "", root == roots.Last(), allProjects);
        }
    }

    private void PrintProjectNode(ProjectInfo project, Dictionary<ProjectInfo, List<ProjectInfo>> graph, string prefix, bool isLast, List<ProjectInfo> allProjects)
    {
        var branch = isLast ? "└── " : "├── ";
        _output.WriteLine($"{prefix}{branch}{project.Name}{FormatId(project.PackageId, allProjects)}", ConsoleColor.Yellow);

        if (!graph.TryGetValue(project, out var deps) || deps.Count == 0)
        {
            return;
        }

        var subPrefix = prefix + (isLast ? "    " : "│   ");
        var orderedDeps = deps.OrderBy(x => x.Name).ToList();

        for (var i = 0; i < orderedDeps.Count; i++)
        {
            PrintProjectNode(orderedDeps[i], graph, subPrefix, i == orderedDeps.Count - 1, allProjects);
        }
    }

    // ---------------------------
    // FULL TREE (Repos + Projects + Packages)
    // ---------------------------
    private void PrintFullTree(GitRepositoryInfo[] repos, string excludePattern, bool onlyPackable)
    {
        _output.WriteLine(".", ConsoleColor.White);

        var orderedRepos = repos.OrderBy(r => r.Name).ToList();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allProjects = repos.SelectMany(r => r.Projects).ToList();

        foreach (var repo in orderedRepos)
        {
            var repoName = GetDisplayName(repo);
            _output.WriteLine($"├── {repoName}", ConsoleColor.Green);

            var repoPrefix = "│   ";
            var projects = repo.Projects
                .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
                .OrderBy(p => p.Name)
                .ToList();

            for (var j = 0; j < projects.Count; j++)
            {
                var project = projects[j];
                var projBranch = j == projects.Count - 1 ? "└── " : "├── ";
                _output.WriteLine($"{repoPrefix}{projBranch}{project.Name}{FormatId(project.PackageId, allProjects)}", ConsoleColor.Yellow);

                var pkgPrefix = repoPrefix + (j == projects.Count - 1 ? "    " : "│   ");
                var packages = project.Packages.OrderBy(p => p.Name).ToList();

                var resolvedGraph = LoadResolvedGraphFromAssets(project.Path); // per project
                for (var k = 0; k < packages.Count; k++)
                {
                    var pkg = packages[k];
                    var pkgBranch = k == packages.Count - 1 ? "└── " : "├── ";
                    _output.WriteLine($"{pkgPrefix}{pkgBranch}{pkg.Name}{FormatId(pkg.PackageId, allProjects)} ({pkg.Version ?? "Project"})", ConsoleColor.DarkGray);

                    if (!string.IsNullOrWhiteSpace(pkg.Name) && !string.IsNullOrWhiteSpace(pkg.Version))
                    {
                        var isPkgLast = k == packages.Count - 1;
                        var childPrefix = pkgPrefix + (isPkgLast ? "    " : "│   ");

                        PrintPackageNodeResolvedFirst(
                            childPrefix,
                            true, // <-- always treat first dependency level as LAST
                            pkg.Name,
                            pkg.Version,
                            resolvedGraph,
                            1,
                            10,
                            visited,
                            false
                        );
                    }
                }
            }
        }
    }

    // ---------------------------
    // MIXED TREE (default)
    // ---------------------------
    private void PrintMixedTree(GitRepositoryInfo[] repos, string excludePattern, bool onlyPackable)
    {
        var repoGraph = BuildRepositoryDependencyGraph(repos); // Dictionary<GitRepositoryInfo, HashSet<GitRepositoryInfo>>
        var projectGraph = BuildProjectDependencyGraph(repos); // Dictionary<ProjectInfo, List<ProjectInfo>>
        var repoRoots = FindRepoRoots(repos, repoGraph); // uses HashSet version

        _output.WriteLine(".", ConsoleColor.White);

        var allProjects = repoRoots.SelectMany(x => x.Projects).ToList();
        foreach (var repo in repoRoots)
        {
            PrintMixedRepoNode(
                repo,
                repoGraph,
                projectGraph,
                "",
                repo == repoRoots.Last(),
                excludePattern,
                onlyPackable,
                allProjects);
        }
    }

    private void PrintMixedRepoNode(
        GitRepositoryInfo repo,
        Dictionary<GitRepositoryInfo, HashSet<GitRepositoryInfo>> repoGraph,
        Dictionary<ProjectInfo, List<ProjectInfo>> projectGraph,
        string prefix,
        bool isLast,
        string excludePattern,
        bool onlyPackable,
        List<ProjectInfo> allProjects)
    {
        var branch = isLast ? "└── " : "├── ";
        var repoName = GetDisplayName(repo);
        _output.WriteLine($"{prefix}{branch}{repoName}", ConsoleColor.Green);

        var subPrefix = prefix + (isLast ? "    " : "│   ");

        // 1) Projects in this repo
        var projects = repo.Projects
            .Where(p => ShouldInclude(p, excludePattern, onlyPackable))
            .OrderBy(p => p.Name)
            .ToList();

        for (var i = 0; i < projects.Count; i++)
        {
            var project = projects[i];
            var repoHasDeps = repoGraph.TryGetValue(repo, out var _deps) && _deps.Count > 0;
            var projIsLast = i == projects.Count - 1 && !repoHasDeps;

            var projBranch = projIsLast ? "└── " : "├── ";
            _output.WriteLine($"{subPrefix}{projBranch}{project.Name}{FormatId(project.PackageId, allProjects)}", ConsoleColor.Yellow);

            // Internal project deps (same repo)
            if (projectGraph.TryGetValue(project, out var projDeps) && projDeps.Count > 0)
            {
                var internalDeps = projDeps
                    .Where(dep => repo.Projects.Contains(dep))
                    .OrderBy(dep => dep.Name)
                    .ToList();

                if (internalDeps.Count > 0)
                {
                    var projSubPrefix = subPrefix + (projIsLast ? "    " : "│   ");
                    for (var j = 0; j < internalDeps.Count; j++)
                    {
                        var dep = internalDeps[j];
                        var depBranch = j == internalDeps.Count - 1 ? "└── " : "├── ";
                        _output.WriteLine($"{projSubPrefix}{depBranch}{dep.Name}{FormatId(dep.PackageId, allProjects)}", ConsoleColor.Yellow);
                    }
                }
            }
        }

        // 2) Dependent repositories
        if (repoGraph.TryGetValue(repo, out var repoDeps) && repoDeps.Count > 0)
        {
            var orderedDeps = repoDeps.OrderBy(x => x.Name).ToList();
            for (var i = 0; i < orderedDeps.Count; i++)
            {
                var dep = orderedDeps[i];
                PrintMixedRepoNode(
                    dep,
                    repoGraph,
                    projectGraph,
                    subPrefix,
                    i == orderedDeps.Count - 1,
                    excludePattern,
                    onlyPackable,
                    allProjects);
            }
        }
    }

    private List<(string Id, string Version)> GetNugetDependencies(string packageId, string version, string requestedTfm)
    {
        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
        {
            return new List<(string Id, string Version)>();
        }

        var cacheKey = $"{packageId.ToLowerInvariant()}:{version}:{requestedTfm?.ToLowerInvariant()}";
        if (_nugetCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            // Locate or download the .nupkg
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localPath = Path.Combine(userProfile, ".nuget", "packages", packageId.ToLowerInvariant(), version, $"{packageId.ToLowerInvariant()}.{version}.nupkg");

            var nupkgPath = localPath;
            if (!File.Exists(nupkgPath))
            {
                var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{version}/{packageId.ToLowerInvariant()}.{version}.nupkg";
                var tmp = Path.Combine(Path.GetTempPath(), $"{packageId.ToLowerInvariant()}.{version}.nupkg");
                using (var http = new HttpClient())
                {
                    var resp = http.GetAsync(url).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode)
                    {
                        _nugetCache[cacheKey] = new List<(string Id, string Version)>();
                        return new List<(string Id, string Version)>();
                    }

                    using var fs = File.Create(tmp);
                    resp.Content.CopyToAsync(fs).GetAwaiter().GetResult();
                }

                nupkgPath = tmp;
            }

            using var zip = ZipFile.OpenRead(nupkgPath);
            var nuspecEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            if (nuspecEntry == null)
            {
                _nugetCache[cacheKey] = new List<(string Id, string Version)>();
                return new List<(string Id, string Version)>();
            }

            using var stream = nuspecEntry.Open();
            var doc = XDocument.Load(stream);
            var ns = doc.Root?.Name.Namespace ?? throw new NullReferenceException("No doc root name.");

            // Prefer grouped dependencies by best TFM match; fall back to ungrouped
            var depsNode = doc.Descendants(ns + "dependencies").FirstOrDefault();
            if (depsNode == null)
            {
                _nugetCache[cacheKey] = new List<(string Id, string Version)>();
                return new List<(string Id, string Version)>();
            }

            var groups = depsNode.Elements(ns + "group").ToList();

            IEnumerable<XElement> chosenDeps;

            if (groups.Count > 0)
            {
                // No requested TFM? Take the ungrouped or first group
                if (string.IsNullOrWhiteSpace(requestedTfm))
                {
                    var any = groups
                        .Where(g => string.IsNullOrWhiteSpace((string)g.Attribute("targetFramework")))
                        .SelectMany(g => g.Elements(ns + "dependency"))
                        .ToList();

                    if (any.Count == 0)
                    {
                        // fallback to first group
                        chosenDeps = groups.First().Elements(ns + "dependency");
                    }
                    else
                    {
                        chosenDeps = any;
                    }
                }
                else
                {
                    // Find best match
                    var compatibleGroups = groups
                        .Select(g => new
                        {
                            Node = g,
                            Tfm = (string)g.Attribute("targetFramework") ?? string.Empty
                        })
                        .Where(x => IsGroupCompatible(x.Tfm, requestedTfm))
                        .OrderByDescending(x => RankTfm(x.Tfm))
                        .ToList();

                    chosenDeps = compatibleGroups.FirstOrDefault()?.Node.Elements(ns + "dependency")
                                 ?? Enumerable.Empty<XElement>();
                }
            }
            else
            {
                // No groups at all — read direct <dependency> children
                chosenDeps = depsNode.Elements(ns + "dependency");
            }

            var deps = chosenDeps
                .Select(x => (
                    Id: (string)x.Attribute("id"),
                    Version: NormalizeVersion((string)x.Attribute("version") ?? "")
                ))
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .Distinct()
                .ToList();

            // Remove self-reference
            deps.RemoveAll(d => d.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));

            _nugetCache[cacheKey] = deps;
            return deps;
        }
        catch
        {
            _nugetCache[cacheKey] = new List<(string Id, string Version)>();
            return new List<(string Id, string Version)>();
        }
    }

    private static int RankTfm(string tfm)
    {
        if (string.IsNullOrWhiteSpace(tfm)) return 0;
        tfm = tfm.ToLowerInvariant();

        // crude but practical ranking (higher is more specific/newer)
        if (tfm.StartsWith("net10.0")) return 10000;
        if (tfm.StartsWith("net9.0")) return 9000;
        if (tfm.StartsWith("net8.0")) return 8000;
        if (tfm.StartsWith("net7.0")) return 7000;
        if (tfm.StartsWith("net6.0")) return 6000;
        if (tfm.StartsWith("net5.0")) return 5000;
        if (tfm.StartsWith("netcoreapp3.1")) return 3100;
        if (tfm.StartsWith("netstandard2.1")) return 2100;
        if (tfm.StartsWith("netstandard2.0")) return 2000;
        if (tfm.StartsWith("netstandard1.")) return 1100;
        if (tfm.StartsWith("net4")) return 400; // net48, etc.
        return 1;
    }

    private static bool IsGroupCompatible(string groupTfm, string requestedTfm)
    {
        if (string.IsNullOrWhiteSpace(groupTfm)) return true; // ungrouped dependencies = universal
        if (string.IsNullOrWhiteSpace(requestedTfm)) return true;

        // Very simple heuristic:
        // prefer exact, else allow older groups that a newer TF can run against (netstandard etc.).
        groupTfm = groupTfm.ToLowerInvariant();
        requestedTfm = requestedTfm.ToLowerInvariant();

        if (requestedTfm == groupTfm) return true;

        // netX can use older netstandard groups
        if (requestedTfm.StartsWith("net") && groupTfm.StartsWith("netstandard")) return true;

        // e.g. net9.0 can consume net6.0, net5.0 in many packages (heuristic)
        if (requestedTfm.StartsWith("net") && groupTfm.StartsWith("net"))
        {
            // compare major
            var reqMajor = ParseNetMajor(requestedTfm);
            var grpMajor = ParseNetMajor(groupTfm);
            return grpMajor <= reqMajor; // allow older
        }

        // allow fallback to universal
        return false;
    }

    private static int ParseNetMajor(string tfm)
    {
        // "net9.0" -> 9 ; "net6.0" -> 6 ; "net5.0" -> 5
        // "netcoreapp3.1" -> 3 ; "net48" -> 4
        tfm = tfm.ToLowerInvariant();
        if (tfm.StartsWith("netcoreapp"))
        {
            var rest = tfm.Substring("netcoreapp".Length);
            if (int.TryParse(rest.Split('.')[0], out var n)) return n;
            return 0;
        }

        if (tfm.StartsWith("netstandard"))
        {
            var rest = tfm.Substring("netstandard".Length);
            if (int.TryParse(rest.Split('.')[0], out var n)) return n;
            return 0;
        }

        if (tfm.StartsWith("net"))
        {
            var rest = tfm.Substring("net".Length);
            // net48 or net9.0
            if (rest.Contains(".")) rest = rest.Split('.')[0];
            if (int.TryParse(rest, out var n)) return n;
        }

        return 0;
    }

    // Normalize version range to a concrete version string.
    // Uses NuGet.Versioning if available; otherwise a simple stripper.
    private static string NormalizeVersion(string rangeOrExact)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rangeOrExact)) return rangeOrExact;
            // If you already reference NuGet.Versioning, prefer:
            // var vr = NuGet.Versioning.VersionRange.Parse(rangeOrExact);
            // return (vr.MinVersion ?? vr.Float?.MinVersion)?.ToNormalizedString() ?? rangeOrExact;

            // Quick fallback: [x.y.z] -> x.y.z ; (>=x.y.z) -> x.y.z
            var s = rangeOrExact.Trim();
            if (s.StartsWith("[") && s.EndsWith("]")) return s.Trim('[', ']');
            if (s.StartsWith("(") || s.StartsWith("[")) s = s.Trim('(', '[', ')', ']');
            if (s.StartsWith(">=")) return s.Substring(2).Trim();
            if (s.Contains(",")) return s.Split(',')[0].Trim();
            return s;
        }
        catch
        {
            return rangeOrExact;
        }
    }

    // Read resolved packages from project.assets.json
    //private sealed class AssetsModel
    //{
    //    public Dictionary<string, Dictionary<string, ResolvedEntry>> Targets { get; init; } = new();
    //}

    //private sealed class ResolvedEntry
    //{
    //    public Dictionary<string, string>? Dependencies { get; init; } // id -> version
    //}

    // Returns a dictionary: packageId => (version => children list)
    private Dictionary<string, Dictionary<string, List<(string Id, string Version)>>> LoadResolvedGraphFromAssets(string projectFilePath)
    {
        var assetsPath = Path.Combine(Path.GetDirectoryName(projectFilePath)!, "obj", "project.assets.json");
        if (!File.Exists(assetsPath)) return new Dictionary<string, Dictionary<string, List<(string Id, string Version)>>>();

        var json = File.ReadAllText(assetsPath);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        if (!root.TryGetProperty("targets", out var targets))
        {
            return new Dictionary<string, Dictionary<string, List<(string Id, string Version)>>>();
        }

        // choose the first target (or pick one matching your project.TargetFramework)
        var target = targets.EnumerateObject().FirstOrDefault();
        if (target.Equals(default(JsonProperty)))
        {
            return new Dictionary<string, Dictionary<string, List<(string Id, string Version)>>>();
        }

        var map = new Dictionary<string, Dictionary<string, List<(string Id, string Version)>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var libProp in target.Value.EnumerateObject())
        {
            // libProp.Name example: "Moq.AutoMock/3.5.0"
            var parts = libProp.Name.Split('/');
            if (parts.Length != 2) continue;
            var id = parts[0];
            var ver = parts[1];

            var deps = new List<(string Id, string Version)>();
            if (libProp.Value.TryGetProperty("dependencies", out var depsProp))
            {
                foreach (var dep in depsProp.EnumerateObject())
                {
                    var depId = dep.Name;
                    var depVer = dep.Value.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(depId) && !string.IsNullOrWhiteSpace(depVer))
                    {
                        deps.Add((depId, depVer));
                    }
                }
            }

            if (!map.TryGetValue(id, out var byVersion))
            {
                byVersion = new Dictionary<string, List<(string Id, string Version)>>(StringComparer.OrdinalIgnoreCase);
                map[id] = byVersion;
            }

            byVersion[ver] = deps;
        }

        return map;
    }

    private void PrintPackageNodeResolvedFirst(
        string prefix,
        bool isLast,
        string packageId,
        string version,
        Dictionary<string, Dictionary<string, List<(string Id, string Version)>>> resolvedGraph,
        int depth,
        int maxDepth,
        HashSet<string> visited,
        bool printSelf)
    {
        if (depth > maxDepth) return;

        var visitKey = $"{packageId}:{version}";
        if (!visited.Add(visitKey)) return;

        if (printSelf)
        {
            var branch = isLast ? "└── " : "├── ";
            _output.WriteLine($"{prefix}{branch}{packageId} ({version})", ConsoleColor.DarkGray);
        }

        // Prefer exact, resolved children from assets.json
        var children =
            resolvedGraph.TryGetValue(packageId, out var byVersion) && byVersion.TryGetValue(version, out var depsFromAssets)
                ? depsFromAssets
                : GetNugetDependencies(packageId, version, null); // fallback to nuspec if not found

        if (children.Count == 0) return;

        var subPrefix = prefix + (isLast ? "    " : "│   ");
        for (var i = 0; i < children.Count; i++)
        {
            var dep = children[i];
            PrintPackageNodeResolvedFirst(
                subPrefix, i == children.Count - 1,
                dep.Id, dep.Version,
                resolvedGraph,
                depth + 1, maxDepth, visited, true);
        }
    }

    private static string GetDisplayName(GitRepositoryInfo repo)
    {
        if (repo == null)
        {
            return "(unknown)";
        }

        if (!string.IsNullOrWhiteSpace(repo.Name) && repo.Name != ".")
        {
            return repo.Name;
        }

        try
        {
            var dirName = Path.GetFileName(Path.GetFullPath(repo.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(dirName) ? repo.Name : dirName;
        }
        catch
        {
            return repo.Name ?? ".";
        }
    }
}