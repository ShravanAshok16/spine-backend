using System.Text.Json;
using Spine.Api.Models;
using Microsoft.Extensions.Logging;

namespace Spine.Api.Services;

public class GoogleBooksService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleBooksService> _logger;
    private readonly string? _apiKey;

    public GoogleBooksService(HttpClient httpClient, ILogger<GoogleBooksService> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["GoogleBooks:ApiKey"];
        _httpClient.BaseAddress = new Uri("https://www.googleapis.com/books/v1/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SpineApp/1.0");
    }

    public async Task<BookSearchResultDto?> GetByIdentifierAsync(string identifier, string? language = null)
    {
        var query = identifier.Length >= 10 && identifier.All(char.IsDigit) ? $"isbn:{identifier}" : identifier;
        var results = await SearchAsync(query, 1, language);
        return results.FirstOrDefault();
    }

    public async Task<List<BookSearchResultDto>> SearchAsync(string query, int limit = 12, string? language = null)
    {
        var results = new List<BookSearchResultDto>();
        try
        {
            var url = $"volumes?q={Uri.EscapeDataString(query)}&maxResults={limit}";
            if (!string.IsNullOrEmpty(_apiKey)) url += $"&key={_apiKey}";
            if (!string.IsNullOrEmpty(language) && language != "en") url += $"&langRestrict={language}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return results;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items)) return results;

            foreach (var item in items.EnumerateArray())
            {
                var volume = item.GetProperty("volumeInfo");

                string? isbn = null;
                if (volume.TryGetProperty("industryIdentifiers", out var ids))
                {
                    foreach (var id in ids.EnumerateArray())
                    {
                        var type = id.GetProperty("type").GetString();
                        if (type is "ISBN_13" or "ISBN_10") { isbn = id.GetProperty("identifier").GetString(); break; }
                    }
                }

                // 🔥 FIX: Extract highest quality available, but DO NOT modify the URL
                string? cover = null;
                if (volume.TryGetProperty("imageLinks", out var images))
                {
                    if (images.TryGetProperty("extraLarge", out var xl)) cover = xl.GetString();
                    else if (images.TryGetProperty("large", out var l)) cover = l.GetString();
                    else if (images.TryGetProperty("medium", out var m)) cover = m.GetString();
                    else if (images.TryGetProperty("thumbnail", out var thumb)) cover = thumb.GetString();
                    else if (images.TryGetProperty("smallThumbnail", out var st)) cover = st.GetString();

                    if (cover != null)
                    {
                        // Only replace http with https. Leave the zoom parameter alone so it doesn't break.
                        cover = cover.Replace("http://", "https://");
                    }
                }

                if (CoverHelper.IsPlaceholder(cover)) cover = null;

                var author = volume.TryGetProperty("authors", out var authArr) && authArr.GetArrayLength() > 0 ? authArr[0].GetString() : "Unknown Author";
                var detectedLang = volume.TryGetProperty("language", out var lang) ? lang.GetString() : "en";

                results.Add(new BookSearchResultDto
                {
                    Isbn = isbn,
                    Title = volume.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown" : "Unknown",
                    Author = author ?? "Unknown Author",
                    CoverUrl = cover,
                    Description = volume.TryGetProperty("description", out var d) ? d.GetString() : null,
                    PageCount = volume.TryGetProperty("pageCount", out var p) ? p.GetInt32() : null,
                    Language = detectedLang,
                    Genres = volume.TryGetProperty("categories", out var cats) && cats.GetArrayLength() > 0 ? string.Join(", ", cats.EnumerateArray().Select(c => c.GetString())) : null
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoogleBooks API threw an exception for query: {Query}", query);
        }
        return results;
    }
}