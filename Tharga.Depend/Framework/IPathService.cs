namespace Tharga.Depend.Framework;

public interface IPathService
{
    void EnsureInUserPath();
    string GetRootPath(List<string> args);
}