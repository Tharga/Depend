using System.Xml.Linq;

namespace Tharga.Depend.Services;

public class VisualStudioProjectService
{
    public void PrintProjectDependencies(string projectFilePath)
    {
        if (!File.Exists(projectFilePath))
        {
            Console.Error.WriteLine($"Project file not found: {projectFilePath}");
            return;
        }

        var doc = XDocument.Load(projectFilePath);

        var ns = doc.Root?.Name.Namespace ?? string.Empty;

        var packageReferences = doc.Descendants(ns + "PackageReference")
            .Select(x => new
            {
                PackageId = x.Attribute("Include")?.Value,
                Version = x.Attribute("Version")?.Value ?? x.Element(ns + "Version")?.Value
            })
            .Where(x => x.PackageId != null)
            .ToList();

        var projectReferences = doc.Descendants(ns + "ProjectReference")
            .Select(x => x.Attribute("Include")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        Console.WriteLine($"Dependencies for: {Path.GetFileName(projectFilePath)}");

        if (packageReferences.Count > 0)
        {
            Console.WriteLine("  NuGet Packages:");
            foreach (var pkg in packageReferences)
            {
                Console.WriteLine($"    - {pkg.PackageId} ({pkg.Version})");
            }
        }

        if (projectReferences.Count > 0)
        {
            Console.WriteLine("  Project References:");
            foreach (var proj in projectReferences)
            {
                Console.WriteLine($"    - {proj}");
            }
        }

        if (packageReferences.Count == 0 && projectReferences.Count == 0)
        {
            Console.WriteLine("  (No dependencies found)");
        }

        Console.WriteLine();
    }
}