namespace Tharga.Depend.Features.Output;

public interface IOutputService
{
    void WriteLine(string message, ConsoleColor color = ConsoleColor.Gray);
    void Error(string message);
    void Warning(string message);
    void PrintHelp();
}