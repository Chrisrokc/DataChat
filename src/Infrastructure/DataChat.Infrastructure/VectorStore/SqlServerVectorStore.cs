using System.Data;
using DataChat.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.VectorStore;

/// <summary>
/// SQL Server 2025 Vector Store implementation using native VECTOR type and DiskANN indexes.
/// Embeddings are stored in VECTOR(1536) columns and searched using VECTOR_DISTANCE function.
/// </summary>
public class SqlServerVectorStore : IVectorStore
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqlServerVectorStore> _logger;

    public SqlServerVectorStore(
        IApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<SqlServerVectorStore> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Stores an embedding in the native VECTOR(1536) column using raw SQL.
    /// EF Core doesn't support the VECTOR type natively, so we use direct SQL.
    /// </summary>
    public async Task StoreEmbeddingAsync(
        Guid documentChunkId,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        // Convert float array to SQL Server VECTOR format: [f1,f2,f3,...]
        var vectorString = "[" + string.Join(",", embedding.Select(f => f.ToString("G9"))) + "]";

        var sql = @"UPDATE DocumentChunks
                    SET Embedding = CAST(@Vector AS VECTOR(1536))
                    WHERE Id = @Id";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Vector", vectorString);
        command.Parameters.AddWithValue("@Id", documentChunkId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        if (rowsAffected == 0)
        {
            _logger.LogWarning("No document chunk found with ID {DocumentChunkId} to store embedding", documentChunkId);
        }
        else
        {
            _logger.LogDebug("Stored embedding for document chunk {DocumentChunkId}", documentChunkId);
        }
    }

    /// <summary>
    /// Searches for similar documents using SQL Server 2025's VECTOR_DISTANCE function.
    /// The DiskANN index provides fast approximate nearest neighbor search.
    /// </summary>
    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        IEnumerable<Guid>? dataSourceFilter = null,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        // Convert embedding to SQL Server vector format
        var vectorString = "[" + string.Join(",", queryEmbedding.Select(f => f.ToString("G9"))) + "]";

        // Build parameterized IN clause to prevent SQL injection
        var dataSourceFilterList = dataSourceFilter?.ToList();
        var filterClause = "";
        var filterParams = new List<SqlParameter>();

        if (dataSourceFilterList?.Any() == true)
        {
            var paramNames = dataSourceFilterList.Select((_, i) => $"@ds{i}").ToList();
            filterClause = $"AND d.DataSourceId IN ({string.Join(",", paramNames)})";
            filterParams.AddRange(dataSourceFilterList.Select((id, i) =>
                new SqlParameter($"@ds{i}", id)));
        }

        // SQL Server 2025 vector search query using VECTOR_DISTANCE with DiskANN index
        var sql = $@"
            SELECT TOP (@TopK)
                c.Id AS DocumentChunkId,
                c.DocumentId,
                d.DataSourceId,
                c.Content,
                VECTOR_DISTANCE('cosine', c.Embedding, CAST(@QueryVector AS VECTOR(1536))) AS Distance,
                c.Metadata
            FROM DocumentChunks c
            INNER JOIN Documents d ON c.DocumentId = d.Id
            WHERE c.Embedding IS NOT NULL
            {filterClause}
            ORDER BY VECTOR_DISTANCE('cosine', c.Embedding, CAST(@QueryVector AS VECTOR(1536)))";

        var results = new List<VectorSearchResult>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TopK", topK);
        command.Parameters.AddWithValue("@QueryVector", vectorString);

        // Add data source filter parameters
        foreach (var param in filterParams)
        {
            command.Parameters.Add(param);
        }

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                // VECTOR_DISTANCE returns double, not float
                var distance = reader.GetDouble(reader.GetOrdinal("Distance"));
                // Convert distance to similarity score (1 - cosine distance)
                var score = (float)(1 - distance);

                results.Add(new VectorSearchResult(
                    DocumentChunkId: reader.GetGuid(reader.GetOrdinal("DocumentChunkId")),
                    DocumentId: reader.GetGuid(reader.GetOrdinal("DocumentId")),
                    DataSourceId: reader.GetGuid(reader.GetOrdinal("DataSourceId")),
                    Content: reader.GetString(reader.GetOrdinal("Content")),
                    Score: score,
                    Metadata: reader.IsDBNull(reader.GetOrdinal("Metadata"))
                        ? null
                        : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                            reader.GetString(reader.GetOrdinal("Metadata")))
                ));
            }

            _logger.LogDebug("Vector search returned {ResultCount} results", results.Count);
        }
        catch (SqlException ex) when (ex.Message.Contains("VECTOR_DISTANCE") ||
                                       ex.Message.Contains("VECTOR") ||
                                       ex.Message.Contains("vector_distance") ||
                                       ex.Message.Contains("invalid for argument"))
        {
            _logger.LogWarning(ex, "SQL Server 2025 vector features not available, falling back to in-memory search");
            // Fallback to in-memory cosine similarity for non-SQL Server 2025
            return await FallbackSearchAsync(queryEmbedding, topK, dataSourceFilter, connectionString!, cancellationToken);
        }

        return results;
    }

    /// <summary>
    /// Fallback search using in-memory cosine similarity calculation.
    /// Used when SQL Server 2025 vector features are not available.
    /// </summary>
    private async Task<IEnumerable<VectorSearchResult>> FallbackSearchAsync(
        float[] queryEmbedding,
        int topK,
        IEnumerable<Guid>? dataSourceFilter,
        string connectionString,
        CancellationToken cancellationToken)
    {
        // Build parameterized IN clause to prevent SQL injection
        var dataSourceFilterList = dataSourceFilter?.ToList();
        var filterClause = "";
        var filterParams = new List<SqlParameter>();

        if (dataSourceFilterList?.Any() == true)
        {
            var paramNames = dataSourceFilterList.Select((_, i) => $"@ds{i}").ToList();
            filterClause = $"AND d.DataSourceId IN ({string.Join(",", paramNames)})";
            filterParams.AddRange(dataSourceFilterList.Select((id, i) =>
                new SqlParameter($"@ds{i}", id)));
        }

        // Fetch embeddings as text and convert back to float arrays
        var sql = $@"
            SELECT
                c.Id AS DocumentChunkId,
                c.DocumentId,
                d.DataSourceId,
                c.Content,
                CAST(c.Embedding AS NVARCHAR(MAX)) AS EmbeddingText,
                c.Metadata
            FROM DocumentChunks c
            INNER JOIN Documents d ON c.DocumentId = d.Id
            WHERE c.Embedding IS NOT NULL
            {filterClause}";

        var chunks = new List<(Guid Id, Guid DocumentId, Guid DataSourceId, string Content, float[] Embedding, string? Metadata)>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);

        // Add data source filter parameters
        foreach (var param in filterParams)
        {
            command.Parameters.Add(param);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var embeddingText = reader.IsDBNull(reader.GetOrdinal("EmbeddingText"))
                ? null
                : reader.GetString(reader.GetOrdinal("EmbeddingText"));

            if (string.IsNullOrEmpty(embeddingText)) continue;

            // Parse the vector string [f1,f2,f3,...] to float array
            var embedding = ParseVectorString(embeddingText);
            if (embedding == null) continue;

            chunks.Add((
                reader.GetGuid(reader.GetOrdinal("DocumentChunkId")),
                reader.GetGuid(reader.GetOrdinal("DocumentId")),
                reader.GetGuid(reader.GetOrdinal("DataSourceId")),
                reader.GetString(reader.GetOrdinal("Content")),
                embedding,
                reader.IsDBNull(reader.GetOrdinal("Metadata")) ? null : reader.GetString(reader.GetOrdinal("Metadata"))
            ));
        }

        // Calculate cosine similarity in memory
        var results = chunks
            .Select(c => new
            {
                c.Id,
                c.DocumentId,
                c.DataSourceId,
                c.Content,
                c.Metadata,
                Score = CosineSimilarity(queryEmbedding, c.Embedding)
            })
            .OrderByDescending(c => c.Score)
            .Take(topK)
            .Select(c => new VectorSearchResult(
                c.Id,
                c.DocumentId,
                c.DataSourceId,
                c.Content,
                c.Score,
                string.IsNullOrEmpty(c.Metadata)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(c.Metadata)))
            .ToList();

        _logger.LogDebug("Fallback vector search returned {ResultCount} results", results.Count);

        return results;
    }

    private static float[]? ParseVectorString(string vectorString)
    {
        try
        {
            // Remove brackets and split by comma
            var trimmed = vectorString.Trim('[', ']');
            if (string.IsNullOrEmpty(trimmed)) return null;

            var parts = trimmed.Split(',');
            var result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = float.Parse(parts[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Deletes embeddings for all chunks of a document using raw SQL.
    /// </summary>
    public async Task DeleteByDocumentIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        var sql = "UPDATE DocumentChunks SET Embedding = NULL WHERE DocumentId = @DocumentId";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DocumentId", documentId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Cleared embeddings for {RowCount} chunks in document {DocumentId}", rowsAffected, documentId);
    }

    /// <summary>
    /// Deletes embeddings for all chunks in a data source using raw SQL.
    /// </summary>
    public async Task DeleteByDataSourceIdAsync(
        Guid dataSourceId,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        var sql = @"UPDATE c
                    SET c.Embedding = NULL
                    FROM DocumentChunks c
                    INNER JOIN Documents d ON c.DocumentId = d.Id
                    WHERE d.DataSourceId = @DataSourceId";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DataSourceId", dataSourceId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Cleared embeddings for {RowCount} chunks in data source {DataSourceId}", rowsAffected, dataSourceId);
    }
}
