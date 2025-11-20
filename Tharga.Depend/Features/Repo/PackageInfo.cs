using Tharga.Depend.Features.Project;

namespace Tharga.Depend.Features.Repo;

public record PackageInfo
{
    public string Path { get; init; }
    public required string Name { get; init; }
    public required string PackageId { get; init; }
    public string Version { get; init; }
    public required PackageType Type { get; init; }
}