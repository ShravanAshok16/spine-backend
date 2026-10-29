namespace Spine.Core.Entities;

public class Book
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Isbn { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? CoverUrl { get; set; } // <-- This must exist
    public string? Description { get; set; }
    public int? PageCount { get; set; }
    public Guid? SeriesId { get; set; }
    public Series? Series { get; set; }
    public int? SeriesPosition { get; set; }
    public string? Genres { get; set; }
    public string? Language { get; set; }
}