using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Sentinel.Domain.Entities;
using Sentinel.Infrastructure;
using Sentinel.Infrastructure.Security;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sentinel.Api.Controllers;

[ApiController]
public class AuthorizationController : Controller
{
    private readonly SentinelDbContext _db;
    private readonly ISecretHasher _hasher;

    public AuthorizationController(SentinelDbContext db, ISecretHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpGet("~/connect/authorize")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Authorize()
    {
        var scopeParam = Request.Query["scope"].ToString();
        var requestedScopes = scopeParam.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // TODO: show a real consent page; for now auto-consent for signed-in user.
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var subject = User.FindFirstValue(Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim(Claims.Subject, subject),
            new Claim(Claims.Email, User.FindFirstValue(Claims.Email) ?? string.Empty),
            new Claim(Claims.Name, User.Identity?.Name ?? string.Empty)
        };

        var tenantClaim = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantClaim))
        {
            claims.Add(new Claim("tenant_id", tenantClaim));
        }

        foreach (var claim in claims)
        {
            claim.SetDestinations(Destinations.AccessToken, Destinations.IdentityToken);
            identity.AddClaim(claim);
        }

        identity.SetScopes(requestedScopes);
        identity.SetResources(await _db.Scopes
            .Where(s => requestedScopes.Contains(s.Name))
            .Select(s => s.Name)
            .ToListAsync());

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(requestedScopes);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/account/logout")]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return string.IsNullOrWhiteSpace(returnUrl)
            ? Ok(new { message = "Logged out." })
            : LocalRedirect(returnUrl);
    }
}
