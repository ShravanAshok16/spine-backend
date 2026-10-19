using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spine.Core.Entities
{
    public class Friendship
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid RequesterId { get; set; }
        public User Requester { get; set; } = null!;

        public Guid ReceiverId { get; set; }
        public User Receiver { get; set; } = null!;

        public string Status { get; set; } = "Pending"; // Pending, Accepted, Blocked
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}