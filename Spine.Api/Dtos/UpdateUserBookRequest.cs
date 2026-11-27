namespace Spine.Api.Models;

public class UpdateUserBookRequest
{
    public string? Status { get; set; }
    public int? CurrentPage { get; set; } // 🔥 MUST BE int? (Nullable)
    public int? PersonalRating { get; set; }
    public string? PrivateNotes { get; set; }
    public string? LentTo { get; set; }
    public string? Ownership { get; set; }
}