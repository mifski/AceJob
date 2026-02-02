using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using AceJob.Model;
using AceJob.Helpers;
using AceJob.Services;

namespace AceJob.Pages
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IAuditService _auditService;

        // User display properties
        public string UserFirstName { get; set; } = string.Empty;
        public string UserLastName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserGender { get; set; } = string.Empty;
        public string MaskedNRIC { get; set; } = string.Empty;
        public string DecryptedNRIC { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }
        public string WhoAmI { get; set; } = string.Empty;
        public string ResumeURL { get; set; } = string.Empty;
        public DateTime? LastLoginTime { get; set; }
        public IEnumerable<AuditLog> RecentActivity { get; set; } = new List<AuditLog>();

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            IAuditService auditService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _auditService = auditService;
        }

        public async Task OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    // Basic user info
                    UserFirstName = user.FirstName;
                    UserLastName = user.LastName;
                    UserEmail = user.Email ?? "";
                    UserGender = user.Gender;
                    DateOfBirth = user.DateOfBirth;
                    WhoAmI = user.WhoAmI;
                    ResumeURL = user.ResumeURL;

                    // Decrypt NRIC for display
                    var encryptionKey = _configuration["Encryption:Key"] ?? "";
                    var encryptionIV = _configuration["Encryption:IV"] ?? "";

                    if (!string.IsNullOrEmpty(user.NRIC))
                    {
                        try
                        {
                            DecryptedNRIC = EncryptionHelper.Decrypt(user.NRIC, encryptionKey, encryptionIV);
                            MaskedNRIC = EncryptionHelper.MaskNRIC(DecryptedNRIC);
                        }
                        catch
                        {
                            MaskedNRIC = "Error decrypting";
                            DecryptedNRIC = "";
                        }
                    }

                    // Get login time from session
                    var loginTimeStr = HttpContext.Session.GetString("LoginTime");
                    if (!string.IsNullOrEmpty(loginTimeStr) && DateTime.TryParse(loginTimeStr, out var loginTime))
                    {
                        LastLoginTime = loginTime.ToLocalTime();
                    }

                    // Get recent activity
                    RecentActivity = await _auditService.GetUserAuditLogsAsync(user.Id, 5);

                    // Log profile view
                    await _auditService.LogProfileViewAsync(user.Id, user.Email ?? "");
                }
            }
        }
    }
}
