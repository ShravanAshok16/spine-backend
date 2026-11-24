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

        [HttpGet("wrapped")]
    public async Task<IActionResult> GetYearlyWrapped()
    {
        var userId = GetCurrentUserId();
        var currentYear = DateTime.UtcNow.Year;

        var books = await _db.UserBooks
            .Include(ub => ub.Book)
            .Where(ub => ub.UserId == userId && !ub.IsDeleted && ub.Status == "Finished")
            .ToListAsync();

        var totalBooks = books.Count;
        var totalPages = books.Sum(b => b.Book.PageCount ?? 0);

        // Calculate Top Genre & Author
        var genres = books
            .Where(b => !string.IsNullOrEmpty(b.Book.Genres))
            .SelectMany(b => b.Book.Genres!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(g => g.Trim())
            .GroupBy(g => g)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "Unknown";

        var topAuthor = books
            .Where(b => !string.IsNullOrEmpty(b.Book.Author))
            .GroupBy(b => b.Book.Author)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "Unknown";

        var longestBook = books
            .OrderByDescending(b => b.Book.PageCount)
            .Select(b => new { b.Book.Title, b.Book.PageCount })
            .FirstOrDefault();

        return Ok(new
        {
            Year = currentYear,
            TotalBooks = totalBooks,
            TotalPages = totalPages,
            TopGenre = genres,
            TopAuthor = topAuthor,
            LongestBook = longestBook
        });
    }

        [HttpPut("goal")]
    public async Task<IActionResult> UpdateReadingGoal([FromBody] UpdateGoalRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.YearlyGoal = request.Goal;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("goal")]
    public async Task<IActionResult> GetReadingGoal()
    {
        var userId = GetCurrentUserId();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        return Ok(new { goal = user?.YearlyGoal ?? 24 });
    }

    public class UpdateGoalRequest { public int Goal { get; set; } }
}