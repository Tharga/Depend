using Tharga.Depend.Services;

namespace Tharga.Depend.Services;

public class FileListingService
{
    private readonly VisualStudioProjectService _projectService;

    public FileListingService(VisualStudioProjectService projectService)
    {
        _projectService = projectService;
    }

    public void ListGitReposWithProjects(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            Console.Error.WriteLine($"The directory '{rootPath}' does not exist.");
            return;
        }

        Console.WriteLine($"Searching for Git repositories and Visual Studio projects under: {rootPath}\n");

        var gitRepos = Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)
            .Where(dir => Directory.Exists(Path.Combine(dir, ".git")));

        foreach (var repoPath in gitRepos)
        {
            var repoName = Path.GetFileName(repoPath);
            Console.WriteLine($"{repoName} ({repoPath})");

            var projectFiles = Directory.EnumerateFiles(repoPath, "*.csproj", SearchOption.AllDirectories);

            foreach (var projectFile in projectFiles)
            {
                var projectName = Path.GetFileNameWithoutExtension(projectFile);
                Console.WriteLine($"- {projectName} ({projectFile})");

                _projectService.PrintProjectDependencies(projectFile);
            }

            Console.WriteLine();
        }
    }
}