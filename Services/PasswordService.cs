using AceJob.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AceJob.Services
{
    /// <summary>
    /// Service interface for password management
    /// </summary>
    public interface IPasswordService
 {
   /// <summary>
  /// Check if password was used in the last N passwords
        /// </summary>
        Task<bool> IsPasswordInHistoryAsync(string userId, string newPassword, int historyCount = 2);

        /// <summary>
     /// Add password to history
        /// </summary>
   Task AddToHistoryAsync(string userId, string passwordHash);

        /// <summary>
     /// Check if password has expired (max age exceeded)
        /// </summary>
  Task<bool> IsPasswordExpiredAsync(ApplicationUser user);

        /// <summary>
  /// Check if password can be changed (min age not reached)
      /// </summary>
        Task<bool> CanChangePasswordAsync(ApplicationUser user);

        /// <summary>
        /// Get days until password expires
        /// </summary>
        int GetDaysUntilExpiry(ApplicationUser user);

   /// <summary>
        /// Clean up old password history entries
   /// </summary>
        Task CleanupOldHistoryAsync(string userId, int keepCount = 2);
    }

    /// <summary>
    /// Implementation of password management service
    /// </summary>
    public class PasswordService : IPasswordService
    {
        private readonly AuthDbContext _context;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PasswordService> _logger;

        // Default password age settings (can be overridden in configuration)
        private readonly int _maxPasswordAgeDays;
        private readonly int _minPasswordAgeDays;
    private readonly int _passwordHistoryCount;

        public PasswordService(
    AuthDbContext context,
     IPasswordHasher<ApplicationUser> passwordHasher,
      IConfiguration configuration,
        ILogger<PasswordService> logger)
        {
         _context = context;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _logger = logger;

     // Load configuration
 _maxPasswordAgeDays = _configuration.GetValue<int>("PasswordPolicy:MaxAgeDays", 90);
          _minPasswordAgeDays = _configuration.GetValue<int>("PasswordPolicy:MinAgeDays", 1);
            _passwordHistoryCount = _configuration.GetValue<int>("PasswordPolicy:HistoryCount", 2);
        }

        /// <summary>
        /// Check if the new password matches any of the last N passwords
        /// </summary>
        public async Task<bool> IsPasswordInHistoryAsync(string userId, string newPassword, int historyCount = 2)
        {
            var count = historyCount > 0 ? historyCount : _passwordHistoryCount;

    var recentPasswords = await _context.PasswordHistories
                .Where(ph => ph.UserId == userId)
            .OrderByDescending(ph => ph.CreatedAt)
    .Take(count)
     .ToListAsync();

        // Create a temporary user for password verification
            var tempUser = new ApplicationUser { Id = userId };

            foreach (var history in recentPasswords)
        {
      var result = _passwordHasher.VerifyHashedPassword(tempUser, history.PasswordHash, newPassword);
   if (result == PasswordVerificationResult.Success || 
        result == PasswordVerificationResult.SuccessRehashNeeded)
    {
           _logger.LogWarning("Password reuse detected for user {UserId}", userId);
    return true;
         }
      }

            return false;
        }

        /// <summary>
        /// Add the current password hash to history
        /// </summary>
        public async Task AddToHistoryAsync(string userId, string passwordHash)
        {
            var history = new PasswordHistory
            {
          UserId = userId,
   PasswordHash = passwordHash,
    CreatedAt = DateTime.UtcNow
 };

     _context.PasswordHistories.Add(history);
          await _context.SaveChangesAsync();

        _logger.LogInformation("Password added to history for user {UserId}", userId);

        // Clean up old entries
         await CleanupOldHistoryAsync(userId, _passwordHistoryCount);
        }

        /// <summary>
  /// Check if password has exceeded maximum age
    /// </summary>
        public async Task<bool> IsPasswordExpiredAsync(ApplicationUser user)
 {
     if (user.PasswordChangedDate == null)
   {
              // If no password change date, consider it expired to force change
     return true;
            }

            var daysSinceChange = (DateTime.UtcNow - user.PasswordChangedDate.Value).TotalDays;
            var isExpired = daysSinceChange > _maxPasswordAgeDays;

            if (isExpired)
       {
       _logger.LogWarning("Password expired for user {UserId}. Days since change: {Days}", 
     user.Id, (int)daysSinceChange);
            }

        return await Task.FromResult(isExpired);
        }

 /// <summary>
        /// Check if minimum password age has been reached (can change password)
      /// </summary>
        public async Task<bool> CanChangePasswordAsync(ApplicationUser user)
        {
     if (user.PasswordChangedDate == null)
   {
        // No previous change, can change anytime
                return await Task.FromResult(true);
    }

   var daysSinceChange = (DateTime.UtcNow - user.PasswordChangedDate.Value).TotalDays;
   var canChange = daysSinceChange >= _minPasswordAgeDays;

if (!canChange)
 {
    _logger.LogWarning("Password change rejected for user {UserId}. Minimum age not reached. Days: {Days}", 
  user.Id, (int)daysSinceChange);
            }

     return canChange;
        }

    /// <summary>
        /// Get number of days until password expires
        /// </summary>
        public int GetDaysUntilExpiry(ApplicationUser user)
   {
            if (user.PasswordChangedDate == null)
            {
                return 0; // Already expired
            }

          var daysSinceChange = (DateTime.UtcNow - user.PasswordChangedDate.Value).TotalDays;
  var daysRemaining = _maxPasswordAgeDays - (int)daysSinceChange;

            return Math.Max(0, daysRemaining);
        }

        /// <summary>
        /// Remove old password history entries, keeping only the most recent N
        /// </summary>
        public async Task CleanupOldHistoryAsync(string userId, int keepCount = 2)
        {
     var allHistory = await _context.PasswordHistories
    .Where(ph => ph.UserId == userId)
                .OrderByDescending(ph => ph.CreatedAt)
       .ToListAsync();

    if (allHistory.Count > keepCount)
            {
         var toRemove = allHistory.Skip(keepCount).ToList();
_context.PasswordHistories.RemoveRange(toRemove);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} old password history entries for user {UserId}", 
               toRemove.Count, userId);
          }
        }
    }
}
