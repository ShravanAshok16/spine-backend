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
}