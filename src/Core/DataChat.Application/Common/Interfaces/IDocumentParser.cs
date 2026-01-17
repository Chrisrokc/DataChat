namespace DataChat.Application.Common.Interfaces;

public interface IDocumentParser
{
    bool CanParse(string filePath);
    Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}

public record ParsedDocument(
    string Content,
    Dictionary<string, object> Metadata,
    int PageCount);

public interface IDocumentParserFactory
{
    IDocumentParser GetParser(string filePath);
}
