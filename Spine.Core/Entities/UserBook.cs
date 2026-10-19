using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spine.Core.Entities
{
    public class UserBook
    {

        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid BookId { get; set; }
        public Book Book { get; set; } = null!;

        public string Status { get; set; } = "Unread"; // Unread, Reading, Finished, DNF
        public int CurrentPage { get; set; } = 0;
        public int? PersonalRating { get; set; }
        public string? PrivateNotes { get; set; }
        public string? LentTo { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Soft delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    }
}