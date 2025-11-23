using Tharga.Depend.Features.Output;

namespace Tharga.Depend.Framework;

internal class PathService : IPathService
{
    private readonly IOutputService _output;

    public PathService(IOutputService output)
    {
        _output = output;
    }

    public void EnsureInUserPath()
    {
        var exePath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";

        var pathSegments = currentPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !File.Exists(Path.Combine(p.TrimEnd(Path.DirectorySeparatorChar), "depend.exe")))
            .Append(exePath);

        var newPath = string.Join(Path.PathSeparator, pathSegments.Distinct(StringComparer.OrdinalIgnoreCase));
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);

        _output.WriteLine($"Updated user PATH to include '{exePath}'. Any previous 'depend.exe' locations were removed.", ConsoleColor.Green);
    }

    public void RemoveFromPath()
    {
        var exePath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";

        var pathSegments = currentPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.Equals(p.TrimEnd(Path.DirectorySeparatorChar), exePath, StringComparison.OrdinalIgnoreCase));

        var newPath = string.Join(Path.PathSeparator, pathSegments);
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);

        _output.WriteLine($"Removed '{exePath}' from user PATH.", ConsoleColor.Yellow);
    }

    public string GetRootPath(List<string> args)
    {
        var rootCandidate = args.NonOptionalParams().FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(rootCandidate) && Directory.Exists(rootCandidate))
        {
            args.Remove(rootCandidate);
            return rootCandidate;
        }

        var current = Environment.CurrentDirectory;

        if (!string.IsNullOrEmpty(rootCandidate))
        {
            var parsed = Path.Combine(current, rootCandidate);
            if (Directory.Exists(parsed))
            {
                args.Remove(rootCandidate);
                return parsed; //NOTE: the provided parameter has been used as a relative path.
            }

            if (rootCandidate?.Contains("\\") ?? false) throw new DirectoryNotFoundException($"Directory '{parsed}' does not exist.");
        }

        if (!Directory.Exists(current)) if (!Directory.Exists(current)) throw new DirectoryNotFoundException($"Directory '{current}' does not exist.");

        return current;
    }
}