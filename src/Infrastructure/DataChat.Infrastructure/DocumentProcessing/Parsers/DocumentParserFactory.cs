using DataChat.Application.Common.Interfaces;

namespace DataChat.Infrastructure.DocumentProcessing.Parsers;

public class DocumentParserFactory : IDocumentParserFactory
{
    private readonly IEnumerable<IDocumentParser> _parsers;

    public DocumentParserFactory(IEnumerable<IDocumentParser> parsers)
    {
        _parsers = parsers;
    }

    public IDocumentParser GetParser(string filePath)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(filePath));

        if (parser == null)
        {
            throw new NotSupportedException(
                $"No parser available for file type: {Path.GetExtension(filePath)}");
        }

        return parser;
    }
}
