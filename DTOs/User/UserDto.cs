namespace TruLoad.Backend.DTOs.User;

public class UserDto
{
    public Guid Id { get; set; }
    public Guid AuthServiceUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? FullName { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? StationId { get; set; }
    public string? StationName { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? SyncStatus { get; set; }
    public DateTime? SyncAt { get; set; }
    public DateTime? LastSyncAt { get; set; } // Alias for SyncAt
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<RoleDto> Roles { get; set; } = new();
}

public class CreateUserRequest
{
    public Guid AuthServiceUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? FullName { get; set; }
    public Guid? StationId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? DepartmentId { get; set; }
    public List<Guid>? RoleIds { get; set; }
}

public class UpdateUserRequest
{
    public string? Phone { get; set; }
    public string? FullName { get; set; }
    public string? Status { get; set; }
    public Guid? StationId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? DepartmentId { get; set; }
}

public class AssignRolesRequest
{
    public List<Guid> RoleIds { get; set; } = new();
}
