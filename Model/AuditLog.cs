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
        public const string Login = "Login";
        public const string Logout = "Logout";
        public const string FailedLogin = "FailedLogin";
        public const string Lockout = "Lockout";
        public const string LockoutRecovery = "LockoutRecovery";
        public const string Registration = "Registration";
        public const string ConcurrentLogin = "ConcurrentLogin";
        
        // Password Management Actions
        // Use non-sensitive short codes as values to avoid storing the literal word "password" in logs
        public const string PasswordChange = "PW_CHG";
        public const string PasswordReset = "PW_RST";
        public const string PasswordResetRequest = "PW_RST_REQ";
        public const string ForcedPasswordChange = "PW_CHG_FORCED";
        
        // Two-Factor Authentication Actions
        public const string TwoFactorEnabled = "2FA_Enabled";
        public const string TwoFactorDisabled = "2FA_Disabled";
        public const string TwoFactorSetup = "2FA_Setup";
        public const string TwoFactorLogin = "2FA_Login";
        public const string TwoFactorRecoveryCodes = "2FA_RecoveryCodes";
        public const string TwoFactorRecoveryUsed = "2FA_RecoveryUsed";
   
        // Profile Actions
        public const string ProfileView = "ProfileView";
        public const string ProfileUpdate = "ProfileUpdate";
     
        // Security Events
        public const string SecurityBreach = "SecurityBreach";
        public const string SuspiciousActivity = "SuspiciousActivity";
        public const string ReCaptchaFailure = "ReCaptchaFailure";
      
        // File Operations
        public const string FileUpload = "FileUpload";
        public const string FileDownload = "FileDownload";
        public const string FileDelete = "FileDelete";
        
        // Administrative Actions
        public const string AdminAction = "AdminAction";
        public const string UserCreated = "UserCreated";
        public const string UserUpdated = "UserUpdated";
        public const string UserDeleted = "UserDeleted";
        public const string RoleAssigned = "RoleAssigned";
        public const string RoleRemoved = "RoleRemoved";
    }
}
