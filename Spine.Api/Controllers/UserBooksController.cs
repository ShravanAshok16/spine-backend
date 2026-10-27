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

    [HttpGet]
    public async Task<IActionResult> GetMyLibrary(
    [FromQuery] string? status = null,
    [FromQuery] string? search = null,
    [FromQuery] string sort = "added_desc")
    {
        var userId = GetCurrentUserId();

        var query = _db.UserBooks
            .AsNoTracking()
            .Where(ub => ub.UserId == userId && !ub.IsDeleted)
            .Include(ub => ub.Book)
            .AsQueryable();

        // 1. Filter by Status (e.g., only show "Reading")
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(ub => ub.Status.ToLower() == status.ToLower());
        }

        // 2. Search by Title or Author
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(ub =>
                ub.Book.Title.ToLower().Contains(term) ||
                ub.Book.Author.ToLower().Contains(term));
        }

        // 3. Sorting
        query = sort switch
        {
            "title_asc" => query.OrderBy(ub => ub.Book.Title),
            "title_desc" => query.OrderByDescending(ub => ub.Book.Title),
            "rating_desc" => query.OrderByDescending(ub => ub.PersonalRating),
            "added_asc" => query.OrderBy(ub => ub.AddedAt),
            _ => query.OrderByDescending(ub => ub.AddedAt) // default: added_desc
        };

        var library = await query
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
                PrivateNotes = ub.PrivateNotes,
                LentTo = ub.LentTo,
                AddedAt = ub.AddedAt
            })
            .ToListAsync();

        return Ok(library);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserBook(Guid id)
    {
        var userId = GetCurrentUserId();

        var userBook = await _db.UserBooks
            .AsNoTracking()
            .Include(ub => ub.Book)
            .FirstOrDefaultAsync(ub => ub.Id == id && ub.UserId == userId && !ub.IsDeleted);

        if (userBook is null) return NotFound();

        return Ok(new UserBookResponse
        {
            Id = userBook.Id,
            BookId = userBook.BookId,
            Title = userBook.Book.Title,
            Author = userBook.Book.Author,
            CoverUrl = userBook.Book.CoverUrl,
            Status = userBook.Status,
            CurrentPage = userBook.CurrentPage,
            PersonalRating = userBook.PersonalRating,
            PrivateNotes = userBook.PrivateNotes,
            LentTo = userBook.LentTo,
            AddedAt = userBook.AddedAt
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUserBook(Guid id, [FromBody] UpdateUserBookRequest request)
    {
        var userId = GetCurrentUserId();

        var userBook = await _db.UserBooks
            .FirstOrDefaultAsync(ub => ub.Id == id && ub.UserId == userId && !ub.IsDeleted);

        if (userBook is null) return NotFound();

        // Only update fields that were provided
        userBook.Status = request.Status;
        userBook.CurrentPage = request.CurrentPage;
        userBook.PersonalRating = request.PersonalRating;
        userBook.PrivateNotes = request.PrivateNotes;
        userBook.LentTo = request.LentTo;

        await _db.SaveChangesAsync();

        return NoContent();
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