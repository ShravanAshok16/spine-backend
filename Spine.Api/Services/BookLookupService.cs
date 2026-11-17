using Spine.Api.Models;
using Spine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Spine.Core.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace Spine.Api.Services;

public class BookLookupService
{
    private readonly OpenLibraryService _openLibrary;
    private readonly GoogleBooksService _googleBooks;
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BookLookupService> _logger;

    public BookLookupService(
        OpenLibraryService openLibrary,
        GoogleBooksService googleBooks,
        AppDbContext db,
        IMemoryCache cache,
        ILogger<BookLookupService> logger)
    {
        _openLibrary = openLibrary;
        _googleBooks = googleBooks;
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<BookSearchResultDto>> SearchAsync(string query, int limit = 12)
    {
        if (string.IsNullOrWhiteSpace(query)) return new();

        string cacheKey = $"search_{query.ToLower()}_{limit}";
        if (_cache.TryGetValue(cacheKey, out List<BookSearchResultDto>? cachedResults))
            return cachedResults!;

        var combined = new List<BookSearchResultDto>();

        try
        {
            var gbTask = _googleBooks.SearchAsync(query, limit);
            var olTask = _openLibrary.SearchBooksAsync(query);
            await Task.WhenAll(gbTask, olTask);

            // 🔥 Google Books first, then Open Library
            combined = (await gbTask).Concat(await olTask).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search providers failed for query: {Query}", query);
        }

        var unique = combined
            .GroupBy(b => $"{Normalize(b.Title)}_{Normalize(b.Author)}")
            .Select(g => g.First())
            .Take(limit)
            .ToList();

        if (unique.Count > 0)
            _cache.Set(cacheKey, unique, TimeSpan.FromHours(12));

        return unique;
    }

    public async Task<BookSearchResultDto?> GetDetailsAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        identifier = Uri.UnescapeDataString(identifier).Trim();

        string cacheKey = $"details_{identifier.ToLower()}";
        if (_cache.TryGetValue(cacheKey, out BookSearchResultDto? cachedBook))
            return cachedBook!;

        BookSearchResultDto? localResult = null;
        string searchTitle = "";
        string searchAuthor = "";
        string? isbn = null;
        bool identifierIsGuid = Guid.TryParse(identifier, out var userBookId);

        // 1. Check Local DB (just to get the Title/Author/ISBN for the API search)
        if (identifierIsGuid)
        {
            var userBook = await _db.UserBooks
                .Include(ub => ub.Book)
                .FirstOrDefaultAsync(ub => ub.Id == userBookId);

            if (userBook?.Book != null)
            {
                localResult = MapToDto(userBook.Book);
                searchTitle = userBook.Book.Title;
                searchAuthor = userBook.Book.Author ?? "";
                isbn = userBook.Book.Isbn;
            }
        }
        else
        {
            var lower = identifier.ToLower();
            var localBook = await _db.Books.FirstOrDefaultAsync(b =>
                b.Isbn == identifier || b.Title.ToLower() == lower);

            if (localBook != null)
            {
                localResult = MapToDto(localBook);
                searchTitle = localBook.Title;
                searchAuthor = localBook.Author ?? "";
                isbn = localBook.Isbn;
            }
            else
            {
                searchTitle = identifier;
                if (IsIsbn(identifier)) isbn = CleanIsbn(identifier);
            }
        }

        // 🔥 2. PRIMARY: Fetch from Google Books
        BookSearchResultDto? gbResult = null;
        try
        {
            gbResult = await FindOnGoogleAsync(isbn, searchTitle, searchAuthor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Books failed for {Identifier}", identifier);
        }

        // If Google Books gave us both Description and Pages, we use it and skip Open Library entirely!
        if (IsComplete(gbResult))
        {
            await SaveOrUpdateLocalBook(gbResult!);
            _cache.Set(cacheKey, gbResult!, TimeSpan.FromHours(12));
            return gbResult;
        }

        // 🔥 3. FALLBACK: Fetch from Open Library
        BookSearchResultDto? olResult = null;
        if (!IsComplete(gbResult))
        {
            try
            {
                var olKey = !string.IsNullOrWhiteSpace(isbn) ? isbn! : searchTitle;
                olResult = await _openLibrary.GetBookByIdentifierAsync(olKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Open Library failed for {Identifier}", identifier);
            }
        }

        // 4. Merge everything together (Google > Open Library > Local DB)
        var finalResult = localResult; // Start with local (if it exists)
        
        if (gbResult != null) 
            finalResult = finalResult == null ? gbResult : Merge(gbResult, finalResult); // GB overwrites Local
            
        if (olResult != null) 
            finalResult = finalResult == null ? olResult : Merge(finalResult, olResult); // OL fills remaining gaps

        // 5. Clean and Save
        if (finalResult != null)
        {
            finalResult.Description = TextCleaner.CleanDescription(finalResult.Description);

            if (!string.IsNullOrWhiteSpace(finalResult.Description) || finalResult.PageCount > 0)
                await SaveOrUpdateLocalBook(finalResult);

            var ttl = IsComplete(finalResult) ? TimeSpan.FromHours(12) : TimeSpan.FromSeconds(45);
            _cache.Set(cacheKey, finalResult, ttl);
        }

        return finalResult;
    }

    private async Task<BookSearchResultDto?> FindOnGoogleAsync(string? isbn, string title, string author)
    {
        var queries = new List<string>();

        if (!string.IsNullOrWhiteSpace(isbn))
            queries.Add($"isbn:{isbn}");

        if (!string.IsNullOrWhiteSpace(title))
        {
            queries.Add(string.IsNullOrWhiteSpace(author)
                ? $"intitle:\"{title}\""
                : $"intitle:\"{title}\" inauthor:\"{author}\"");
        }

        foreach (var q in queries)
        {
            var results = await _googleBooks.SearchAsync(q, 5);
            if (results.Count == 0) continue;

            var best = results
                .Where(b => string.IsNullOrWhiteSpace(title)
                    || Normalize(b.Title).Contains(Normalize(title))
                    || Normalize(title).Contains(Normalize(b.Title)))
                .OrderByDescending(b =>
                    (!string.IsNullOrWhiteSpace(b.Description) ? 4 : 0) +
                    (b.PageCount > 0 ? 3 : 0) +
                    (!string.IsNullOrEmpty(b.CoverUrl) ? 1 : 0))
                .FirstOrDefault();

            if (best != null) return best;
        }

        return null;
    }

    private static bool IsComplete(BookSearchResultDto? r) =>
        r != null && !string.IsNullOrWhiteSpace(r.Description) && r.PageCount > 0;

    private static bool IsIsbn(string s)
    {
        var c = CleanIsbn(s);
        return (c.Length == 10 || c.Length == 13) && c.All(char.IsDigit);
    }

    private static string CleanIsbn(string s) => s.Replace("-", "").Replace(" ", "");

    // 🔥 Merge keeps Primary's data, only fills in blanks from Secondary
    private static BookSearchResultDto Merge(BookSearchResultDto primary, BookSearchResultDto secondary)
    {
        if (string.IsNullOrWhiteSpace(primary.Description)) primary.Description = secondary.Description;
        if (!primary.PageCount.HasValue || primary.PageCount == 0) primary.PageCount = secondary.PageCount;
        if (string.IsNullOrWhiteSpace(primary.CoverUrl)) primary.CoverUrl = secondary.CoverUrl; // Prefer Primary's cover
        if (string.IsNullOrWhiteSpace(primary.Genres)) primary.Genres = secondary.Genres;
        if (string.IsNullOrWhiteSpace(primary.Isbn)) primary.Isbn = secondary.Isbn;
        if (string.IsNullOrWhiteSpace(primary.Language)) primary.Language = secondary.Language;
        if (string.IsNullOrWhiteSpace(primary.SeriesName)) primary.SeriesName = secondary.SeriesName;
        if (string.IsNullOrWhiteSpace(primary.Author) || primary.Author == "Unknown Author") primary.Author = secondary.Author;
        return primary;
    }

    private BookSearchResultDto MapToDto(Book b) => new()
    {
        Title = b.Title,
        Author = b.Author,
        Isbn = b.Isbn,
        CoverUrl = b.CoverUrl,
        Description = b.Description,
        PageCount = b.PageCount,
        Language = b.Language,
        Genres = b.Genres
    };

    private async Task SaveOrUpdateLocalBook(BookSearchResultDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return;

        var titleLower = dto.Title.ToLower();
        var existing = await _db.Books.FirstOrDefaultAsync(b =>
            (!string.IsNullOrEmpty(dto.Isbn) && b.Isbn == dto.Isbn) ||
            b.Title.ToLower() == titleLower);

        if (existing == null) return;

        bool changed = false;

        // Only update if the new data is BETTER (e.g., longer description, higher res cover)
        if (!string.IsNullOrEmpty(dto.Description) && (string.IsNullOrEmpty(existing.Description) || dto.Description.Length > existing.Description.Length))
        { existing.Description = dto.Description; changed = true; }

        if (dto.PageCount > 0 && (!existing.PageCount.HasValue || existing.PageCount == 0))
        { existing.PageCount = dto.PageCount; changed = true; }

        // Always overwrite the cover if we got a high-res Google cover
        if (!string.IsNullOrEmpty(dto.CoverUrl) && (string.IsNullOrEmpty(existing.CoverUrl) || dto.CoverUrl.Contains("books.google")))
        { existing.CoverUrl = dto.CoverUrl; changed = true; }

        if (string.IsNullOrEmpty(existing.Genres) && !string.IsNullOrEmpty(dto.Genres))
        { existing.Genres = dto.Genres; changed = true; }

        if (changed) await _db.SaveChangesAsync();
    }

    private static string Normalize(string? value) =>
        Regex.Replace(value?.ToLowerInvariant() ?? "", @"[^a-z0-9]", "");
}