using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OtpNet;
using QRCoder;
using System.ComponentModel.DataAnnotations;

namespace TwoFAuthDemo.Pages
{
    public class Enable2FAModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public Enable2FAModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();
        public string? QrCodeSvg { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Secret { get; set; }
        public string? QrCodeUrl { get; set; }

        public class InputModel
        {
            [Display(Name = "Kod 2FA")]
            public string? TwoFACode { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Login");
            
            if (user.Is2FAEnabled)
                return RedirectToPage("/Index");
            
            if (!string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                Secret = user.TwoFactorSecret;
                QrCodeUrl = GenerateQrCodeUrl(user.Email ?? user.UserName ?? "user", user.TwoFactorSecret);
                QrCodeSvg = GenerateQrCodeSvg(QrCodeUrl);
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Login");
            
            if (user.Is2FAEnabled)
                return RedirectToPage("/Index");

            if (string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                // Generuj sekret
                var secret = KeyGeneration.GenerateRandomKey(20);
                user.TwoFactorSecret = Base32Encoding.ToString(secret);
                await _userManager.UpdateAsync(user);
                
                Secret = user.TwoFactorSecret;
                QrCodeUrl = GenerateQrCodeUrl(user.Email ?? user.UserName ?? "user", user.TwoFactorSecret);
                QrCodeSvg = GenerateQrCodeSvg(QrCodeUrl);
                return Page();
            }
            else if (!string.IsNullOrEmpty(Input.TwoFACode))
            {
                // Weryfikuj kod
                var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
                if (totp.VerifyTotp(Input.TwoFACode, out _, new VerificationWindow(1, 1)))
                {
                    user.Is2FAEnabled = true;
                    await _userManager.UpdateAsync(user);
                    return RedirectToPage("/Index");
                }
                
                Secret = user.TwoFactorSecret;
                QrCodeUrl = GenerateQrCodeUrl(user.Email ?? user.UserName ?? "user", user.TwoFactorSecret);
                QrCodeSvg = GenerateQrCodeSvg(QrCodeUrl);
                ErrorMessage = "Nieprawidłowy kod 2FA.";
                return Page();
            }
            
            Secret = user.TwoFactorSecret;
            QrCodeUrl = GenerateQrCodeUrl(user.Email ?? user.UserName ?? "user", user.TwoFactorSecret);
            QrCodeSvg = GenerateQrCodeSvg(QrCodeUrl);
            return Page();
        }

        private string GenerateQrCodeUrl(string email, string secret)
        {
            var issuer = "TwoFAuthDemo";
            return $"otpauth://totp/{issuer}:{email}?secret={secret}&issuer={issuer}&digits=6";
        }

        private string GenerateQrCodeSvg(string totpUrl)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(totpUrl, QRCodeGenerator.ECCLevel.Q);
            var svgQrCode = new SvgQRCode(qrCodeData);
            return svgQrCode.GetGraphic(6);
        }
    }
} 