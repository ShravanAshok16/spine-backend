using System.Text.Json;
using Spine.Api.Models;

namespace Spine.Api.Services;

public class OpenLibraryService
{
    private readonly HttpClient _httpClient;

    public OpenLibraryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://openlibrary.org/");
    }

    public async Task<BookSearchResultDto?> GetBookByIsbnAsync(string isbn)
    {
        var result = new BookSearchResultDto { Isbn = isbn };

        // 1. Fetch from Open Library
        var response = await _httpClient.GetAsync($"isbn/{isbn}.json");
        
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            result.Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown Title" : "Unknown Title";
            result.PageCount = root.TryGetProperty("number_of_pages", out var pages) && pages.ValueKind == JsonValueKind.Number 
                ? pages.GetInt32() : null;
            
            result.CoverUrl = $"https://covers.openlibrary.org/b/isbn/{isbn}-L.jpg";

            // Fetch Author
            if (root.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
            {
                var authorKey = authors[0].GetProperty("key").GetString();
                if (!string.IsNullOrEmpty(authorKey))
                {
                    var authorResponse = await _httpClient.GetAsync($"{authorKey}.json");
                    if (authorResponse.IsSuccessStatusCode)
                    {
                        var authorJson = await authorResponse.Content.ReadAsStringAsync();
                        using var authorDoc = JsonDocument.Parse(authorJson);
                        if (authorDoc.RootElement.TryGetProperty("name", out var nameProp))
                            result.Author = nameProp.GetString() ?? "Unknown Author";
                    }
                }
            }

            // Fetch Description & Genres (Subjects)
            if (root.TryGetProperty("works", out var works) && works.GetArrayLength() > 0)
            {
                var workKey = works[0].GetProperty("key").GetString();
                if (!string.IsNullOrEmpty(workKey))
                {
                    var workResponse = await _httpClient.GetAsync($"{workKey}.json");
                    if (workResponse.IsSuccessStatusCode)
                    {
                        var workJson = await workResponse.Content.ReadAsStringAsync();
                        using var workDoc = JsonDocument.Parse(workJson);
                        
                        // Description
                        if (workDoc.RootElement.TryGetProperty("description", out var descProp))
                        {
                            result.Description = descProp.ValueKind == JsonValueKind.String 
                                ? descProp.GetString() 
                                : descProp.TryGetProperty("value", out var valProp) ? valProp.GetString() : null;
                        }

                        // Genres (Subjects)
                        if (workDoc.RootElement.TryGetProperty("subjects", out var subjects) && subjects.GetArrayLength() > 0)
                        {
                            var genres = subjects.EnumerateArray().Take(3).Select(s => s.GetString());
                            result.Genres = string.Join(", ", genres);
                        }
                    }
                }
            }
        }

        // 2. FALLBACK: Google Books for missing Author/Description/Genres
        if (result.Author == "Unknown Author" || string.IsNullOrEmpty(result.Description) || string.IsNullOrEmpty(result.Genres))
        {
            await EnrichWithGoogleBooksAsync(isbn, result);
        }

        // 3. Language Detection (Unicode fallback)
        if (!string.IsNullOrEmpty(result.Title))
        {
            if (result.Title.Any(c => c >= 0x0C80 && c <= 0x0CFF)) result.Language = "kn";      // Kannada
            else if (result.Title.Any(c => c >= 0x0C00 && c <= 0x0C7F)) result.Language = "te"; // Telugu
            else if (result.Title.Any(c => c >= 0x0B80 && c <= 0x0BFF)) result.Language = "ta"; // Tamil
            else result.Language = "en";                                                        // Default English
        }

        // 4. Fallback Cover
        if (string.IsNullOrEmpty(result.CoverUrl))
            result.CoverUrl = "https://via.placeholder.com/300x450?text=No+Cover";

        return result.Title == "Unknown Title" && result.Author == "Unknown Author" ? null : result;
    }

       private async Task EnrichWithGoogleBooksAsync(string isbn, BookSearchResultDto result)
    {
        var googleUrl = $"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}";
        
        try
        {
            var response = await _httpClient.GetAsync(googleUrl);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
                return;

            var volumeInfo = items[0].GetProperty("volumeInfo");

            // 1. Fill Title if missing
            if (string.IsNullOrEmpty(result.Title) && volumeInfo.TryGetProperty("title", out var title))
                result.Title = title.GetString() ?? result.Title;

            // 2. Fill Author if missing
            if (result.Author == "Unknown Author" && volumeInfo.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
                result.Author = string.Join(", ", authors.EnumerateArray().Select(a => a.GetString()));

            // 3. Fill Description if missing
            if (string.IsNullOrEmpty(result.Description) && volumeInfo.TryGetProperty("description", out var desc))
                result.Description = desc.GetString();

            // 4. Fill Genres if missing
            if (string.IsNullOrEmpty(result.Genres) && volumeInfo.TryGetProperty("categories", out var cats) && cats.GetArrayLength() > 0)
                result.Genres = string.Join(", ", cats.EnumerateArray().Select(c => c.GetString()));

            // 5. Fill Page Count if missing
            if (!result.PageCount.HasValue && volumeInfo.TryGetProperty("pageCount", out var pages) && pages.ValueKind == JsonValueKind.Number)
                result.PageCount = pages.GetInt32();

            // 6. Fill Cover Image if missing (Google returns a thumbnail)
            if (string.IsNullOrEmpty(result.CoverUrl) && volumeInfo.TryGetProperty("imageLinks", out var imageLinks))
            {
                if (imageLinks.TryGetProperty("thumbnail", out var thumb))
                {
                    // Google sometimes returns http, force it to https
                    var url = thumb.GetString();
                    if (url != null) result.CoverUrl = url.Replace("http://", "https://");
                }
            }

            // 7. Fill Language if missing
            if (string.IsNullOrEmpty(result.Language) && volumeInfo.TryGetProperty("language", out var lang))
                result.Language = lang.GetString();
        }
        catch
        {
            // Ignore fallback errors so the app doesn't crash if Google API is down
        }
    }
}