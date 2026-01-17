using System.Security.Cryptography;
using System.Text;
using DataChat.Application.Common.Interfaces;

namespace DataChat.Infrastructure.DocumentProcessing.Chunking;

public class RecursiveChunkingStrategy : IChunkingStrategy
{
    private static readonly string[] Separators = { "\n\n", "\n", ". ", " ", "" };

    // Default chunk size reduced to 400 tokens to stay safely under embedding model limits
    // text-embedding-ada-002 has 8192 token limit, but with metadata and safety margin,
    // smaller chunks work better and improve retrieval precision
    public IEnumerable<TextChunk> ChunkDocument(
        ParsedDocument document,
        int chunkSize = 400,
        int overlap = 50)
    {
        var chunks = new List<TextChunk>();
        var text = document.Content;

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        var chunkIndex = 0;
        var textChunks = SplitTextRecursively(text, chunkSize, overlap);

        foreach (var chunk in textChunks)
        {
            if (string.IsNullOrWhiteSpace(chunk))
                continue;

            chunks.Add(new TextChunk(
                Index: chunkIndex++,
                Content: chunk.Trim(),
                ContentHash: ComputeHash(chunk),
                TokenCount: EstimateTokenCount(chunk),
                Metadata: document.Metadata));
        }

        return chunks;
    }

    private IEnumerable<string> SplitTextRecursively(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        // Estimate if text fits in one chunk
        if (EstimateTokenCount(text) <= chunkSize)
        {
            yield return text;
            yield break;
        }

        // Try each separator
        foreach (var separator in Separators)
        {
            if (string.IsNullOrEmpty(separator))
            {
                // Last resort: split by character count
                foreach (var chunk in SplitByCharacterCount(text, chunkSize * 4, overlap * 4))
                {
                    yield return chunk;
                }
                yield break;
            }

            var parts = text.Split(new[] { separator }, StringSplitOptions.None);

            if (parts.Length <= 1)
                continue;

            var currentChunk = new StringBuilder();
            var previousChunkEnd = string.Empty;

            foreach (var part in parts)
            {
                // If this single part exceeds chunk size, it needs to be split further
                if (EstimateTokenCount(part) > chunkSize)
                {
                    // Yield any accumulated chunk first
                    if (currentChunk.Length > 0)
                    {
                        yield return currentChunk.ToString();
                        currentChunk.Clear();
                    }

                    // Recursively split the oversized part using remaining separators
                    var separatorIndex = Array.IndexOf(Separators, separator);
                    var remainingSeparators = Separators.Skip(separatorIndex + 1).ToArray();

                    if (remainingSeparators.Length > 0 && !string.IsNullOrEmpty(remainingSeparators[0]))
                    {
                        // Try next separator level
                        foreach (var subChunk in SplitTextRecursively(part, chunkSize, overlap))
                        {
                            yield return subChunk;
                        }
                    }
                    else
                    {
                        // Fall back to character splitting for this oversized part
                        foreach (var subChunk in SplitByCharacterCount(part, chunkSize * 4, overlap * 4))
                        {
                            yield return subChunk;
                        }
                    }

                    previousChunkEnd = part.Length > overlap * 4 ? part[^(overlap * 4)..] : part;
                    continue;
                }

                var testChunk = currentChunk.Length > 0
                    ? currentChunk + separator + part
                    : part;

                if (EstimateTokenCount(testChunk) > chunkSize && currentChunk.Length > 0)
                {
                    // Yield current chunk
                    yield return currentChunk.ToString();

                    // Start new chunk with overlap
                    currentChunk.Clear();
                    if (!string.IsNullOrEmpty(previousChunkEnd))
                    {
                        currentChunk.Append(previousChunkEnd);
                        currentChunk.Append(separator);
                    }
                    currentChunk.Append(part);
                }
                else
                {
                    if (currentChunk.Length > 0)
                        currentChunk.Append(separator);
                    currentChunk.Append(part);
                }

                // Keep track of last part for overlap
                previousChunkEnd = part.Length > overlap * 4
                    ? part[^(overlap * 4)..]
                    : part;
            }

            if (currentChunk.Length > 0)
            {
                yield return currentChunk.ToString();
            }

            yield break;
        }
    }

    private IEnumerable<string> SplitByCharacterCount(string text, int maxChars, int overlap)
    {
        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(maxChars, text.Length - start);
            yield return text.Substring(start, length);
            start += maxChars - overlap;
        }
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private static int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token for English text
        return text.Length / 4;
    }
}
