using System.Text;
using ClosedXML.Excel;
using DataChat.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.DocumentProcessing.Parsers;

/// <summary>
/// Parser for spreadsheet files (Excel and CSV).
/// Converts tabular data to human-readable text format for RAG.
/// </summary>
public class SpreadsheetParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".xlsx", ".xls", ".csv" };
    private readonly ILogger<SpreadsheetParser> _logger;

    public SpreadsheetParser(ILogger<SpreadsheetParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var metadata = new Dictionary<string, object>
        {
            ["FileName"] = Path.GetFileName(filePath),
            ["Extension"] = extension,
            ["Type"] = "Spreadsheet"
        };

        try
        {
            string content;
            int sheetCount;

            if (extension == ".csv")
            {
                content = await ParseCsvAsync(filePath, cancellationToken);
                sheetCount = 1;
                metadata["Format"] = "CSV";
            }
            else
            {
                (content, sheetCount) = ParseExcel(filePath);
                metadata["Format"] = "Excel";
                metadata["SheetCount"] = sheetCount;
            }

            // Estimate page count based on content length
            var pageCount = Math.Max(1, content.Length / 3000);

            return new ParsedDocument(content, metadata, pageCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing spreadsheet: {FilePath}", filePath);
            return new ParsedDocument(
                $"[Spreadsheet file: {Path.GetFileName(filePath)} - Parsing failed: {ex.Message}]",
                metadata,
                1);
        }
    }

    private async Task<string> ParseCsvAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);

        if (lines.Length == 0)
            return "[Empty CSV file]";

        var sb = new StringBuilder();
        sb.AppendLine($"CSV Data from: {Path.GetFileName(filePath)}");
        sb.AppendLine(new string('-', 40));

        // Parse header
        var headers = ParseCsvLine(lines[0]);

        // Process data rows
        var rowCount = 0;
        for (int i = 1; i < lines.Length && rowCount < 1000; i++) // Limit to 1000 rows
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var values = ParseCsvLine(lines[i]);
            sb.AppendLine($"Row {rowCount + 1}:");

            for (int j = 0; j < Math.Min(headers.Length, values.Length); j++)
            {
                var header = headers[j].Trim();
                var value = values[j].Trim();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    sb.AppendLine($"  {header}: {value}");
                }
            }

            sb.AppendLine();
            rowCount++;
        }

        if (lines.Length > 1001)
        {
            sb.AppendLine($"[Note: Showing first 1000 of {lines.Length - 1} total rows]");
        }

        _logger.LogDebug("Parsed CSV {FilePath}: {RowCount} rows", filePath, rowCount);

        return sb.ToString();
    }

    private (string content, int sheetCount) ParseExcel(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sb = new StringBuilder();
        var sheetCount = workbook.Worksheets.Count;

        sb.AppendLine($"Excel Workbook: {Path.GetFileName(filePath)}");
        sb.AppendLine($"Sheets: {sheetCount}");
        sb.AppendLine(new string('=', 50));

        foreach (var worksheet in workbook.Worksheets)
        {
            sb.AppendLine();
            sb.AppendLine($"Sheet: {worksheet.Name}");
            sb.AppendLine(new string('-', 40));

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                sb.AppendLine("[Empty sheet]");
                continue;
            }

            var firstRow = usedRange.FirstRow().RowNumber();
            var lastRow = Math.Min(usedRange.LastRow().RowNumber(), firstRow + 999); // Limit to 1000 rows per sheet
            var firstCol = usedRange.FirstColumn().ColumnNumber();
            var lastCol = usedRange.LastColumn().ColumnNumber();

            // Get headers from first row
            var headers = new List<string>();
            for (int col = firstCol; col <= lastCol; col++)
            {
                var cell = worksheet.Cell(firstRow, col);
                headers.Add(cell.GetString().Trim());
            }

            // Process data rows
            var rowCount = 0;
            for (int row = firstRow + 1; row <= lastRow; row++)
            {
                sb.AppendLine($"Row {rowCount + 1}:");

                for (int col = firstCol; col <= lastCol; col++)
                {
                    var cell = worksheet.Cell(row, col);
                    var value = cell.GetString().Trim();
                    var headerIndex = col - firstCol;
                    var header = headerIndex < headers.Count ? headers[headerIndex] : $"Column{col}";

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        sb.AppendLine($"  {header}: {value}");
                    }
                }

                sb.AppendLine();
                rowCount++;
            }

            if (usedRange.LastRow().RowNumber() > lastRow)
            {
                sb.AppendLine($"[Note: Showing first 1000 of {usedRange.LastRow().RowNumber() - firstRow} total rows]");
            }

            _logger.LogDebug("Parsed Excel sheet {SheetName}: {RowCount} rows", worksheet.Name, rowCount);
        }

        return (sb.ToString(), sheetCount);
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var currentValue = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(c);
            }
        }

        result.Add(currentValue.ToString());
        return result.ToArray();
    }
}
