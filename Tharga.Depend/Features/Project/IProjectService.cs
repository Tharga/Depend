using Tharga.Depend.Features.Repo;

namespace Tharga.Depend.Features.Project;

public interface IProjectService
{
    Task<ProjectInfo> ParseProject(string projectFilePath);
}