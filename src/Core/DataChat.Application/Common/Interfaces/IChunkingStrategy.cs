namespace DataChat.Application.Common.Interfaces;

public interface IChunkingStrategy
{
    IEnumerable<TextChunk> ChunkDocument(
        ParsedDocument document,
        int chunkSize = 512,
        int overlap = 50);
}

public record TextChunk(
    int Index,
    string Content,
    string ContentHash,
    int TokenCount,
    Dictionary<string, object>? Metadata);
