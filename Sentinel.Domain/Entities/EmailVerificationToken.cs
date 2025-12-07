using System;

namespace Sentinel.Domain.Entities;

/// <summary>
/// Email verification token issued during registration to confirm ownership of the email address.
/// </summary>
public class EmailVerificationToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }

    private EmailVerificationToken(Guid id, Guid userId, string token, DateTime expiresAt, DateTime createdAt)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Token is required.", nameof(token));

        Id = id;
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public static EmailVerificationToken Create(Guid userId, string token, TimeSpan lifetime)
    {
        return new EmailVerificationToken(Guid.NewGuid(), userId, token, DateTime.UtcNow.Add(lifetime), DateTime.UtcNow);
    }

    public bool IsValid() => ConsumedAt is null && DateTime.UtcNow <= ExpiresAt;

    public void MarkConsumed()
    {
        ConsumedAt = DateTime.UtcNow;
    }
}
