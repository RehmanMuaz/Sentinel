using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;
using Sentinel.Domain.Entities;

namespace Sentinel.Infrastructure;

public class SentinelDbContext : DbContext
{
    public SentinelDbContext(DbContextOptions<SentinelDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Scope> Scopes => Set<Scope>();
    public DbSet<User> Users => Set<User>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseOpenIddict();
        builder.ApplyConfigurationsFromAssembly(typeof(SentinelDbContext).Assembly);
    }
}
