using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public interface IProjectService
{
    Task<ProjectInfo> ParseProject(string projectFilePath);
}