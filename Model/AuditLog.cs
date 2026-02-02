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
        /// IP address of the user
        /// </summary>
  [MaxLength(45)]
        public string? IpAddress { get; set; }

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
        public const string Login = "Login";
        public const string Logout = "Logout";
   public const string FailedLogin = "FailedLogin";
        public const string Lockout = "Lockout";
        public const string Registration = "Registration";
  public const string PasswordChange = "PasswordChange";
        public const string ProfileUpdate = "ProfileUpdate";
 public const string ProfileView = "ProfileView";
        public const string ConcurrentLogin = "ConcurrentLogin";
    }
}
