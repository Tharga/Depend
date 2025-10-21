using Tharga.Depend.Models;
using Tharga.Depend.Services;

public interface IGitRepositoryService
{
    IAsyncEnumerable<GitRepositoryInfo> GetAsync(string rootPath);
}

internal class GitRepositoryService : IGitRepositoryService
{
    private readonly IProjectService _projectService;
    private readonly string _rootPath;

    public GitRepositoryService(IProjectService projectService, string rootPath)
    {
        _projectService = projectService;
        _rootPath = rootPath;
    }

    public async IAsyncEnumerable<GitRepositoryInfo> GetAsync(string rootPath)
    {
        if (!Directory.Exists(rootPath)) throw new InvalidOperationException($"Path {rootPath} does not exist.");

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
            var relative = Path.GetRelativePath(_rootPath, repoPath);
            return relative.Replace(Path.DirectorySeparatorChar, '/'); // Consistent format
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not resolve repository name for '{repoPath}': {ex.Message}");
            return new DirectoryInfo(repoPath).Name;
        }
    }
}