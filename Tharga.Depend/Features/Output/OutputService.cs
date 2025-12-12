using System.Reflection;

namespace Tharga.Depend.Features.Output;

internal class OutputService : IOutputService
{
    private readonly IFileService _fileService;

    public OutputService(IFileService fileService)
    {
        _fileService = fileService;
    }

    public void WriteLine(string message, ConsoleColor color = ConsoleColor.Gray)
    {
        if (string.IsNullOrEmpty(message)) return;

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }

    public void Error(string message) => WriteLine(message, ConsoleColor.Red);
    public void Warning(string message) => WriteLine(message, ConsoleColor.Yellow);

    public void PrintHelp()
    {
        var assemblyName = Assembly.GetEntryAssembly()?.GetName();
        var version = assemblyName?.Version;

        Console.WriteLine($"{assemblyName?.Name} version {version} by Thargelion AB.");
        Console.WriteLine();

        var helpPath = Path.Combine(AppContext.BaseDirectory, "Resources", "HELP");
        var text = File.ReadAllText(helpPath);
        Console.WriteLine(text);
    }
}