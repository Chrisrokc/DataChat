using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataChat.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs slow-running requests.
/// Requests taking longer than 500ms are logged as warnings for monitoring.
/// </summary>
public class PerformanceLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly Stopwatch _timer = new();
    private const int SlowRequestThresholdMs = 500;

    public PerformanceLoggingBehavior(ILogger<PerformanceLoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMs = _timer.ElapsedMilliseconds;

        if (elapsedMs > SlowRequestThresholdMs)
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogWarning(
                "Long running request: {RequestName} took {ElapsedMs}ms",
                requestName,
                elapsedMs);
        }

        return response;
    }
}
