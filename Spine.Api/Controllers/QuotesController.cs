using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private static string? _cachedQuote = null;
    private static string? _cachedAuthor = null;
    private static DateTime _cacheDate = DateTime.MinValue;

    public QuotesController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpGet("daily")]
    public async Task<IActionResult> GetDailyQuote()
    {
        if (_cacheDate.Date == DateTime.UtcNow.Date && _cachedQuote != null)
            return Ok(new { Text = _cachedQuote, Author = _cachedAuthor });

        try
        {
            var response = await _httpClient.GetAsync("https://api.quotable.io/random?tags=books,reading,literature");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                _cachedQuote = doc.RootElement.GetProperty("content").GetString();
                _cachedAuthor = doc.RootElement.GetProperty("author").GetString();
                _cacheDate = DateTime.UtcNow;
                return Ok(new { Text = _cachedQuote, Author = _cachedAuthor });
            }
        }
        catch { /* Fallback below */ }

        return Ok(new { 
            Text = "A room without books is like a body without a soul.", 
            Author = "Marcus Tullius Cicero" 
        });
    }
}