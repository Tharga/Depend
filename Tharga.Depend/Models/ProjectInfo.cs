namespace Tharga.Depend.Models;

public record ProjectInfo
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public string PackageId { get; init; }
    public required PackageInfo[] Packages { get; init; }
}