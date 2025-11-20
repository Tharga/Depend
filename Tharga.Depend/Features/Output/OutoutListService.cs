using Tharga.Depend.Features.Repo;

namespace Tharga.Depend.Features.Output;

public class OutoutListService : OutputBase, IOutoutListService
{
    public OutoutListService(IOutputService output)
        : base(output)
    {
    }

    public void PrintRepositoryList(IEnumerable<GitRepositoryInfo> repositories, string projectName, string excludePattern, bool onlyPackable, ViewMode viewMode, bool showRepoDeps, bool showProjectDeps, bool showRepoUsages, bool showProjectUsages, Dictionary<string, string> latestVersions)
    {
        var repos = repositories.ToArray();

        var showPackages = viewMode == ViewMode.Full;
        var showProjects = viewMode is ViewMode.Default or ViewMode.Full;
        var showRepos = viewMode != ViewMode.ProjectOnly;

        var allProjects = repos.SelectMany(r => r.Projects).ToList();

        //NOTE: Only show project level.

        if (viewMode == ViewMode.ProjectOnly)
        {
            foreach (var project in allProjects.Where(p => ShouldInclude(p, excludePattern, onlyPackable)).OrderBy(p => p.Name))
            {
                PrintProjectLine(repos, project, 0);
                PrintProjectDeps(repos, excludePattern, onlyPackable, showProjectDeps, project, 2);
                PrintProjectUsages(project, repos, showProjectUsages, excludePattern, onlyPackable, 2);
            }

            return;
        }

        //NOTE: Show repository level, with different options of projects and packages.
        foreach (var repo in repos.OrderBy(r => r.Name))
        {
            if (!string.IsNullOrEmpty(projectName) && repo.Projects.All(p => p.Name != projectName)) continue;

            var filteredProjects = repo.Projects.Where(p => ShouldInclude(p, excludePattern, onlyPackable)).ToArray();

            if (!filteredProjects.Any() && showProjects) continue;

            if (showRepos)
            {
                PrintRepositoryLine(repos, repo);
                PrintRepoDeps(repos, showRepoDeps, repo, 2);
                PrintRepoUsages(repo, repos, showRepoUsages, 2);
            }

            if (!showProjects) continue;

            foreach (var project in filteredProjects.OrderBy(p => p.Name))
            {
                PrintProjectLine(repos, project, 2);
                PrintProjectDeps(repos, excludePattern, onlyPackable, showProjectDeps, project, 4);
                PrintProjectUsages(project, repos, showProjectUsages, excludePattern, onlyPackable, 4);
                PrintProjectPackages(project, latestVersions, excludePattern, onlyPackable, showPackages, 4, allProjects);
            }
        }
    }
}