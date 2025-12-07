using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Entities;
using Sentinel.Infrastructure;
using Sentinel.Infrastructure.Security;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sentinel.Api.Controllers;

[Authorize(Policy = "ManageClients")]
[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly SentinelDbContext _db;
    private readonly ISecretHasher _secretHasher;
    private readonly IOpenIddictApplicationManager _appManager;

    public ClientsController(SentinelDbContext db, ISecretHasher secretHasher, IOpenIddictApplicationManager appManager)
    {
        _db = db;
        _secretHasher = secretHasher;
        _appManager = appManager;
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

        // Also register with OpenIddict
        var descriptor = BuildDescriptor(request, client.Name);
        await _appManager.CreateAsync(descriptor);

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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientResponse>> Update(Guid id, [FromBody] CreateClientRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null) return NotFound();

        if (!Enum.TryParse<ClientType>(request.Type, true, out var clientType))
        {
            return BadRequest("Invalid client type. Use Confidential or Public.");
        }

        client.UpdateName(request.Name);
        client.SetType(clientType);
        client.ReplaceRedirectUris(request.RedirectUris ?? Array.Empty<string>());
        client.ReplaceAllowedScopes(request.AllowedScopes ?? Array.Empty<string>());

        if (clientType == ClientType.Confidential)
        {
            if (string.IsNullOrWhiteSpace(request.ClientSecret))
            {
                return BadRequest("Client secret is required for confidential clients.");
            }
            client.SetSecretHash(_secretHasher.Hash(request.ClientSecret));
        }
        else
        {
            client.SetSecretHash(null);
        }

        await _db.SaveChangesAsync();

        // Update OpenIddict application if it exists
        var app = await _appManager.FindByClientIdAsync(client.ClientId);
        var descriptor = BuildDescriptor(request, client.Name);
        if (app is null)
        {
            await _appManager.CreateAsync(descriptor);
        }
        else
        {
            await _appManager.UpdateAsync(app, descriptor);
        }

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

        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null) return NotFound();

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync();

        var app = await _appManager.FindByClientIdAsync(client.ClientId);
        if (app is not null)
        {
            await _appManager.DeleteAsync(app);
        }

        return NoContent();
    }

    private OpenIddictApplicationDescriptor BuildDescriptor(CreateClientRequest request, string displayName)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = request.ClientId,
            DisplayName = displayName,
            ClientType = string.Equals(request.Type, "Public", StringComparison.OrdinalIgnoreCase)
                ? ClientTypes.Public
                : ClientTypes.Confidential
        };

        if (!string.Equals(request.Type, "Public", StringComparison.OrdinalIgnoreCase))
        {
            descriptor.ClientSecret = request.ClientSecret;
        }

        if (request.RedirectUris?.Any() == true)
        {
            foreach (var uri in request.RedirectUris.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }
        }

        var scopes = request.AllowedScopes ?? Array.Empty<string>();
        descriptor.Permissions.Clear();
        descriptor.Permissions.UnionWith(new[]
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.Revocation,
            Permissions.Endpoints.Introspection,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + Scopes.OpenId
        });

        if (!string.Equals(request.Type, "Public", StringComparison.OrdinalIgnoreCase))
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        }

        foreach (var scope in scopes.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        descriptor.Requirements.Clear();
        if (string.Equals(request.Type, "Public", StringComparison.OrdinalIgnoreCase))
        {
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }

        return descriptor;
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
