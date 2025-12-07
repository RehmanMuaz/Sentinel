using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Entities;
using Sentinel.Infrastructure;

namespace Sentinel.Api.Controllers;

[ApiController]
[Route("api/scopes")]
[Authorize(Policy = "ManageClients")]
public class ScopesController : ControllerBase
{
    private readonly SentinelDbContext _db;

    public ScopesController(SentinelDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ScopeResponse>>> GetAll()
    {
        var scopes = await _db.Scopes
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new ScopeResponse
            {
                Id = s.Id,
                TenantId = s.TenantId,
                Name = s.Name,
                Description = s.Description
            })
            .ToListAsync();

        return Ok(scopes);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScopeResponse>> Get(Guid id)
    {
        var scope = await _db.Scopes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (scope is null) return NotFound();

        return Ok(new ScopeResponse
        {
            Id = scope.Id,
            TenantId = scope.TenantId,
            Name = scope.Name,
            Description = scope.Description
        });
    }

    [HttpPost]
    public async Task<ActionResult<ScopeResponse>> Create([FromBody] CreateScopeRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var name = request.Name.Trim();
        var exists = await _db.Scopes.AsNoTracking()
            .AnyAsync(s => s.Name == name && s.TenantId == request.TenantId);
        if (exists) return Conflict("Scope name already exists for this tenant.");

        if (request.TenantId.HasValue)
        {
            var tenantExists = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == request.TenantId.Value);
            if (!tenantExists) return BadRequest("Tenant does not exist.");
        }

        var scope = Scope.Create(name, request.Description, request.TenantId);
        _db.Scopes.Add(scope);
        await _db.SaveChangesAsync();

        var response = new ScopeResponse
        {
            Id = scope.Id,
            TenantId = scope.TenantId,
            Name = scope.Name,
            Description = scope.Description
        };

        return CreatedAtAction(nameof(Get), new { id = scope.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ScopeResponse>> Update(Guid id, [FromBody] UpdateScopeRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var scope = await _db.Scopes.FirstOrDefaultAsync(s => s.Id == id);
        if (scope is null) return NotFound();

        var name = request.Name.Trim();
        var used = await _db.Scopes.AsNoTracking()
            .AnyAsync(s => s.Id != id && s.Name == name && s.TenantId == request.TenantId);
        if (used) return Conflict("Scope name already exists for this tenant.");

        if (request.TenantId.HasValue)
        {
            var tenantExists = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == request.TenantId.Value);
            if (!tenantExists) return BadRequest("Tenant does not exist.");
        }

        scope.Update(name, request.Description, request.TenantId);
        await _db.SaveChangesAsync();

        return Ok(new ScopeResponse
        {
            Id = scope.Id,
            TenantId = scope.TenantId,
            Name = scope.Name,
            Description = scope.Description
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var scope = await _db.Scopes.FirstOrDefaultAsync(s => s.Id == id);
        if (scope is null) return NotFound();

        var clientScopes = await _db.Clients.AsNoTracking()
            .Select(c => new { c.Id, c.AllowedScopes, c.TenantId })
            .ToListAsync();

        var inUse = clientScopes.Any(c => c.AllowedScopes.Any(s => string.Equals(s, scope.Name, StringComparison.OrdinalIgnoreCase))
                                          && (scope.TenantId == null || c.TenantId == scope.TenantId));
        if (inUse) return Conflict("Scope is used by one or more clients.");

        _db.Scopes.Remove(scope);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class CreateScopeRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? TenantId { get; set; }
}

public class UpdateScopeRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? TenantId { get; set; }
}

public class ScopeResponse
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
