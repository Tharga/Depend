namespace Tharga.Depend.Features.Repo;

public interface IGitRepositoryService
{
    IAsyncEnumerable<GitRepositoryInfo> GetAsync(string rootPath);
}