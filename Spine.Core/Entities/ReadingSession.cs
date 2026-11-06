using System;

namespace Spine.Core.Entities;

public class ReadingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserBookId { get; set; }
    public UserBook UserBook { get; set; } = null!;
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int PagesRead { get; set; } = 0;
    public string? Notes { get; set; }
}