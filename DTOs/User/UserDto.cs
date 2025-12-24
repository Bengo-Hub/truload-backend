namespace TruLoad.Backend.DTOs.User;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? FullName { get; set; }
    public Guid? StationId { get; set; }
    public string? StationName { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? FullName { get; set; }
    public string? Password { get; set; }
    public Guid? StationId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? DepartmentId { get; set; }
    public List<string>? RoleNames { get; set; }
}

public class UpdateUserRequest
{
    public string? PhoneNumber { get; set; }
    public string? FullName { get; set; }
    public Guid? StationId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? DepartmentId { get; set; }
}

public class AssignRolesRequest
{
    public List<Guid> RoleIds { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();
}
