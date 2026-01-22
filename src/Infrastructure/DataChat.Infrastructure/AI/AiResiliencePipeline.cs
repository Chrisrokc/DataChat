using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace DataChat.Infrastructure.AI;

/// <summary>
/// Provides resilience patterns (retry, circuit breaker, timeout) for AI service calls.
/// This protects the application from cascading failures when AI services are unavailable.
/// </summary>
public interface IAiResiliencePipeline
{
    /// <summary>
    /// Executes an async operation with retry and circuit breaker protection.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    /// <summary>
    /// Executes a streaming operation with circuit breaker protection.
    /// Note: Streaming operations use circuit breaker but not retry (can't replay a stream).
    /// </summary>
    IAsyncEnumerable<T> ExecuteStreamAsync<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> operation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current circuit breaker state for monitoring.
    /// </summary>
    CircuitState CircuitState { get; }
}

public class AiResiliencePipeline : IAiResiliencePipeline
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ResiliencePipeline _streamPipeline;
    private readonly ILogger<AiResiliencePipeline> _logger;
    private CircuitState _circuitState = CircuitState.Closed;

    public CircuitState CircuitState => _circuitState;

    public AiResiliencePipeline(ILogger<AiResiliencePipeline> logger)
    {
        _logger = logger;

        // Build the resilience pipeline for non-streaming operations
        _pipeline = new ResiliencePipelineBuilder()
            // Timeout: Don't wait forever for AI responses
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(120),
                OnTimeout = args =>
                {
                    _logger.LogWarning("AI request timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            })
            // Retry: Retry transient failures with exponential backoff
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<TimeoutRejectedException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "AI request failed (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}ms. Error: {Error}",
                        args.AttemptNumber + 1,
                        3,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return default;
                }
            })
            // Circuit Breaker: Stop trying when service is clearly down
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,                    // Open if 50% of requests fail
                SamplingDuration = TimeSpan.FromSeconds(30),  // Within 30 second window
                MinimumThroughput = 5,                 // Need at least 5 requests to evaluate
                BreakDuration = TimeSpan.FromSeconds(30),     // Stay open for 30 seconds
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<TimeoutRejectedException>(),
                OnOpened = args =>
                {
                    _circuitState = CircuitState.Open;
                    _logger.LogError(
                        "AI service circuit breaker OPENED. Service unavailable for {Duration}s. Reason: {Reason}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.Message ?? "Multiple failures");
                    return default;
                },
                OnClosed = args =>
                {
                    _circuitState = CircuitState.Closed;
                    _logger.LogInformation("AI service circuit breaker CLOSED. Service recovered.");
                    return default;
                },
                OnHalfOpened = args =>
                {
                    _circuitState = CircuitState.HalfOpen;
                    _logger.LogInformation("AI service circuit breaker HALF-OPEN. Testing service availability.");
                    return default;
                }
            })
            .Build();

        // Simpler pipeline for streaming (no retry - can't replay streams)
        _streamPipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested),
                OnOpened = args =>
                {
                    _circuitState = CircuitState.Open;
                    _logger.LogError(
                        "AI service circuit breaker OPENED (streaming). Service unavailable for {Duration}s.",
                        args.BreakDuration.TotalSeconds);
                    return default;
                },
                OnClosed = args =>
                {
                    _circuitState = CircuitState.Closed;
                    _logger.LogInformation("AI service circuit breaker CLOSED (streaming). Service recovered.");
                    return default;
                },
                OnHalfOpened = args =>
                {
                    _circuitState = CircuitState.HalfOpen;
                    _logger.LogInformation("AI service circuit breaker HALF-OPEN (streaming). Testing availability.");
                    return default;
                }
            })
            .Build();
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await operation(token),
                cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning("AI service circuit is open. Request rejected immediately.");
            throw new InvalidOperationException(
                "AI service is temporarily unavailable due to multiple failures. Please try again in 30 seconds.",
                ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning("AI request timed out after all retry attempts.");
            throw new InvalidOperationException(
                "AI service request timed out. Please try again.",
                ex);
        }
    }

    public async IAsyncEnumerable<T> ExecuteStreamAsync<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> operation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Check circuit state before starting stream
        if (_circuitState == CircuitState.Open)
        {
            throw new InvalidOperationException(
                "AI service is temporarily unavailable due to multiple failures. Please try again in 30 seconds.");
        }

        IAsyncEnumerable<T>? stream = null;

        try
        {
            // Execute the initial connection through the circuit breaker
            stream = await _streamPipeline.ExecuteAsync(
                token =>
                {
                    var result = operation(token);
                    return ValueTask.FromResult(result);
                },
                cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning("AI service circuit is open. Streaming request rejected immediately.");
            throw new InvalidOperationException(
                "AI service is temporarily unavailable due to multiple failures. Please try again in 30 seconds.",
                ex);
        }

        // Stream the results (failures here will affect circuit breaker state)
        if (stream != null)
        {
            await foreach (var item in stream.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
    }
}

/// <summary>
/// Circuit breaker state for monitoring purposes.
/// </summary>
public enum CircuitState
{
    /// <summary>Circuit is closed - requests flow normally.</summary>
    Closed,
    /// <summary>Circuit is open - requests fail immediately.</summary>
    Open,
    /// <summary>Circuit is half-open - testing if service recovered.</summary>
    HalfOpen
}
