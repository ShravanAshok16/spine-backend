using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spine.Api.Models
{
    public class UpdateEmailRequest
    {
        public string NewEmail { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
    }
}