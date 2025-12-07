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

    [HttpGet("~/account/logout")]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return string.IsNullOrWhiteSpace(returnUrl)
            ? Ok(new { message = "Logged out." })
            : LocalRedirect(returnUrl);
    }
}
