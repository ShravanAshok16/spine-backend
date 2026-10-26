using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spine.Api.Models
{
    public class LoginRequest
    {
        public string Identifier { get; set; } = string.Empty; 
        public string Password { get; set; } = string.Empty;
    }
}