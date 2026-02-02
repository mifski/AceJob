using AceJob.Model;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AceJob.Model
{
    public class AuthDbContext : IdentityDbContext<ApplicationUser>
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Audit logs for tracking user activities
        /// </summary>
        public DbSet<AuditLog> AuditLogs { get; set; }

        /// <summary>
        /// Password history for preventing password reuse
        /// </summary>
        public DbSet<PasswordHistory> PasswordHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure AuditLog entity
            builder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.Action);
            });

            // Configure PasswordHistory entity
            builder.Entity<PasswordHistory>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(ph => ph.User)
                    .WithMany(u => u.PasswordHistories)
                    .HasForeignKey(ph => ph.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}

