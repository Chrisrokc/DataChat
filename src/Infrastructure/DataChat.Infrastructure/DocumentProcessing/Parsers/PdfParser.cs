using System.Text;
using DataChat.Application.Common.Interfaces;
using UglyToad.PdfPig;

namespace DataChat.Infrastructure.DocumentProcessing.Parsers;

public class PdfParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".pdf" };

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(filePath);

        var contentBuilder = new StringBuilder();
        var metadata = new Dictionary<string, object>();
        var pageCount = document.NumberOfPages;

        for (int i = 1; i <= pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = document.GetPage(i);
            var text = page.Text;

            if (!string.IsNullOrWhiteSpace(text))
            {
                contentBuilder.AppendLine($"[Page {i}]");
                contentBuilder.AppendLine(text);
                contentBuilder.AppendLine();
            }
        }

        // Extract document metadata
        if (document.Information != null)
        {
            if (!string.IsNullOrEmpty(document.Information.Title))
                metadata["Title"] = document.Information.Title;
            if (!string.IsNullOrEmpty(document.Information.Author))
                metadata["Author"] = document.Information.Author;
            if (!string.IsNullOrEmpty(document.Information.Subject))
                metadata["Subject"] = document.Information.Subject;
            if (!string.IsNullOrEmpty(document.Information.CreationDate))
                metadata["CreatedDate"] = document.Information.CreationDate;
        }

        return Task.FromResult(new ParsedDocument(
            contentBuilder.ToString(),
            metadata,
            pageCount));
    }
}
