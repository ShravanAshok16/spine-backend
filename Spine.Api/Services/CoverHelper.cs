using Microsoft.Extensions.Caching.Memory;

namespace Spine.Api.Services;

public static class CoverHelper
{
    private static IMemoryCache? _cache;

    public static void Configure(IMemoryCache cache) => _cache = cache;

    public static bool IsPlaceholder(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;

        return url.Contains("/b/id/-1-", StringComparison.OrdinalIgnoreCase)
            || url.Contains("id=-1", StringComparison.OrdinalIgnoreCase)
            || url.Contains("nophoto", StringComparison.OrdinalIgnoreCase)
            || url.Contains("no_cover", StringComparison.OrdinalIgnoreCase)
            || url.Contains("nocover", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith("/null", StringComparison.OrdinalIgnoreCase)
            || url.Contains("placeholder", StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetCached(string key)
    {
        if (_cache != null && _cache.TryGetValue(key, out string? url))
            return url;
        return null;
    }

    public static void SetCached(string key, string? url)
    {
        if (string.IsNullOrWhiteSpace(key) || IsPlaceholder(url)) return;

        _cache?.Set(key, url, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(14),
            Size = 1
        });
    }

    public static string BuildOpenLibraryIsbnCover(string isbn, bool preferDefaultPlaceholder = false)
    {
        var clean = isbn.Replace("-", "").Replace(" ", "");
        var def = preferDefaultPlaceholder ? "true" : "false";
        return $"https://covers.openlibrary.org/b/isbn/{clean}-L.jpg?default={def}";
    }
}