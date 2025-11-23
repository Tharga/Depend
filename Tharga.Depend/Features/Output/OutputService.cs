using System.Reflection;

namespace Tharga.Depend.Features.Output;

internal class OutputService : IOutputService
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
        var assemblyName = Assembly.GetEntryAssembly()?.GetName();
        var version = assemblyName?.Version;

        Console.WriteLine($"{assemblyName?.Name} version {version} by Thargelion AB.");
        Console.WriteLine("""
                      Usage:
                        depend [<folder>] [<parameter>] [--output <list|dependency>] [--project <ProjectName>]

                      Arguments:
                        <folder>                 Root folder containing Git repositories and projects.
                        <parameter>              Can be any of the output modes or view modes, in full text.

                      Options:
                        --output, -o <mode>      Output mode:
                                                   dependency, d  Ordered by build dependency (default)
                                                   list, l        Grouped by repo
                                                   tree, t        Dependency tree

                        --view, -v <view>        View mode:
                                                   default, d       Repos + projects (default)
                                                   full, f          Repos + projects + packages
                                                   repo, r          Only repos
                                                   project, p       Only projects

                        --only-packable, -n      Show only NuGet-packable projects (projects with a PackageId).
                        --repo-deps, -rd         Show Git repository dependencies under each repo. (Not for --output tree)
                        --repo-usages, -ru       Show Git repository usages under each repo. (Not for --output tree)
                        --project-deps, -pd      Show project dependencies under each project. (Not for --output tree)
                        --project-usages, -pu    Show project usages under each project. (Not for --output tree)

                        --register               Registers depend.exe to Path so it can be executed everywhere.
                        --unregister             Removes Path registration.

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
                        - Names surrounded by [] are nuget packages that is part of a project.
                        - In the tree output, '...' means there are omited packages that have already been printed.

                      Examples:
                        depend
                        depend C:\dev --output dependency
                        depend C:\dev --output dependency --project Tharga.MongoDB
                        depend C:\dev -v full -pd -pu
                        depend full tree
                      """);
    }

    //  --exclude, -x <pattern>  Exclude projects containing this text from output (e.g. ".Tests").
    //  --project, -p            Filter results to a specific project by name.
}