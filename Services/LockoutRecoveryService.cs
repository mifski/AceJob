using AceJob.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AceJob.Services
{
 /// <summary>
 /// Interface for lockout recovery service
 /// </summary>
 public interface ILockoutRecoveryService
 {
 /// <summary>
 /// Check and recover users whose lockout period has expired
 /// </summary>
 Task RecoverExpiredLockoutsAsync();

 /// <summary>
 /// Get all users currently locked out
 /// </summary>
 Task<List<ApplicationUser>> GetLockedOutUsersAsync();

 /// <summary>
 /// Manually unlock a specific user
 /// </summary>
 Task UnlockUserAsync(string userId, string? adminUserId = null);
 }

 /// <summary>
 /// Service to handle automatic recovery of account lockouts
 /// </summary>
 public class LockoutRecoveryService : ILockoutRecoveryService
 {
 private readonly UserManager<ApplicationUser> _userManager;
 private readonly IAuditService _auditService;
 private readonly ILogger<LockoutRecoveryService> _logger;

 public LockoutRecoveryService(
 UserManager<ApplicationUser> userManager,
 IAuditService auditService,
 ILogger<LockoutRecoveryService> logger)
 {
 _userManager = userManager;
 _auditService = auditService;
 _logger = logger;
 }

 /// <summary>
 /// Check and automatically unlock users whose lockout period has naturally expired
 /// </summary>
 public async Task RecoverExpiredLockoutsAsync()
 {
 try
 {
 var lockedOutUsers = await GetLockedOutUsersAsync();
 var recoveredCount =0;

 foreach (var user in lockedOutUsers)
 {
 // Check if lockout has naturally expired
 if (!await _userManager.IsLockedOutAsync(user))
 {
 // Reset failed attempt count
 await _userManager.ResetAccessFailedCountAsync(user);

 // Log the automatic recovery
 await _auditService.LogAsync(
 AuditActions.LockoutRecovery,
 user.Id,
 user.Email,
 "Account lockout automatically expired and was recovered",
 true
 );

 recoveredCount++;
 }
 }

 if (recoveredCount >0)
 {
 _logger.LogInformation("Automatically recovered {Count} expired account lockouts", recoveredCount);
 }
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error occurred during automatic lockout recovery");
 }
 }

 /// <summary>
 /// Get all users that are currently locked out
 /// </summary>
 public async Task<List<ApplicationUser>> GetLockedOutUsersAsync()
 {
 var allUsers = await _userManager.Users.ToListAsync();
 var lockedOutUsers = new List<ApplicationUser>();

 foreach (var user in allUsers)
 {
 if (await _userManager.IsLockedOutAsync(user))
 {
 lockedOutUsers.Add(user);
 }
 }

 return lockedOutUsers;
 }

 /// <summary>
 /// Manually unlock a specific user (typically done by admin)
 /// </summary>
 public async Task UnlockUserAsync(string userId, string? adminUserId = null)
 {
 var user = await _userManager.FindByIdAsync(userId);
 if (user == null)
 {
 _logger.LogWarning("Attempted to unlock non-existent user: {UserId}", userId);
 return;
 }

 if (!await _userManager.IsLockedOutAsync(user))
 {
 _logger.LogInformation("User {UserId} is not locked out", userId);
 return;
 }

 // Set lockout end date to now (effectively unlocking)
 await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(-1));

 // Reset failed attempt count
 await _userManager.ResetAccessFailedCountAsync(user);

 var description = adminUserId != null
 ? $"Account manually unlocked by administrator {adminUserId}"
 : "Account manually unlocked";

 await _audit_service_log_safe(user, description);

 _logger.LogInformation("User {UserId} ({Email}) has been manually unlocked", user.Id, user.Email);
 }

 // Helper to log audit safely (avoid throwing from audit failures)
 private async Task _audit_service_log_safe(ApplicationUser user, string description)
 {
 try
 {
 await _auditService.LogAsync(
 AuditActions.LockoutRecovery,
 user.Id,
 user.Email,
 description,
 true
 );
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "Failed to write lockout recovery audit for user {UserId}", user.Id);
 }
 }
 }

 /// <summary>
 /// Background service to automatically recover expired lockouts
 /// </summary>
 public class LockoutRecoveryBackgroundService : BackgroundService
 {
 private readonly IServiceProvider _serviceProvider;
 private readonly ILogger<LockoutRecoveryBackgroundService> _logger;
 private readonly TimeSpan _interval = TimeSpan.FromMinutes(5); // Check every5 minutes

 public LockoutRecoveryBackgroundService(
 IServiceProvider serviceProvider,
 ILogger<LockoutRecoveryBackgroundService> logger)
 {
 _serviceProvider = serviceProvider;
 _logger = logger;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 _logger.LogInformation("Lockout Recovery Background Service started");

 while (!stoppingToken.IsCancellationRequested)
 {
 try
 {
 using var scope = _serviceProvider.CreateScope();
 var lockoutService = scope.ServiceProvider.GetRequiredService<ILockoutRecoveryService>();

 await lockoutService.RecoverExpiredLockoutsAsync();
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error in lockout recovery background service");
 }

 // Wait for next interval
 await Task.Delay(_interval, stoppingToken);
 }

 _logger.LogInformation("Lockout Recovery Background Service stopped");
 }
 }
}