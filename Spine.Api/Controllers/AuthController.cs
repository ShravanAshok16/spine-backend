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
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly KafkaProducerService _kafkaProducer;
    private readonly IConfiguration _config;

    public AuthController(
        AppDbContext db, 
        TokenService tokenService, 
        KafkaProducerService kafkaProducer,
        IConfiguration config)
    {
        _db = db;
        _tokenService = tokenService;
        _kafkaProducer = kafkaProducer;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var expiresMinutes = int.TryParse(_config["Jwt:ExpiresMinutes"], out var m) ? m : 30;

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return BadRequest(new { error = "Email already registered." });

        var displayName = !string.IsNullOrWhiteSpace(request.DisplayName)
            ? request.DisplayName.Trim()
            : $"{request.FirstName} {request.LastName}".Trim();

        // Generate a verification token
        var verifyToken = Guid.NewGuid().ToString("N");

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false,
            EmailVerificationToken = verifyToken
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Publish Event to Kafka (You will need to create this event class)
        var verifyEvent = new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Token = verifyToken
        };
        
        await _kafkaProducer.PublishUserRegisteredEventAsync(verifyEvent);

        // Note: We don't return the JWT yet, because they need to verify first.
        return Ok(new { message = "Registration successful. Please check your email to verify your account." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var identifier = request.Identifier.Trim().ToLowerInvariant();
        var expiresMinutes = int.TryParse(_config["Jwt:ExpiresMinutes"], out var m) ? m : 30;

        // Allow login via Email OR DisplayName
        var user = await _db.Users.FirstOrDefaultAsync(u => 
            u.Email == identifier || 
            u.DisplayName.ToLower() == identifier);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials." });

        // Enforce Email Verification
        if (!user.IsEmailVerified)
            return Unauthorized(new { error = "Please verify your email before logging in." });

        var token = _tokenService.CreateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes)
        });
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "Token is required." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);

        if (user is null)
            return BadRequest(new { error = "Invalid or expired verification token." });

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null; // Clear the token so it can't be reused
        await _db.SaveChangesAsync();

        return Ok(new { message = "Email verified successfully. You can now log in." });
    }
}