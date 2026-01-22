using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataChat.Web.HealthChecks;

/// <summary>
/// Health check that verifies database connectivity.
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
            return HealthCheckResult.Unhealthy(
                "Database connection failed.",
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
}
