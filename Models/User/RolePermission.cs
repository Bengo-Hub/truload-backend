using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models;

/// <summary>
/// Junction table linking roles to permissions in a many-to-many relationship.
/// Enables flexible role-permission assignments.
/// </summary>
public class RolePermission
    {
        /// <summary>
        /// Foreign key to the ApplicationRole.
        /// Part of the composite primary key.
        /// </summary>
        public Guid RoleId { get; set; }

        /// <summary>
        /// Foreign key to the Permission.
        /// Part of the composite primary key.
        /// </summary>
        public Guid PermissionId { get; set; }

        /// <summary>
        /// Timestamp when this permission was assigned to the role.
        /// </summary>
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        /// <summary>
        /// Navigation property to the ApplicationRole.
        /// </summary>
        public ApplicationRole Role { get; set; } = null!;

        /// <summary>
        /// Navigation property to the Permission.
        /// </summary>
        public Permission Permission { get; set; } = null!;
}
