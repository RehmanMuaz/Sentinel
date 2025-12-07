using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sentinel.Domain.Entities;
using System.Text.Json;

namespace Sentinel.Infrastructure.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.ClientId).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Type).IsRequired();
        builder.Property(c => c.ClientSecretHash).HasMaxLength(500);
        builder.Property(c => c.CreatedAt).IsRequired();

        var listToJsonConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var listComparer = new ValueComparer<List<string>>(
            (l1, l2) => l1!.SequenceEqual(l2!),
            l => l.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            l => l.ToList());

        var redirectProperty = builder.Property<List<string>>("_redirectUris");
        redirectProperty.HasConversion(listToJsonConverter);
        redirectProperty.HasColumnName("_redirectUris");
        redirectProperty.Metadata.SetValueComparer(listComparer);
        redirectProperty.Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        var scopesProperty = builder.Property<List<string>>("_allowedScopes");
        scopesProperty.HasConversion(listToJsonConverter);
        scopesProperty.HasColumnName("_allowedScopes");
        scopesProperty.Metadata.SetValueComparer(listComparer);
        scopesProperty.Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => new { c.TenantId, c.ClientId }).IsUnique();
    }
}
