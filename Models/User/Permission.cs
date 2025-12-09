namespace TruLoad.Backend.Models;

/// <summary>
/// Represents a fine-grained permission in the RBAC system.
/// Permissions are assigned to roles, which are then assigned to users.
/// </summary>
public class Permission
    {
        /// <summary>
        /// Unique identifier for the permission.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Unique code for the permission (e.g., "weighing.create", "case.read_own").
        /// Used to check permissions programmatically.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the permission (e.g., "Create Weighing", "Read Own Cases").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Category of the permission.
        /// Categories: weighing, case, prosecution, user, station, config, report, system
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of what this permission allows.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether this permission is currently active.
        /// Inactive permissions are not assigned to roles.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Timestamp when the permission was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        /// <summary>
        /// Collection of role-permission assignments for this permission.
        /// </summary>
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
