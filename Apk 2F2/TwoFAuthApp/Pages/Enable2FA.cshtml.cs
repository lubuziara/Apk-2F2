using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OtpNet;
using QRCoder;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using TwoFAuthApp.Models;

namespace TwoFAuthApp.Pages
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
        public string? QrCodeImageUrl { get; set; }
        public string? ErrorMessage { get; set; }
        private string? Secret { get; set; }

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
                // Generuj sekret i QR
                var secret = KeyGeneration.GenerateRandomKey(20);
                user.TwoFactorSecret = Base32Encoding.ToString(secret);
                await _userManager.UpdateAsync(user);
                QrCodeImageUrl = GenerateQrCode(user.Email, user.TwoFactorSecret);
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
                QrCodeImageUrl = GenerateQrCode(user.Email, user.TwoFactorSecret);
                ErrorMessage = "Nieprawidłowy kod 2FA.";
                return Page();
            }
            QrCodeImageUrl = GenerateQrCode(user.Email, user.TwoFactorSecret);
            return Page();
        }

        private string GenerateQrCode(string email, string secret)
        {
            var issuer = "TwoFAuthApp";
            var totpUrl =
                $"otpauth://totp/{issuer}:{email}?secret={secret}&issuer={issuer}&digits=6";
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(totpUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrCodeData);
            using var bitmap = qrCode.GetGraphic(20);
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var base64 = Convert.ToBase64String(ms.ToArray());
            return $"data:image/png;base64,{base64}";
        }
    }
} 