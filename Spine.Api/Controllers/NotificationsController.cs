using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spine.Infrastructure.Data;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = GetCurrentUserId();

        // Fetch pending friend requests as notifications
        var requests = await _db.Friendships
            .Where(f => f.ReceiverId == userId && f.Status == "Pending")
            .Include(f => f.Requester)
            .Select(f => new {
                Id = f.Id.ToString(),
                Type = "FriendRequest",
                Title = "New Friend Request",
                Body = $"{f.Requester.DisplayName} sent you a friend request.",
                Time = f.CreatedAt,
                Unread = true
            })
            .ToListAsync();

        return Ok(requests);
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(sub!);
    }
}