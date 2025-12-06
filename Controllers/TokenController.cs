using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Sentinel.Domain.Entities;
using Sentinel.Infrastructure;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sentinel.Api.Controllers;

[ApiController]
public class TokenController : ControllerBase
{
    private readonly SentinelDbContext _db;

    public TokenController(SentinelDbContext db)
    {
        _db = db;
    }

    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        var grantType = Request.Form["grant_type"].ToString();
        if (!string.Equals(grantType, "client_credentials", StringComparison.Ordinal))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var (clientId, clientSecret) = ExtractClientCredentials();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientId == clientId);
        if (client is null)
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (client.Type == ClientType.Confidential)
        {
            if (string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(client.ClientSecretHash))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // TODO: replace with proper secret hashing/verification.
            if (!string.Equals(clientSecret, client.ClientSecretHash, StringComparison.Ordinal))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
        }

        var requestedScopes = Request.Form["scope"].ToString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        if (requestedScopes.Any())
        {
            var allowed = client.AllowedScopes.ToHashSet(StringComparer.Ordinal);
            if (requestedScopes.Any(s => !allowed.Contains(s)))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
        }

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(Claims.Subject, client.ClientId);
        identity.AddClaim(Claims.ClientId, client.ClientId);
        identity.AddClaim("tenant_id", client.TenantId.ToString());

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(requestedScopes);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private (string clientId, string? clientSecret) ExtractClientCredentials()
    {
        var formClientId = Request.Form["client_id"].ToString();
        var formSecret = Request.Form["client_secret"].ToString();

        if (!string.IsNullOrWhiteSpace(formClientId))
        {
            return (formClientId, formSecret);
        }

        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var header = authHeader.ToString();
            if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var token = header.Substring("Basic ".Length).Trim();
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = decoded.Split(':', 2);
                if (parts.Length == 2)
                {
                    return (parts[0], parts[1]);
                }
            }
        }

        return (string.Empty, null);
    }
}
