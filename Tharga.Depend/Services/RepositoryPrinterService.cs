using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public class RepositoryPrinterService
{
    public void Print(List<GitRepositoryInfo> repos)
    {
        foreach (var repo in repos)
        {
            Console.WriteLine($"{repo.Name} ({repo.Path})");

            foreach (var project in repo.Projects)
            {
                Console.WriteLine($"- {project.Name} ({project.Path})");

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

                Console.WriteLine();
            }

            Console.WriteLine();
        }
    }
}