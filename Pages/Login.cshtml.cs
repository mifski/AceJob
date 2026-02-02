using System.Security.Claims;
using AceJob.Model;
using AceJob.ViewModels;
using AceJob.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AceJob.Pages
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRecaptchaService _recaptchaService;
        private readonly IAuditService _auditService;
        private readonly ILogger<LoginModel> _logger;
        private readonly IConfiguration _configuration;

        [BindProperty]
        public Login LModel { get; set; } = null!;

        [TempData]
        public string? InfoMessage { get; set; }

        private const string SessionVersionClaimType = "sv";

        public LoginModel(
          SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
            IRecaptchaService recaptchaService,
        IAuditService auditService,
        ILogger<LoginModel> logger,
            IConfiguration configuration)
        {
 _signInManager = signInManager;
     _userManager = userManager;
         _recaptchaService = recaptchaService;
      _auditService = auditService;
    _logger = logger;
     _configuration = configuration;
  }

        public void OnGet()
        {
            // Display a message if the user was redirected here for a specific reason
            if (Request.Query.TryGetValue("reason", out var reasonValues))
            {
                var reason = reasonValues.FirstOrDefault();
                InfoMessage = reason switch
                {
                    "session-timeout" => "Your session has expired due to inactivity. Please log in again.",
                    "concurrent-login" => "You have been logged out because this account was signed into from another location.",
                    _ => null
                };
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Validate reCAPTCHA if enabled
            if (!await ValidateRecaptchaAsync())
            {
                return Page();
            }

            // Attempt to sign in the user
            return await AttemptSignInAsync();
        }

        private async Task<bool> ValidateRecaptchaAsync()
        {
            var recaptchaEnabled = _configuration.GetValue<bool>("RecaptchaSettings:Enabled", true);

            if (!recaptchaEnabled)
            {
                _logger.LogWarning("reCAPTCHA validation skipped - disabled in configuration");
                return true;
            }

            // Check if token is missing
            if (string.IsNullOrEmpty(LModel.RecaptchaToken))
            {
                _logger.LogWarning("reCAPTCHA token is missing for email: {Email}", LModel.Email);
                ModelState.AddModelError(string.Empty, "Security verification is missing. Please refresh and try again.");
                return false;
            }

            // Check if browser blocked reCAPTCHA
            if (LModel.RecaptchaToken == "browser-blocked")
            {
                _logger.LogWarning("Login attempt with browser-blocked reCAPTCHA token for email: {Email}", LModel.Email);
                ModelState.AddModelError(string.Empty,
                    "Your browser's tracking prevention is blocking security verification. " +
                    "Please disable tracking prevention, allow cookies from google.com, or use a different browser.");
                return false;
            }

            _logger.LogInformation("Validating reCAPTCHA token for email: {Email}", LModel.Email);

            var recaptchaResult = await _recaptchaService.VerifyToken(LModel.RecaptchaToken);

            // Check if validation was successful
            if (!recaptchaResult.Success)
            {
                HandleRecaptchaFailure(recaptchaResult);
                return false;
            }

            // Check score threshold
            var minScore = _configuration.GetValue<double>("RecaptchaSettings:MinScore", 0.5);
            if (recaptchaResult.Score < minScore)
            {
                _logger.LogWarning("reCAPTCHA score too low: {Score} (minimum: {MinScore}) for email: {Email}",
                    recaptchaResult.Score, minScore, LModel.Email);
                ModelState.AddModelError(string.Empty,
                    "Security verification failed. Please try again in a few moments.");
                return false;
            }

            _logger.LogInformation("reCAPTCHA validation successful. Score: {Score}", recaptchaResult.Score);
            return true;
        }

        private void HandleRecaptchaFailure(RecaptchaResponse recaptchaResult)
        {
            _logger.LogWarning("reCAPTCHA validation failed. Success=false, Errors: {Errors}",
                recaptchaResult.ErrorCodes != null ? string.Join(", ", recaptchaResult.ErrorCodes) : "None");

            var errorMessage = "Security verification failed. ";

            if (recaptchaResult.ErrorCodes is { Length: > 0 })
            {
                errorMessage += GetRecaptchaErrorMessage(recaptchaResult.ErrorCodes[0]);
            }
            else
            {
                errorMessage += "Please try again.";
            }

            ModelState.AddModelError(string.Empty, errorMessage);
        }

        private static string GetRecaptchaErrorMessage(string errorCode)
        {
            return errorCode switch
            {
                "browser-error" => "Your browser's privacy settings are blocking verification. Please disable tracking prevention or strict privacy mode, or try using Chrome/Safari.",
                "missing-input-secret" => "Server configuration error. Please contact support.",
                "invalid-input-secret" => "Server configuration error. Please contact support.",
                "missing-input-response" => "Verification token is missing.",
                "invalid-input-response" => "Verification token is invalid or expired. Please try again.",
                "bad-request" => "Invalid request. Please refresh and try again.",
                "timeout-or-duplicate" => "Verification expired. Please try again.",
                _ => "Please refresh the page and try again."
            };
        }

        private async Task<IActionResult> AttemptSignInAsync()
        {
            var user = await _userManager.FindByEmailAsync(LModel.Email);

            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent email: {Email}", LModel.Email);
                
                // Log failed login attempt (no user found)
                await _auditService.LogLoginAsync(null, LModel.Email, false, "User not found");
            
                ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your email and password.");
                return Page();
            }

            // Check if account is already locked out
            if (await _userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                var remainingTime = lockoutEnd.HasValue 
                    ? (int)Math.Ceiling((lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes) 
                    : 15;

                _logger.LogWarning("Login attempt for locked out account: {Email}", LModel.Email);
                await _auditService.LogLoginAsync(user.Id, LModel.Email, false, "Account locked out");
                
                ModelState.AddModelError(string.Empty, 
                    $"Account is locked out due to multiple failed login attempts. Please try again in {remainingTime} minutes.");
                return Page();
            }

            // Attempt password sign-in (this does NOT complete sign-in if 2FA is required)
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                LModel.Password,
                LModel.RememberMe,
                lockoutOnFailure: true);

            // Handle 2FA requirement FIRST - this is returned when password is correct but 2FA is enabled
            if (result.RequiresTwoFactor)
  {
     _logger.LogInformation("User {Email} requires 2FA verification", LModel.Email);
 
  return RedirectToPage("/LoginWith2fa", new { rememberMe = LModel.RememberMe });
    }

    if (result.Succeeded)
{
    _logger.LogInformation("User {Email} logged in successfully", LModel.Email);
        await _auditService.LogLoginAsync(user.Id, LModel.Email, true);

        // Store user session data
        HttpContext.Session.SetString("UserId", user.Id);
      HttpContext.Session.SetString("UserEmail", user.Email ?? "");
        HttpContext.Session.SetString("LoginTime", DateTime.UtcNow.ToString("O"));

     // Increment SessionVersion to invalidate other sessions
        user.SessionVersion++;
        await _userManager.UpdateAsync(user);

        // Create a new principal with the SessionVersion claim
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
  await HttpContext.SignInAsync(
     IdentityConstants.ApplicationScheme,
     principal,
 new AuthenticationProperties
  {
         IsPersistent = LModel.RememberMe,
          IssuedUtc = DateTimeOffset.UtcNow
  });

     return RedirectToPage("/Index");
    }

    // Handle other failure cases
    if (result.IsLockedOut)
    {
        _logger.LogWarning("User {Email} account is now locked out", LModel.Email);
    await _auditService.LogAsync(
      AuditActions.Lockout,
user.Id,
        LModel.Email,
              "Account locked out after multiple failed login attempts",
         false);
  
      ModelState.AddModelError(string.Empty, 
  "Account locked out due to too many failed login attempts. Please try again in 15 minutes.");
            }
else
    {
        _logger.LogWarning("Invalid login attempt for email: {Email}", LModel.Email);
                
     var failedCount = await _userManager.GetAccessFailedCountAsync(user);
  var remainingAttempts = 3 - failedCount;
          
        await _auditService.LogLoginAsync(user.Id, LModel.Email, false, "Invalid password");
     
     if (remainingAttempts > 0)
     {
 ModelState.AddModelError(string.Empty, 
  $"Invalid login attempt. {remainingAttempts} attempt(s) remaining before account lockout.");
    }
   else
    {
      ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your email and password.");
  }
       }

          return Page();
        }
    }
}
