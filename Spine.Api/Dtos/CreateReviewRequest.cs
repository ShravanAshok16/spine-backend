namespace Spine.Api.Models;

public class CreateReviewRequest
{
    public int Rating { get; set; } // 1-5
    public string ReviewText { get; set; } = string.Empty;
}