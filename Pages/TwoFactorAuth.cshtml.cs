using AceJob.Model;
using AceJob.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;

namespace AceJob.Pages
{
    [Authorize]
    public class TwoFactorAuthModel : PageModel
    {
 private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
 private readonly IAuditService _auditService;
        private readonly ILogger<TwoFactorAuthModel> _logger;
        private readonly UrlEncoder _urlEncoder;

        private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

        public TwoFactorAuthModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
IAuditService auditService,
  ILogger<TwoFactorAuthModel> logger,
   UrlEncoder urlEncoder)
   {
   _userManager = userManager;
   _signInManager = signInManager;
_auditService = auditService;
  _logger = logger;
   _urlEncoder = urlEncoder;
      }

        [BindProperty]
   [Required(ErrorMessage = "Verification code is required")]
  [StringLength(6, MinimumLength = 6, ErrorMessage = "Verification code must be 6 digits")]
        public string VerificationCode { get; set; } = string.Empty;

        public bool Is2faEnabled { get; set; }
        public bool ShowSetup { get; set; }
        public string? QrCodeUrl { get; set; }
        public string? SharedKey { get; set; }
 public string? StatusMessage { get; set; }
     public string[]? RecoveryCodes { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
        if (user == null)
 {
       return RedirectToPage("/Login");
   }

            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
return Page();
        }

        public async Task<IActionResult> OnPostSetupAsync()
        {
   var user = await _userManager.GetUserAsync(User);
  if (user == null)
     {
   return RedirectToPage("/Login");
         }

   // Reset the authenticator key
       await _userManager.ResetAuthenticatorKeyAsync(user);
var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);

   if (string.IsNullOrEmpty(unformattedKey))
            {
  await _userManager.ResetAuthenticatorKeyAsync(user);
          unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
 }

            SharedKey = FormatKey(unformattedKey!);
       QrCodeUrl = GenerateQrCodeUri(user.Email!, unformattedKey!);
            ShowSetup = true;
    Is2faEnabled = false;

  _logger.LogInformation("User {Email} initiated 2FA setup", user.Email);

     return Page();
        }

        public async Task<IActionResult> OnPostEnableAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
  {
  return RedirectToPage("/Login");
  }

     // Strip spaces and hyphens from the code
  var verificationCode = VerificationCode.Replace(" ", "").Replace("-", "");

var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
       user,
    _userManager.Options.Tokens.AuthenticatorTokenProvider,
          verificationCode);

            if (!is2faTokenValid)
  {
      ModelState.AddModelError("VerificationCode", "Invalid verification code. Please try again.");
      
       // Reload setup data
 var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
       SharedKey = FormatKey(unformattedKey!);
   QrCodeUrl = GenerateQrCodeUri(user.Email!, unformattedKey!);
   ShowSetup = true;

           await _auditService.LogAsync(
 "2FA_Setup",
    user.Id,
       user.Email,
    "2FA setup failed - invalid verification code",
     false);

return Page();
    }

     // Enable 2FA
      await _userManager.SetTwoFactorEnabledAsync(user, true);

   _logger.LogInformation("User {Email} enabled 2FA", user.Email);

  await _auditService.LogAsync(
     "2FA_Enabled",
user.Id,
        user.Email,
"Two-Factor Authentication enabled",
   true);

  // Generate recovery codes
       RecoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();

   Is2faEnabled = true;
       ShowSetup = false;
       StatusMessage = "Two-Factor Authentication has been enabled successfully!";

      await _signInManager.RefreshSignInAsync(user);

  return Page();
  }

 public async Task<IActionResult> OnPostDisableAsync()
        {
      var user = await _userManager.GetUserAsync(User);
  if (user == null)
     {
          return RedirectToPage("/Login");
   }

    // Disable 2FA
    await _userManager.SetTwoFactorEnabledAsync(user, false);
 await _userManager.ResetAuthenticatorKeyAsync(user);

   _logger.LogInformation("User {Email} disabled 2FA", user.Email);

  await _auditService.LogAsync(
       "2FA_Disabled",
  user.Id,
 user.Email,
        "Two-Factor Authentication disabled",
    true);

            StatusMessage = "Two-Factor Authentication has been disabled.";
  Is2faEnabled = false;

          await _signInManager.RefreshSignInAsync(user);

   return Page();
   }

public async Task<IActionResult> OnGetGenerateRecoveryCodesAsync()
        {
     var user = await _userManager.GetUserAsync(User);
  if (user == null)
         {
   return RedirectToPage("/Login");
  }

    var is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
   if (!is2faEnabled)
  {
         StatusMessage = "Error: Cannot generate recovery codes because 2FA is not enabled.";
      return Page();
      }

        RecoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
    Is2faEnabled = true;

      _logger.LogInformation("User {Email} generated new recovery codes", user.Email);

     await _auditService.LogAsync(
    "2FA_RecoveryCodes",
       user.Id,
        user.Email,
     "Generated new 2FA recovery codes",
true);

       return Page();
        }

  private static string FormatKey(string unformattedKey)
        {
       var result = new StringBuilder();
   var currentPosition = 0;

while (currentPosition + 4 < unformattedKey.Length)
    {
        result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
 currentPosition += 4;
   }

 if (currentPosition < unformattedKey.Length)
 {
      result.Append(unformattedKey.AsSpan(currentPosition));
  }

  return result.ToString().ToLowerInvariant();
        }

        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
         var uri = string.Format(
  AuthenticatorUriFormat,
           _urlEncoder.Encode("AceJob"),
      _urlEncoder.Encode(email),
      unformattedKey);

            // Generate QR code using a QR code API
return $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={Uri.EscapeDataString(uri)}";
      }
    }
}
