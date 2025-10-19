using Tharga.Depend.Models;

public class RepositoryPrinterService
{
    public void Print(List<GitRepositoryInfo> repos, bool groupByGit = false)
    {
        if (!groupByGit)
        {
            // Flat listing
            foreach (var repo in repos)
            {
                foreach (var project in repo.Projects)
                {
                    PrintProject(project);
                    Console.WriteLine();
                }
            }
        }
        else
        {
            // Grouped listing
            foreach (var repo in repos.OrderBy(r => r.Path))
            {
                Console.WriteLine($"📁 Git Repository: {repo.Path}");

                foreach (var project in repo.Projects.OrderBy(p => p.Path))
                {
                    Console.WriteLine($"  • {project.Name} ({project.Path})");

                    if (project.IsPackable)
                        Console.WriteLine("    → Builds NuGet Package");

                    if (project.PackageReferences.Any())
                    {
                        Console.WriteLine("    NuGet Packages:");
                        foreach (var pkg in project.PackageReferences)
                            Console.WriteLine($"      - {pkg.PackageId} ({pkg.Version})");
                    }

                    if (project.ProjectReferences.Any())
                    {
                        Console.WriteLine("    Project References:");
                        foreach (var proj in project.ProjectReferences)
                            Console.WriteLine($"      - {proj.RelativePath}");
                    }

                    Console.WriteLine();
                }

                Console.WriteLine();
            }
        }
    }

    public void PrintSingle(ProjectInfo project)
    {
        PrintProject(project);
    }

    private void PrintProject(ProjectInfo project)
    {
        Console.WriteLine($"{project.Name} ({project.Path})");

        if (project.IsPackable)
            Console.WriteLine("  → Builds NuGet Package");

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
}
