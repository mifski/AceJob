using System.Security.Claims;
using AceJob.Model;
using AceJob.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace AceJob.Pages
{
    public class LoginWith2faModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
      private readonly IAuditService _auditService;
  private readonly ILogger<LoginWith2faModel> _logger;

        private const string SessionVersionClaimType = "sv";

        public LoginWith2faModel(
     SignInManager<ApplicationUser> signInManager,
     UserManager<ApplicationUser> userManager,
    IAuditService auditService,
     ILogger<LoginWith2faModel> logger)
     {
     _signInManager = signInManager;
            _userManager = userManager;
_auditService = auditService;
     _logger = logger;
        }

   [BindProperty]
        [Required(ErrorMessage = "Authentication code is required")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Authentication code must be 6 digits")]
      [Display(Name = "Authentication Code")]
        public string TwoFactorCode { get; set; } = string.Empty;

        [BindProperty]
     [Display(Name = "Remember this device")]
     public bool RememberMachine { get; set; }

      [BindProperty]
      public bool RememberMe { get; set; }

public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(bool rememberMe = false, string? returnUrl = null)
        {
            // Ensure the user has gone through the login page first
    var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

      if (user == null)
       {
      return RedirectToPage("/Login");
   }

       // Store the rememberMe value from the login page
  RememberMe = rememberMe;
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

        var authenticatorCode = TwoFactorCode.Replace(" ", "").Replace("-", "");

         // Perform 2FA sign-in
      var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
   authenticatorCode,
      RememberMe,
           RememberMachine);

            if (result.Succeeded)
            {
 _logger.LogInformation("User {Email} logged in with 2FA. RememberMe: {RememberMe}, RememberMachine: {RememberMachine}",
   user.Email, RememberMe, RememberMachine);

       // Store session data
        HttpContext.Session.SetString("UserId", user.Id);
      HttpContext.Session.SetString("UserEmail", user.Email ?? "");
        HttpContext.Session.SetString("LoginTime", DateTime.UtcNow.ToString("O"));

     // Increment SessionVersion to invalidate other sessions
        user.SessionVersion++;
  await _userManager.UpdateAsync(user);

     // Create a new principal with the SessionVersion claim
  // This is the KEY - we need to manually add the claim and re-sign-in
 var principal = await _signInManager.CreateUserPrincipalAsync(user);
  var identity = (ClaimsIdentity)principal.Identity!;
            
 // Remove any existing sv claim and add the new one
         var existingSvClaim = identity.FindFirst(SessionVersionClaimType);
         if (existingSvClaim != null)
       {
         identity.RemoveClaim(existingSvClaim);
}
            identity.AddClaim(new Claim(SessionVersionClaimType, user.SessionVersion.ToString()));

   // Sign in with the updated principal
                // This replaces the cookie created by TwoFactorAuthenticatorSignInAsync
      // but the TwoFactorRememberMe cookie is separate and remains intact
       await HttpContext.SignInAsync(
         IdentityConstants.ApplicationScheme,
   principal,
 new AuthenticationProperties
         {
               IsPersistent = RememberMe,
         IssuedUtc = DateTimeOffset.UtcNow
          });

      await _auditService.LogAsync(
         AuditActions.Login,
     user.Id,
        user.Email,
            $"Login successful with 2FA (RememberDevice: {RememberMachine})",
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
        "Account locked out during 2FA verification",
      false);

      return RedirectToPage("/Error", new { code = "lockout" });
 }

         // Invalid code
   _logger.LogWarning("Invalid 2FA code for user {Email}", user.Email);

            await _auditService.LogAsync(
    "2FA_Failed",
   user.Id,
user.Email,
              "Invalid 2FA verification code",
   false);

    ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
            return Page();
        }
    }
}
