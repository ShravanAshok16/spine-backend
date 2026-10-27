using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spine.Api.Models;
using Spine.Core.Entities;
using Spine.Infrastructure.Data;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReviewsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("book/{bookId:guid}")]
    public async Task<IActionResult> CreateReview(Guid bookId, [FromBody] CreateReviewRequest request)
    {
        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest(new { message = "Rating must be between 1 and 5." });

        var userId = GetCurrentUserId();

        // Check if the user has already reviewed this book
        var existingReview = await _db.Reviews
            .FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);

        if (existingReview is not null)
            return BadRequest(new { message = "You have already reviewed this book." });

        // Check if the book exists
        var bookExists = await _db.Books.AnyAsync(b => b.Id == bookId);
        if (!bookExists) return NotFound(new { message = "Book not found." });

        var review = new Review
        {
            UserId = userId,
            BookId = bookId,
            Rating = request.Rating,
            ReviewText = request.ReviewText,
            CreatedAt = DateTime.UtcNow
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Review created successfully." });
    }

    [HttpGet("book/{bookId:guid}")]
    [AllowAnonymous] // Anyone can read reviews for a book
    public async Task<IActionResult> GetBookReviews(Guid bookId)
    {
        var reviews = await _db.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.BookId == bookId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.ReviewText,
                r.CreatedAt,
                ReviewerName = r.User.DisplayName
            })
            .ToListAsync();

        var averageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

        return Ok(new
        {
            AverageRating = Math.Round(averageRating, 1),
            TotalReviews = reviews.Count,
            Reviews = reviews
        });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sub)) throw new UnauthorizedAccessException();

        return Guid.Parse(sub);
    }
}