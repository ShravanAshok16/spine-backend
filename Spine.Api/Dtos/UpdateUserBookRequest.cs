namespace Spine.Api.Models;

public class UpdateUserBookRequest
{
    public string Status { get; set; } = string.Empty; // Unread, Reading, Finished, DNF
    public int CurrentPage { get; set; }
    public int? PersonalRating { get; set; } // 1-5
    public string? PrivateNotes { get; set; }
    public string? LentTo { get; set; }
}