using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spine.Api.Models;
using Spine.Infrastructure.Data;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Ok(new List<object>());

        var currentUserId = GetCurrentUserId();
        var term = q.ToLower();

        var users = await _db.Users
            .Where(u => u.Id != currentUserId && 
                       (u.DisplayName.ToLower().Contains(term) || u.Email.ToLower().Contains(term)))
            .Take(10)
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.PublicUsername })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{username}")]
    public async Task<IActionResult> GetPublicProfile(string username)
    {
        var currentUserId = GetCurrentUserId();
        
        // Find user by PublicUsername OR DisplayName (fallback)
        var user = await _db.Users
            .Include(u => u.UserBooks)
                .ThenInclude(ub => ub.Book)
            .FirstOrDefaultAsync(u => u.PublicUsername == username || u.DisplayName == username);

        if (user == null) return NotFound(new { message = "User not found." });

        // Privacy Check
        if (user.PrivacySetting == "Private" && user.Id != currentUserId)
            return Forbid();

        var isFriend = await _db.Friendships.AnyAsync(f => 
            ((f.RequesterId == currentUserId && f.ReceiverId == user.Id) || 
             (f.RequesterId == user.Id && f.ReceiverId == currentUserId)) && 
            f.Status == "Accepted");

        if (user.PrivacySetting == "FriendsOnly" && !isFriend && user.Id != currentUserId)
            return Forbid();

        // Fetch public books (redacting private notes)
        var books = user.UserBooks
            .Where(ub => !ub.IsDeleted)
            .Select(ub => new {
                ub.Id,
                Title = ub.Book.Title,
                Author = ub.Book.Author,
                CoverUrl = ub.Book.CoverUrl,
                Status = ub.Status,
                Rating = ub.PersonalRating
            }).ToList();

        return Ok(new {
            user.Id,
            user.DisplayName,
            user.Bio,
            user.PublicUsername,
            Stats = new { TotalBooks = books.Count, Finished = books.Count(b => b.Status == "Finished") },
            Books = books
        });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(sub!);
    }
}