namespace Tharga.Depend.Services;

public interface IOutputService
{
    void WriteLine(string message, ConsoleColor color = ConsoleColor.Gray);
    void Error(string message);
    void Warning(string message);
    void PrintHelp();
}

public class OutputService : IOutputService
{
    public void WriteLine(string message, ConsoleColor color = ConsoleColor.Gray)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }

    public void Error(string message) => WriteLine(message, ConsoleColor.Red);
    public void Warning(string message) => WriteLine(message, ConsoleColor.Yellow);

   public void PrintHelp()
   {
       Console.WriteLine("""
                         Usage:
                           Tharga.Depend.exe <folder> [--output <list|dependency>] [--project <ProjectName>]

                         Arguments:
                           <folder>                 Root folder containing Git repositories and projects.

                         Options:
                           --output, -o             Output format. Values:
                                                      list       - Show projects and their dependencies.
                                                      dependency - Show NuGet-packable build order by dependency.
                                                    Default: list

                           --project, -p            Filter results to a specific project by name.

                           --exclude, -x <pattern>  Exclude projects containing this text from output (e.g. ".Tests").
                           --only-packable, -n      Show only NuGet-packable projects (those with a PackageId).
                           --verbose, -v            Include referenced packages in the output (NuGet/project dependencies).

                           --help, -h               Show this help message.

                         Examples:
                           Tharga.Depend.exe C:\dev
                           Tharga.Depend.exe C:\dev --output dependency
                           Tharga.Depend.exe C:\dev --output dependency --project Tharga.MongoDB
                         """);
    }
}