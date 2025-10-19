using Tharga.Depend.Services;

string inputPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

var projectService = new VisualStudioProjectService();
var fileService = new FileListingService(projectService);

if (File.Exists(inputPath) && Path.GetExtension(inputPath).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
{
    // Single project file mode
    projectService.PrintProjectDependencies(inputPath);
}
else
{
    // Folder mode
    fileService.ListGitReposWithProjects(inputPath);
}