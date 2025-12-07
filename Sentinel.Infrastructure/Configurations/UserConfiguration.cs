using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinel.Domain.Entities;

namespace Sentinel.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.TenantId).IsRequired();
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.IsAdmin).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
    }
}
