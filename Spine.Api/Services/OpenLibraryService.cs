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
        // 1. Fetch the main book data
        var response = await _httpClient.GetAsync($"isbn/{isbn}.json");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var title = root.TryGetProperty("title", out var t) ? t.GetString() : "Unknown Title";
        var pageCount = root.TryGetProperty("number_of_pages", out var pages) && pages.ValueKind == JsonValueKind.Number 
            ? pages.GetInt32() 
            : (int?)null;

        string? author = null;
        string? description = null;

        // 2. Fetch the Author (requires a 2nd API call)
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
                    {
                        author = nameProp.GetString();
                    }
                }
            }
        }

        // 3. Fetch the Description (requires a 3rd API call to the "Works" endpoint)
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
                    
                    if (workDoc.RootElement.TryGetProperty("description", out var descProp))
                    {
                        // Sometimes description is a string, sometimes it's an object with a "value" property
                        description = descProp.ValueKind == JsonValueKind.String 
                            ? descProp.GetString() 
                            : descProp.TryGetProperty("value", out var valProp) ? valProp.GetString() : null;
                    }
                }
            }
        }

        var coverUrl = $"https://covers.openlibrary.org/b/isbn/{isbn}-L.jpg";

        return new BookSearchResultDto
        {
            Isbn = isbn,
            Title = title ?? "Unknown",
            Author = author ?? "Unknown Author",
            CoverUrl = coverUrl,
            Description = description,
            PageCount = pageCount
        };
    }
}