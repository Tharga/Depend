using System.Xml.Linq;
using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public class VisualStudioProjectService
{
    public ProjectInfo ParseProject(string projectFilePath)
    {
        var doc = XDocument.Load(projectFilePath);
        var ns = doc.Root?.Name.Namespace ?? string.Empty;

        var project = new ProjectInfo
        {
            Name = Path.GetFileNameWithoutExtension(projectFilePath),
            Path = projectFilePath
        };

        // Step 1: Explicit IsPackable
        var rawIsPackable = doc.Descendants(ns + "IsPackable")
            .Select(x => x.Value?.Trim().ToLowerInvariant())
            .FirstOrDefault();

        var hasExplicitIsPackable = rawIsPackable is "true" or "false";
        var isExplicitlyPackable = rawIsPackable == "true";

        // Step 2: Heuristic detection based on NuGet-related elements
        var hasNugetMetadata = doc.Descendants()
            .Any(x =>
                x.Name.LocalName is "PackageId" or "Version" or "Authors" or "Company" or "Product" or "Description" or "PackageIconUrl" or "PackageProjectUrl" or "PackageReadmeFile");

        // Step 3: Combine logic
        var isPackable = hasExplicitIsPackable
            ? isExplicitlyPackable
            : hasNugetMetadata;

        project.IsPackable = isPackable;

        // Optional logging for inferred packable projects
        if (!hasExplicitIsPackable && isPackable)
        {
            Console.WriteLine($"ℹ️  Project '{project.Name}' inferred as packable (based on NuGet metadata).");
        }

        // Package references
        project.PackageReferences = doc.Descendants(ns + "PackageReference")
            .Select(x => new PackageReferenceInfo
            {
                PackageId = x.Attribute("Include")?.Value ?? string.Empty,
                Version = x.Attribute("Version")?.Value ?? x.Element(ns + "Version")?.Value ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.PackageId))
            .ToList();

        // Project references
        project.ProjectReferences = doc.Descendants(ns + "ProjectReference")
            .Select(x => new ProjectReferenceInfo
            {
                RelativePath = x.Attribute("Include")?.Value ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.RelativePath))
            .ToList();

        // PackageId fallback
        project.PackageId = doc.Descendants(ns + "PackageId").FirstOrDefault()?.Value
                            ?? project.Name;
        return project;
    }

}