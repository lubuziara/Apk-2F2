using Microsoft.AspNetCore.Identity;

namespace TwoFAuthApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? TwoFactorSecret { get; set; }
        public bool Is2FAEnabled { get; set; }
    }
} 