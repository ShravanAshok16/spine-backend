using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spine.Api.Models;
using Spine.Api.Services;
using Spine.Infrastructure.Data;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly BookLookupService _bookLookup;
    private readonly AppDbContext _db;

    public BooksController(BookLookupService bookLookup, AppDbContext db)
    {
        _bookLookup = bookLookup;
        _db = db;
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchBooks([FromQuery] string q, [FromQuery] int limit = 12)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 3) return Ok(new List<BookSearchResultDto>());
        var results = await _bookLookup.SearchAsync(q, limit);
        return Ok(results);
    }

    [HttpGet("details/{identifier}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBookDetails(string identifier)
    {
        var book = await _bookLookup.GetDetailsAsync(identifier);
        if (book == null) return NotFound(new { message = "Book not found" });
        return Ok(book);
    }

    [HttpGet("discover")]
    [AllowAnonymous]
    public async Task<IActionResult> Discover(
        [FromQuery] string? lang = null,
        [FromQuery] string? subject = null,
        [FromQuery] int limit = 12)
    {
        string query;

        if (!string.IsNullOrEmpty(lang) && !string.IsNullOrEmpty(subject))
            query = $"subject:{subject} language:{lang}";
        else if (!string.IsNullOrEmpty(lang))
            query = $"language:{lang}";
        else if (!string.IsNullOrEmpty(subject))
            query = $"subject:{subject}";
        else
            query = "fiction"; // safe default

        var results = await _bookLookup.SearchAsync(query, limit);
        return Ok(results);
    }
}