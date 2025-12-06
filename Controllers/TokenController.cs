using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sentinel.Api.Controllers;

[ApiController]
public class TokenController : ControllerBase
{
    [HttpPost("~/connect/token")]
    public IActionResult Exchange()
    {
        // OpenIddict validates the request before invoking this action when passthrough is enabled.
        var grantType = Request.Form["grant_type"].ToString();
        if (string.Equals(grantType, "client_credentials", StringComparison.Ordinal))
        {
            // Prefer form client_id; fall back to Basic auth header.
            var clientId = Request.Form["client_id"].ToString();
            if (string.IsNullOrEmpty(clientId) && Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var header = authHeader.ToString();
                if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = header.Substring("Basic ".Length).Trim();
                    var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
                    var parts = decoded.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        clientId = parts[0];
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(clientId))
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            identity.AddClaim(Claims.Subject, clientId);
            identity.AddClaim(Claims.ClientId, clientId);

            var principal = new ClaimsPrincipal(identity);
            var scopes = Request.Form["scope"].ToString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            principal.SetScopes(scopes);

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
