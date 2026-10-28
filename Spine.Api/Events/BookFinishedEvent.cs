namespace Spine.Api.Events;

public class BookFinishedEvent
{
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public int? Rating { get; set; }
}