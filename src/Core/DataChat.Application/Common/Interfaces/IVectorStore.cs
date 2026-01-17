namespace DataChat.Application.Common.Interfaces;

public interface IVectorStore
{
    Task StoreEmbeddingAsync(
        Guid documentChunkId,
        float[] embedding,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        IEnumerable<Guid>? dataSourceFilter = null,
        CancellationToken cancellationToken = default);

    Task DeleteByDocumentIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task DeleteByDataSourceIdAsync(
        Guid dataSourceId,
        CancellationToken cancellationToken = default);
}

public record VectorSearchResult(
    Guid DocumentChunkId,
    Guid DocumentId,
    Guid DataSourceId,
    string Content,
    float Score,
    Dictionary<string, object>? Metadata);
