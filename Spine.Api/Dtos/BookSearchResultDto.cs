namespace Spine.Api.Models;

public class BookSearchResultDto
{
    public string? Isbn { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string? Description { get; set; }
    public int? PageCount { get; set; }
}