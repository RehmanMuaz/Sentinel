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
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configuration: base + environment + user secrets + env vars. Keep secrets out of appsettings.json.
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

// Redis multiplexer for caching/locks/blacklists (singleton per app)
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => 
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") 
    ?? builder.Configuration.GetValue<string>("Redis:ConnectionString")
    ?? "localhost:6379"));

// Combined auth: prefer bearer when Authorization header exists, otherwise cookie
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
    // Admin policy: scope OR admin role/claim
    options.AddPolicy("ManageClients", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim(c => c.Type == "scope" && (c.Value == "manage:clients" || c.Value == "api")) ||
            ctx.User.IsInRole("Admin") ||
            ctx.User.HasClaim("role", "Admin")));
});
builder.Services.AddControllers();
builder.Services.AddSingleton<ISecretHasher, Pbkdf2SecretHasher>();
builder.Services.AddSingleton<HealthCheckResponseFormatter>();
builder.Services.Configure<Sentinel.Infrastructure.Services.BrevoEmailOptions>(builder.Configuration.GetSection("Email:Brevo"));
builder.Services.AddHttpClient<Sentinel.Infrastructure.Services.BrevoEmailSender>();
builder.Services.AddSingleton<Sentinel.Infrastructure.Services.IEmailSender>(sp =>
    sp.GetRequiredService<Sentinel.Infrastructure.Services.BrevoEmailSender>());
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

        // Key material: require certs in prod; dev falls back to ephemeral keys
        var signingCertPath = builder.Configuration["Auth:SigningCertificate:Path"];
        var signingCertPassword = builder.Configuration["Auth:SigningCertificate:Password"];
        var encryptionCertPath = builder.Configuration["Auth:EncryptionCertificate:Path"];
        var encryptionCertPassword = builder.Configuration["Auth:EncryptionCertificate:Password"];

        var isDev = builder.Environment.IsDevelopment();

        if (!string.IsNullOrWhiteSpace(signingCertPath) && File.Exists(signingCertPath))
        {
            var signingCert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes(signingCertPath),
                signingCertPassword ?? string.Empty,
                X509KeyStorageFlags.MachineKeySet);
            options.AddSigningCertificate(signingCert);
        }
        else if (isDev)
        {
            options.AddEphemeralSigningKey(); // dev fallback
        }
        else
        {
            throw new InvalidOperationException("Signing certificate is required in production. Configure Auth:SigningCertificate:Path/Password.");
        }

        if (!string.IsNullOrWhiteSpace(encryptionCertPath) && File.Exists(encryptionCertPath))
        {
            var encryptionCert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes(encryptionCertPath),
                encryptionCertPassword ?? string.Empty,
                X509KeyStorageFlags.MachineKeySet);
            options.AddEncryptionCertificate(encryptionCert);
        }
        else if (isDev)
        {
            options.AddEphemeralEncryptionKey(); // dev fallback
        }
        else
        {
            throw new InvalidOperationException("Encryption certificate is required in production. Configure Auth:EncryptionCertificate:Path/Password.");
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

// Rate limiting: apply conservative limits to auth/token endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddSlidingWindowLimiter("auth-limit", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow = 6;
        o.QueueLimit = 2;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddFixedWindowLimiter("token-limit", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
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

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseRateLimiter();
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
