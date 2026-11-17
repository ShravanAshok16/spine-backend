namespace Spine.Api.Models;

public class UserBookResponse
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CurrentPage { get; set; }
    public int? PersonalRating { get; set; }
    public string? PrivateNotes { get; set; }
    public string? LentTo { get; set; }
    public DateTime AddedAt { get; set; }
    public int? TotalPages { get; set; }
    public string? Language { get; set; }
    public string? Genres { get; set; }
    public Guid? SeriesId { get; set; }
    public string? SeriesName { get; set; }
    public bool IsFavourite { get; set; }
    public string Ownership { get; set; } = "Owned";
    public string? Isbn { get; set; }
    public string? Description { get; set; }
    public int? PageCount { get; set; }
    public int? PublishYear { get; set; } // Optional, if you have it
}