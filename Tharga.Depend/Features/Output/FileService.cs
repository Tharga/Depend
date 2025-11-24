namespace Tharga.Depend.Features.Output;

internal class FileService : IFileService
{
    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }
}