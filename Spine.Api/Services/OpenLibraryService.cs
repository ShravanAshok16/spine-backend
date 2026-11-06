using System.Text.Json;
using Spine.Api.Models;
using System.Text.RegularExpressions;

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

        // 2. FALLBACK: Google Books for missing Author/Description/Genres/Cover
        if (result.Author == "Unknown Author" || string.IsNullOrEmpty(result.Description) || string.IsNullOrEmpty(result.Genres) || string.IsNullOrEmpty(result.CoverUrl))
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

        // 🔥 FIX: Do NOT set a placeholder URL. Leave it null so the frontend can generate a gradient cover.
        // if (string.IsNullOrEmpty(result.CoverUrl))
        //    result.CoverUrl = "https://via.placeholder.com/300x450?text=No+Cover";

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

            if (string.IsNullOrEmpty(result.Title) && volumeInfo.TryGetProperty("title", out var title))
                result.Title = title.GetString() ?? result.Title;

            if (result.Author == "Unknown Author" && volumeInfo.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
                result.Author = string.Join(", ", authors.EnumerateArray().Select(a => a.GetString()));

            if (string.IsNullOrEmpty(result.Description) && volumeInfo.TryGetProperty("description", out var desc))
                result.Description = desc.GetString();

            if (string.IsNullOrEmpty(result.Genres) && volumeInfo.TryGetProperty("categories", out var cats) && cats.GetArrayLength() > 0)
                result.Genres = string.Join(", ", cats.EnumerateArray().Select(c => c.GetString()));

            if (!result.PageCount.HasValue && volumeInfo.TryGetProperty("pageCount", out var pages) && pages.ValueKind == JsonValueKind.Number)
                result.PageCount = pages.GetInt32();

            // Fill Cover Image if missing (Google returns a thumbnail)
            if (string.IsNullOrEmpty(result.CoverUrl) && volumeInfo.TryGetProperty("imageLinks", out var imageLinks))
            {
                if (imageLinks.TryGetProperty("thumbnail", out var thumb))
                {
                    var url = thumb.GetString();
                    if (url != null) result.CoverUrl = url.Replace("http://", "https://");
                }
            }

            if (string.IsNullOrEmpty(result.Language) && volumeInfo.TryGetProperty("language", out var lang))
                result.Language = lang.GetString();
        }
        catch
        {
            // Ignore fallback errors so the app doesn't crash if Google API is down
        }
    }

    public async Task<List<BookSearchResultDto>> SearchBooksAsync(string query)
    {
        var results = new List<BookSearchResultDto>();
        try
        {
            var url = $"https://openlibrary.org/search.json?q={Uri.EscapeDataString(query)}&limit=12";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("docs", out var docs)) return results;

                foreach (var item in docs.EnumerateArray())
                {
                    string? isbn = item.TryGetProperty("isbn", out var isbns) && isbns.GetArrayLength() > 0 ? isbns[0].GetString() : null;
                    string? coverUrl = item.TryGetProperty("cover_i", out var coverId) ? $"https://covers.openlibrary.org/b/id/{coverId}-M.jpg" : null;
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown" : "Unknown";
                    var author = item.TryGetProperty("author_name", out var a) && a.GetArrayLength() > 0 ? a[0].GetString() ?? "Unknown Author" : "Unknown Author";

                    var result = new BookSearchResultDto
                    {
                        Isbn = isbn,
                        Title = title,
                        Author = author,
                        CoverUrl = coverUrl,
                        Language = "en"
                    };

                    // 🔥 SMART SERIES DETECTION 🔥

                    // 1. Check the "series" array if it exists (rare but happens)
                    if (item.TryGetProperty("series", out var seriesArray) && seriesArray.GetArrayLength() > 0)
                    {
                        result.SeriesName = seriesArray[0].GetString()?.Replace("[", "").Replace("]", "").Trim();
                    }

                    // 2. Check "subject" array for the word "series" or look for a subject ending in "series"
                    if (string.IsNullOrEmpty(result.SeriesName) && item.TryGetProperty("subject", out var subjects))
                    {
                        foreach (var subject in subjects.EnumerateArray())
                        {
                            var sub = subject.GetString();
                            if (sub != null && (sub.EndsWith(" series") || sub.Contains("Series")))
                            {
                                // e.g., "The Stormlight Archive series" -> "The Stormlight Archive"
                                result.SeriesName = Regex.Replace(sub, @"\s*series$", "", RegexOptions.IgnoreCase).Trim();
                                break;
                            }
                        }
                    }

                    // 3. Fallback: Parse the Title (e.g. "The Way of Kings: Book One of the Stormlight Archive")
                    if (string.IsNullOrEmpty(result.SeriesName))
                    {
                        var seriesMatch = Regex.Match(title, @"(?:Book\s+\d+\s+of\s+(?:the\s+)?)(.*?)(?:$|,|:|\.)", RegexOptions.IgnoreCase);
                        if (seriesMatch.Success)
                        {
                            result.SeriesName = seriesMatch.Groups[1].Value.Trim();
                        }
                    }

                    results.Add(result);
                }
            }
        }
        catch { }
        return results;
    }
}