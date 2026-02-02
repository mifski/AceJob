using AceJob.Model;
using AceJob.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace AceJob.Pages
{
    public class LoginWithRecoveryCodeModel : PageModel
    {
   private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
      private readonly IAuditService _auditService;
 private readonly ILogger<LoginWithRecoveryCodeModel> _logger;

 public LoginWithRecoveryCodeModel(
   SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
IAuditService auditService,
  ILogger<LoginWithRecoveryCodeModel> logger)
  {
     _signInManager = signInManager;
         _userManager = userManager;
   _auditService = auditService;
   _logger = logger;
     }

  [BindProperty]
        [Required(ErrorMessage = "Recovery code is required")]
        [Display(Name = "Recovery Code")]
     public string RecoveryCode { get; set; } = string.Empty;

 public string? ReturnUrl { get; set; }

   public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
   {
  // Ensure the user has gone through the login page first
 var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

     if (user == null)
   {
       return RedirectToPage("/Login");
  }

   ReturnUrl = returnUrl ?? Url.Content("~/");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
  {
  if (!ModelState.IsValid)
       {
         return Page();
   }

   returnUrl ??= Url.Content("~/");

 var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
    {
    return RedirectToPage("/Login");
   }

      var recoveryCode = RecoveryCode.Replace(" ", "").Replace("-", "");

     var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

       if (result.Succeeded)
  {
   _logger.LogInformation("User {Email} logged in with recovery code", user.Email);

// Store session data
     HttpContext.Session.SetString("UserId", user.Id);
   HttpContext.Session.SetString("UserEmail", user.Email ?? "");
       HttpContext.Session.SetString("LoginTime", DateTime.UtcNow.ToString("O"));

await _auditService.LogAsync(
           "2FA_RecoveryLogin",
    user.Id,
    user.Email,
        "Login successful using recovery code",
    true);

   return LocalRedirect(returnUrl);
         }

    if (result.IsLockedOut)
  {
         _logger.LogWarning("User {Email} account locked out", user.Email);
 
      await _auditService.LogAsync(
AuditActions.Lockout,
    user.Id,
     user.Email,
  "Account locked out during recovery code verification",
  false);

      return RedirectToPage("/Error", new { code = "lockout" });
 }

          _logger.LogWarning("Invalid recovery code for user {Email}", user.Email);

            await _auditService.LogAsync(
         "2FA_RecoveryFailed",
 user.Id,
      user.Email,
    "Invalid recovery code",
    false);

ModelState.AddModelError(string.Empty, "Invalid recovery code.");
            return Page();
        }
    }
}
