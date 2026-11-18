using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Spine.Api.Models;
using Spine.Infrastructure.Data;

namespace Spine.Api.Services;

public class OpenLibraryService
{
    private readonly HttpClient _httpClient;

    public OpenLibraryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://openlibrary.org/");

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SpineApp/1.0");
    }

    public async Task<BookSearchResultDto?> GetBookByIdentifierAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;
        identifier = identifier.Trim();

        var cleanIsbn = identifier.Replace("-", "").Replace(" ", "");
        if ((cleanIsbn.Length == 10 || cleanIsbn.Length == 13) && cleanIsbn.All(char.IsDigit))
            return await GetBookByIsbnAsync(cleanIsbn);

        if (identifier.StartsWith("OL", StringComparison.OrdinalIgnoreCase) && identifier.EndsWith("M"))
            return await GetByOpenLibraryEditionAsync(identifier);

        if (identifier.StartsWith("OL", StringComparison.OrdinalIgnoreCase) && identifier.EndsWith("W"))
            return await GetByOpenLibraryWorkAsync(identifier);

        var results = await SearchBooksAsync(identifier);
        var match = results.FirstOrDefault();

        if (match != null && !string.IsNullOrEmpty(match.Isbn))
        {
            var detailed = await GetBookByIsbnAsync(match.Isbn);
            if (detailed != null) return detailed;
        }
        return match;
    }

    public async Task<BookSearchResultDto?> GetBookByIsbnAsync(string isbn)
    {
        var cacheKey = $"ol:isbn:{isbn}";
        var cachedCover = CoverHelper.GetCached(cacheKey);
        var result = new BookSearchResultDto { Isbn = isbn };

        try
        {
            var url = $"https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var key = $"ISBN:{isbn}";

                if (doc.RootElement.TryGetProperty(key, out var data))
                {
                    result.Title = data.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown Title" : "Unknown Title";

                    if (data.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
                        result.Author = authors[0].TryGetProperty("name", out var n) ? n.GetString() ?? "Unknown Author" : "Unknown Author";

                    if (data.TryGetProperty("number_of_pages", out var pages) && pages.ValueKind == JsonValueKind.Number)
                        result.PageCount = pages.GetInt32();

                    // 🔥 FIX: Reject -1 placeholders
                    string? coverFromData = null;
                    if (data.TryGetProperty("cover", out var cover))
                    {
                        if (cover.TryGetProperty("large", out var large)) coverFromData = large.GetString();
                        else if (cover.TryGetProperty("medium", out var medium)) coverFromData = medium.GetString();
                    }
                    if (CoverHelper.IsPlaceholder(coverFromData)) coverFromData = null;
                    result.CoverUrl = coverFromData;

                    if (data.TryGetProperty("subjects", out var subjects) && subjects.GetArrayLength() > 0)
                    {
                        var genres = subjects.EnumerateArray().Take(4).Select(s => s.TryGetProperty("name", out var name) ? name.GetString() : null).Where(s => !string.IsNullOrEmpty(s));
                        result.Genres = string.Join(", ", genres!);
                    }
                }
            }
        }
        catch { }

        await EnrichFromWorkByIsbnAsync(isbn, result);

        // Always use the reliable ISBN fallback if cover is still missing
        if (CoverHelper.IsPlaceholder(result.CoverUrl))
        {
            result.CoverUrl = CoverHelper.BuildOpenLibraryIsbnCover(isbn, preferDefaultPlaceholder: false);
        }

        if (!CoverHelper.IsPlaceholder(result.CoverUrl)) CoverHelper.SetCached(cacheKey, result.CoverUrl);
        else if (cachedCover != null) result.CoverUrl = cachedCover;

        DetectLanguage(result);
        return result.Title == "Unknown Title" ? null : result;
    }

    private async Task<BookSearchResultDto?> GetByOpenLibraryEditionAsync(string olid)
    {
        try
        {
            var response = await _httpClient.GetAsync($"books/{olid}.json");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new BookSearchResultDto
            {
                Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown Title" : "Unknown Title",
                PageCount = root.TryGetProperty("number_of_pages", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null
            };

            if (root.TryGetProperty("isbn_13", out var isbn13) && isbn13.GetArrayLength() > 0) result.Isbn = isbn13[0].GetString();
            else if (root.TryGetProperty("isbn_10", out var isbn10) && isbn10.GetArrayLength() > 0) result.Isbn = isbn10[0].GetString();

            if (root.TryGetProperty("covers", out var covers) && covers.GetArrayLength() > 0)
            {
                var coverId = covers[0].GetInt32();
                if (coverId > 0) result.CoverUrl = $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg?default=false";
            }

            if (root.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
            {
                var authorKey = authors[0].GetProperty("key").GetString();
                if (!string.IsNullOrEmpty(authorKey))
                {
                    var authorRes = await _httpClient.GetAsync($"{authorKey}.json");
                    if (authorRes.IsSuccessStatusCode)
                    {
                        var authorJson = await authorRes.Content.ReadAsStringAsync();
                        using var authorDoc = JsonDocument.Parse(authorJson);
                        if (authorDoc.RootElement.TryGetProperty("name", out var name)) result.Author = name.GetString() ?? "Unknown Author";
                    }
                }
            }

            if (root.TryGetProperty("works", out var works) && works.GetArrayLength() > 0)
            {
                var workKey = works[0].GetProperty("key").GetString();
                if (!string.IsNullOrEmpty(workKey)) await EnrichFromWorkKeyAsync(workKey, result);
            }

            if (!string.IsNullOrEmpty(result.Isbn) && CoverHelper.IsPlaceholder(result.CoverUrl))
                result.CoverUrl = CoverHelper.BuildOpenLibraryIsbnCover(result.Isbn);

            DetectLanguage(result);
            return result;
        }
        catch { return null; }
    }

    private async Task<BookSearchResultDto?> GetByOpenLibraryWorkAsync(string workId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"works/{workId}.json");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new BookSearchResultDto { Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown Title" : "Unknown Title" };

            if (root.TryGetProperty("description", out var descProp))
            {
                if (descProp.ValueKind == JsonValueKind.String) result.Description = descProp.GetString();
                else if (descProp.TryGetProperty("value", out var val)) result.Description = val.GetString();
            }

            if (root.TryGetProperty("subjects", out var subjects) && subjects.GetArrayLength() > 0)
            {
                var genres = subjects.EnumerateArray().Take(4).Select(s => s.GetString()).Where(s => !string.IsNullOrEmpty(s));
                result.Genres = string.Join(", ", genres!);
            }

            if (root.TryGetProperty("covers", out var covers) && covers.GetArrayLength() > 0)
            {
                var coverId = covers[0].GetInt32();
                if (coverId > 0) result.CoverUrl = $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg?default=false";
            }

            if (root.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
            {
                var authorKey = authors[0].GetProperty("author").GetProperty("key").GetString();
                if (!string.IsNullOrEmpty(authorKey))
                {
                    var authorRes = await _httpClient.GetAsync($"{authorKey}.json");
                    if (authorRes.IsSuccessStatusCode)
                    {
                        var authorJson = await authorRes.Content.ReadAsStringAsync();
                        using var authorDoc = JsonDocument.Parse(authorJson);
                        if (authorDoc.RootElement.TryGetProperty("name", out var name)) result.Author = name.GetString() ?? "Unknown Author";
                    }
                }
            }

            DetectLanguage(result);
            return result;
        }
        catch { return null; }
    }

    private async Task EnrichFromWorkByIsbnAsync(string isbn, BookSearchResultDto result)
    {
        try
        {
            var response = await _httpClient.GetAsync($"isbn/{isbn}.json");
            if (!response.IsSuccessStatusCode) return;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("works", out var works) || works.GetArrayLength() == 0) return;
            var workKey = works[0].GetProperty("key").GetString();
            if (!string.IsNullOrEmpty(workKey)) await EnrichFromWorkKeyAsync(workKey, result);
        }
        catch { }
    }

    private async Task EnrichFromWorkKeyAsync(string workKey, BookSearchResultDto result)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{workKey}.json");
            if (!response.IsSuccessStatusCode) return;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (string.IsNullOrEmpty(result.Description) && root.TryGetProperty("description", out var descProp))
            {
                if (descProp.ValueKind == JsonValueKind.String) result.Description = descProp.GetString();
                else if (descProp.TryGetProperty("value", out var val)) result.Description = val.GetString();
            }

            if (string.IsNullOrEmpty(result.Genres) && root.TryGetProperty("subjects", out var subjects) && subjects.GetArrayLength() > 0)
            {
                var genres = subjects.EnumerateArray().Take(4).Select(s => s.GetString()).Where(s => !string.IsNullOrEmpty(s));
                result.Genres = string.Join(", ", genres!);
            }
        }
        catch { }
    }

    public async Task<List<BookSearchResultDto>> SearchBooksAsync(string query)
    {
        var results = new List<BookSearchResultDto>();
        try
        {
            var url = $"search.json?q={Uri.EscapeDataString(query)}&limit=12";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return results;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("docs", out var docs)) return results;

            foreach (var item in docs.EnumerateArray())
            {
                string? isbn = item.TryGetProperty("isbn", out var isbns) && isbns.GetArrayLength() > 0 ? isbns[0].GetString() : null;

                string? coverUrl = null;
                if (item.TryGetProperty("cover_i", out var coverId))
                {
                    var id = coverId.GetInt32();
                    if (id > 0) coverUrl = $"https://covers.openlibrary.org/b/id/{id}-L.jpg?default=false";
                }

                if (!string.IsNullOrEmpty(isbn) && CoverHelper.IsPlaceholder(coverUrl))
                    coverUrl = CoverHelper.BuildOpenLibraryIsbnCover(isbn);

                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown" : "Unknown";
                var author = item.TryGetProperty("author_name", out var a) && a.GetArrayLength() > 0 ? a[0].GetString() ?? "Unknown Author" : "Unknown Author";

                var result = new BookSearchResultDto { Isbn = isbn, Title = title, Author = author, CoverUrl = coverUrl, Language = "en" };

                if (item.TryGetProperty("series", out var seriesArray) && seriesArray.GetArrayLength() > 0)
                    result.SeriesName = seriesArray[0].GetString()?.Replace("[", "").Replace("]", "").Trim();

                DetectLanguage(result);
                results.Add(result);
            }
        }
        catch { }
        return results;
    }

    public async Task<List<BookSearchResultDto>> DiscoverBooksAsync(string subject)
    {
        var results = new List<BookSearchResultDto>();
        try
        {
            var url = $"subjects/{Uri.EscapeDataString(subject.ToLower())}.json?limit=20";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return results;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("works", out var works)) return results;

            foreach (var item in works.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? t.GetString() : "Unknown";
                var author = "Unknown Author";
                string? coverUrl = null;

                if (item.TryGetProperty("cover_id", out var coverId))
                {
                    var id = coverId.GetInt32();
                    if (id > 0) coverUrl = $"https://covers.openlibrary.org/b/id/{id}-L.jpg?default=false";
                }

                if (item.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
                    author = authors[0].TryGetProperty("name", out var a) ? a.GetString() ?? "Unknown Author" : "Unknown Author";

                if (!string.IsNullOrEmpty(title) && title != "Unknown")
                {
                    var result = new BookSearchResultDto { Title = title!, Author = author, CoverUrl = coverUrl, Language = "en", Genres = subject };
                    DetectLanguage(result);
                    results.Add(result);
                }
            }
        }
        catch { }
        return results;
    }

    public async Task<List<BookSearchResultDto>> GetRecommendationsForUserAsync(AppDbContext db, Guid userId)
    {
        var topGenres = await db.UserBooks.Where(ub => ub.UserId == userId && ub.Book.Genres != null).Select(ub => ub.Book.Genres).ToListAsync();
        var genreList = topGenres.SelectMany(g => g!.Split(',', StringSplitOptions.RemoveEmptyEntries)).Select(g => g.Trim().ToLower()).GroupBy(g => g).OrderByDescending(g => g.Count()).Take(2).Select(g => g.Key).ToList();

        if (!genreList.Any()) return new();
        var recommendations = new List<BookSearchResultDto>();
        foreach (var genre in genreList) recommendations.AddRange(await DiscoverBooksAsync(genre));

        var ownedIsbns = await db.UserBooks.Where(ub => ub.UserId == userId && ub.Book.Isbn != null).Select(ub => ub.Book.Isbn).ToListAsync();
        return recommendations.Where(r => r.Isbn == null || !ownedIsbns.Contains(r.Isbn)).DistinctBy(r => r.Title).Take(20).ToList();
    }

    private static void DetectLanguage(BookSearchResultDto result)
    {
        if (string.IsNullOrEmpty(result.Title)) { result.Language = "en"; return; }
        if (result.Title.Any(c => c is >= '\u0C80' and <= '\u0CFF')) result.Language = "kn";
        else if (result.Title.Any(c => c is >= '\u0C00' and <= '\u0C7F')) result.Language = "te";
        else if (result.Title.Any(c => c is >= '\u0B80' and <= '\u0BFF')) result.Language = "ta";
        else if (result.Title.Any(c => c is >= '\u0900' and <= '\u097F')) result.Language = "hi";
        else if (result.Title.Any(c => c is >= '\u0980' and <= '\u09FF')) result.Language = "bn";
        else if (result.Title.Any(c => c is >= '\u0D00' and <= '\u0D7F')) result.Language = "ml";
        else result.Language ??= "en";
    }
}