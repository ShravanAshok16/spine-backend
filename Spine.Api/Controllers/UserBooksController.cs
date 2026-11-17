using Microsoft.AspNetCore.Authorization;
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
[Authorize]
public class UserBooksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OpenLibraryService _openLibraryService;
    private readonly KafkaProducerService _kafkaProducer;

    public UserBooksController(
        AppDbContext db,
        OpenLibraryService openLibraryService,
        KafkaProducerService kafkaProducer)
    {
        _db = db;
        _openLibraryService = openLibraryService;
        _kafkaProducer = kafkaProducer;
    }

    [HttpPost]
    public async Task<IActionResult> AddBookToLibrary([FromBody] AddBookToLibraryRequest request)
    {
        var userId = GetCurrentUserId();
        var isbn = request.Isbn.Trim();

        var bookData = await _openLibraryService.GetBookByIsbnAsync(isbn);
        if (bookData is null)
            return BadRequest(new { message = "Could not find book details for this ISBN." });

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
                PageCount = bookData.PageCount,
                Language = bookData.Language,
                Genres = bookData.Genres
            };
            _db.Books.Add(existingBook);
            await _db.SaveChangesAsync();
        }

        var alreadyOwned = await _db.UserBooks
            .AnyAsync(ub => ub.UserId == userId && ub.BookId == existingBook.Id);

        if (alreadyOwned)
            return BadRequest(new { message = "You already have this book in your library." });

        var userBook = new UserBook
        {
            UserId = userId,
            BookId = existingBook.Id,
            Status = request.Status,
            AddedAt = DateTime.UtcNow
        };

        _db.UserBooks.Add(userBook);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Book added to library successfully.", userBookId = userBook.Id, title = existingBook.Title });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyLibrary(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? language = null,
        [FromQuery] string? genre = null,
        [FromQuery] string? tag = null,
        [FromQuery] string sort = "added_desc")
    {
        var userId = GetCurrentUserId();

        var query = _db.UserBooks
            .AsNoTracking()
            .Where(ub => ub.UserId == userId && !ub.IsDeleted)
            .Include(ub => ub.Book)
            .Include(ub => ub.Book.Series)
            .AsQueryable();

        // 1. Filters
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(ub => ub.Status.ToLower() == status.ToLower());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(ub =>
                ub.Book.Title.ToLower().Contains(term) ||
                ub.Book.Author.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(language))
            query = query.Where(ub => ub.Book.Language == language);

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(ub => ub.Book.Genres != null && ub.Book.Genres.ToLower().Contains(genre.ToLower()));

        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(ub => ub.Tags.Any(ubt => ubt.Tag.Name.ToLower() == tag.ToLower()));

        // 2. Sorting
        query = sort switch
        {
            "title_asc" => query.OrderBy(ub => ub.Book.Title),
            "title_desc" => query.OrderByDescending(ub => ub.Book.Title),
            "rating_desc" => query.OrderByDescending(ub => ub.PersonalRating),
            "added_asc" => query.OrderBy(ub => ub.AddedAt),
            _ => query.OrderByDescending(ub => ub.AddedAt)
        };

        // 3. Projection
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
                TotalPages = ub.Book.PageCount, // <-- Added
                Language = ub.Book.Language,    // <-- Added
                Genres = ub.Book.Genres,        // <-- Added
                SeriesId = ub.Book.SeriesId,
                SeriesName = ub.Book.Series != null ? ub.Book.Series.Name : null,
                PersonalRating = ub.PersonalRating,
                PrivateNotes = ub.PrivateNotes,
                LentTo = ub.LentTo,
                AddedAt = ub.AddedAt,
                IsFavourite = ub.IsFavourite,
                Ownership = ub.Ownership.ToString(),
                Isbn = ub.Book.Isbn,
                Description = ub.Book.Description,
                PageCount = ub.Book.PageCount
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
            .Include(ub => ub.Book.Series)
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
            TotalPages = userBook.Book.PageCount, 
            Language = userBook.Book.Language,    
            Genres = userBook.Book.Genres,        
            SeriesId = userBook.Book.SeriesId,
            SeriesName = userBook.Book.Series != null ? userBook.Book.Series.Name : null,
            PersonalRating = userBook.PersonalRating,
            PrivateNotes = userBook.PrivateNotes,
            LentTo = userBook.LentTo,
            AddedAt = userBook.AddedAt,
            IsFavourite = userBook.IsFavourite,
            Isbn = userBook.Book.Isbn,
            Ownership = userBook.Ownership.ToString(),
            
            // 🔥 ADD THESE LINES:
            Description = userBook.Book.Description,
            PageCount = userBook.Book.PageCount,
            PublishYear = userBook.Book.PublishYear // If you have this field
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUserBook(Guid id, [FromBody] UpdateUserBookRequest request)
    {
        var userId = GetCurrentUserId();

        // 🔥 FIX: Added .Include(ub => ub.Book)
        var userBook = await _db.UserBooks
            .Include(ub => ub.Book)
            .FirstOrDefaultAsync(ub => ub.Id == id && ub.UserId == userId && !ub.IsDeleted);

        if (userBook is null) return NotFound();

        var previousStatus = userBook.Status;

        userBook.Status = request.Status;
        userBook.PersonalRating = request.PersonalRating;
        userBook.PrivateNotes = request.PrivateNotes;
        userBook.LentTo = request.LentTo;

        // Auto-complete page count if finished
        if (request.Status == "Finished" && userBook.Book.PageCount.HasValue)
        {
            userBook.CurrentPage = userBook.Book.PageCount.Value;
        }
        else
        {
            userBook.CurrentPage = request.CurrentPage;
        }

        await _db.SaveChangesAsync();

        if (previousStatus != "Finished" && userBook.Status == "Finished")
        {
            var user = await _db.Users.FindAsync(userId);
            var book = userBook.Book;

            if (user != null && book != null)
            {
                var finishedEvent = new BookFinishedEvent
                {
                    UserId = user.Id,
                    UserDisplayName = user.DisplayName,
                    BookId = book.Id,
                    BookTitle = book.Title,
                    Rating = userBook.PersonalRating
                };

                await _kafkaProducer.PublishBookFinishedEventAsync(finishedEvent);
            }
        }

        return NoContent();
    }

    [HttpPost("manual")]
    public async Task<IActionResult> AddBookManually([FromBody] AddBookManuallyRequest request)
    {
        var userId = GetCurrentUserId();

        // 1. Check for duplicates
        var existingBook = await _db.Books.FirstOrDefaultAsync(b =>
            (!string.IsNullOrEmpty(request.Isbn) && b.Isbn == request.Isbn) ||
            (b.Title.ToLower() == request.Title.ToLower() && b.Author.ToLower() == request.Author.ToLower()));

        Book book;

        if (existingBook != null)
        {
            var alreadyOwned = await _db.UserBooks.AnyAsync(ub => ub.UserId == userId && ub.BookId == existingBook.Id);
            if (alreadyOwned) return BadRequest(new { message = "You already have this book." });
            book = existingBook;
        }
        else
        {
            // 2. Handle Series Creation
            Guid? seriesId = null;
            if (!string.IsNullOrEmpty(request.SeriesName))
            {
                var existingSeries = await _db.Series.FirstOrDefaultAsync(s => s.Name.ToLower() == request.SeriesName.ToLower());
                if (existingSeries != null)
                {
                    seriesId = existingSeries.Id;
                }
                else
                {
                    var newSeries = new Series { Name = request.SeriesName };
                    _db.Series.Add(newSeries);
                    await _db.SaveChangesAsync();
                    seriesId = newSeries.Id;
                }
            }

            // 3. Create Book
            book = new Book
            {
                Title = request.Title,
                Author = request.Author,
                Language = request.Language,
                Isbn = request.Isbn,
                Description = request.Description,
                PageCount = request.PageCount,
                Genres = request.Genres,
                CoverUrl = request.CoverUrl,
                SeriesId = seriesId,                     // <-- Save Series
                SeriesPosition = request.SeriesPosition  // <-- Save Position
            };
            _db.Books.Add(book);
            await _db.SaveChangesAsync();
        }

        var userBook = new UserBook
        {
            UserId = userId,
            BookId = book.Id,
            Status = "Unread",
            AddedAt = DateTime.UtcNow
        };

        _db.UserBooks.Add(userBook);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Book added successfully." });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sub))
            throw new UnauthorizedAccessException("User identity not found in token.");

        return Guid.Parse(sub);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUserBook(Guid id)
    {
        var userId = GetCurrentUserId();

        var userBook = await _db.UserBooks
            .FirstOrDefaultAsync(ub => ub.Id == id && ub.UserId == userId && !ub.IsDeleted);

        if (userBook is null) return NotFound();

        // Soft delete
        userBook.IsDeleted = true;
        userBook.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // 1. DTOs for the new endpoints
    public class BatchDeleteRequest { public List<Guid> UserBookIds { get; set; } = new(); }
    public class UpdateProgressRequest { public int CurrentPage { get; set; } }

    // 3. New Endpoints:

    [HttpPost("batch-delete")]
    public async Task<IActionResult> BatchDelete([FromBody] BatchDeleteRequest request)
    {
        var userId = GetCurrentUserId();
        var books = await _db.UserBooks
            .Where(ub => request.UserBookIds.Contains(ub.Id) && ub.UserId == userId)
            .ToListAsync();

        foreach (var book in books)
        {
            book.IsDeleted = true;
            book.DeletedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return Ok(new { message = $"{books.Count} books moved to trash." });
    }

    [HttpPatch("{id:guid}/favourite")]
    public async Task<IActionResult> ToggleFavourite(Guid id)
    {
        var userId = GetCurrentUserId();
        var book = await _db.UserBooks.FirstOrDefaultAsync(ub => ub.Id == id && ub.UserId == userId);
        if (book == null) return NotFound();

        book.IsFavourite = !book.IsFavourite;
        await _db.SaveChangesAsync();
        return Ok(new { isFavourite = book.IsFavourite });
    }

    [HttpPatch("{id:guid}/progress")]
    public async Task<IActionResult> UpdateProgress(Guid id, [FromBody] UpdateProgressRequest request)
    {
        var userId = GetCurrentUserId();
        var book = await _db.UserBooks
            .Include(ub => ub.Book)
            .FirstOrDefaultAsync(ub => ub.Id == id && ub.UserId == userId);

        if (book == null) return NotFound();

        book.CurrentPage = request.CurrentPage;

        // Auto-finish logic
        if (book.Book.PageCount.HasValue && book.CurrentPage >= book.Book.PageCount.Value)
        {
            book.CurrentPage = book.Book.PageCount.Value;
            book.Status = "Finished";
        }

        await _db.SaveChangesAsync();
        return Ok(new { currentPage = book.CurrentPage, status = book.Status });
    }

    // Add this DTO at the bottom of the class (next to the other DTOs):
    public class LinkSeriesRequest
    {
        public string SeriesName { get; set; } = string.Empty;
        public List<Guid> BookIds { get; set; } = new();
    }

    [HttpPost("link-series")]
    public async Task<IActionResult> LinkSeries([FromBody] LinkSeriesRequest request)
    {
        var userId = GetCurrentUserId();

        // 1. Find or create the Series
        var series = await _db.Series.FirstOrDefaultAsync(s => s.Name.ToLower() == request.SeriesName.ToLower());
        if (series == null)
        {
            series = new Series { Name = request.SeriesName };
            _db.Series.Add(series);
            await _db.SaveChangesAsync();
        }

        // 2. Get the books belonging to the user
        var userBooks = await _db.UserBooks
            .Include(ub => ub.Book)
            .Where(ub => request.BookIds.Contains(ub.BookId) && ub.UserId == userId)
            .ToListAsync();

        // 3. Link them to the Series
        foreach (var ub in userBooks)
        {
            ub.Book.SeriesId = series.Id;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = $"Linked {userBooks.Count} books to {series.Name}." });
    }

    public class LogSessionRequest { public int PagesRead { get; set; } public string? Mood { get; set; } }

    [HttpPost("{id:guid}/sessions")]
    public async Task<IActionResult> LogSession(Guid id, [FromBody] LogSessionRequest request)
    {
        var userId = GetCurrentUserId();
        var book = await _db.UserBooks.Include(ub => ub.Book).FirstOrDefaultAsync(ub => ub.Id == id && ub.UserId == userId);
        if (book == null) return NotFound();

        var session = new ReadingSession
        {
            UserBookId = book.Id,
            PagesRead = request.PagesRead,
            Mood = request.Mood,
            EndedAt = DateTime.UtcNow
        };

        // Update total pages progress
        book.CurrentPage = Math.Min(book.CurrentPage + request.PagesRead, book.Book.PageCount ?? 9999);
        if (book.CurrentPage >= (book.Book.PageCount ?? 9999)) book.Status = "Finished";

        _db.ReadingSessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(new { currentPage = book.CurrentPage, status = book.Status });
    }
}