namespace Spine.Core.Entities;

public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}