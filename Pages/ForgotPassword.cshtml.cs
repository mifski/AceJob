using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AceJob.Model;
using AceJob.Services;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace AceJob.Pages
{
 public class ForgotPasswordModel : PageModel
 {
 private readonly UserManager<ApplicationUser> _userManager;
 private readonly GmailEmailService _emailService;
 private readonly ILogger<ForgotPasswordModel> _logger;

 public ForgotPasswordModel(
 UserManager<ApplicationUser> userManager,
 GmailEmailService emailService,
 ILogger<ForgotPasswordModel> logger)
 {
 _userManager = userManager;
 _emailService = emailService;
 _logger = logger;
 }

 [BindProperty]
 public InputModel Input { get; set; } = new InputModel();

 public string Message { get; set; }

 public class InputModel
 {
 [Required]
 public string Contact { get; set; } = string.Empty;
 }

 public void OnGet()
 {
 }

 public async Task<IActionResult> OnPostAsync()
 {
 if (!ModelState.IsValid)
 return Page();

 var contact = Input.Contact?.Trim();
 if (string.IsNullOrEmpty(contact))
 {
 ModelState.AddModelError(string.Empty, "Please enter an email address.");
 return Page();
 }

 if (contact.Contains("@"))
 {
 // treat as email
 var user = await _userManager.FindByEmailAsync(contact);
 if (user == null)
 {
 // Do not reveal that the user does not exist
 Message = "If an account with that email exists, a reset link will be sent.";
 return Page();
 }

 var token = await _userManager.GeneratePasswordResetTokenAsync(user);
 var url = Url.Page("/ResetPassword", null, new { userId = user.Id, token }, Request.Scheme);

 // Send email
 var html = $"<p>Click <a href=\"{url}\">here</a> to reset your password. If you did not request this, ignore this message.</p>";
 await _emailService.SendEmailAsync(user.Email!, "Reset your password", html);

 Message = "If an account with that email exists, a reset link will be sent.";
 return Page();
 }

 // Phone input handling removed - SMS is no longer supported
 _logger.LogInformation("ForgotPassword requested for phone number but SMS is disabled. Input: {Contact}", contact);
 Message = "If an account with that phone exists, a reset link will be sent (SMS support is disabled).";
 return Page();
 }
 }
}
