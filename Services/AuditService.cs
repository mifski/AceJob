using AceJob.Model;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace AceJob.Services
{
    /// <summary>
    /// Service interface for audit logging
    /// </summary>
    public interface IAuditService
 {
 Task LogAsync(string action, string? userId, string? userEmail, string? description, bool isSuccess, string? additionalData = null);
 Task LogLoginAsync(string? userId, string email, bool isSuccess, string? failureReason = null);
 Task LogLogoutAsync(string userId, string email);
 Task LogRegistrationAsync(string userId, string email);
 Task LogProfileViewAsync(string userId, string email);
 Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, int count =10);
 }

 /// <summary>
 /// Implementation of audit logging service
 /// </summary>
 public class AuditService : IAuditService
 {
 private readonly AuthDbContext _context;
 private readonly IHttpContextAccessor _httpContextAccessor;
 private readonly ILogger<AuditService> _logger;

 public AuditService(
 AuthDbContext context,
 IHttpContextAccessor httpContextAccessor,
 ILogger<AuditService> logger)
 {
 _context = context;
 _httpContextAccessor = httpContextAccessor;
 _logger = logger;
 }

 /// <summary>
 /// Hash email address for privacy protection in logs
 /// </summary>
 private string HashEmail(string email)
 {
 if (string.IsNullOrEmpty(email))
 return "unknown";

 using (var sha256 = SHA256.Create())
 {
 var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(email));
 return Convert.ToBase64String(hashBytes).Substring(0, 8);
 }
 }

 /// <summary>
 /// Get the user agent from the HTTP context
 /// </summary>
 private string? GetUserAgent()
 {
 var httpContext = _httpContextAccessor.HttpContext;
 var userAgent = httpContext?.Request.Headers["User-Agent"].FirstOrDefault();

 // Truncate if too long
 if (userAgent?.Length >500)
 {
 userAgent = userAgent.Substring(0,500);
 }

 return userAgent;
 }

 /// <summary>
 /// Log an action to the audit log
 /// </summary>
 public async Task LogAsync(
 string action,
 string? userId,
 string? userEmail,
 string? description,
 bool isSuccess,
 string? additionalData = null)
 {
 try
 {
 var auditLog = new AuditLog
 {
 Action = action,
 UserId = userId,
 UserEmail = userEmail,
 Description = description,
 UserAgent = GetUserAgent(),
 Timestamp = DateTime.UtcNow,
 IsSuccess = isSuccess,
 AdditionalData = additionalData
 };

 _context.AuditLogs.Add(auditLog);
 await _context.SaveChangesAsync();

 // Hash email for logging to prevent exposure
 var hashedEmail = userEmail != null ? HashEmail(userEmail) : "Unknown";
 _logger.LogInformation(
 "Audit: {Action} by user hash {EmailHash} - Success: {IsSuccess}",
 action, hashedEmail, isSuccess);
 }
 catch (Exception ex)
 {
 // Don't let audit logging failures break the application
 _logger.LogError(ex, "Failed to save audit log for action: {Action}", action);
 }
 }

 /// <summary>
 /// Log a login attempt
 /// </summary>
 public async Task LogLoginAsync(string? userId, string email, bool isSuccess, string? failureReason = null)
 {
 var description = isSuccess
 ? "User logged in successfully"
 : $"Login failed: {failureReason ?? "Invalid credentials"}";

 var action = isSuccess ? AuditActions.Login : AuditActions.FailedLogin;

 await LogAsync(action, userId, email, description, isSuccess);
 }

 /// <summary>
 /// Log a logout action
 /// </summary>
 public async Task LogLogoutAsync(string userId, string email)
 {
 await LogAsync(
 AuditActions.Logout,
 userId,
 email,
 "User logged out",
 isSuccess: true);
 }

 /// <summary>
 /// Log a registration
 /// </summary>
 public async Task LogRegistrationAsync(string userId, string email)
 {
 await LogAsync(
 AuditActions.Registration,
 userId,
 email,
 "New user registered",
 isSuccess: true);
 }

 /// <summary>
 /// Log a profile view
 /// </summary>
 public async Task LogProfileViewAsync(string userId, string email)
 {
 await LogAsync(
 AuditActions.ProfileView,
 userId,
 email,
 "User viewed their profile",
 isSuccess: true);
 }

 /// <summary>
 /// Get recent audit logs for a user
 /// </summary>
 public async Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, int count =10)
 {
 return await Task.FromResult(
 _context.AuditLogs
 .Where(a => a.UserId == userId)
 .OrderByDescending(a => a.Timestamp)
 .Take(count)
 .ToList());
 }
 }
}
