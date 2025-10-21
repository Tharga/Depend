using Tharga.Depend.Models;
using Tharga.Depend.Services;

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