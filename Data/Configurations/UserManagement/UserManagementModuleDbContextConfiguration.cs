using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Data.Configurations.UserManagement;

/// <summary>
/// User Management Module DbContext Configuration
/// Contains configurations for user management entities including:
/// - Organizations, Departments
/// - Permissions, RolePermissions
/// - WorkShifts, WorkShiftSchedules, ShiftRotations, RotationShifts, UserShifts
/// - Stations, AuditLogs
/// - Identity entities (ApplicationUser, ApplicationRole, Identity tables)
/// </summary>
public static class UserManagementModuleDbContextConfiguration
{
    /// <summary>
    /// Applies user management module configurations to the model builder
    /// </summary>
    public static ModelBuilder ApplyUserManagementConfigurations(this ModelBuilder modelBuilder)
    {
        // ===== Configure Identity Tables with snake_case naming =====

        // AspNetUsers
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("asp_net_users");

            entity.Property(e => e.FullName)
                .HasColumnName("full_name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.StationId)
                .HasColumnName("station_id");

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id");

            entity.Property(e => e.DepartmentId)
                .HasColumnName("department_id");

            entity.Property(e => e.LastLoginAt)
                .HasColumnName("last_login_at");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Standard Identity properties with snake_case
            entity.Property(e => e.UserName).HasColumnName("user_name");
            entity.Property(e => e.NormalizedUserName).HasColumnName("normalized_user_name");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.NormalizedEmail).HasColumnName("normalized_email");
            entity.Property(e => e.EmailConfirmed).HasColumnName("email_confirmed");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.SecurityStamp).HasColumnName("security_stamp");
            entity.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number");
            entity.Property(e => e.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
            entity.Property(e => e.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            entity.Property(e => e.LockoutEnd).HasColumnName("lockout_end");
            entity.Property(e => e.LockoutEnabled).HasColumnName("lockout_enabled");
            entity.Property(e => e.AccessFailedCount).HasColumnName("access_failed_count");

            // Indexes
            entity.HasIndex(e => e.StationId).HasDatabaseName("idx_users_station_id");
            entity.HasIndex(e => e.OrganizationId).HasDatabaseName("idx_users_organization_id");
            entity.HasIndex(e => e.DepartmentId).HasDatabaseName("idx_users_department_id");

            // Relationships
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Department)
                .WithMany()
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Station)
                .WithMany()
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AspNetRoles
        modelBuilder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("asp_net_roles");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasMaxLength(500);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            // Standard Identity properties with snake_case
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.NormalizedName).HasColumnName("normalized_name");
            entity.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp");

            entity.HasIndex(e => e.Code).HasDatabaseName("idx_roles_code");
        });

        // AspNetUserRoles
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("asp_net_user_roles");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
        });

        // AspNetUserClaims
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("asp_net_user_claims");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ClaimType).HasColumnName("claim_type");
            entity.Property(e => e.ClaimValue).HasColumnName("claim_value");
        });

        // AspNetUserLogins
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("asp_net_user_logins");
            entity.Property(e => e.LoginProvider).HasColumnName("login_provider");
            entity.Property(e => e.ProviderKey).HasColumnName("provider_key");
            entity.Property(e => e.ProviderDisplayName).HasColumnName("provider_display_name");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        // AspNetUserTokens
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("asp_net_user_tokens");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.LoginProvider).HasColumnName("login_provider");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        // ===== Organization Entity Configuration =====
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.OrgType)
                .HasColumnName("org_type")
                .HasMaxLength(50);

            entity.Property(e => e.ContactEmail)
                .HasColumnName("contact_email")
                .HasMaxLength(255);

            entity.Property(e => e.ContactPhone)
                .HasColumnName("contact_phone")
                .HasMaxLength(20);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_organizations_code");
        });

        // ===== Department Entity Configuration =====
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("departments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id")
                .IsRequired();

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Departments)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Permission Entity Configuration =====
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Category)
                .HasColumnName("category")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            // Indexes per ERD
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_permissions_code");

            entity.HasIndex(e => e.Category)
                .HasDatabaseName("idx_permissions_category");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("idx_permissions_active");
        });

        // ===== RolePermission Junction Table Configuration =====
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(e => new { e.RoleId, e.PermissionId });

            entity.Property(e => e.RoleId)
                .HasColumnName("role_id");

            entity.Property(e => e.PermissionId)
                .HasColumnName("permission_id");

            entity.Property(e => e.AssignedAt)
                .HasColumnName("assigned_at")
                .HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(e => e.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for efficient permission lookups
            entity.HasIndex(e => e.PermissionId)
                .HasDatabaseName("idx_role_permissions_permission");
        });

        // ===== WorkShift Entity Configuration =====
        modelBuilder.Entity<WorkShift>(entity =>
        {
            entity.ToTable("work_shifts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50);

            entity.Property(e => e.TotalHoursPerWeek)
                .HasColumnName("total_hours_per_week")
                .HasColumnType("decimal(5,2)");

            entity.Property(e => e.GraceMinutes)
                .HasColumnName("grace_minutes");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");
        });

        // ===== WorkShiftSchedule Entity Configuration =====
        modelBuilder.Entity<WorkShiftSchedule>(entity =>
        {
            entity.ToTable("work_shift_schedules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.WorkShiftId)
                .HasColumnName("work_shift_id")
                .IsRequired();

            entity.Property(e => e.Day)
                .HasColumnName("day")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.StartTimeStr)
                .HasColumnName("start_time_str")
                .HasMaxLength(5);

            entity.Property(e => e.EndTimeStr)
                .HasColumnName("end_time_str")
                .HasMaxLength(5);

            entity.Property(e => e.StartTime)
                .HasColumnName("start_time");

            entity.Property(e => e.EndTime)
                .HasColumnName("end_time");

            entity.Property(e => e.BreakHours)
                .HasColumnName("break_hours")
                .HasColumnType("decimal(4,2)");

            entity.Property(e => e.IsWorkingDay)
                .HasColumnName("is_working_day");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasOne(e => e.WorkShift)
                .WithMany(w => w.WorkShiftSchedules)
                .HasForeignKey(e => e.WorkShiftId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== ShiftRotation Entity Configuration =====
        modelBuilder.Entity<ShiftRotation>(entity =>
        {
            entity.ToTable("shift_rotations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .HasColumnName("title")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.CurrentActiveShiftId)
                .HasColumnName("current_active_shift_id");

            entity.Property(e => e.RunDuration)
                .HasColumnName("run_duration");

            entity.Property(e => e.RunUnit)
                .HasColumnName("run_unit")
                .HasMaxLength(20);

            entity.Property(e => e.BreakDuration)
                .HasColumnName("break_duration");

            entity.Property(e => e.BreakUnit)
                .HasColumnName("break_unit")
                .HasMaxLength(20);

            entity.Property(e => e.NextChangeDate)
                .HasColumnName("next_change_date");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active");

            entity.HasOne(e => e.CurrentActiveShift)
                .WithMany()
                .HasForeignKey(e => e.CurrentActiveShiftId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ===== RotationShift Junction Table Configuration =====
        modelBuilder.Entity<RotationShift>(entity =>
        {
            entity.ToTable("rotation_shifts");
            entity.HasKey(e => new { e.RotationId, e.WorkShiftId });

            entity.Property(e => e.RotationId)
                .HasColumnName("rotation_id");

            entity.Property(e => e.WorkShiftId)
                .HasColumnName("work_shift_id");

            entity.Property(e => e.SequenceOrder)
                .HasColumnName("sequence_order");

            entity.HasOne(e => e.ShiftRotation)
                .WithMany(s => s.RotationShifts)
                .HasForeignKey(e => e.RotationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.WorkShift)
                .WithMany(w => w.RotationShifts)
                .HasForeignKey(e => e.WorkShiftId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== UserShift Entity Configuration =====
        modelBuilder.Entity<UserShift>(entity =>
        {
            entity.ToTable("user_shifts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            entity.Property(e => e.WorkShiftId)
                .HasColumnName("work_shift_id");

            entity.Property(e => e.ShiftRotationId)
                .HasColumnName("shift_rotation_id");

            entity.Property(e => e.StartsOn)
                .HasColumnName("starts_on")
                .IsRequired();

            entity.Property(e => e.EndsOn)
                .HasColumnName("ends_on");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserShifts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.WorkShift)
                .WithMany(w => w.UserShifts)
                .HasForeignKey(e => e.WorkShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ShiftRotation)
                .WithMany(s => s.UserShifts)
                .HasForeignKey(e => e.ShiftRotationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ===== Station Entity Configuration =====
        modelBuilder.Entity<Station>(entity =>
        {
            entity.ToTable("stations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.StationType)
                .HasColumnName("station_type")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Location)
                .HasColumnName("location")
                .HasMaxLength(500);

            entity.Property(e => e.RoadId)
                .HasColumnName("road_id");

            entity.Property(e => e.CountyId)
                .HasColumnName("county_id");

            entity.Property(e => e.Latitude)
                .HasColumnName("latitude")
                .HasColumnType("decimal(10,8)");

            entity.Property(e => e.Longitude)
                .HasColumnName("longitude")
                .HasColumnType("decimal(11,8)");

            entity.Property(e => e.SupportsBidirectional)
                .HasColumnName("supports_bidirectional");

            entity.Property(e => e.BoundACode)
                .HasColumnName("bound_a_code")
                .HasMaxLength(50);

            entity.Property(e => e.BoundBCode)
                .HasColumnName("bound_b_code")
                .HasMaxLength(50);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_stations_code");

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Road)
                .WithMany()
                .HasForeignKey(e => e.RoadId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.County)
                .WithMany()
                .HasForeignKey(e => e.CountyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ===== AuditLog Entity Configuration =====
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .HasColumnName("user_id");

            entity.Property(e => e.ResourceType)
                .HasColumnName("resource_type")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.ResourceId)
                .HasColumnName("resource_id");

            entity.Property(e => e.Action)
                .HasColumnName("action")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.OldValues)
                .HasColumnName("old_values")
                .HasColumnType("jsonb");

            entity.Property(e => e.NewValues)
                .HasColumnName("new_values")
                .HasColumnType("jsonb");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e => e.IpAddress)
                .HasColumnName("ip_address")
                .HasMaxLength(45);

            entity.Property(e => e.UserAgent)
                .HasColumnName("user_agent")
                .HasMaxLength(500);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes per ERD
            entity.HasIndex(e => e.ResourceType)
                .HasDatabaseName("idx_audit_logs_resource_type");

            entity.HasIndex(e => e.ResourceId)
                .HasDatabaseName("idx_audit_logs_resource_id");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_audit_logs_created_at");
        });

        return modelBuilder;
    }
}
