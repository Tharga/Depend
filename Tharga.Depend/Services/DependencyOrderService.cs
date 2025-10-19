using Tharga.Depend.Models;

namespace Tharga.Depend.Services;

public class DependencyOrderService
{
    public void Print(Dictionary<ProjectInfo, int> levels)
    {
        var sorted = levels.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key.Name);

        foreach (var (project, level) in sorted)
        {
            Console.WriteLine($"[{level}] {project.PackageId} ({project.Path})");
        }
    }
}