/** DbContext Factory for design time db context creation. Used for EF migrations/updates **/

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace Sentinel.Infrastructure;

public class SentinelDbContextFactory : IDesignTimeDbContextFactory<SentinelDbContext>
{
    // EF tooling uses this to spin up the DbContext without running the full app.
    public SentinelDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<SentinelDbContextFactory>(optional: true) // uses the UserSecretsId on this assembly
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SentinelDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("Default"));
        optionsBuilder.UseOpenIddict();

        return new SentinelDbContext(optionsBuilder.Options);
    }
}
