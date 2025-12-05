using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Sentinel.Infrastructure;
using StackExchange.Redis;
using System.Linq;
using System.Security.Claims;

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
    options.DefaultAuthenticateScheme = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});
builder.Services.AddAuthorization();

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

        options
            .AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate(); // Dev env only

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableStatusCodePagesIntegration();

        options.DisableAccessTokenEncryption();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    }
);


var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

// Basic userinfo endpoint: returns claims for the authenticated user.
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

app.MapGet("/me", (HttpContext ctx) =>
{
    var user = ctx.User;
    return Results.Json(new
    {
       subject = user.FindFirst("sub")?.Value ?? user.Identity?.Name,
       name = user.FindFirst("name")?.Value,
       email = user.FindFirst("email")?.Value,
       scopes = user.FindAll("scope").Select(c => c.Value).ToArray() 
    });
    
}).RequireAuthorization();

app.Run();
