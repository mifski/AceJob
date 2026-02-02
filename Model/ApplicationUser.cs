using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace AceJob.Model
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string NRIC { get; set; } = string.Empty; // Will be encrypted in database
        public DateOnly DateOfBirth { get; set; }
        public string ResumeURL { get; set; } = string.Empty;
        public string WhoAmI { get; set; } = string.Empty;

        // Password age tracking
        /// <summary>
        /// When the password was last changed
        /// </summary>
        public DateTime? PasswordChangedDate { get; set; }

        /// <summary>
        /// Whether the user must change password on next login
        /// </summary>
        public bool MustChangePassword { get; set; } = false;

        // Navigation property for password history
        public virtual ICollection<PasswordHistory> PasswordHistories { get; set; } = new List<PasswordHistory>();

        /// <summary>
        /// Incremented on each successful login. Used to detect and invalidate older sessions (multi-login detection).
        /// </summary>
        public int SessionVersion { get; set; } = 0;
    }
}
