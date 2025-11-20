using Tharga.Depend.Features.Output;
using Tharga.Depend.Features.Project;

namespace Tharga.Depend.Features.Repo;

internal class GitRepositoryService : IGitRepositoryService
{
    private readonly IProjectService _projectService;
    private readonly IOutputService _output;

    public GitRepositoryService(IProjectService projectService, IOutputService output)
    {
        _projectService = projectService;
        _output = output;
    }

    public async IAsyncEnumerable<GitRepositoryInfo> GetAsync(string rootPath)
    {
        if (!Directory.Exists(rootPath)) throw new InvalidOperationException($"Path {rootPath} does not exist.");

        foreach (var repoPath in EnumerateGitReposSafely(rootPath))
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
                Name = GetRepositoryName(rootPath, repoPath),
                Projects = parsedProjects.ToArray(),
            };
        }
    }

    private IEnumerable<string> EnumerateGitReposSafely(string rootPath)
    {
        var pending = new Queue<string>();
        pending.Enqueue(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            var subDirs = Array.Empty<string>();
            var hasGit = false;

            // Try to get subdirectories
            try
            {
                subDirs = Directory.GetDirectories(current);
            }
            catch (UnauthorizedAccessException ex)
            {
                _output.Error($"Warning: Access denied to '{current}': {ex.Message}");
            }
            catch (IOException ex)
            {
                _output.Error($"Warning: IO error in '{current}': {ex.Message}");
            }

            // Try to detect .git folder
            try
            {
                hasGit = Directory.EnumerateDirectories(current, ".git", SearchOption.TopDirectoryOnly).Any();
            }
            catch (Exception ex)
            {
                _output.Error($"Warning: Failed to check for '.git' in '{current}': {ex.Message}");
            }

            if (hasGit) yield return current;

            foreach (var subdir in subDirs)
            {
                pending.Enqueue(subdir);
            }
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

    private string GetRepositoryName(string rootPath, string repoPath)
    {
        try
        {
            var relative = Path.GetRelativePath(rootPath, repoPath);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (Exception ex)
        {
            _output.Error($"Could not resolve repository name for '{repoPath}': {ex.Message}");
            return new DirectoryInfo(repoPath).Name;
        }
    }
}