using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DataChat.Infrastructure.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Build configuration - find the web project directory
        var basePath = Directory.GetCurrentDirectory();

        // Try to find the appsettings.json in various locations
        var possiblePaths = new[]
        {
            basePath, // Current directory (if running from Web project)
            Path.Combine(basePath, "src/Presentation/DataChat.Web"),
            Path.Combine(basePath, "../Presentation/DataChat.Web"),
            Path.Combine(basePath, "../../Presentation/DataChat.Web")
        };

        string? configPath = null;
        foreach (var path in possiblePaths)
        {
            if (File.Exists(Path.Combine(path, "appsettings.json")))
            {
                configPath = path;
                break;
            }
        }

        if (configPath == null)
        {
            throw new InvalidOperationException(
                $"Could not find appsettings.json. Searched in: {string.Join(", ", possiblePaths)}. Current directory: {basePath}");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        optionsBuilder.UseSqlServer(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        });

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
