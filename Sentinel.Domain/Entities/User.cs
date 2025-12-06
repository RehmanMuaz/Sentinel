using System;

namespace Sentinel.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User(Guid id, Guid tenantId, string email, string passwordHash, bool isActive, DateTime createdAt)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        Id = id;
        TenantId = tenantId;
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public static User Create(Guid tenantId, string email, string passwordHash)
        => new User(Guid.NewGuid(), tenantId, email, passwordHash, isActive: true, createdAt: DateTime.UtcNow);

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
