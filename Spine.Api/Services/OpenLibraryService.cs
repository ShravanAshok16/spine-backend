using System.Text.Json;

namespace Spine.Api.Services;

public class OpenLibraryService
{
    private readonly HttpClient _httpClient;

    public OpenLibraryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> GetCoverUrlByIsbnAsync(string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return null;

        // Open Library has a direct image endpoint based on ISBN
        // "L" means Large
        var coverUrl = $"https://covers.openlibrary.org/b/isbn/{isbn}-L.jpg";
        
        // We do a quick HEAD request to see if the image actually exists
        var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, coverUrl));
        
        return response.IsSuccessStatusCode ? coverUrl : null;
    }
}