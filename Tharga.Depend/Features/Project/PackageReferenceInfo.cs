namespace Tharga.Depend.Features.Project;

public record PackageReferenceInfo
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
}