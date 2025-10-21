using System.Xml.Linq;
using Tharga.Depend.Models;
using Tharga.Depend.Services;

internal class ProjectService : IProjectService
{
    public async Task<ProjectInfo> ParseProject(string projectFilePath)
    {
        var doc = XDocument.Load(projectFilePath);
        var ns = doc.Root?.Name.Namespace ?? string.Empty;

        var name = Path.GetFileNameWithoutExtension(projectFilePath);

        // Package references
        var packageReferences = doc.Descendants(ns + "PackageReference")
            .Select(x => new PackageReferenceInfo
            {
                PackageId = x.Attribute("Include")?.Value ?? string.Empty,
                Version = x.Attribute("Version")?.Value ?? x.Element(ns + "Version")?.Value ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.PackageId))
            .ToList();

        // Project references
        var projectReferences = doc.Descendants(ns + "ProjectReference")
            .Select(x => new ProjectReferenceInfo
            {
                RelativePath = x.Attribute("Include")?.Value ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.RelativePath))
            .ToList();

        var packages = await BuildPackageInfos(projectFilePath, packageReferences).ToArrayAsync();
        var projects = await BuildProjectInfos(projectFilePath, projectReferences).ToArrayAsync();

        return new ProjectInfo
        {
            Path = projectFilePath,
            Name = name,
            PackageId = GetPackageId(doc, name),
            Packages = projects.Union(packages).ToArray()
        };
    }

    private async IAsyncEnumerable<PackageInfo> BuildPackageInfos(string projectFilePath, IEnumerable<PackageReferenceInfo> projectReferences)
    {
        foreach (var x in projectReferences)
        {
            var packageInfo = new PackageInfo
            {
                Name = x.PackageId,
                PackageId = x.PackageId,
                Version = x.Version,
                Type = PackageType.Reference
            };

            yield return packageInfo;
        }
    }

    private async IAsyncEnumerable<PackageInfo> BuildProjectInfos(string projectFilePath, IEnumerable<ProjectReferenceInfo> projectReferences)
    {
        foreach (var x in projectReferences)
        {
            var result = await GetProjectName(projectFilePath, x.RelativePath);
            var packageInfo = new PackageInfo
            {
                Name = result.Name,
                PackageId = result.PackageId,
                Path = result.Path,
                Type = PackageType.Project
            };
            yield return packageInfo;
        }
    }

    private async Task<(string Name, string Path, bool Exists, string PackageId)> GetProjectName(string projectFilePath, string argRelativePath)
    {
        var dir = Path.GetDirectoryName(projectFilePath);
        var fullProjectPath = Path.Combine(dir, argRelativePath);
        var exists = File.Exists(fullProjectPath);

        string packageId = null;
        if (exists)
        {
            //TODO: Open to get the packageId
            var parsed = await ParseProject(fullProjectPath);
            packageId = parsed.PackageId;
        }

        var fn = Path.GetFileName(fullProjectPath).Replace(".csproj", "");
        return (fn, fullProjectPath, exists, packageId);
    }

    private static string GetPackageId(XDocument doc, string name)
    {
        var ns = doc.Root?.Name.Namespace ?? string.Empty;

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
        var packageId = isPackable ? (doc.Descendants(ns + "PackageId").FirstOrDefault()?.Value ?? name) : null;
        return packageId;
    }
}