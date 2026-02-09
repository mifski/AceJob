using System.ComponentModel.DataAnnotations;

namespace AceJob.Model
{
    /// <summary>
    /// Audit log entity for tracking user activities
    /// </summary>
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// User ID (null for anonymous actions like failed login attempts)
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Email or username associated with the action
        /// </summary>
        [MaxLength(256)]
        public string? UserEmail { get; set; }

        /// <summary>
        /// Type of action performed (Login, Logout, FailedLogin, Registration, PasswordChange, etc.)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of the action
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// User agent (browser info)
        /// </summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Timestamp of the action
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the action was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Additional data in JSON format (optional)
        /// </summary>
        public string? AdditionalData { get; set; }
    }

    /// <summary>
    /// Common audit action types
    /// </summary>
    public static class AuditActions
    {
        // Authentication Actions
        public static readonly string Login = "Login";
        public static readonly string Logout = "Logout";
        public static readonly string FailedLogin = "FailedLogin";
        public static readonly string Lockout = "Lockout";
        public static readonly string LockoutRecovery = "LockoutRecovery";
        public static readonly string Registration = "Registration";
        public static readonly string ConcurrentLogin = "ConcurrentLogin";
        
        // Password Management Actions
        // Use non-sensitive short codes as values to avoid storing the literal word "password" in logs
        public static readonly string PasswordChange = "PW_CHG";
        public static readonly string PasswordReset = "PW_RST";
        public static readonly string PasswordResetRequest = "PW_RST_REQ";
        public static readonly string ForcedPasswordChange = "PW_CHG_FORCED";
        
        // Two-Factor Authentication Actions
        public static readonly string TwoFactorEnabled = "2FA_Enabled";
        public static readonly string TwoFactorDisabled = "2FA_Disabled";
        public static readonly string TwoFactorSetup = "2FA_Setup";
        public static readonly string TwoFactorLogin = "2FA_Login";
        public static readonly string TwoFactorRecoveryCodes = "2FA_RecoveryCodes";
        public static readonly string TwoFactorRecoveryUsed = "2FA_RecoveryUsed";
   
        // Profile Actions
        public static readonly string ProfileView = "ProfileView";
        public static readonly string ProfileUpdate = "ProfileUpdate";
     
        // Security Events
        public static readonly string SecurityBreach = "SecurityBreach";
        public static readonly string SuspiciousActivity = "SuspiciousActivity";
        public static readonly string ReCaptchaFailure = "ReCaptchaFailure";
      
        // File Operations
        public static readonly string FileUpload = "FileUpload";
        public static readonly string FileDownload = "FileDownload";
        public static readonly string FileDelete = "FileDelete";
        
        // Administrative Actions
        public static readonly string AdminAction = "AdminAction";
        public static readonly string UserCreated = "UserCreated";
        public static readonly string UserUpdated = "UserUpdated";
        public static readonly string UserDeleted = "UserDeleted";
        public static readonly string RoleAssigned = "RoleAssigned";
        public static readonly string RoleRemoved = "RoleRemoved";
    }
}
