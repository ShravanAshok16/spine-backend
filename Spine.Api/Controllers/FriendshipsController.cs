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
public class FriendshipsController : ControllerBase
{
    private readonly AppDbContext _db;

    public FriendshipsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("request/{targetUserId:guid}")]
    public async Task<IActionResult> SendFriendRequest(Guid targetUserId)
    {
        var userId = GetCurrentUserId();

        if (userId == targetUserId)
            return BadRequest(new { message = "You cannot add yourself." });

        // Check if target user exists
        var targetExists = await _db.Users.AnyAsync(u => u.Id == targetUserId);
        if (!targetExists) return NotFound(new { message = "User not found." });

        // Check for existing friendship or pending request in either direction
        var existing = await _db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.ReceiverId == targetUserId) ||
            (f.RequesterId == targetUserId && f.ReceiverId == userId));

        if (existing is not null)
        {
            if (existing.Status == "Accepted") return BadRequest(new { message = "You are already friends." });
            if (existing.Status == "Pending") return BadRequest(new { message = "Friend request already pending." });
            if (existing.Status == "Blocked") return BadRequest(new { message = "Unable to send request." });
        }

        var friendship = new Friendship
        {
            RequesterId = userId,
            ReceiverId = targetUserId,
            Status = "Pending"
        };

        _db.Friendships.Add(friendship);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Friend request sent." });
    }

    [HttpPut("{friendshipId:guid}/accept")]
    public async Task<IActionResult> AcceptFriendRequest(Guid friendshipId)
    {
        var userId = GetCurrentUserId();

        var friendship = await _db.Friendships.FindAsync(friendshipId);

        if (friendship is null || friendship.ReceiverId != userId || friendship.Status != "Pending")
            return NotFound(new { message = "Pending request not found." });

        friendship.Status = "Accepted";
        await _db.SaveChangesAsync();

        return Ok(new { message = "Friend request accepted." });
    }

    [HttpGet]
    public async Task<IActionResult> GetFriends()
    {
        var userId = GetCurrentUserId();

        var friends = await _db.Friendships
            .AsNoTracking()
            .Where(f => (f.RequesterId == userId || f.ReceiverId == userId) && f.Status == "Accepted")
            .Select(f => new
            {
                FriendshipId = f.Id,
                FriendId = f.RequesterId == userId ? f.ReceiverId : f.RequesterId,
                // Include the friend's details
                DisplayName = f.RequesterId == userId ? f.Receiver.DisplayName : f.Requester.DisplayName,
                PrivacySetting = f.RequesterId == userId ? f.Receiver.PrivacySetting : f.Requester.PrivacySetting
            })
            .ToListAsync();

        return Ok(friends);
    }

    [HttpGet("{friendId:guid}/library")]
    public async Task<IActionResult> GetFriendLibrary(Guid friendId)
    {
        var userId = GetCurrentUserId();

        // 1. Verify they are actually friends
        var isFriend = await _db.Friendships.AnyAsync(f =>
            f.Status == "Accepted" &&
            ((f.RequesterId == userId && f.ReceiverId == friendId) ||
             (f.ReceiverId == userId && f.RequesterId == friendId)));

        if (!isFriend)
            return Forbid("You are not friends with this user.");

        // 2. Fetch the friend and check privacy
        var friend = await _db.Users.FindAsync(friendId);
        if (friend is null) return NotFound();

        if (friend.PrivacySetting == "Private")
            return Forbid("This user's library is private.");

        // 3. Fetch their library (hiding private notes!)
        var books = await _db.UserBooks
            .AsNoTracking()
            .Where(ub => ub.UserId == friendId && !ub.IsDeleted)
            .Include(ub => ub.Book)
            .Select(ub => new UserBookResponse
            {
                Id = ub.Id,
                BookId = ub.BookId,
                Title = ub.Book.Title,
                Author = ub.Book.Author,
                CoverUrl = ub.Book.CoverUrl,
                Status = ub.Status,
                CurrentPage = ub.CurrentPage,
                PersonalRating = ub.PersonalRating,
                PrivateNotes = null, // <-- IMPORTANT: Hide notes from friends
                LentTo = ub.LentTo,
                AddedAt = ub.AddedAt
            })
            .ToListAsync();

        return Ok(new FriendLibraryResponse
        {
            UserId = friend.Id,
            DisplayName = friend.DisplayName,
            Books = books
        });
    }

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed()
    {
        var userId = GetCurrentUserId();

        var feed = await _db.Activities
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Book) // Assuming you add a Navigation Property for Book in Activity
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new
            {
                a.Id,
                Action = a.ActionType,
                BookTitle = a.Book != null ? a.Book.Title : "Unknown",
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(feed);
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub)) throw new UnauthorizedAccessException();
        return Guid.Parse(sub);
    }
}