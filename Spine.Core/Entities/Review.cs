using System;

namespace Spine.Core.Entities;

public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // 🔥 ADD THIS LINE
    public DateTime? UpdatedAt { get; set; } 
}