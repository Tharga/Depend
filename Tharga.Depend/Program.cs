using Tharga.Depend.Services;

var argsList = args.Select(a => a.ToLowerInvariant()).ToList();

if (argsList.Contains("--help") || argsList.Contains("-h"))
{
    PrintHelp();
    return;
}

string inputPath = args.FirstOrDefault(a => !a.StartsWith("-"))
                   ?? Directory.GetCurrentDirectory();

var projectService = new VisualStudioProjectService();
var fileService = new FileListingService(projectService);

var repos = fileService.GetGitReposWithProjects(inputPath);

if (argsList.Contains("--list"))
{
    var printer = new RepositoryPrinterService();
    printer.Print(repos);
}
else if (argsList.Contains("--order"))
{
    var graphService = new DependencyGraphService();
    var levels = graphService.CalculateDependencyLevels(repos);
    var outputService = new DependencyOrderService();
    outputService.Print(levels);
}
else
{
    PrintHelp();
}

void PrintHelp()
{
    Console.WriteLine("""
                      Usage:
                        dotnet run -- <path> [--list | --order]

                      Arguments:
                        <path>         The folder to scan or a .csproj file. Defaults to current directory.

                      Options:
                        --list         Lists all git repositories and their projects with references.
                        --order        Outputs NuGet-packable projects in build/update order by dependency.
                        --help, -h     Shows this help message.
                      """);
}