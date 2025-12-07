using System;

namespace Sentinel.Domain.Entities;

public class Client
{
    public Guid Id { get; private set; }
    public Guid TenantId  { get; private set; }
    public String ClientId { get; private set; }
    public String Name { get; private set; }
    public String? ClientSecretHash { get; private set; } // Null for public client
    public ClientType Type { get; private set; }
    public IReadOnlyCollection<string> RedirectUris => _redirectUris.AsReadOnly();
    public IReadOnlyCollection<string> AllowedScopes => _allowedScopes.AsReadOnly();
    public DateTime CreatedAt { get; private set; }

    private readonly List<string> _redirectUris = new();
    private readonly List<string> _allowedScopes = new();

    private Client(Guid id, Guid tenantId, String clientId, String name, ClientType type, DateTime createdAt)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentNullException("ClientId is required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
    
        Id = id;
        TenantId = tenantId;
        ClientId = clientId;
        Name = name.Trim();
        Type = type;
        CreatedAt = createdAt;
    }
    public static Client Create(Guid tenantId, string clientId, string name, ClientType type)
        => new Client(Guid.NewGuid(), tenantId, clientId, name, type, DateTime.UtcNow);

    public void SetSecretHash(string? hash)
    {
        if (Type == ClientType.Public && !string.IsNullOrEmpty(hash))
            throw new InvalidOperationException("Public clients cannot have secrets.");
        ClientSecretHash = hash;
    }

    public void AddRedirectUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) throw new ArgumentException("Redirect URI is required.", nameof(uri));
        if (!_redirectUris.Contains(uri)) _redirectUris.Add(uri);
    }

    public void AddScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("Scope is required.", nameof(scope));
        if (!_allowedScopes.Contains(scope)) _allowedScopes.Add(scope);
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
    }

    public void SetType(ClientType type)
    {
        Type = type;
    }

    public void ReplaceRedirectUris(IEnumerable<string> uris)
    {
        _redirectUris.Clear();
        foreach (var uri in uris ?? Array.Empty<string>())
        {
            AddRedirectUri(uri);
        }
    }

    public void ReplaceAllowedScopes(IEnumerable<string> scopes)
    {
        _allowedScopes.Clear();
        foreach (var scope in scopes ?? Array.Empty<string>())
        {
            AddScope(scope);
        }
    }
}
public enum ClientType
{
    Confidential = 0,
    Public = 1
}
