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

        var gitRepos = Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)
            .Where(dir => Directory.Exists(Path.Combine(dir, ".git")));

        foreach (var repoPath in gitRepos)
        {
            var repo = new GitRepositoryInfo
            {
                Name = Path.GetFileName(repoPath),
                Path = repoPath
            };

            var projectFiles = Directory.EnumerateFiles(repoPath, "*.csproj", SearchOption.AllDirectories);

            foreach (var projectFile in projectFiles)
            {
                var project = _projectService.ParseProject(projectFile);
                repo.Projects.Add(project);
            }

            result.Add(repo);
        }

        return result;
    }
}