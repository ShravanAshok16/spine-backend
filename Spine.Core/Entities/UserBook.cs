using Spine.Core.Enums;

namespace Spine.Core.Entities;

public class UserBook
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    // Statuses: Unread, Reading, Paused, Finished, DNF
    public string Status { get; set; } = "Unread";

    public bool IsFavourite { get; set; } = false;
    public OwnershipStatus Ownership { get; set; } = OwnershipStatus.Owned;

    public int CurrentPage { get; set; } = 0;
    public int? PersonalRating { get; set; }
    public string? PrivateNotes { get; set; }
    public string? LentTo { get; set; }
    public DateTime? LentDate { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReadingSession> ReadingSessions { get; set; } = new List<ReadingSession>();
    public ICollection<UserBookTag> Tags { get; set; } = new List<UserBookTag>();
}