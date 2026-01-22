using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.AI;

/// <summary>
/// Manages concurrent AI requests to prevent overwhelming AI services
/// and provides backpressure when the system is overloaded.
/// </summary>
public interface IAiRequestQueue
{
    /// <summary>
    /// Executes an async operation with concurrency control.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);

    /// <summary>
    /// Executes a streaming operation with concurrency control.
    /// </summary>
    IAsyncEnumerable<T> ExecuteStreamAsync<T>(Func<CancellationToken, IAsyncEnumerable<T>> operation, CancellationToken ct);

    /// <summary>
    /// Gets the current number of requests in the queue.
    /// </summary>
    int QueuedCount { get; }

    /// <summary>
    /// Gets the current number of active requests.
    /// </summary>
    int ActiveCount { get; }
}

public class AiRequestQueue : IAiRequestQueue
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<AiRequestQueue> _logger;
    private const int MaxConcurrent = 15; // Max concurrent AI requests
    private const int MaxQueued = 100;    // Max queued requests before rejection
    private int _queuedCount;
    private int _activeCount;

    public int QueuedCount => _queuedCount;
    public int ActiveCount => _activeCount;

    public AiRequestQueue(ILogger<AiRequestQueue> logger)
    {
        _semaphore = new SemaphoreSlim(MaxConcurrent);
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        var currentQueued = Interlocked.Increment(ref _queuedCount);

        if (currentQueued > MaxQueued)
        {
            Interlocked.Decrement(ref _queuedCount);
            _logger.LogWarning("AI request queue is full ({QueuedCount}/{MaxQueued}). Rejecting request.",
                currentQueued - 1, MaxQueued);
            throw new InvalidOperationException("AI service is currently overloaded. Please try again in a moment.");
        }

        _logger.LogDebug("AI request queued. Queue depth: {QueuedCount}, Active: {ActiveCount}",
            currentQueued, _activeCount);

        try
        {
            await _semaphore.WaitAsync(ct);
            Interlocked.Increment(ref _activeCount);

            try
            {
                return await operation(ct);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCount);
                _semaphore.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _queuedCount);
        }
    }

    public async IAsyncEnumerable<T> ExecuteStreamAsync<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> operation,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var currentQueued = Interlocked.Increment(ref _queuedCount);

        if (currentQueued > MaxQueued)
        {
            Interlocked.Decrement(ref _queuedCount);
            _logger.LogWarning("AI request queue is full ({QueuedCount}/{MaxQueued}). Rejecting streaming request.",
                currentQueued - 1, MaxQueued);
            throw new InvalidOperationException("AI service is currently overloaded. Please try again in a moment.");
        }

        _logger.LogDebug("AI streaming request queued. Queue depth: {QueuedCount}, Active: {ActiveCount}",
            currentQueued, _activeCount);

        try
        {
            await _semaphore.WaitAsync(ct);
            Interlocked.Increment(ref _activeCount);

            try
            {
                await foreach (var item in operation(ct))
                {
                    yield return item;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeCount);
                _semaphore.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _queuedCount);
        }
    }
}
