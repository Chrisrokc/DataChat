namespace DataChat.Application.Common.Interfaces;

/// <summary>
/// Service for creating audit log entries to track user actions and system events.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an action without entity change tracking.
    /// Used for events like login, logout, access denied, etc.
    /// </summary>
    Task LogAsync(string action, string? details = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an action on a specific entity.
    /// Used for create, update, delete operations.
    /// </summary>
    Task LogAsync(
        string action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an action for a specific user (when current user context is not available).
    /// Used for background processes or system-initiated actions.
    /// </summary>
    Task LogAsync(
        Guid? userId,
        string action,
        string? entityType = null,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Common audit action constants for consistency.
/// </summary>
public static class AuditActions
{
    // Authentication
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";

    // Entity operations
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";

    // Admin operations
    public const string ConfigurationChanged = "ConfigurationChanged";
    public const string BrandingChanged = "BrandingChanged";
    public const string SystemPromptChanged = "SystemPromptChanged";

    // User management
    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";
    public const string UserDeleted = "UserDeleted";
    public const string RoleAssigned = "RoleAssigned";
    public const string RoleRemoved = "RoleRemoved";

    // Data source operations
    public const string DataSourceCreated = "DataSourceCreated";
    public const string DataSourceUpdated = "DataSourceUpdated";
    public const string DataSourceDeleted = "DataSourceDeleted";
    public const string DataSourceSynced = "DataSourceSynced";
    public const string PermissionGranted = "PermissionGranted";
    public const string PermissionRevoked = "PermissionRevoked";

    // Document operations
    public const string DocumentUploaded = "DocumentUploaded";
    public const string DocumentDeleted = "DocumentDeleted";
}
