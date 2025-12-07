using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sentinel.Infrastructure;
using System.Security.Claims;

public class VerifyModel : PageModel
{
    private readonly SentinelDbContext _db;

    public VerifyModel(SentinelDbContext db)
    {
        _db = db;
    }

    public string? Message { get; set; }
    public bool IsError { get; set; }

    public async Task OnGet(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            IsError = true;
            Message = "Missing token.";
            return;
        }

        var record = await _db.EmailVerificationTokens
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Token == token);

        if (record is null || !record.IsValid())
        {
            IsError = true;
            Message = "Invalid or expired token.";
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == record.UserId);
        if (user is null)
        {
            IsError = true;
            Message = "User not found.";
            return;
        }

        user.Activate();
        record.MarkConsumed();
        await _db.SaveChangesAsync();

        // Optionally sign the user in after verification
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

        IsError = false;
        Message = "Email verified. You are now signed in.";
    }
}
