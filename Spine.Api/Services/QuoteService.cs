using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Spine.Infrastructure.Services;

public class QuoteService
{
    private readonly HttpClient _http;
    private readonly ILogger<QuoteService> _logger;

    // 🔥 Reliable hardcoded fallback quotes
    private static readonly (string Text, string Author)[] FallbackQuotes =
    {
        ("A reader lives a thousand lives before he dies.", "George R.R. Martin"),
        ("Until I feared I would lose it, I never loved to read. One does not love breathing.", "Harper Lee"),
        ("Books are a uniquely portable magic.", "Stephen King"),
        ("There is no friend as loyal as a book.", "Ernest Hemingway"),
        ("I have always imagined that Paradise will be a kind of library.", "Jorge Luis Borges"),
        ("A room without books is like a body without a soul.", "Cicero"),
        ("So many books, so little time.", "Frank Zappa"),
        ("You can never get a cup of tea large enough or a book long enough to suit me.", "C.S. Lewis"),
    };

    public QuoteService(HttpClient http, ILogger<QuoteService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(string Text, string Author)> GetDailyQuoteAsync()
    {
        try
        {
            var response = await _http.GetAsync("https://zenquotes.io/api/random");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var first = doc.RootElement[0];
                return (first.GetProperty("q").GetString() ?? "", first.GetProperty("a").GetString() ?? "");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch quote from zenquotes, using fallback.");
        }

        // 🔥 Return a random fallback quote
        var random = FallbackQuotes[Random.Shared.Next(FallbackQuotes.Length)];
        return random;
    }
}