using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;
using Sentinel.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SentinelDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
    options.UseOpenIddict();
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
