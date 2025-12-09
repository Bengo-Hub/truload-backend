using TruLoad.Backend.Models;

namespace TruLoad.Backend.DTOs;

/// <summary>
/// Extension methods for mapping Permission entities to DTOs and vice versa.
/// </summary>
public static class PermissionMappingExtensions
{
    /// <summary>
    /// Map a Permission entity to PermissionDto.
    /// </summary>
    public static PermissionDto ToDto(this Permission permission)
    {
        if (permission == null)
            throw new ArgumentNullException(nameof(permission));

        return new PermissionDto
        {
            Id = permission.Id,
            Code = permission.Code,
            Name = permission.Name,
            Category = permission.Category,
            Description = permission.Description,
            IsActive = permission.IsActive,
            CreatedAt = permission.CreatedAt
        };
    }

    /// <summary>
    /// Map a collection of Permission entities to PermissionDtos.
    /// </summary>
    public static IEnumerable<PermissionDto> ToDto(this IEnumerable<Permission> permissions)
    {
        if (permissions == null)
            throw new ArgumentNullException(nameof(permissions));

        return permissions.Select(p => p.ToDto());
    }

    /// <summary>
    /// Map a PermissionDto to Permission entity.
    /// Typically used for create/update operations (Id and CreatedAt will be set elsewhere).
    /// </summary>
    public static Permission ToEntity(this PermissionDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        return new Permission
        {
            Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
            Code = dto.Code,
            Name = dto.Name,
            Category = dto.Category,
            Description = dto.Description,
            IsActive = dto.IsActive,
            CreatedAt = dto.CreatedAt == default ? DateTime.UtcNow : dto.CreatedAt
        };
    }
}
