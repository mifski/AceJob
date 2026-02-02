using AceJob.Model;
using AceJob.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AceJob.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService,
            ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
            _logger = logger;
        }

        // Handle GET requests (when clicking logout link) - Show confirmation page
        public void OnGet()
        {
            // Just display the confirmation page, don't logout yet
        }

        // Handle POST requests (when clicking "Yes, Logout" button)
        public async Task<IActionResult> OnPostAsync()
        {
            // Get current user info before signing out (for audit log)
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;
            var userEmail = user?.Email;

            // Log the logout event
            if (userId != null && userEmail != null)
            {
                await _auditService.LogLogoutAsync(userId, userEmail);
                _logger.LogInformation("User {Email} logged out", userEmail);
            }

            // Clear all session data
            HttpContext.Session.Clear();

            // Delete the authentication cookie and sign out
            await _signInManager.SignOutAsync();

            // Delete any additional cookies
            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }

            _logger.LogInformation("Session cleared and user signed out");

            // Redirect to login page after logout
            return RedirectToPage("/Login");
        }

        /// <summary>
        /// Handle GET request for immediate logout (e.g., from navbar link)
        /// Redirects to POST for proper logout
        /// </summary>
        public async Task<IActionResult> OnGetLogoutAsync()
        {
            // Perform immediate logout via GET (for convenience)
            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                await _auditService.LogLogoutAsync(user.Id, user.Email ?? "");
                _logger.LogInformation("User {Email} logged out via GET", user.Email);
            }

            HttpContext.Session.Clear();
            await _signInManager.SignOutAsync();

            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }

            return RedirectToPage("/Login");
        }
    }
}
