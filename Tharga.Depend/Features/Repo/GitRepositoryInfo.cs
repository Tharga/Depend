namespace Tharga.Depend.Features.Repo;

public record GitRepositoryInfo
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required ProjectInfo[] Projects { get; init; }
}