using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spine.Core.Entities
{
    public class Tag
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<UserBook> UserBooks { get; set; } = new List<UserBook>();
    }
}