using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sentinel.Api.Controllers;

[ApiController]
[Route("api/openiddict/clients")]
[Authorize(Policy = "ManageClients")]
public class OpenIddictApplicationsController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _manager;

    public OpenIddictApplicationsController(IOpenIddictApplicationManager manager)
    {
        _manager = manager;
    }

    [HttpGet]
    public async IAsyncEnumerable<OpenIddictClientSummary> GetAll()
    {
        await foreach (var app in _manager.ListAsync())
        {
            var clientId = await _manager.GetClientIdAsync(app);
            var display = await _manager.GetDisplayNameAsync(app);
            var type = await _manager.GetClientTypeAsync(app);
            var scopes = await _manager.GetPermissionsAsync(app);
            yield return new OpenIddictClientSummary
            {
                ClientId = clientId ?? string.Empty,
                DisplayName = display ?? string.Empty,
                ClientType = type ?? string.Empty,
                Permissions = scopes.ToArray()
            };
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOidcClientRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var existing = await _manager.FindByClientIdAsync(request.ClientId);
        if (existing is not null) return Conflict("client_id already exists.");

        var descriptor = BuildDescriptor(request);

        await _manager.CreateAsync(descriptor);
        return Ok(new { message = "client created", clientId = request.ClientId });
    }

    [HttpPut("{clientId}")]
    public async Task<IActionResult> Update(string clientId, [FromBody] CreateOidcClientRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!string.Equals(clientId, request.ClientId, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Route clientId must match request clientId.");

        var app = await _manager.FindByClientIdAsync(clientId);
        if (app is null) return NotFound();

        var descriptor = BuildDescriptor(request);
        await _manager.UpdateAsync(app, descriptor);
        return Ok(new { message = "client updated", clientId = request.ClientId });
    }

    [HttpDelete("{clientId}")]
    public async Task<IActionResult> Delete(string clientId)
    {
        var app = await _manager.FindByClientIdAsync(clientId);
        if (app is null) return NotFound();
        await _manager.DeleteAsync(app);
        return NoContent();
    }

    private static OpenIddictApplicationDescriptor BuildDescriptor(CreateOidcClientRequest request)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = request.ClientId,
            DisplayName = request.DisplayName,
            ClientType = request.IsPublic ? ClientTypes.Public : ClientTypes.Confidential
        };

        if (!request.IsPublic)
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

        if (request.AllowClientCredentials)
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        }

        if (request.AllowedScopes?.Any() == true)
        {
            foreach (var scope in request.AllowedScopes.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
            }
        }
        else
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + "api");
        }

        descriptor.Requirements.Clear();
        if (request.RequirePkce)
        {
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }

        return descriptor;
    }
}

public class CreateOidcClientRequest
{
    [Required]
    public string ClientId { get; set; } = string.Empty;
    [Required]
    public string DisplayName { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = true;
    public string? ClientSecret { get; set; }
    public string[]? RedirectUris { get; set; }
    public string[]? AllowedScopes { get; set; }
    public bool AllowClientCredentials { get; set; } = false;
    public bool RequirePkce { get; set; } = true;
}

public class OpenIddictClientSummary
{
    public string ClientId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ClientType { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = Array.Empty<string>();
}
