# Sprint 5: Role Management Endpoints

**Module:** User Management & Security  
**Start Date:** December 31, 2025  
**Estimated Duration:** 8-12 hours  
**Priority:** High  

## Objective

Implement complete role management functionality including role CRUD operations, user-role assignments, and role-permission management to provide comprehensive RBAC capabilities.

## Current State Analysis

**✅ Completed (from previous sprints):**
- RolesController with basic CRUD operations (GET, POST, PUT, DELETE)
- User role assignment during user creation
- Role models and DTOs
- Permission model with 77 permissions across 8 categories
- RolePermission junction table for role-permission assignments

**🔄 Partially Implemented:**
- User role assignment exists (POST /users/{id}/roles) but replaces all roles
- No dedicated endpoints for role permission management
- No endpoints for granular user-role operations

**❌ Missing Functionality:**
- Role permission assignment/removal endpoints
- Granular user-role management (add/remove specific roles)
- Get users by role queries
- Get role permissions endpoint

## Requirements

### 1. Role Permission Management Endpoints

**Endpoints to implement:**
- `GET /api/v1/user-management/roles/{id}/permissions` - Get permissions assigned to a role
- `POST /api/v1/user-management/roles/{id}/permissions` - Assign permissions to a role
- `DELETE /api/v1/user-management/roles/{id}/permissions/{permissionId}` - Remove permission from role

**Authorization:** `user.manage_permissions`

### 2. Enhanced User-Role Management Endpoints

**Endpoints to implement:**
- `GET /api/v1/user-management/users/{id}/roles` - Get roles assigned to a user
- `POST /api/v1/user-management/users/{id}/roles/{roleId}` - Add specific role to user
- `DELETE /api/v1/user-management/users/{id}/roles/{roleId}` - Remove specific role from user
- `GET /api/v1/user-management/roles/{id}/users` - Get users assigned to a role

**Authorization:** `user.assign_roles`

### 3. DTOs and Validation

**New DTOs needed:**
- `AssignPermissionsRequest` - For role permission assignment
- `RolePermissionsDto` - For role permissions response
- `UserRolesDto` - For user roles response

**Validation:** FluentValidation rules for all new DTOs

## Implementation Plan

### Phase 1: Role Permission Management (4 hours)

1. **Add Permission Management to RolesController**
   - Inject IPermissionService and IPermissionRepository
   - Implement GET /roles/{id}/permissions endpoint
   - Implement POST /roles/{id}/permissions endpoint
   - Implement DELETE /roles/{id}/permissions/{permissionId} endpoint

2. **Create DTOs**
   - AssignPermissionsRequest
   - PermissionDto (if not exists)
   - RolePermissionsDto

3. **Add Validation**
   - Create validators for new DTOs

### Phase 2: Enhanced User-Role Management (4 hours)

1. **Add Granular Role Management to UsersController**
   - GET /users/{id}/roles endpoint
   - POST /users/{id}/roles/{roleId} endpoint
   - DELETE /users/{id}/roles/{roleId} endpoint

2. **Add Role-based Queries to RolesController**
   - GET /roles/{id}/users endpoint

3. **Create DTOs**
   - UserRolesDto
   - RoleUsersDto

### Phase 3: Testing and Validation (4 hours)

1. **Unit Tests**
   - Test new controller endpoints
   - Test permission assignment logic
   - Test role assignment logic

2. **Integration Tests**
   - Test role-permission relationships
   - Test user-role assignments
   - Test authorization policies

3. **Build and Test Verification**
   - Ensure all tests pass
   - Verify API documentation
   - Update plan.md with completion status

## Technical Considerations

### Database Operations
- Use existing RolePermission junction table
- Leverage IPermissionService for caching
- Ensure atomic operations for permission assignments

### Authorization
- `user.manage_permissions` for role permission management
- `user.assign_roles` for user-role assignments
- Proper permission validation in service layer

### Performance
- Use caching for permission lookups
- Optimize queries with proper indexing
- Consider pagination for user lists

### Error Handling
- Validate role and permission existence
- Handle concurrent modifications
- Provide meaningful error messages

## Acceptance Criteria

- [ ] All role permission management endpoints implemented and tested
- [ ] All user-role management endpoints implemented and tested
- [ ] Proper authorization applied to all endpoints
- [ ] Comprehensive unit and integration tests passing
- [ ] API documentation updated
- [ ] Build successful with no errors
- [ ] plan.md updated with completion status

## Dependencies

- Existing permission model and services
- ASP.NET Core Identity RoleManager
- FluentValidation for DTO validation
- Existing authorization infrastructure

## Risk Assessment

**Low Risk:** Building on existing patterns and infrastructure
**Mitigation:** Follow established controller patterns and authorization standards

## Success Metrics

- All endpoints return correct HTTP status codes
- Authorization properly enforced
- No duplicate permissions or roles assigned
- Performance meets requirements (< 500ms response time)
- 100% test coverage for new functionality</content>
<parameter name="filePath">d:\Projects\BengoBox\TruLoad\truload-backend\docs\sprints\sprint-05-role-management-endpoints.md