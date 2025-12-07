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

    [HttpGet("~/account/login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null) =>
        Results.Json(new { message = "POST credentials to /account/login", returnUrl });

    [HttpPost("~/account/login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginPost([FromForm] string email, [FromForm] string password, [FromForm] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return BadRequest("Email and password are required.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        if (user is null || !_hasher.Verify(user.PasswordHash, password) || !user.IsActive)
            return Unauthorized("Invalid credentials.");

        var claims = new List<Claim>
        {
            new Claim(Claims.Subject, user.Id.ToString()),
            new Claim(Claims.Email, user.Email),
            new Claim(Claims.Name, user.Email),
            new Claim("tenant_id", user.TenantId.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return string.IsNullOrWhiteSpace(returnUrl)
            ? Ok(new { message = "Logged in." })
            : LocalRedirect(returnUrl);
    }

    [HttpGet("~/connect/authorize")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // TODO: show a real consent page; for now auto-consent for signed-in user.
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.AddClaim(Claims.Subject, User.FindFirstValue(Claims.Subject) ?? User.Identity?.Name ?? Guid.NewGuid().ToString());
        identity.AddClaim(Claims.Email, User.FindFirstValue(Claims.Email) ?? string.Empty);
        identity.AddClaim(Claims.Name, User.Identity?.Name ?? string.Empty);
        var tenantClaim = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantClaim))
        {
            identity.AddClaim("tenant_id", tenantClaim);
        }

        identity.SetScopes(request.GetScopes());
        identity.SetResources(await _db.Scopes
            .Where(s => request.GetScopes().Contains(s.Name))
            .Select(s => s.Name)
            .ToListAsync());

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
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
