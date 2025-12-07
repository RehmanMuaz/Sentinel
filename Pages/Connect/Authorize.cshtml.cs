using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Sentinel.Infrastructure;
using static OpenIddict.Abstractions.OpenIddictConstants;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class AuthorizeModel : PageModel
{
    private readonly SentinelDbContext _db;

    public AuthorizeModel(SentinelDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string ClientId { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();

    public IActionResult OnGet()
    {
        var clientId = Request.Query["client_id"].ToString();
        var scopeParam = Request.Query["scope"].ToString();
        var scopes = scopeParam.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        if (string.IsNullOrWhiteSpace(clientId)) return BadRequest("Missing client_id.");

        ClientId = clientId;
        Scopes = scopes;
        ReturnUrl = Request.Path + Request.QueryString;
        return Page();
    }

    public async Task<IActionResult> OnPostAccept([FromForm] string returnUrl, [FromForm] string[] scopes)
    {
        var subject = User.FindFirstValue(Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.NewGuid().ToString();
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

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        foreach (var claim in claims)
        {
            claim.SetDestinations(Destinations.AccessToken, Destinations.IdentityToken);
            identity.AddClaim(claim);
        }

        identity.SetScopes(scopes);
        identity.SetResources(await _db.Scopes
            .Where(s => scopes.Contains(s.Name))
            .Select(s => s.Name)
            .ToListAsync());

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    public IActionResult OnPostDeny([FromForm] string returnUrl)
    {
        return Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user denied the authorization request."
            }));
    }
}
