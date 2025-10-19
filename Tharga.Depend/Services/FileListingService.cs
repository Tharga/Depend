using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public class FileListingService
{
    private readonly VisualStudioProjectService _projectService;

    public FileListingService(VisualStudioProjectService projectService)
    {
        _projectService = projectService;
    }

    public List<GitRepositoryInfo> GetGitReposWithProjects(string rootPath)
    {
        var result = new List<GitRepositoryInfo>();

        if (!Directory.Exists(rootPath))
            return result;

        // Step 1: Find all Git repos (deepest first)
        var allGitRepos = Directory
            .EnumerateDirectories(rootPath, ".git", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(path => path != null)
            .Distinct()
            .OrderByDescending(path => path!.Count(c => c == Path.DirectorySeparatorChar))
            .ToList();

        var assignedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var gitRepo in allGitRepos)
        {
            // Only look for .csproj files not already claimed by inner repos
            var projects = Directory.EnumerateFiles(gitRepo!, "*.csproj", SearchOption.AllDirectories)
                .Where(p => !assignedProjects.Contains(p))
                .ToList();

            if (!projects.Any())
            {
                // ✅ This repo contains no unique projects — skip it
                continue;
            }

            var repoInfo = new GitRepositoryInfo
            {
                Name = Path.GetFileName(gitRepo),
                Path = gitRepo!,
                Projects = projects
                    .Select(_projectService.ParseProject)
                    .ToList()
            };

            foreach (var p in projects)
                assignedProjects.Add(p);

            result.Add(repoInfo);
        }

        return result;
    }

}