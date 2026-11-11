using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CitizenTech.Api.Data;

/// <summary>Loads design-time configuration without opening a connection.</summary>
public sealed class GuidanceDbContextFactory : IDesignTimeDbContextFactory<GuidanceDbContext>
{
    public GuidanceDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<GuidanceDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connection = configuration.GetConnectionString("Oracle");
        var options = new DbContextOptionsBuilder<GuidanceDbContext>();

        // Offline generation needs a provider, not a connection string. Leaving it
        // unset also makes database update fail locally instead of contacting a placeholder.
        if (string.IsNullOrWhiteSpace(connection))
            options.UseOracle(oracle =>
                oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        else
            options.UseOracle(connection, oracle =>
                oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));

        return new GuidanceDbContext(options.Options);
    }
}
