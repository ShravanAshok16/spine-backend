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
    public ReviewsController(AppDbContext db) => _db = db;

    [HttpGet("book/{bookId:guid}")]
    public async Task<IActionResult> GetBookReviews(Guid bookId)
    {
        var reviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.BookId == bookId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewResponse
            {
                Id = r.Id,
                UserId = r.UserId,
                UserDisplayName = r.User.DisplayName,
                Rating = r.Rating,
                ReviewText = r.ReviewText,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        var average = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;
        return Ok(new { averageRating = average, totalReviews = reviews.Count, reviews });
    }

    [HttpPost("book/{bookId:guid}")]
    public async Task<IActionResult> CreateReview(Guid bookId, [FromBody] CreateReviewRequest request)
    {
        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest(new { message = "Rating must be between 1 and 5." });

        var userId = GetCurrentUserId();
        var existing = await _db.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);

        if (existing != null)
        {
            existing.Rating = request.Rating;
            existing.ReviewText = request.ReviewText?.Trim();
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var review = new Review
            {
                BookId = bookId,
                UserId = userId,
                Rating = request.Rating,
                ReviewText = request.ReviewText?.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.Reviews.Add(review);
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Review saved." });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(sub!);
    }
}