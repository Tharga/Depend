namespace Tharga.Depend.Models;

public record PackageReferenceInfo
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
}