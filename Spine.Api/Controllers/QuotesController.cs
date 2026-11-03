using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spine.Infrastructure.Data;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly AppDbContext _db;
    public QuotesController(AppDbContext db) => _db = db;

    [HttpGet("random")]
    public async Task<IActionResult> GetRandomQuote()
    {
        // 🔥 Safely extract the user ID (checking both standard claim types)
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
               
        if (string.IsNullOrEmpty(sub)) return Unauthorized();
        var userId = Guid.Parse(sub);
        
        var quote = await _db.Quotes
            .Include(q => q.Book)
            .Where(q => q.UserId == userId)
            .OrderBy(r => Guid.NewGuid()) // Random
            .FirstOrDefaultAsync();

        if (quote == null) return NoContent();

        return Ok(new { 
            Text = quote.Text, 
            PageNumber = quote.PageNumber, 
            BookTitle = quote.Book.Title, 
            Author = quote.Book.Author 
        });
    }
}