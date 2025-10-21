using Tharga.Depend.Models;

public interface IGitRepositoryService
{
    IAsyncEnumerable<GitRepositoryInfo> GetAsync(string rootPath);
}