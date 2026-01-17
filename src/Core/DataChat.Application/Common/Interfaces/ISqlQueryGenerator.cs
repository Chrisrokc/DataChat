namespace DataChat.Application.Common.Interfaces;

public interface ISqlQueryGenerator
{
    Task<SqlQueryResult> GenerateQueryAsync(
        string naturalLanguageQuery,
        SqlViewMetadata viewMetadata,
        CancellationToken cancellationToken = default);

    bool ValidateQuerySafety(string sqlQuery);
}

public record SqlQueryResult(
    string GeneratedSql,
    string Explanation,
    bool IsValid,
    IEnumerable<string>? Warnings);

public record SqlViewMetadata(
    string ViewName,
    string Schema,
    string? Description,
    IEnumerable<ColumnMetadata> Columns);

public record ColumnMetadata(
    string Name,
    string DataType,
    string? Description,
    bool IsNullable);
