using System.Diagnostics;
using Tharga.Depend.Features.Output;
using Tharga.Depend.Features.Repo;
using Tharga.Depend.Framework;

namespace Tharga.Depend.Features.Command;

internal class CommandService : ICommandService
{
    private const int ExitSuccess = 0;
    private const int ExitInvalidPath = 1;
    private const int ExitUnknownOutputType = 2;
    private const int ExitUnknownViewMode = 3;
    private const int ExitUnhandledError = 99;

    private readonly IOutputService _output;
    private readonly IGitRepositoryService _gitRepoService;
    private readonly IPathService _pathService;
    private readonly IOutputTreeService _outputTreeService;
    private readonly IOutoutListService _outoutListService;
    private readonly IOutputDependencyService _outputDependencyService;

    public CommandService(IOutputService output, IGitRepositoryService gitRepoService, IPathService pathService, IOutputTreeService outputTreeService, IOutoutListService outoutListService, IOutputDependencyService outputDependencyService)
    {
        _output = output;
        _gitRepoService = gitRepoService;
        _pathService = pathService;
        _outputTreeService = outputTreeService;
        _outoutListService = outoutListService;
        _outputDependencyService = outputDependencyService;
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

            if (argsList.Contains("--register"))
            {
                _pathService.EnsureInUserPath();
                return ExitWithCode(ExitSuccess);
            }

            if (argsList.Contains("--unregister"))
            {
                _pathService.RemoveFromPath();
                return ExitWithCode(ExitSuccess);
            }

            var rootPath = _pathService.GetRootPath(argsList);
            if (rootPath == null)
            {
                _output.Error("Error: Please provide a valid folder path.");
                _output.PrintHelp();
                return ExitWithCode(ExitInvalidPath);
            }

            //var defaultOutput = prm ?? "dependency";

            // Load all repositories
            var allRepos = await _gitRepoService.GetAsync(rootPath).ToArrayAsync();

            // Compute latest known package versions from all repos
            var latestPackageVersions = GetLatestPackageVersions(allRepos);

            WarnIfDuplicateProjectNames(allRepos);

            // Now parse CLI options
            var outputType = GetOptionValue(argsList, "dependency", ["--output", "-o"], ["tree", "list", "dependency"]);
            var viewMode = ParseViewMode(GetOptionValue(argsList, "default", ["--view", "-v"], ["default", "full", "repo", "project"]));
            if (!viewMode.HasValue) return ExitWithCode(ExitUnknownViewMode);
            //var projectName = GetOptionValue(argsList, "", "--project", "-p");
            var projectName = (string)null;
            //var excludePattern = GetOptionValue(argsList, "", "--exclude", "-x");
            var excludePattern = (string)null;
            var onlyPackable = argsList.Contains("--only-packable") || argsList.Contains("-n");
            var showRepoDeps = argsList.Contains("--repo-deps") || argsList.Contains("-rd");
            var showProjectDeps = argsList.Contains("--project-deps") || argsList.Contains("-pd");
            var showRepoUsages = argsList.Contains("--repo-usages") || argsList.Contains("-ru");
            var showProjectUsages = argsList.Contains("--project-usages") || argsList.Contains("-pu");

            if (argsList.NonOptionalParams().Any()) throw new InvalidOperationException($"Unknown parameter '{argsList.NonOptionalParams().First()}'.");

            // Filter for display only if -p is specified
            //var displayRepos = string.IsNullOrWhiteSpace(projectName)
            //    ? allRepos
            //    : FilterReposByProject(allRepos, projectName);
            var displayRepos = allRepos;

            switch (outputType)
            {
                case "l":
                case "list":
                    _outoutListService.PrintRepositoryList(displayRepos, projectName, excludePattern, onlyPackable, viewMode.Value, showRepoDeps, showProjectDeps, showRepoUsages, showProjectUsages, latestPackageVersions);
                    break;

                case "d":
                case "dependency":
                    _outputDependencyService.PrintDependencyList(displayRepos, projectName, excludePattern, onlyPackable, viewMode.Value, showRepoDeps, showProjectDeps, showRepoUsages, showProjectUsages, latestPackageVersions);
                    break;

                case "t":
                case "tree":
                    _outputTreeService.PrintTree(displayRepos, viewMode.Value, excludePattern, onlyPackable);
                    break;

                default:
                    _output.Error($"Unknown output type: {outputType}");
                    return ExitWithCode(ExitUnknownOutputType);
            }

            return ExitWithCode(ExitSuccess);
        }
        catch (Exception e)
        {
            _output.Error($"{e.Message}\n@{e.StackTrace}");
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

    private static string GetOptionValue(List<string> argsList, string defaultValue, string[] keys, string[] alternativs = null)
    {
        for (var i = 0; i < argsList.Count - 1; i++)
        {
            if (keys.Contains(argsList[i], StringComparer.OrdinalIgnoreCase))
            {
                return argsList[i + 1];
            }
        }

        var firstMatch = argsList.NonOptionalParams().FirstOrDefault(x => (alternativs ?? []).Any(y => y.Equals(x, StringComparison.InvariantCultureIgnoreCase)));
        if (firstMatch != null)
        {
            argsList.Remove(firstMatch);
            return firstMatch.ToLower();
        }

        return defaultValue;
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
            "repo" or "repo-only" or "repoonly" or "r" => ViewMode.RepoOnly,
            "project" or "project-only" or "projectonly" or "p" => ViewMode.ProjectOnly,
            "default" or "d" or null => ViewMode.Default,
            _ => null
        };
    }
}