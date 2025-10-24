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
        Console.WriteLine("""
                      Usage:
                        depend <folder> [--output <list|dependency>] [--project <ProjectName>]

                      Arguments:
                        <folder>                 Root folder containing Git repositories and projects.

                      Options:
                        --output, -o <mode>      Output structure:
                                                   list, l        Grouped by repo (default)
                                                   dependency, d  Ordered by build dependency

                        --view, -v <view>        Output content level:
                                                   default, d       Repos + projects (default)
                                                   full, f          Repos + projects + packages
                                                   repo-only, r     Only repos
                                                   project-only, p  Only projects

                        --project, -p            Filter results to a specific project by name.

                        --exclude, -x <pattern>  Exclude projects containing this text from output (e.g. ".Tests").
                        --only-packable, -n      Show only NuGet-packable projects (projects with a PackageId).
                        --repo-deps, -rd         Show Git repository dependencies under each repo (only for --output dependency).
                        --repo-usages, -ru       Show Git repository usages under each repo (only for --output dependency).
                        --project-deps, -pd      Show project dependencies under each project (only for --output dependency).
                        --project-usages, -pu    Show project usages under each project (only for --output dependency).

                        --help, -h               Show this help message.

                      Exit Codes:
                        0  Success
                        1  Invalid or missing path
                        2  Unknown output type
                        99 Unhandled error occurred

                      Colors:
                        Green    Repository name
                        Yellow   Project name
                        DarkGray Package reference
                        DarkYellow Project dependency
                        Red      Warning or outdated package version
                        Gray     Info (e.g., "Used by:" or "Dependencies:")

                      Notes:
                        - 'Used by' indicates how many projects or repos depend on the item.
                        - 'References' indicates how many projects or repos the item depends on.
                        - Circular dependencies will be reported with an error message.
                        - All output is deterministic and ordered for consistent CI/CD results.

                      Examples:
                        depend C:\dev
                        depend C:\dev --output dependency
                        depend C:\dev --output dependency --project Tharga.MongoDB
                        depend C:\dev -v full -pd -pu
                      """);
    }

}