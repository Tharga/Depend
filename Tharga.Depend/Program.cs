using Tharga.Depend.Services;

string inputPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

var projectService = new VisualStudioProjectService();
var fileService = new FileListingService(projectService);

if (File.Exists(inputPath) && Path.GetExtension(inputPath).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
{
    var project = projectService.ParseProject(inputPath);

    Console.WriteLine($"{project.Name} ({project.Path})");

    if (project.PackageReferences.Any())
    {
        Console.WriteLine("  NuGet Packages:");
        foreach (var pkg in project.PackageReferences)
            Console.WriteLine($"    - {pkg.PackageId} ({pkg.Version})");
    }

    if (project.ProjectReferences.Any())
    {
        Console.WriteLine("  Project References:");
        foreach (var proj in project.ProjectReferences)
            Console.WriteLine($"    - {proj.RelativePath}");
    }
}
else
{
    var repos = fileService.GetGitReposWithProjects(inputPath);

    foreach (var repo in repos)
    {
        Console.WriteLine($"{repo.Name} ({repo.Path})");

        foreach (var project in repo.Projects)
        {
            Console.WriteLine($"- {project.Name} ({project.Path})");

            Console.WriteLine($"- {project.Name} ({project.Path})");
            if (project.IsPackable)
            {
                Console.WriteLine("  → Builds NuGet Package");
            }

            if (project.PackageReferences.Any())
            {
                Console.WriteLine("  NuGet Packages:");
                foreach (var pkg in project.PackageReferences)
                    Console.WriteLine($"    - {pkg.PackageId} ({pkg.Version})");
            }

            if (project.ProjectReferences.Any())
            {
                Console.WriteLine("  Project References:");
                foreach (var proj in project.ProjectReferences)
                    Console.WriteLine($"    - {proj.RelativePath}");
            }

            Console.WriteLine();
        }

        Console.WriteLine();
    }
}