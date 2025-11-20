namespace Tharga.Depend.Framework;

public interface IPathService
{
    void EnsureInUserPath();
    void RemoveFromPath();
    string GetRootPath(List<string> args);
}