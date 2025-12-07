using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Entities;
using Sentinel.Infrastructure;
using Sentinel.Infrastructure.Security;

namespace Sentinel.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "ManageClients")]
public class UsersController : ControllerBase
{
    private readonly SentinelDbContext _db;
    private readonly ISecretHasher _hasher;

    public UsersController(SentinelDbContext db, ISecretHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        var result = users.Select(u => new UserResponse
        {
            Id = u.Id,
            TenantId = u.TenantId,
            Email = u.Email,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt
        });
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var email = request.Email.Trim().ToLowerInvariant();
        var tenantExists = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == request.TenantId);
        if (!tenantExists) return BadRequest("Tenant does not exist.");

        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.TenantId == request.TenantId && u.Email == email);
        if (exists) return Conflict("A user with this email already exists for the tenant.");

        var user = Sentinel.Domain.Entities.User.Create(request.TenantId, email, _hasher.Hash(request.Password));
        if (!request.IsActive) user.Deactivate();

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var response = new UserResponse
        {
            Id = user.Id,
            TenantId = user.TenantId,
            Email = user.Email,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };

        return CreatedAtAction(nameof(GetAll), new { id = user.Id }, response);
    }
}

public class CreateUserRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class UserResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
