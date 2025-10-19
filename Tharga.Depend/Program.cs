using Tharga.Depend.Models;
using Tharga.Depend.Services;

var argsList = args.ToList();

if (argsList.Contains("--help") || argsList.Contains("-h"))
{
    PrintHelp();
    return;
}

// Extract path (first non-option argument)
string? rootPath = argsList.FirstOrDefault(a => !a.StartsWith("-"));
if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
{
    Console.Error.WriteLine("❌ Please provide a valid folder path.");
    PrintHelp();
    return;
}

bool isList = argsList.Contains("--list");
bool isOrder = argsList.Contains("--order");
bool isGitMode = argsList.Contains("--git");

var projectIdIndex = argsList.IndexOf("--project");
string? targetProjectId = null;
if (projectIdIndex >= 0 && projectIdIndex + 1 < argsList.Count)
{
    targetProjectId = argsList[projectIdIndex + 1];
}

if (!isList && !isOrder)
{
    PrintHelp();
    return;
}

// Initialize services
var projectService = new VisualStudioProjectService();
var fileService = new FileListingService(projectService);
var graphService = new DependencyGraphService();
var printerService = new RepositoryPrinterService();
var orderService = new DependencyOrderService();

// Scan all repositories
var repos = fileService.GetGitReposWithProjects(rootPath);

if (isList)
{
    if (!string.IsNullOrEmpty(targetProjectId))
    {
        var match = repos
            .SelectMany(r => r.Projects)
            .FirstOrDefault(p => string.Equals(p.PackageId, targetProjectId, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            Console.WriteLine($"⚠️ Project not found: {targetProjectId}");
            return;
        }

        printerService.PrintSingle(match);
    }
    else
    {
        printerService.Print(repos, groupByGit: isGitMode);
    }
}
else if (isOrder)
{
    var levels = graphService.CalculateDependencyLevels(repos);

    if (isGitMode)
    {
        var gitDepOrderService = new GitDependencyOrderService();
        gitDepOrderService.Print(repos, graphService);
    }
    else
    {
        if (!string.IsNullOrEmpty(targetProjectId))
        {
            var target = levels.Keys.FirstOrDefault(p =>
                string.Equals(p.PackageId, targetProjectId, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                Console.WriteLine($"⚠️ Target project '{targetProjectId}' not found in scanned repos.");
                return;
            }

            // Filter only dependencies needed to build this project
            var required = levels
                .Where(kv => kv.Key.PackageId != target.PackageId)
                .Where(kv => IsTransitiveDependency(kv.Key, target, graphService, levels))
                .OrderBy(kv => kv.Value)
                .ToList();

            Console.WriteLine($"[?] Build order dependencies for: {target.PackageId}\n");
            foreach (var (dep, level) in required)
            {
                Console.WriteLine($"[{level}] {dep.PackageId} ({dep.Path})");
            }
        }
        else
        {
            orderService.Print(levels);
        }
    }
}

void PrintHelp()
{
    Console.WriteLine("""
Usage:
  Tharga.Depend.exe <folder> [--list | --order] [--project <PackageId>]

Arguments:
  <folder>        Root folder containing Git repositories and projects.

Options:
  --list          Show projects and dependencies (default: full list).
  --order         Show NuGet-packable build order by dependency.
  --project <id>  Filter to show output related to a specific NuGet project.
  --help, -h      Show this help message.

Examples:
  Tharga.Depend.exe C:\dev --list
  Tharga.Depend.exe C:\dev --order
  Tharga.Depend.exe C:\dev --order --project Tharga.MongoDB
""");
}

bool IsTransitiveDependency(ProjectInfo candidate, ProjectInfo target, DependencyGraphService graph, Dictionary<ProjectInfo, int> levels)
{
    var visited = new HashSet<ProjectInfo>();
    var stack = new Stack<ProjectInfo>();
    stack.Push(target);

    while (stack.Any())
    {
        var current = stack.Pop();
        if (!levels.ContainsKey(current))
            continue;

        var deps = graph.GetProjectDependencies(current, levels.Keys.ToList());

        foreach (var dep in deps)
        {
            if (dep == candidate)
                return true;

            if (visited.Add(dep))
                stack.Push(dep);
        }
    }

    return false;
}
