namespace Tharga.Depend.Models;

public class GitRepositoryInfo
{
    public string Name { get; set; } = default!;
    public string Path { get; set; } = default!;
    public List<ProjectInfo> Projects { get; set; } = new();
}

public class ProjectInfo
{
    public string Name { get; set; } = default!;
    public string Path { get; set; } = default!;
    public bool IsPackable { get; set; }
    public List<PackageReferenceInfo> PackageReferences { get; set; } = new();
    public List<ProjectReferenceInfo> ProjectReferences { get; set; } = new();
}

public class PackageReferenceInfo
{
    public string PackageId { get; set; } = default!;
    public string Version { get; set; } = default!;
}

public class ProjectReferenceInfo
{
    public string RelativePath { get; set; } = default!;
}