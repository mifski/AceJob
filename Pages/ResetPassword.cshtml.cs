using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AceJob.Model;

namespace AceJob.Pages
{
 public class ResetPasswordModel : PageModel
 {
 private readonly UserManager<ApplicationUser> _userManager;

 public ResetPasswordModel(UserManager<ApplicationUser> userManager)
 {
 _userManager = userManager;
 }

 [BindProperty]
 public InputModel Input { get; set; }

 public string Message { get; set; }

 public class InputModel
 {
 public string UserId { get; set; }
 public string Token { get; set; }

 [Required]
 [DataType(DataType.Password)]
 public string Password { get; set; }

 [DataType(DataType.Password)]
 [Compare("Password", ErrorMessage = "Passwords do not match")]
 public string ConfirmPassword { get; set; }
 }

 public void OnGet(string userId, string token)
 {
 Input = new InputModel { UserId = userId, Token = token };
 }

 public async Task<IActionResult> OnPostAsync()
 {
 if (!ModelState.IsValid)
 return Page();

 var user = await _userManager.FindByIdAsync(Input.UserId);
 if (user == null)
 {
 // Do not reveal
 Message = "Password reset failed.";
 return Page();
 }

 var result = await _userManager.ResetPasswordAsync(user, Input.Token, Input.Password);
 if (result.Succeeded)
 {
 Message = "Password has been reset. You may now log in.";
 return Page();
 }

 foreach (var err in result.Errors)
 ModelState.AddModelError(string.Empty, err.Description);

 return Page();
 }
 }
}
