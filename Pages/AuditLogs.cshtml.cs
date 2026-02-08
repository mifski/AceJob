using AceJob.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace AceJob.Pages
{
    [Authorize]
    public class AuditLogsModel : PageModel
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<AuditLogsModel> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuditLogsModel(AuthDbContext context, ILogger<AuditLogsModel> logger, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // Filters (bound from query string)
        [BindProperty(SupportsGet = true)]
        public string? ActionFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? From { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? To { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "all"; // all, success, failure

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 20;

        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public List<AuditLog> Logs { get; set; } = new();

        public List<string> AvailableActions { get; set; } = new();

        public async Task OnGetAsync()
        {
            var query = _context.AuditLogs.AsQueryable();

            // Determine current user and roles
            var currentUserId = _userManager.GetUserId(User);
            var isAdminOrHr = User.IsInRole("Admin") || User.IsInRole("HR");

            // Actions list for filter dropdown - if not admin, only include actions for this user
            var actionsQuery = _context.AuditLogs.AsQueryable();
            if (!isAdminOrHr && currentUserId != null)
            {
                actionsQuery = actionsQuery.Where(a => a.UserId == currentUserId);
            }

            AvailableActions = await actionsQuery
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            // If non-admin, restrict logs to current user only
            if (!isAdminOrHr && currentUserId != null)
            {
                query = query.Where(a => a.UserId == currentUserId);
            }

            // Apply filters
            if (!string.IsNullOrWhiteSpace(ActionFilter))
            {
                query = query.Where(a => a.Action == ActionFilter);
            }

            if (From.HasValue)
            {
                query = query.Where(a => a.Timestamp >= From.Value.ToUniversalTime());
            }

            if (To.HasValue)
            {
                var toUtc = To.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
                query = query.Where(a => a.Timestamp <= toUtc);
            }

            if (StatusFilter == "success")
            {
                query = query.Where(a => a.IsSuccess == true);
            }
            else if (StatusFilter == "failure")
            {
                query = query.Where(a => a.IsSuccess == false);
            }

            // Order by newest first
            query = query.OrderByDescending(a => a.Timestamp);

            TotalCount = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > TotalPages && TotalPages > 0) Page = TotalPages;

            Logs = await query.Skip((Page - 1) * PageSize).Take(PageSize).ToListAsync();
        }
    }
}
