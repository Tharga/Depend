namespace Tharga.Depend.Features.Command;

public interface ICommandService
{
    Task<int> ExecuteAsync(string[] args);
}