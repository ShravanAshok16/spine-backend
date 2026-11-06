using System;
using System.Collections.Generic;

namespace Spine.Core.Entities
{
    public class Tag
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;

        // 🔥 Change this to point to the join table
        public ICollection<UserBookTag> UserBookTags { get; set; } = new List<UserBookTag>();
    }
}