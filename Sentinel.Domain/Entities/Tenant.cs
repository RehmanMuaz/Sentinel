using System;

namespace Sentinel.Domain.Entities;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Tenant(Guid id, string name, string slug, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Tenant slug is required.", nameof(slug));

        Id = id;
        Name = name.Trim();
        Slug = NormalizeSlug(slug);
        CreatedAt = createdAt;
    }

    public static Tenant Create(string name, string slug)
        => new Tenant(Guid.NewGuid(), name, slug, DateTime.UtcNow);

    private static string NormalizeSlug(string slug)
    {
        var value = slug.Trim().ToLowerInvariant();
        return value.Replace(' ', '-');
    }
}
