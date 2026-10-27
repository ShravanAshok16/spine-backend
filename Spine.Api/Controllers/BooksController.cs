using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spine.Api.Services;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Users must be logged in to search
public class BooksController : ControllerBase
{
    private readonly OpenLibraryService _openLibraryService;

    public BooksController(OpenLibraryService openLibraryService)
    {
        _openLibraryService = openLibraryService;
    }

    [HttpGet("lookup/{isbn}")]
    public async Task<IActionResult> LookupIsbn(string isbn)
    {
        var result = await _openLibraryService.GetBookByIsbnAsync(isbn);
        
        if (result is null)
            return NotFound(new { message = "Book not found on Open Library." });

        return Ok(result);
    }
}