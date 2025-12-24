using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;

namespace truload_backend.Data;

/// <summary>
/// Main database context for TruLoad application
/// Uses ASP.NET Core Identity for local authentication
/// </summary>
public class TruLoadDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public TruLoadDbContext(DbContextOptions<TruLoadDbContext> options)
        : base(options)
    {
    }

    // ===== Sprint 1: User Management & Identity =====
    // Note: Users and Roles are managed by Identity (AspNetUsers, AspNetRoles tables)
    public DbSet<Organization> Organizations { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<WorkShift> WorkShifts { get; set; } = null!;
    public DbSet<WorkShiftSchedule> WorkShiftSchedules { get; set; } = null!;
    public DbSet<ShiftRotation> ShiftRotations { get; set; } = null!;
    public DbSet<RotationShift> RotationShifts { get; set; } = null!;
    public DbSet<UserShift> UserShifts { get; set; } = null!;
    public DbSet<Station> Stations { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    
    // ===== Weighing Operations: Axle Configurations & References =====
    public DbSet<TyreType> TyreTypes { get; set; } = null!;
    public DbSet<AxleGroup> AxleGroups { get; set; } = null!;
    public DbSet<AxleConfiguration> AxleConfigurations { get; set; } = null!;
    public DbSet<AxleWeightReference> AxleWeightReferences { get; set; } = null!;
    public DbSet<AxleFeeSchedule> AxleFeeSchedules { get; set; } = null!;
    public DbSet<WeighingAxle> WeighingAxles { get; set; } = null!;
    
    // ===== Traffic: Driver & Demerit Points Management =====
    public DbSet<Driver> Drivers { get; set; } = null!;
    public DbSet<DriverDemeritRecord> DriverDemeritRecords { get; set; } = null!;
    
    // ===== System Configuration =====
    public DbSet<PermitType> PermitTypes { get; set; } = null!;
    public DbSet<ToleranceSetting> ToleranceSettings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

        // AspNetRoleClaims
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("asp_net_role_claims");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.ClaimType).HasColumnName("claim_type");
            entity.Property(e => e.ClaimValue).HasColumnName("claim_value");
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
            
            entity.Property(e => e.ShiftName)
                .HasColumnName("shift_name")
                .HasMaxLength(100);
            
            entity.Property(e => e.ShiftCode)
                .HasColumnName("shift_code")
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
            
            entity.Property(e => e.StationCode)
                .HasColumnName("station_code")
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50);
            
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(e => e.StationName)
                .HasColumnName("station_name")
                .HasMaxLength(255);
            
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(50);
            
            entity.Property(e => e.StationType)
                .HasColumnName("station_type")
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.Location)
                .HasColumnName("location")
                .HasMaxLength(500);
            
            entity.Property(e => e.Latitude)
                .HasColumnName("latitude")
                .HasColumnType("decimal(10,8)");
            
            entity.Property(e => e.Longitude)
                .HasColumnName("longitude")
                .HasColumnType("decimal(11,8)");
            
            entity.Property(e => e.SupportsBidirectional)
                .HasColumnName("supports_bidirectional");
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active");
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");
            
            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");
            
            entity.HasIndex(e => e.StationCode)
                .IsUnique()
                .HasDatabaseName("idx_stations_code");
            
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_stations_code_alias");
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

        // ===== AxleConfiguration Entity Configuration (Unified: Standard & Derived) =====
        modelBuilder.Entity<AxleConfiguration>(entity =>
        {
            entity.ToTable("axle_configurations");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.AxleCode)
                .HasColumnName("axle_code")
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.AxleName)
                .HasColumnName("axle_name")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");
            
            entity.Property(e => e.AxleNumber)
                .HasColumnName("axle_number")
                .IsRequired();
            
            entity.Property(e => e.GvwPermissibleKg)
                .HasColumnName("gvw_permissible_kg")
                .IsRequired();
            
            entity.Property(e => e.IsStandard)
                .HasColumnName("is_standard")
                .IsRequired()
                .HasDefaultValue(false);
            
            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("BOTH");
            
            entity.Property(e => e.VisualDiagramUrl)
                .HasColumnName("visual_diagram_url")
                .HasColumnType("text");
            
            entity.Property(e => e.Notes)
                .HasColumnName("notes")
                .HasColumnType("text");
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();
            
            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");
            
            entity.Property(e => e.CreatedByUserId)
                .HasColumnName("created_by_user_id");
            
            // Unique constraint on axle_code
            entity.HasIndex(e => e.AxleCode)
                .IsUnique()
                .HasDatabaseName("idx_axle_configurations_code_unique");
            
            // Index on is_standard (for filtering standard configs)
            entity.HasIndex(e => e.IsStandard)
                .HasFilter("is_standard = true")
                .HasDatabaseName("idx_axle_configurations_standard");
            
            // Index on axle_number
            entity.HasIndex(e => e.AxleNumber)
                .HasDatabaseName("idx_axle_configurations_axle_number");
            
            // Index on legal_framework
            entity.HasIndex(e => e.LegalFramework)
                .HasDatabaseName("idx_axle_configurations_framework");
            
            // Index on is_active and soft delete
            entity.HasIndex(e => new { e.IsActive, e.DeletedAt })
                .HasFilter("is_active = true AND deleted_at IS NULL")
                .HasDatabaseName("idx_axle_configurations_active");
            
            // Relationship to User (creator of derived configs)
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Relationships to navigation properties
            entity.HasMany(e => e.AxleWeightReferences)
                .WithOne(awr => awr.AxleConfiguration)
                .HasForeignKey(awr => awr.AxleConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasMany(e => e.WeighingAxles)
                .WithOne(wa => wa.AxleConfiguration)
                .HasForeignKey(wa => wa.AxleConfigurationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ===== AxleWeightReference Entity Configuration =====
        modelBuilder.Entity<AxleWeightReference>(entity =>
        {
            entity.ToTable("axle_weight_references");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.AxleConfigurationId)
                .HasColumnName("axle_configuration_id")
                .IsRequired();
            
            entity.Property(e => e.AxlePosition)
                .HasColumnName("axle_position")
                .IsRequired();
            
            entity.Property(e => e.AxleLegalWeightKg)
                .HasColumnName("axle_legal_weight_kg")
                .IsRequired();
            
            entity.Property(e => e.AxleGroupId)
                .HasColumnName("axle_group_id")
                .IsRequired();
            
            entity.Property(e => e.AxleGrouping)
                .HasColumnName("axle_grouping")
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.TyreTypeId)
                .HasColumnName("tyre_type_id");
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
            
            // Relationship to AxleConfiguration
            entity.HasOne(e => e.AxleConfiguration)
                .WithMany(ac => ac.AxleWeightReferences)
                .HasForeignKey(e => e.AxleConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Relationship to AxleGroup
            entity.HasOne(e => e.AxleGroup)
                .WithMany(ag => ag.AxleWeightReferences)
                .HasForeignKey(e => e.AxleGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Relationship to TyreType
            entity.HasOne(e => e.TyreType)
                .WithMany(tt => tt.AxleWeightReferences)
                .HasForeignKey(e => e.TyreTypeId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Unique constraint: one position per configuration
            entity.HasIndex(e => new { e.AxleConfigurationId, e.AxlePosition })
                .IsUnique()
                .HasDatabaseName("idx_axle_weight_ref_config_position_unique");
            
            // Foreign key indexes for performance
            entity.HasIndex(e => e.AxleConfigurationId)
                .HasDatabaseName("idx_axle_weight_ref_config_id");
            
            entity.HasIndex(e => e.AxleGroupId)
                .HasDatabaseName("idx_axle_weight_ref_group_id");
            
            entity.HasIndex(e => e.TyreTypeId)
                .HasDatabaseName("idx_axle_weight_ref_tyre_type_id");
        });

        // ===== TyreType Entity Configuration =====
        modelBuilder.Entity<TyreType>(entity =>
        {
            entity.ToTable("tyre_types");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(1)
                .IsRequired();
            
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");
            
            entity.Property(e => e.TypicalMaxWeightKg)
                .HasColumnName("typical_max_weight_kg");
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
            
            // Unique index on code
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_tyre_types_code_unique");
            
            // Index on active status
            entity.HasIndex(e => e.IsActive)
                .HasFilter("is_active = true")
                .HasDatabaseName("idx_tyre_types_active");
        });

        // ===== AxleGroup Entity Configuration =====
        modelBuilder.Entity<AxleGroup>(entity =>
        {
            entity.ToTable("axle_groups");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(20)
                .IsRequired();
            
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");
            
            entity.Property(e => e.TypicalWeightKg)
                .HasColumnName("typical_weight_kg")
                .IsRequired();
            
            entity.Property(e => e.MinSpacingFeet)
                .HasColumnName("min_spacing_feet")
                .HasColumnType("numeric(4,1)");
            
            entity.Property(e => e.MaxSpacingFeet)
                .HasColumnName("max_spacing_feet")
                .HasColumnType("numeric(4,1)");
            
            entity.Property(e => e.AxleCountInGroup)
                .HasColumnName("axle_count_in_group")
                .IsRequired()
                .HasDefaultValue(1);
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
            
            // Unique index on code
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_axle_groups_code_unique");
            
            // Index on active status
            entity.HasIndex(e => e.IsActive)
                .HasFilter("is_active = true")
                .HasDatabaseName("idx_axle_groups_active");
        });

        // ===== AxleFeeSchedule Entity Configuration =====
        modelBuilder.Entity<AxleFeeSchedule>(entity =>
        {
            entity.ToTable("axle_fee_schedules");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(20)
                .IsRequired();
            
            entity.Property(e => e.FeeType)
                .HasColumnName("fee_type")
                .HasMaxLength(20)
                .IsRequired();
            
            entity.Property(e => e.OverloadMinKg)
                .HasColumnName("overload_min_kg")
                .IsRequired();
            
            entity.Property(e => e.OverloadMaxKg)
                .HasColumnName("overload_max_kg");
            
            entity.Property(e => e.FeePerKgUsd)
                .HasColumnName("fee_per_kg_usd")
                .HasColumnType("numeric(10,4)")
                .IsRequired();
            
            entity.Property(e => e.FlatFeeUsd)
                .HasColumnName("flat_fee_usd")
                .HasColumnType("numeric(10,2)")
                .IsRequired()
                .HasDefaultValue(0m);
            
            entity.Property(e => e.DemeritPoints)
                .HasColumnName("demerit_points")
                .IsRequired()
                .HasDefaultValue(0);
            
            entity.Property(e => e.PenaltyDescription)
                .HasColumnName("penalty_description")
                .HasColumnType("text");
            
            entity.Property(e => e.EffectiveFrom)
                .HasColumnName("effective_from")
                .IsRequired();
            
            entity.Property(e => e.EffectiveTo)
                .HasColumnName("effective_to");
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
            
            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");
            
            // Check constraint on legal framework
            entity.HasCheckConstraint("chk_legal_framework", 
                "legal_framework IN ('EAC', 'TRAFFIC_ACT')");
            
            // Check constraint on fee type
            entity.HasCheckConstraint("chk_fee_type", 
                "fee_type IN ('GVW', 'AXLE')");
            
            // Composite index on framework and fee type
            entity.HasIndex(e => new { e.LegalFramework, e.FeeType })
                .HasDatabaseName("idx_axle_fee_schedule_framework_type");
            
            // Index on effective date range
            entity.HasIndex(e => new { e.EffectiveFrom, e.EffectiveTo })
                .HasDatabaseName("idx_axle_fee_schedule_effective");
        });

        // ===== WeighingAxle Entity Configuration =====
        modelBuilder.Entity<WeighingAxle>(entity =>
        {
            entity.ToTable("weighing_axles");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.WeighingId)
                .HasColumnName("weighing_id")
                .IsRequired();
            
            entity.Property(e => e.AxleNumber)
                .HasColumnName("axle_number")
                .IsRequired();
            
            entity.Property(e => e.MeasuredWeightKg)
                .HasColumnName("measured_weight_kg")
                .IsRequired();
            
            entity.Property(e => e.PermissibleWeightKg)
                .HasColumnName("permissible_weight_kg")
                .IsRequired();
            
            entity.Property(e => e.AxleConfigurationId)
                .HasColumnName("axle_configuration_id")
                .IsRequired();
            
            entity.Property(e => e.AxleWeightReferenceId)
                .HasColumnName("axle_weight_reference_id");
            
            entity.Property(e => e.AxleGroupId)
                .HasColumnName("axle_group_id")
                .IsRequired();
            
            entity.Property(e => e.AxleGrouping)
                .HasColumnName("axle_grouping")
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.TyreTypeId)
                .HasColumnName("tyre_type_id");
            
            entity.Property(e => e.FeeUsd)
                .HasColumnName("fee_usd")
                .HasColumnType("numeric(18,2)")
                .IsRequired()
                .HasDefaultValue(0m);
            
            entity.Property(e => e.CapturedAt)
                .HasColumnName("captured_at")
                .IsRequired();
            
            // Relationship to Weighing (placeholder - will be configured when Weighing model exists)
            // entity.HasOne(e => e.Weighing)
            //     .WithMany(w => w.WeighingAxles)
            //     .HasForeignKey(e => e.WeighingId)
            //     .OnDelete(DeleteBehavior.Cascade);
            
            // Relationship to AxleConfiguration
            entity.HasOne(e => e.AxleConfiguration)
                .WithMany(ac => ac.WeighingAxles)
                .HasForeignKey(e => e.AxleConfigurationId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Relationship to AxleWeightReference
            entity.HasOne(e => e.AxleWeightReference)
                .WithMany()
                .HasForeignKey(e => e.AxleWeightReferenceId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Relationship to AxleGroup
            entity.HasOne(e => e.AxleGroup)
                .WithMany(ag => ag.WeighingAxles)
                .HasForeignKey(e => e.AxleGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Relationship to TyreType
            entity.HasOne(e => e.TyreType)
                .WithMany(tt => tt.WeighingAxles)
                .HasForeignKey(e => e.TyreTypeId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Unique constraint: one entry per weighing per axle
            entity.HasIndex(e => new { e.WeighingId, e.AxleNumber })
                .IsUnique()
                .HasDatabaseName("idx_weighing_axles_weighing_axle_unique");
            
            // Indexes for performance
            entity.HasIndex(e => e.WeighingId)
                .HasDatabaseName("idx_weighing_axles_weighing");
            
            entity.HasIndex(e => e.AxleConfigurationId)
                .HasDatabaseName("idx_weighing_axles_configuration");
            
            entity.HasIndex(e => e.AxleGroupId)
                .HasDatabaseName("idx_weighing_axles_group");
        });

        // ===== PermitType Entity Configuration =====
        modelBuilder.Entity<PermitType>(entity =>
        {
            entity.ToTable("permit_types");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(20)
                .IsRequired();
            
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");
            
            entity.Property(e => e.AxleExtensionKg)
                .HasColumnName("axle_extension_kg")
                .IsRequired();
            
            entity.Property(e => e.GvwExtensionKg)
                .HasColumnName("gvw_extension_kg")
                .IsRequired();
            
            entity.Property(e => e.ValidityDays)
                .HasColumnName("validity_days");
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired();
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");
            
            // Unique constraint on code
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_permit_types_code");
        });

        // ===== ToleranceSetting Entity Configuration =====
        modelBuilder.Entity<ToleranceSetting>(entity =>
        {
            entity.ToTable("tolerance_settings");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.TolerancePercentage)
                .HasColumnName("tolerance_percentage")
                .HasColumnType("decimal(5,2)")
                .IsRequired();
            
            entity.Property(e => e.ToleranceKg)
                .HasColumnName("tolerance_kg");
            
            entity.Property(e => e.AppliesTo)
                .HasColumnName("applies_to")
                .HasMaxLength(20)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");
            
            entity.Property(e => e.EffectiveFrom)
                .HasColumnName("effective_from")
                .IsRequired();
            
            entity.Property(e => e.EffectiveTo)
                .HasColumnName("effective_to");
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired();
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");
            
            // Unique constraint on code
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_tolerance_settings_code");
            
            // Index on effective dates for active settings
            entity.HasIndex(e => new { e.EffectiveFrom, e.EffectiveTo })
                .HasDatabaseName("idx_tolerance_settings_effective_dates");
        });

        // ===== Driver Entity Configuration =====
        modelBuilder.Entity<Driver>(entity =>
        {
            entity.ToTable("drivers");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.NtsaId)
                .HasColumnName("ntsa_id")
                .HasMaxLength(50);
            
            entity.Property(e => e.IdNumber)
                .HasColumnName("id_number")
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.DrivingLicenseNo)
                .HasColumnName("driving_license_no")
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.FullNames)
                .HasColumnName("full_names")
                .HasMaxLength(200)
                .IsRequired();
            
            entity.Property(e => e.Surname)
                .HasColumnName("surname")
                .HasMaxLength(100)
                .IsRequired();
            
            entity.Property(e => e.Gender)
                .HasColumnName("gender")
                .HasMaxLength(10);
            
            entity.Property(e => e.Nationality)
                .HasColumnName("nationality")
                .HasMaxLength(100)
                .HasDefaultValue("Kenya");
            
            entity.Property(e => e.DateOfBirth)
                .HasColumnName("date_of_birth");
            
            entity.Property(e => e.Address)
                .HasColumnName("address")
                .HasMaxLength(500);
            
            entity.Property(e => e.PhoneNumber)
                .HasColumnName("phone_number")
                .HasMaxLength(20);
            
            entity.Property(e => e.Email)
                .HasColumnName("email")
                .HasMaxLength(100);
            
            entity.Property(e => e.LicenseClass)
                .HasColumnName("license_class")
                .HasMaxLength(50);
            
            entity.Property(e => e.LicenseIssueDate)
                .HasColumnName("license_issue_date");
            
            entity.Property(e => e.LicenseExpiryDate)
                .HasColumnName("license_expiry_date");
            
            entity.Property(e => e.LicenseStatus)
                .HasColumnName("license_status")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("active");
            
            entity.Property(e => e.IsProfessionalDriver)
                .HasColumnName("is_professional_driver")
                .IsRequired()
                .HasDefaultValue(false);
            
            entity.Property(e => e.CurrentDemeritPoints)
                .HasColumnName("current_demerit_points")
                .IsRequired()
                .HasDefaultValue(0);
            
            entity.Property(e => e.SuspensionStartDate)
                .HasColumnName("suspension_start_date");
            
            entity.Property(e => e.SuspensionEndDate)
                .HasColumnName("suspension_end_date");
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");
            
            // Unique indexes
            entity.HasIndex(e => e.NtsaId)
                .IsUnique()
                .HasDatabaseName("idx_drivers_ntsa_id_unique");
            
            entity.HasIndex(e => e.DrivingLicenseNo)
                .IsUnique()
                .HasDatabaseName("idx_drivers_license_no_unique");
            
            entity.HasIndex(e => e.IdNumber)
                .IsUnique()
                .HasDatabaseName("idx_drivers_id_number_unique");
            
            // Composite index for license status queries
            entity.HasIndex(e => new { e.LicenseStatus, e.IsActive })
                .HasDatabaseName("idx_drivers_license_status_active");
            
            // Index for suspended drivers
            entity.HasIndex(e => new { e.SuspensionStartDate, e.SuspensionEndDate })
                .HasFilter("suspension_start_date IS NOT NULL")
                .HasDatabaseName("idx_drivers_suspension_dates");
            
            // Check constraint for license status enum
            entity.HasCheckConstraint(
                "chk_driver_license_status",
                "license_status IN ('active', 'suspended', 'revoked', 'expired')"
            );
            
            // Relationship: Driver -> DriverDemeritRecords (1-to-many)
            entity.HasMany(d => d.DemeritRecords)
                .WithOne(r => r.Driver)
                .HasForeignKey(r => r.DriverId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== DriverDemeritRecord Entity Configuration =====
        modelBuilder.Entity<DriverDemeritRecord>(entity =>
        {
            entity.ToTable("driver_demerit_records");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.DriverId)
                .HasColumnName("driver_id")
                .IsRequired();
            
            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id");
            
            entity.Property(e => e.WeighingId)
                .HasColumnName("weighing_id");
            
            entity.Property(e => e.ViolationDate)
                .HasColumnName("violation_date")
                .IsRequired();
            
            entity.Property(e => e.PointsAssigned)
                .HasColumnName("points_assigned")
                .IsRequired();
            
            entity.Property(e => e.FeeScheduleId)
                .HasColumnName("fee_schedule_id");
            
            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(20)
                .IsRequired();
            
            entity.Property(e => e.ViolationType)
                .HasColumnName("violation_type")
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.OverloadKg)
                .HasColumnName("overload_kg");
            
            entity.Property(e => e.PenaltyAmountUsd)
                .HasColumnName("penalty_amount_usd")
                .HasColumnType("decimal(12,2)")
                .IsRequired()
                .HasDefaultValue(0);
            
            entity.Property(e => e.PaymentStatus)
                .HasColumnName("payment_status")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("pending");
            
            entity.Property(e => e.PointsExpiryDate)
                .HasColumnName("points_expiry_date")
                .IsRequired();
            
            entity.Property(e => e.IsExpired)
                .HasColumnName("is_expired")
                .IsRequired()
                .HasDefaultValue(false);
            
            entity.Property(e => e.Notes)
                .HasColumnName("notes")
                .HasColumnType("text");
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
            
            // Composite index for driver history queries
            entity.HasIndex(e => new { e.DriverId, e.ViolationDate })
                .HasDatabaseName("idx_demerit_records_driver_date");
            
            // Index for expiry background job
            entity.HasIndex(e => e.PointsExpiryDate)
                .HasFilter("is_expired = false")
                .HasDatabaseName("idx_demerit_records_expiry_date");
            
            // Index for payment status tracking
            entity.HasIndex(e => new { e.PaymentStatus, e.DriverId })
                .HasDatabaseName("idx_demerit_records_payment_driver");
            
            // Check constraints for enums
            entity.HasCheckConstraint(
                "chk_demerit_payment_status",
                "payment_status IN ('pending', 'paid', 'waived')"
            );
            
            entity.HasCheckConstraint(
                "chk_demerit_legal_framework",
                "legal_framework IN ('EAC', 'TRAFFIC_ACT')"
            );
            
            entity.HasCheckConstraint(
                "chk_demerit_violation_type",
                "violation_type IN ('GVW_OVERLOAD', 'AXLE_OVERLOAD', 'PERMIT_VIOLATION', 'OTHER')"
            );
            
            entity.HasCheckConstraint(
                "chk_demerit_points_range",
                "points_assigned >= 0 AND points_assigned <= 20"
            );
            
            // Relationship: DriverDemeritRecord -> Driver (many-to-1)
            entity.HasOne(r => r.Driver)
                .WithMany(d => d.DemeritRecords)
                .HasForeignKey(r => r.DriverId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Relationship: DriverDemeritRecord -> AxleFeeSchedule (many-to-1, optional)
            entity.HasOne(r => r.FeeSchedule)
                .WithMany()
                .HasForeignKey(r => r.FeeScheduleId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

