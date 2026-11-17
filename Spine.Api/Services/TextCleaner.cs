using System.Text.RegularExpressions;
using System.Net;

namespace Spine.Api.Services;

public static class TextCleaner
{
    public static string? CleanDescription(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var text = WebUtility.HtmlDecode(input);
        text = Regex.Replace(text, "<.*?>", string.Empty);
        text = Regex.Replace(text, @"-{3,}", "");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }
}