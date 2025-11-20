namespace Tharga.Depend.Features.Repo;

public record ProjectInfo
{
    public required string TargetFramework { get; init; }
    public required string Path { get; init; }
    public required string Name { get; init; }
    public string PackageId { get; init; }
    public required PackageInfo[] Packages { get; init; }
}