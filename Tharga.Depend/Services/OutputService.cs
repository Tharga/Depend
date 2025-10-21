namespace Tharga.Depend.Services;

internal class OutputService : IOutputService
{
    public void PrintHelp()
    {
        System.Console.WriteLine("""
    Usage:
      Tharga.Depend.exe <folder> [--list | --order] [--project <PackageId>]

    Arguments:
      <folder>        Root folder containing Git repositories and projects.

    Options:
      --list          Show projects and dependencies (default: full list).
      --order         Show NuGet-packable build order by dependency.
      --project <id>  Filter to show output related to a specific NuGet project.
      --help, -h      Show this help message.

    Examples:
      Tharga.Depend.exe C:\dev --list
      Tharga.Depend.exe C:\dev --order
      Tharga.Depend.exe C:\dev --order --project Tharga.MongoDB
    """);
    }
}