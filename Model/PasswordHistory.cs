using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AceJob.Model
{
    /// <summary>
    /// Stores password history for preventing password reuse
    /// </summary>
    public class PasswordHistory
 {
        [Key]
        public int Id { get; set; }

        /// <summary>
  /// User ID this password history belongs to
        /// </summary>
     [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

      /// <summary>
        /// The hashed password
        /// </summary>
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// When this password was created/changed
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
/// Navigation property to ApplicationUser
        /// </summary>
        [ForeignKey("UserId")]
    public virtual ApplicationUser? User { get; set; }
    }
}
