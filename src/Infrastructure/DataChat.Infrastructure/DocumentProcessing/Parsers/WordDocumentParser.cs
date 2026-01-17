using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DataChat.Application.Common.Interfaces;

namespace DataChat.Infrastructure.DocumentProcessing.Parsers;

public class WordDocumentParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".docx", ".doc" };

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var contentBuilder = new StringBuilder();
        var metadata = new Dictionary<string, object>();

        using (var doc = WordprocessingDocument.Open(filePath, false))
        {
            // Extract core properties
            if (doc.PackageProperties != null)
            {
                if (!string.IsNullOrEmpty(doc.PackageProperties.Title))
                    metadata["Title"] = doc.PackageProperties.Title;
                if (!string.IsNullOrEmpty(doc.PackageProperties.Creator))
                    metadata["Author"] = doc.PackageProperties.Creator;
                if (!string.IsNullOrEmpty(doc.PackageProperties.Subject))
                    metadata["Subject"] = doc.PackageProperties.Subject;
                if (doc.PackageProperties.Created.HasValue)
                    metadata["CreatedDate"] = doc.PackageProperties.Created.Value;
            }

            // Extract body text
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                foreach (var element in body.Elements())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ExtractText(element, contentBuilder);
                }
            }

            // Extract text from headers and footers
            if (doc.MainDocumentPart != null)
            {
                foreach (var headerPart in doc.MainDocumentPart.HeaderParts)
                {
                    var header = headerPart.Header;
                    if (header != null)
                    {
                        foreach (var para in header.Elements<Paragraph>())
                        {
                            ExtractText(para, contentBuilder);
                        }
                    }
                }
            }
        }

        // Estimate page count (rough approximation based on content length)
        var pageCount = Math.Max(1, contentBuilder.Length / 3000);

        return Task.FromResult(new ParsedDocument(
            contentBuilder.ToString(),
            metadata,
            pageCount));
    }

    private static void ExtractText(OpenXmlElement element, StringBuilder builder)
    {
        if (element is Paragraph para)
        {
            var text = para.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text);
            }
        }
        else if (element is Table table)
        {
            foreach (var row in table.Elements<TableRow>())
            {
                var rowText = new List<string>();
                foreach (var cell in row.Elements<TableCell>())
                {
                    rowText.Add(cell.InnerText);
                }
                if (rowText.Any(t => !string.IsNullOrWhiteSpace(t)))
                {
                    builder.AppendLine(string.Join(" | ", rowText));
                }
            }
        }
        else
        {
            foreach (var child in element.Elements())
            {
                ExtractText(child, builder);
            }
        }
    }
}
