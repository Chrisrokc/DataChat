namespace DataChat.Application.Common.Interfaces;

public interface IFileSystemService
{
    bool DirectoryExists(string path);
    IEnumerable<string> GetFiles(string path, string searchPattern, bool includeSubfolders);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
    string GetFileHash(string path);
    long GetFileSize(string path);
    string GetMimeType(string path);
}
