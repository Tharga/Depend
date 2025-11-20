using Tharga.Depend.Features.Repo;

namespace Tharga.Depend.Features.Output;

public interface IOutputDependencyService
{
    void PrintDependencyList(GitRepositoryInfo[] repos, string projectName, string excludePattern, bool onlyPackable, ViewMode viewMode, bool showRepoDeps, bool showProjectDeps, bool showRepoUsages, bool showProjectUsages, Dictionary<string, string> latestVersions);
}