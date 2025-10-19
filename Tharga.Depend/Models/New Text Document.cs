using System.Collections;

namespace Tharga.Depend.Models;

public record ProjectInfo
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public string PackageId { get; init; }
    public required PackageInfo[] Packages { get; set; }
}

public record GitRepositoryInfo
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required ProjectInfo[] Projects { get; init; }
}

public record PackageInfo
{
    public string Path { get; init; }
    public required string Name { get; init; }
    public required string PackageId { get; init; }
    public string Version { get; init; }
    public required PackageType Type { get; init; }
}

public enum PackageType
{
    Reference,
    Project
}

public record PackageReferenceInfo
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
}

public record ProjectReferenceInfo
{
    //public required string Name { get; set; }
    public required string RelativePath { get; init; }
}

//public class GitRepositoryInfo
//{
//    public string Name { get; set; } = default!;
//    public string Path { get; set; } = default!;
//    public List<ProjectInfo> Projects { get; set; } = new();
//}

//public class ProjectInfo
//{
//    public string Name { get; set; } = default!;
//    public string Path { get; set; } = default!;
//    public string? PackageId { get; set; } // Optional explicit ID
//    public bool IsPackable { get; set; }
//    public List<PackageReferenceInfo> PackageReferences { get; set; } = new();
//    public List<ProjectReferenceInfo> ProjectReferences { get; set; } = new();
//}


//public class PackageReferenceInfo
//{
//    public string PackageId { get; set; } = default!;
//    public string Version { get; set; } = default!;
//}

//public class ProjectReferenceInfo
//{
//    public string RelativePath { get; set; } = default!;
//}