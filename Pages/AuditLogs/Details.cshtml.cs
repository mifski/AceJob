using AceJob.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;

namespace AceJob.Pages.AuditLogs
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly AuthDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(AuthDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public AuditLog? Log { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Log = await _context.AuditLogs.FindAsync(id);
            if (Log == null) return NotFound();

            // Check ownership or admin/HR role
            var currentUserId = _userManager.GetUserId(User);
            var isAdminOrHr = User.IsInRole("Admin") || User.IsInRole("HR");
            if (!isAdminOrHr && Log.UserId != currentUserId)
            {
                return Forbid();
            }

            return Page();
        }
    }
}
