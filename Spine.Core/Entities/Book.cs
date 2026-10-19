using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spine.Core.Entities
{
    public class Book
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? Isbn { get; set; } // Nullable for manually added books
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public string? Description { get; set; }
        public int? PageCount { get; set; }

        public Guid? SeriesId { get; set; }
        public Series? Series { get; set; }
        public int? SeriesPosition { get; set; }
    }
}