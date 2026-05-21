using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Portal;

namespace TruLoad.Backend.Data.Configurations.Portal
{
    /// <summary>
    /// Module-specific DbContext configuration for Portal entities.
    /// </summary>
    public static class PortalModuleDbContextConfiguration
    {
        /// <summary>
        /// Applies all portal module entity configurations to the model builder.
        /// </summary>
        public static void ApplyPortalConfigurations(this ModelBuilder modelBuilder)
        {
            // PortalTeamMembership entity configuration
            modelBuilder.Entity<PortalTeamMembership>(entity =>
            {
                entity.ToTable("portal_team_memberships", "weighing");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("uuid")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.TransporterId)
                    .HasColumnName("transporter_id")
                    .HasColumnType("uuid")
                    .IsRequired();

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("uuid")
                    .IsRequired();

                entity.Property(e => e.UserEmail)
                    .HasColumnName("user_email")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(e => e.UserName)
                    .HasColumnName("user_name")
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.Role)
                    .HasColumnName("role")
                    .HasMaxLength(20)
                    .HasDefaultValue("viewer")
                    .IsRequired();

                entity.Property(e => e.InvitedByUserId)
                    .HasColumnName("invited_by_user_id")
                    .HasColumnType("uuid");

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasColumnType("timestamptz")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasColumnType("timestamptz")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // FK: TransporterId → Transporters(Id) ON DELETE CASCADE
                entity.HasOne(e => e.Transporter)
                    .WithMany()
                    .HasForeignKey(e => e.TransporterId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique index: (TransporterId, UserId) where IsActive
                entity.HasIndex(e => new { e.TransporterId, e.UserId })
                    .IsUnique()
                    .HasDatabaseName("IX_portal_team_memberships_transporter_user_active")
                    .HasFilter("is_active = true");

                // Index: UserId for lookup
                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_portal_team_memberships_user_id");
            });

            // PortalTeamInvitation entity configuration
            modelBuilder.Entity<PortalTeamInvitation>(entity =>
            {
                entity.ToTable("portal_team_invitations", "weighing");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("uuid")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.TransporterId)
                    .HasColumnName("transporter_id")
                    .HasColumnType("uuid")
                    .IsRequired();

                entity.Property(e => e.InvitedEmail)
                    .HasColumnName("invited_email")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(e => e.Role)
                    .HasColumnName("role")
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.Token)
                    .HasColumnName("token")
                    .HasMaxLength(128)
                    .IsRequired();

                entity.Property(e => e.CreatedByUserId)
                    .HasColumnName("created_by_user_id")
                    .HasColumnType("uuid");

                entity.Property(e => e.ExpiresAt)
                    .HasColumnName("expires_at")
                    .HasColumnType("timestamptz");

                entity.Property(e => e.AcceptedAt)
                    .HasColumnName("accepted_at")
                    .HasColumnType("timestamptz");

                entity.Property(e => e.IsRevoked)
                    .HasColumnName("is_revoked")
                    .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasColumnType("timestamptz")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // FK: TransporterId → Transporters(Id)
                entity.HasOne(e => e.Transporter)
                    .WithMany()
                    .HasForeignKey(e => e.TransporterId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique index on Token
                entity.HasIndex(e => e.Token)
                    .IsUnique()
                    .HasDatabaseName("IX_portal_team_invitations_token");

                entity.HasIndex(e => e.TransporterId)
                    .HasDatabaseName("IX_portal_team_invitations_transporter_id");

                entity.HasIndex(e => e.InvitedEmail)
                    .HasDatabaseName("IX_portal_team_invitations_invited_email");
            });
        }
    }
}
