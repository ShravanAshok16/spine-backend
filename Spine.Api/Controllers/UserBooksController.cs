using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spine.Api.Models;
using Spine.Api.Services;
using Spine.Core.Entities;
using Spine.Infrastructure.Data;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserBooksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OpenLibraryService _openLibraryService;

    public UserBooksController(AppDbContext db, OpenLibraryService openLibraryService)
    {
        _db = db;
        _openLibraryService = openLibraryService;
    }

    [HttpPost]
    public async Task<IActionResult> AddBookToLibrary([FromBody] AddBookToLibraryRequest request)
    {
        var userId = GetCurrentUserId();
        var isbn = request.Isbn.Trim();

        // 1. Fetch metadata from Open Library
        var bookData = await _openLibraryService.GetBookByIsbnAsync(isbn);
        if (bookData is null)
            return BadRequest(new { message = "Could not find book details for this ISBN." });

        // 2. Check if the book already exists in our Global Catalog
        var existingBook = await _db.Books.FirstOrDefaultAsync(b => b.Isbn == isbn);
        
        if (existingBook is null)
        {
            existingBook = new Book
            {
                Isbn = isbn,
                Title = bookData.Title,
                Author = bookData.Author,
                CoverUrl = bookData.CoverUrl,
                Description = bookData.Description,
                PageCount = bookData.PageCount
            };
            _db.Books.Add(existingBook);
            await _db.SaveChangesAsync(); // Save to generate the BookId
        }

        // 3. Check if the user already has this book
        var alreadyOwned = await _db.UserBooks
            .AnyAsync(ub => ub.UserId == userId && ub.BookId == existingBook.Id);

        if (alreadyOwned)
            return BadRequest(new { message = "You already have this book in your library." });

        // 4. Add the book to the user's library
        var userBook = new UserBook
        {
            UserId = userId,
            BookId = existingBook.Id,
            Status = request.Status,
            AddedAt = DateTime.UtcNow
        };

        _db.UserBooks.Add(userBook);
        await _db.SaveChangesAsync();

        return Ok(new 
        { 
            message = "Book added to library successfully.",
            userBookId = userBook.Id,
            title = existingBook.Title
        });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sub))
            throw new UnauthorizedAccessException("User identity not found in token.");

        return Guid.Parse(sub);
    }
}