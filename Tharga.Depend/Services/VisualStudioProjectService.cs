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

        var isPackable = doc.Descendants(ns + "IsPackable")
                .Select(x => x.Value?.Trim().ToLowerInvariant())
                .FirstOrDefault() switch
            {
                "false" => false,
                "true" => true,
                _ => true // Default: SDK projects are packable unless stated otherwise
            };

        var hasPackageIdOrVersion = doc.Descendants()
            .Any(x => x.Name.LocalName == "PackageId" || x.Name.LocalName == "Version");

        project.IsPackable = isPackable && hasPackageIdOrVersion;

        // Existing parsing code...

        project.PackageReferences = doc.Descendants(ns + "PackageReference")
            .Select(x => new PackageReferenceInfo
            {
                PackageId = x.Attribute("Include")?.Value ?? string.Empty,
                Version = x.Attribute("Version")?.Value ?? x.Element(ns + "Version")?.Value ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.PackageId))
            .ToList();

        project.ProjectReferences = doc.Descendants(ns + "ProjectReference")
            .Select(x => new ProjectReferenceInfo
            {
                RelativePath = x.Attribute("Include")?.Value ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.RelativePath))
            .ToList();

        project.PackageId = doc.Descendants(ns + "PackageId").FirstOrDefault()?.Value
                            ?? project.Name;

        return project;
    }
}