using AceJob.Model;
using AceJob.ViewModels;
using AceJob.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AceJob.Pages
{
    [Authorize]
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
      private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IPasswordService _passwordService;
private readonly IAuditService _auditService;
        private readonly ILogger<ChangePasswordModel> _logger;

        [BindProperty]
        public ChangePassword ChangeModel { get; set; } = new();

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
  public int? DaysUntilExpiry { get; set; }

        public ChangePasswordModel(
            UserManager<ApplicationUser> userManager,
 SignInManager<ApplicationUser> signInManager,
 IPasswordService passwordService,
    IAuditService auditService,
 ILogger<ChangePasswordModel> logger)
   {
            _userManager = userManager;
     _signInManager = signInManager;
        _passwordService = passwordService;
       _auditService = auditService;
   _logger = logger;
        }

 public async Task<IActionResult> OnGetAsync()
        {
    var user = await _userManager.GetUserAsync(User);
     if (user == null)
     {
       return RedirectToPage("/Login");
            }

    // Check password expiry warning
       DaysUntilExpiry = _passwordService.GetDaysUntilExpiry(user);

     return Page();
        }

 public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
  {
   return Page();
            }

      var user = await _userManager.GetUserAsync(User);
   if (user == null)
     {
     return RedirectToPage("/Login");
  }

            // Check minimum password age
 if (!await _passwordService.CanChangePasswordAsync(user))
   {
   ErrorMessage = "You cannot change your password yet. Please wait at least 1 day after your last password change.";
  await _auditService.LogAsync(
      AuditActions.PasswordChange,
     user.Id,
 user.Email,
 "Password change rejected - minimum age not reached",
   false);
    return Page();
   }

     // Verify current password
var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, ChangeModel.CurrentPassword);
     if (!isCurrentPasswordValid)
   {
          ModelState.AddModelError("ChangeModel.CurrentPassword", "Current password is incorrect.");
       await _auditService.LogAsync(
  AuditActions.PasswordChange,
     user.Id,
  user.Email,
       "Password change failed - incorrect current password",
 false);
      return Page();
            }

    // Check if new password is same as current
   if (ChangeModel.CurrentPassword == ChangeModel.NewPassword)
    {
     ModelState.AddModelError("ChangeModel.NewPassword", "New password must be different from current password.");
   return Page();
   }

     // Check password history
 if (await _passwordService.IsPasswordInHistoryAsync(user.Id, ChangeModel.NewPassword))
  {
      ModelState.AddModelError("ChangeModel.NewPassword", 
        "This password has been used recently. Please choose a different password.");
 await _auditService.LogAsync(
   AuditActions.PasswordChange,
    user.Id,
       user.Email,
           "Password change rejected - password in history",
 false);
           return Page();
          }

    // Change the password
       var result = await _userManager.ChangePasswordAsync(user, ChangeModel.CurrentPassword, ChangeModel.NewPassword);

  if (result.Succeeded)
            {
   // Add old password to history before updating
     await _passwordService.AddToHistoryAsync(user.Id, user.PasswordHash!);

         // Update password changed date
  user.PasswordChangedDate = DateTime.UtcNow;
user.MustChangePassword = false;
     await _userManager.UpdateAsync(user);

    // Refresh sign-in cookie
    await _signInManager.RefreshSignInAsync(user);

    _logger.LogInformation("User {Email} changed their password successfully", user.Email);
   
  await _auditService.LogAsync(
     AuditActions.PasswordChange,
  user.Id,
          user.Email,
            "Password changed successfully",
  true);

       SuccessMessage = "Your password has been changed successfully!";
         DaysUntilExpiry = _passwordService.GetDaysUntilExpiry(user);

       return Page();
        }

    // Handle errors
 foreach (var error in result.Errors)
  {
     ModelState.AddModelError(string.Empty, error.Description);
   }

     await _auditService.LogAsync(
     AuditActions.PasswordChange,
        user.Id,
          user.Email,
 $"Password change failed - {string.Join(", ", result.Errors.Select(e => e.Description))}",
      false);

 return Page();
        }
  }
}
