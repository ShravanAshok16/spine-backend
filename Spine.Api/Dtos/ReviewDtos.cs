namespace Spine.Api.Models;

public class CreateReviewRequest
{
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
}

public class ReviewResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTime CreatedAt { get; set; }
}