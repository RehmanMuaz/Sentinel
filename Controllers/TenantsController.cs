using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Entities;
using Sentinel.Infrastructure;

namespace Sentinel.Api.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize(Policy = "ManageClients")]
public class TenantsController : ControllerBase
{
    private readonly SentinelDbContext _db;

    public TenantsController(SentinelDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenantResponse>>> GetAll()
    {
        var tenants = await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TenantResponse
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return Ok(tenants);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TenantResponse>> Get(Guid id)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        return Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            CreatedAt = tenant.CreatedAt
        });
    }

    [HttpPost]
    public async Task<ActionResult<TenantResponse>> Create([FromBody] CreateTenantRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var slug = request.Slug.Trim().ToLowerInvariant();
        var exists = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Slug == slug);
        if (exists) return Conflict("Slug already exists.");

        var tenant = Tenant.Create(request.Name, request.Slug);
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var response = new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            CreatedAt = tenant.CreatedAt
        };

        return CreatedAtAction(nameof(Get), new { id = tenant.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TenantResponse>> Update(Guid id, [FromBody] UpdateTenantRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        var slug = request.Slug.Trim().ToLowerInvariant();
        var used = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Slug == slug && t.Id != id);
        if (used) return Conflict("Slug already exists.");

        tenant.Update(request.Name, request.Slug);
        await _db.SaveChangesAsync();

        return Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            CreatedAt = tenant.CreatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        var hasClients = await _db.Clients.AsNoTracking().AnyAsync(c => c.TenantId == id);
        var hasUsers = await _db.Users.AsNoTracking().AnyAsync(u => u.TenantId == id);
        if (hasClients || hasUsers) return Conflict("Tenant has dependent clients/users.");

        _db.Tenants.Remove(tenant);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class CreateTenantRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Slug { get; set; } = string.Empty;
}

public class UpdateTenantRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Slug { get; set; } = string.Empty;
}

public class TenantResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
