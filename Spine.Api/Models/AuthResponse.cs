using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spine.Api.Models
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}