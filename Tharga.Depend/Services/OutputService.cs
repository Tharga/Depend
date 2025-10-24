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
                           --output, -o <mode>      Output structure:
                                                      list         Grouped by repo (default)
                                                      dependency   Ordered by build dependency

                           --view, -v <view>        Output content level:
                                                      default      Repos + projects (default)
                                                      full         Repos + projects + packages
                                                      repo-only    Only repos
                                                      project-only Only projects

                           --project, -p            Filter results to a specific project by name.

                           --exclude, -x <pattern>  Exclude projects containing this text from output (e.g. ".Tests").
                           --only-packable, -n      Show only NuGet-packable projects (projects with a PackageId).
                           --repo-deps, -rd         Show Git repository dependencies under each repo (only for --output dependency).
                           --repo-usages, -ru       Show Git repository usages under each project (only for --output dependency).
                           --project-deps, -pd      Show project dependencies under each project (only for --output dependency).
                           --project-usages, -pu    Show project usages under each project (only for --output dependency).

                           --help, -h               Show this help message.

                         Examples:
                           depend C:\dev
                           depend C:\dev --output dependency
                           depend C:\dev --output dependency --project Tharga.MongoDB
                         """);
    }
}