using System;

namespace Spine.Core.Entities
{
    public class UserBookTag
    {
        public Guid UserBookId { get; set; }
        public UserBook UserBook { get; set; } = null!;
        
        public Guid TagId { get; set; }
        public Tag Tag { get; set; } = null!;
    }
}