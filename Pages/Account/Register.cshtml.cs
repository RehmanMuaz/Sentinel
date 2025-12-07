using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sentinel.Infrastructure;
using Sentinel.Infrastructure.Security;

public class RegisterModel : PageModel
{
    private readonly SentinelDbContext _db;
    private readonly ISecretHasher _hasher;

    public RegisterModel(SentinelDbContext db, ISecretHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }
    public string? Success { get; set; }

    public void OnGet()
    {
        // no-op, just render form
    }

    public async Task<IActionResult> OnPost(string slug, string email, string password, string confirmPassword, string? returnUrl)
    {
        Slug = slug;
        ReturnUrl = returnUrl;

        if (string.IsNullOrWhiteSpace(slug))
        {
            Error = "Missing tenant slug.";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            Error = "Email and password are required.";
            return Page();
        }
        if (!password.Equals(confirmPassword))
        {
            Error = "Passwords do not match.";
            return Page();
        }
        if (password.Length < 6)
        {
            Error = "Password must be at least 6 characters.";
            return Page();
        }

        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant());
        if (tenant is null)
        {
            Error = "Tenant not found.";
            return Page();
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.TenantId == tenant.Id && u.Email == normalizedEmail);
        if (exists)
        {
            Error = "A user with this email already exists for the tenant.";
            return Page();
        }

        var hash = _hasher.Hash(password);
        var user = Sentinel.Domain.Entities.User.Create(tenant.Id, normalizedEmail, hash, isAdmin: false);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Auto sign-in; remove if you prefer email verification.
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("tenant_id", user.TenantId.ToString())
        };
        if (user.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim("role", "Admin"));
            claims.Add(new Claim("scope", "manage:clients"));
        }
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        Success = "Account created and signed in.";
        return Page();
    }
}
