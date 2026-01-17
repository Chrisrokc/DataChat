using DataChat.Application.Common.Interfaces;

namespace DataChat.Infrastructure.DocumentProcessing.Parsers;

public class TextFileParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".txt", ".md", ".csv" };

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var metadata = new Dictionary<string, object>
        {
            ["FileName"] = Path.GetFileName(filePath),
            ["Extension"] = Path.GetExtension(filePath)
        };

        // Estimate page count
        var pageCount = Math.Max(1, content.Length / 3000);

        return new ParsedDocument(content, metadata, pageCount);
    }
}
