using Tharga.Depend.Features.Repo;

namespace Tharga.Depend.Features.Output;

public interface IOutputTreeService
{
    void PrintTree(GitRepositoryInfo[] repos, ViewMode viewMode, string excludePattern, bool onlyPackable);
}