using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;

namespace Sentinel.Infrastructure;

public class SentinelDbContext : DbContext
{
    public SentinelDbContext(DbContextOptions<SentinelDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseOpenIddict();

        // TODO: Add entity configurations here (Users, Tenants, etc.)
    }
}
