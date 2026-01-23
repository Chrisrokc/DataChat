using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataChat.Web.HealthChecks;

/// <summary>
/// Health check that verifies database connectivity.
/// Returns setup-aware status when database is not configured.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public DatabaseHealthCheck(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Check if connection string is configured
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return HealthCheckResult.Degraded(
                "Database connection not configured. Setup required.",
                data: new Dictionary<string, object>
                {
                    ["SetupRequired"] = true,
                    ["Reason"] = "ConnectionStringMissing"
                });
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("Database connection is healthy.");
        }
        catch (SqlException ex)
        {
            // Determine if this is a setup issue or a runtime issue
            var isSetupIssue = ex.Number switch
            {
                4060 => true, // Cannot open database (database doesn't exist)
                4063 => true, // Cannot open database
                18456 => false, // Login failed (credentials issue)
                -1 => true, // Network error (server unreachable)
                -2 => true, // Timeout
                _ => false
            };

            if (isSetupIssue)
            {
                return HealthCheckResult.Degraded(
                    GetUserFriendlyMessage(ex),
                    ex,
                    new Dictionary<string, object>
                    {
                        ["SetupRequired"] = true,
                        ["Reason"] = "DatabaseUnreachable",
                        ["ErrorNumber"] = ex.Number,
                        ["ErrorMessage"] = ex.Message
                    });
            }

            return HealthCheckResult.Unhealthy(
                GetUserFriendlyMessage(ex),
                ex,
                new Dictionary<string, object>
                {
                    ["ErrorNumber"] = ex.Number,
                    ["ErrorMessage"] = ex.Message
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database connection failed with unexpected error.",
                ex);
        }
    }

    private static string GetUserFriendlyMessage(SqlException ex)
    {
        return ex.Number switch
        {
            -1 => "Cannot connect to database server. Server may be offline.",
            -2 => "Database connection timed out.",
            4060 => "Database does not exist. Run setup to create it.",
            4063 => "Cannot open database. Setup may be required.",
            18456 => "Database login failed. Check credentials.",
            _ => $"Database error: {ex.Message}"
        };
    }
}
