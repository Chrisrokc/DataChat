using System.Text.Json;
using DataChat.Application.Common.Interfaces;
using DataChat.Domain.Entities;
using DataChat.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.Services;

/// <summary>
/// Service for creating audit log entries.
/// Uses a separate DbContext to ensure audit logs are saved even if the main transaction fails.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDateTimeService _dateTime;
    private readonly ILogger<AuditService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AuditService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor,
        IDateTimeService dateTime,
        ILogger<AuditService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task LogAsync(string action, string? details = null, CancellationToken cancellationToken = default)
    {
        await LogAsync(
            _currentUser.UserId,
            action,
            entityType: null,
            entityId: null,
            oldValues: null,
            newValues: details != null ? new { Details = details } : null,
            cancellationToken);
    }

    public async Task LogAsync(
        string action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default)
    {
        await LogAsync(
            _currentUser.UserId,
            action,
            entityType,
            entityId,
            oldValues,
            newValues,
            cancellationToken);
    }

    public async Task LogAsync(
        Guid? userId,
        string action,
        string? entityType = null,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use a separate DbContext to ensure audit logs are saved independently
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = SerializeValues(oldValues),
                NewValues = SerializeValues(newValues),
                IpAddress = GetClientIpAddress(),
                Timestamp = _dateTime.UtcNow
            };

            dbContext.AuditLogs.Add(auditLog);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Audit log created: {Action} on {EntityType} {EntityId} by user {UserId}",
                action, entityType ?? "N/A", entityId ?? "N/A", userId);
        }
        catch (Exception ex)
        {
            // Don't let audit logging failures affect the main operation
            _logger.LogError(ex,
                "Failed to create audit log: {Action} on {EntityType} {EntityId}",
                action, entityType ?? "N/A", entityId ?? "N/A");
        }
    }

    private string? SerializeValues(object? values)
    {
        if (values == null)
            return null;

        try
        {
            return JsonSerializer.Serialize(values, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize audit log values");
            return null;
        }
    }

    private string? GetClientIpAddress()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return null;

            // Check for forwarded IP (when behind proxy/load balancer)
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Take the first IP if there are multiple
                return forwardedFor.Split(',')[0].Trim();
            }

            // Fall back to direct connection IP
            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
