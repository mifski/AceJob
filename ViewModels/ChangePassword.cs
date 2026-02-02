using System.ComponentModel.DataAnnotations;

namespace AceJob.ViewModels
{
    public class ChangePassword
  {
     [Required(ErrorMessage = "Current password is required")]
 [DataType(DataType.Password)]
      [Display(Name = "Current Password")]
     public string CurrentPassword { get; set; } = string.Empty;

      [Required(ErrorMessage = "New password is required")]
        [DataType(DataType.Password)]
     [Display(Name = "New Password")]
    [StringLength(100, ErrorMessage = "Password must be at least {2} characters long.", MinimumLength = 12)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your new password")]
 [DataType(DataType.Password)]
      [Display(Name = "Confirm New Password")]
[Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
   public string ConfirmPassword { get; set; } = string.Empty;
    }
}
