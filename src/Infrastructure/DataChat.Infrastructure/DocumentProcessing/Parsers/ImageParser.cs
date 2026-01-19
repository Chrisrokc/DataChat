using DataChat.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Tesseract;

namespace DataChat.Infrastructure.DocumentProcessing.Parsers;

/// <summary>
/// Parser for image files using Tesseract OCR.
/// Requires tessdata folder with language files in the application root.
/// Download from: https://github.com/tesseract-ocr/tessdata
/// </summary>
public class ImageParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".tiff", ".bmp", ".gif" };
    private readonly ILogger<ImageParser> _logger;
    private readonly string _tessDataPath;

    public ImageParser(ILogger<ImageParser> logger)
    {
        _logger = logger;
        // Look for tessdata in multiple locations
        _tessDataPath = FindTessDataPath();
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var metadata = new Dictionary<string, object>
        {
            ["FileName"] = Path.GetFileName(filePath),
            ["Extension"] = Path.GetExtension(filePath),
            ["Type"] = "Image (OCR)"
        };

        if (string.IsNullOrEmpty(_tessDataPath) || !Directory.Exists(_tessDataPath))
        {
            _logger.LogWarning("Tesseract data path not found. OCR will not be performed. Expected path: tessdata folder in application root");
            return new ParsedDocument(
                $"[Image file: {Path.GetFileName(filePath)} - OCR not available. Tesseract language data not found.]",
                metadata,
                1);
        }

        try
        {
            var content = await Task.Run(() => ExtractTextFromImage(filePath), cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                content = $"[Image file: {Path.GetFileName(filePath)} - No text could be extracted via OCR]";
            }

            metadata["CharacterCount"] = content.Length;

            return new ParsedDocument(content, metadata, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing OCR on image: {FilePath}", filePath);
            return new ParsedDocument(
                $"[Image file: {Path.GetFileName(filePath)} - OCR failed: {ex.Message}]",
                metadata,
                1);
        }
    }

    private string ExtractTextFromImage(string filePath)
    {
        using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
        using var img = Pix.LoadFromFile(filePath);
        using var page = engine.Process(img);

        var text = page.GetText();
        var confidence = page.GetMeanConfidence();

        _logger.LogDebug("OCR completed for {FilePath} with confidence {Confidence:P0}", filePath, confidence);

        return text?.Trim() ?? string.Empty;
    }

    private static string FindTessDataPath()
    {
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "tessdata"),
            "/usr/share/tesseract-ocr/5/tessdata",
            "/usr/share/tesseract-ocr/4.00/tessdata",
            "/usr/local/share/tessdata"
        };

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "eng.traineddata")))
            {
                return path;
            }
        }

        // Return default path even if not found (will show warning at parse time)
        return possiblePaths[0];
    }
}
