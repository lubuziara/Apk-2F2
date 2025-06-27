using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OtpNet;
using System.ComponentModel.DataAnnotations;

namespace TwoFAuthDemo.Pages
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public bool Require2FA { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email jest wymagany")]
            [EmailAddress(ErrorMessage = "Nieprawidłowy format email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Hasło jest wymagane")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public string? TwoFACode { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ErrorMessage = "Nieprawidłowy email lub hasło.";
                return Page();
            }

            if (user.Is2FAEnabled)
            {
                Require2FA = true;
                if (string.IsNullOrEmpty(Input.TwoFACode))
                {
                    ErrorMessage = "Podaj kod 2FA.";
                    return Page();
                }
                
                // Weryfikacja TOTP
                if (string.IsNullOrEmpty(user.TwoFactorSecret))
                {
                    ErrorMessage = "Błąd konfiguracji 2FA.";
                    return Page();
                }
                
                var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
                if (!totp.VerifyTotp(Input.TwoFACode, out _, new VerificationWindow(1, 1)))
                {
                    ErrorMessage = "Nieprawidłowy kod 2FA.";
                    return Page();
                }
            }

            var result = await _signInManager.PasswordSignInAsync(user, Input.Password, false, false);
            if (result.Succeeded)
            {
                return RedirectToPage("/Index");
            }
            
            ErrorMessage = "Nieprawidłowy email lub hasło.";
            return Page();
        }
    }
} 