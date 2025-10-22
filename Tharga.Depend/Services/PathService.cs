namespace Tharga.Depend.Services;

public interface IPathService
{
    void EnsureInUserPath();
    string GetRootPath(IEnumerable<string> args);
}

public class PathService : IPathService
{
    private readonly IOutputService _output;

    public PathService(IOutputService output)
    {
        _output = output;
    }

    public string GetRootPath(IEnumerable<string> args)
    {
        var rootCandidate = args.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(rootCandidate) && Directory.Exists(rootCandidate)) return rootCandidate;

        var current = Environment.CurrentDirectory;
        if (!Directory.Exists(current)) throw new DirectoryNotFoundException($"Current directory does not exist: {current}");

        return current;
    }

    public void EnsureInUserPath()
    {
        var exePath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";

        var pathSegments = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Contains(exePath, StringComparer.OrdinalIgnoreCase)) return;

        var newPath = string.Join(Path.PathSeparator, pathSegments.Append(exePath));
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);

        _output.WriteLine($"Added '{exePath}' to user PATH. You can now run this tool from anywhere.", ConsoleColor.Green);
    }
}