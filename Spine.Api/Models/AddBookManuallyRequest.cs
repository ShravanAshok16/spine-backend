namespace Spine.Api.Models;

public class AddBookManuallyRequest
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string? Isbn { get; set; }
    public string? Description { get; set; }
    public int? PageCount { get; set; }
    public string? Genres { get; set; }
    public string? CoverUrl { get; set; }
    public Guid? SeriesId { get; set; }
    public string? SeriesName { get; set; }
    public int? SeriesPosition { get; set; }
}