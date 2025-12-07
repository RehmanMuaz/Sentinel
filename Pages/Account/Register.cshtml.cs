using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sentinel.Infrastructure;
using Sentinel.Infrastructure.Security;
using Sentinel.Infrastructure.Services;
using System.Security.Cryptography;

public class RegisterModel : PageModel
{
    private readonly SentinelDbContext _db;
    private readonly ISecretHasher _hasher;
    private readonly IEmailSender _emailSender;

    public RegisterModel(SentinelDbContext db, ISecretHasher hasher, IEmailSender emailSender)
    {
        _db = db;
        _hasher = hasher;
        _emailSender = emailSender;
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
        // New users remain inactive until email is verified
        user.Deactivate();
        _db.Users.Add(user);

        // Issue email verification token
        var token = GenerateToken();
        var verification = Sentinel.Domain.Entities.EmailVerificationToken.Create(user.Id, token, TimeSpan.FromHours(24));
        _db.EmailVerificationTokens.Add(verification);
        await _db.SaveChangesAsync();

        // Send verification email (dev logger in this environment)
        var callbackUrl = Url.Page("/Account/Verify", null, new { token }, Request.Scheme, Request.Host.ToString());
        await _emailSender.SendAsync(user.Email, "Verify your email", $"Verify your account by visiting: {callbackUrl}");

        Success = "Account created. Check your email to verify your account.";
        return Page();
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
