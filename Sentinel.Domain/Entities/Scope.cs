using System;

namespace Sentinel.Domain.Entities;

public class Scope
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; } // null if global
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private Scope(Guid id, Guid? tenantId, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Scope name is required.", nameof(name));

        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        Description = description?.Trim();
    }

    public static Scope Create(string name, string? description = null, Guid? tenantId = null)
        => new Scope(Guid.NewGuid(), tenantId, name, description);

    public void Update(string name, string? description, Guid? tenantId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Scope name is required.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        TenantId = tenantId;
    }
}
