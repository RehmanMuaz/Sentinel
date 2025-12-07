using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Sentinel.Infrastructure;
using StackExchange.Redis;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Sentinel.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Get Settings
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

// Database context using PostgreSQL and OpenIddict
builder.Services.AddDbContext<SentinelDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
    options.UseOpenIddict();
});

// Redis Singleton Middleware
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => 
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") 
    ?? builder.Configuration.GetValue<string>("Redis:ConnectionString")
    ?? "localhost:6379"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Combined";
    options.DefaultChallengeScheme = "Combined";
})
.AddPolicyScheme("Combined", "Cookie or Bearer", options =>
{
    options.ForwardDefaultSelector = context =>
        context.Request.Headers.ContainsKey("Authorization")
            ? OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
            : Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.SlidingExpiration = true;
    options.Cookie.Name = "sentinel.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManageClients", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim(c => c.Type == "scope" && (c.Value == "manage:clients" || c.Value == "api")) ||
            ctx.User.IsInRole("Admin") ||
            ctx.User.HasClaim("role", "Admin")));
});
builder.Services.AddControllers();
builder.Services.AddSingleton<ISecretHasher, Pbkdf2SecretHasher>();
builder.Services.AddSingleton<HealthCheckResponseFormatter>();
builder.Services.AddRazorPages();
builder.Services.AddRazorPages();

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<SentinelDbContext>();
    })
    .AddServer(options =>
    {
        options
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetIntrospectionEndpointUris("/connect/introspect")
            .SetRevocationEndpointUris("/connect/revocation");

        options
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .AllowClientCredentialsFlow();

        var signingCertPath = builder.Configuration["Auth:SigningCertificate:Path"];
        var signingCertPassword = builder.Configuration["Auth:SigningCertificate:Password"];
        var encryptionCertPath = builder.Configuration["Auth:EncryptionCertificate:Path"];
        var encryptionCertPassword = builder.Configuration["Auth:EncryptionCertificate:Password"];

        if (!string.IsNullOrWhiteSpace(signingCertPath) && File.Exists(signingCertPath))
        {
            var signingCert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes(signingCertPath),
                signingCertPassword ?? string.Empty,
                X509KeyStorageFlags.MachineKeySet);
            options.AddSigningCertificate(signingCert);
        }
        else
        {
            options.AddEphemeralSigningKey(); // fallback for dev
        }

        if (!string.IsNullOrWhiteSpace(encryptionCertPath) && File.Exists(encryptionCertPath))
        {
            var encryptionCert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes(encryptionCertPath),
                encryptionCertPassword ?? string.Empty,
                X509KeyStorageFlags.MachineKeySet);
            options.AddEncryptionCertificate(encryptionCert);
        }
        else
        {
            options.AddEphemeralEncryptionKey(); // fallback for dev
        }

        options.UseAspNetCore()
            .EnableStatusCodePagesIntegration()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough();

        options.DisableAccessTokenEncryption();

        options.RegisterScopes("api", "manage:clients");
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

var defaultDb = builder.Configuration.GetConnectionString("Default");
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";

var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());

if (!string.IsNullOrWhiteSpace(defaultDb))
{
    healthChecks.AddNpgSql(defaultDb, name: "postgres");
}

if (!string.IsNullOrWhiteSpace(redisConn))
{
    healthChecks.AddRedis(redisConn, name: "redis");
}

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers(); // exposes OpenIddict endpoints like /connect/token

app.MapRazorPages();

// Public liveness test
app.MapGet("/", () => "Hello World!");

// Returns claims for the authenticated user.
app.MapGet("/connect/userinfo", (HttpContext ctx) =>
{
    var user = ctx.User;
    return Results.Json(new
    {
        sub = user.FindFirst("sub")?.Value ?? user.Identity?.Name,
        name = user.FindFirst("name")?.Value,
        email = user.FindFirst("email")?.Value,
        scopes = user.FindAll("scope").Select(c => c.Value).ToArray()
    });
}).RequireAuthorization();

var formatter = app.Services.GetRequiredService<HealthCheckResponseFormatter>();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = formatter.WriteResponse
});

app.Run();
