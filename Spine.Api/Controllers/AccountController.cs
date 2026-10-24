using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spine.Api.Events;
using Spine.Api.Models;
using Spine.Api.Services;
using Spine.Core.Entities;
using Spine.Infrastructure.Data;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // <-- This ensures only logged-in users can access these endpoints
public class AccountController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly KafkaProducerService _kafkaProducer;

    public AccountController(AppDbContext db, KafkaProducerService kafkaProducer)
    {
        _db = db;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetCurrentUserId();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.FirstName,
            user.LastName
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user is null) return NotFound();

        user.DisplayName = request.DisplayName.Trim();
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user is null) return NotFound();

        // 1. Verify current password
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Incorrect password." });

        // 2. Check if email is taken
        var emailInUse = await _db.Users.AnyAsync(u => u.Email == request.NewEmail.ToLower() && u.Id != userId);
        if (emailInUse)
            return BadRequest(new { message = "Email is already in use." });

        // 3. Update
        user.Email = request.NewEmail.ToLower().Trim();
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new { message = "New passwords do not match." });

        var userId = GetCurrentUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user is null) return NotFound();

        // 1. Verify current password
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Incorrect password." });

        // 2. Hash and update
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] Models.ForgotPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

        // SECURITY: Always return 200 OK, even if the email doesn't exist. 
        // This prevents attackers from guessing which emails are registered.
        if (user is null)
            return Ok(new { message = "If that email exists, a reset link has been sent." });

        // 1. Generate a secure random token
        var token = Guid.NewGuid().ToString("N");

        // 2. Save the token to the database (expires in 15 mins)
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = token, // In a production app, hash this before saving!
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

        _db.PasswordResetTokens.Add(resetToken);
        await _db.SaveChangesAsync();

        // 3. Publish the event to Kafka
        var resetEvent = new PasswordResetRequestedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Token = token
        };

        await _kafkaProducer.PublishPasswordResetEventAsync(resetEvent);

        return Ok(new { message = "If that email exists, a reset link has been sent." });
    }

    // 🔥 This helper now lives inside the protected AccountController
    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sub))
            throw new UnauthorizedAccessException("User identity not found in token.");

        return Guid.Parse(sub);
    }
}