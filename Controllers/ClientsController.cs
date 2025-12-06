using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Entities;
using Sentinel.Infrastructure;
using Sentinel.Infrastructure.Security;

namespace Sentinel.Api.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly SentinelDbContext _db;
    private readonly ISecretHasher _secretHasher;

    public ClientsController(SentinelDbContext db, ISecretHasher secretHasher)
    {
        _db = db;
        _secretHasher = secretHasher;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClientResponse>>> GetAll()
    {
        var clients = await _db.Clients.AsNoTracking().ToListAsync();
        var result = clients.Select(c => new ClientResponse
        {
            Id = c.Id,
            TenantId = c.TenantId,
            ClientId = c.ClientId,
            Name = c.Name,
            Type = c.Type.ToString(),
            AllowedScopes = c.AllowedScopes.ToArray(),
            RedirectUris = c.RedirectUris.ToArray(),
            CreatedAt = c.CreatedAt
        });

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ClientResponse>> Create([FromBody] CreateClientRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!Enum.TryParse<ClientType>(request.Type, true, out var clientType))
        {
            return BadRequest("Invalid client type. Use Confidential or Public.");
        }

        var tenantExists = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == request.TenantId);
        if (!tenantExists)
        {
            return BadRequest("Tenant does not exist.");
        }

        var exists = await _db.Clients.AsNoTracking().AnyAsync(c => c.TenantId == request.TenantId && c.ClientId == request.ClientId);
        if (exists)
        {
            return Conflict("ClientId already exists for this tenant.");
        }

        var client = Client.Create(request.TenantId, request.ClientId, request.Name, clientType);

        if (clientType == ClientType.Confidential)
        {
            if (string.IsNullOrWhiteSpace(request.ClientSecret))
            {
                return BadRequest("Client secret is required for confidential clients.");
            }
            client.SetSecretHash(_secretHasher.Hash(request.ClientSecret));
        }

        if (request.RedirectUris != null)
        {
            foreach (var uri in request.RedirectUris.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                client.AddRedirectUri(uri);
            }
        }

        if (request.AllowedScopes != null)
        {
            foreach (var scope in request.AllowedScopes.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                client.AddScope(scope);
            }
        }

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        var response = new ClientResponse
        {
            Id = client.Id,
            TenantId = client.TenantId,
            ClientId = client.ClientId,
            Name = client.Name,
            Type = client.Type.ToString(),
            AllowedScopes = client.AllowedScopes.ToArray(),
            RedirectUris = client.RedirectUris.ToArray(),
            CreatedAt = client.CreatedAt
        };

        return CreatedAtAction(nameof(GetAll), new { id = client.Id }, response);
    }
}

public class CreateClientRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "Confidential"; // Confidential | Public

    public string? ClientSecret { get; set; }

    public string[]? RedirectUris { get; set; }

    public string[]? AllowedScopes { get; set; }
}

public class ClientResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string[] AllowedScopes { get; set; } = Array.Empty<string>();
    public string[] RedirectUris { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
}
