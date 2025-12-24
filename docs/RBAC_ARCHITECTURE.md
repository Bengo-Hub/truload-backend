# RBAC Architecture - TruLoad Backend & Frontend

## Overview

TruLoad uses a comprehensive Role-Based Access Control (RBAC) system with ASP.NET Core Identity and JWT-based authentication. Permissions are embedded in JWT tokens and verified on both frontend (for UX) and backend (for security).

## Authentication Flow

### Default Admin Credentials
- **Email**: `gadmin@masterspace.co.ke`
- **Password**: `ChangeMe123!`

### Login Process

1. **Client Request**:
```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "gadmin@masterspace.co.ke",
  "password": "ChangeMe123!"
}
```

2. **Server Response**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 3600,
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "gadmin@masterspace.co.ke",
    "fullName": "Global Admin",
    "roles": ["GlobalAdmin"],
    "permissions": [
      "user.create",
      "user.read",
      "user.update",
      "user.delete",
      "weighing.create",
      "weighing.read",
      "case.create",
      "case.update",
      // ... all 77 permissions
    ],
    "organizationId": "org-123",
    "stationId": "station-456",
    "departmentId": "dept-789"
  }
}
```

### JWT Token Structure

The JWT token contains embedded claims:

```json
{
  "sub": "550e8400-e29b-41d4-a716-446655440000",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "550e8400-e29b-41d4-a716-446655440000",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress": "gadmin@masterspace.co.ke",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "Global Admin",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": ["GlobalAdmin"],
  "permission": [
    "user.create",
    "user.read",
    "user.update",
    "weighing.create",
    // ... all permissions for user's roles
  ],
  "organization_id": "org-123",
  "station_id": "station-456",
  "department_id": "dept-789",
  "exp": 1704123456,
  "iss": "truload-backend",
  "aud": "truload-frontend"
}
```

## Backend Authorization

### Permission Naming Convention

Permissions follow a hierarchical structure: `{domain}.{action}[.{scope}]`

**Examples**:
- `user.create` - Create users
- `user.read` - Read all users
- `user.read_own` - Read own user data only
- `user.update` - Update any user
- `user.update_own` - Update own user only
- `user.delete` - Delete users
- `weighing.create` - Create weighing records
- `weighing.read` - View weighing records
- `weighing.update` - Modify weighing records
- `case.create` - Create prosecution cases
- `case.update` - Update cases
- `case.close` - Close cases
- `prosecution.initiate` - Initiate prosecutions
- `station.manage` - Manage station configuration
- `system.view_config` - View system configuration
- `system.manage_roles` - Manage roles and permissions
- `analytics.view_reports` - View analytics reports
- `analytics.export_data` - Export analytics data

### Protecting Endpoints with Policies

#### Option 1: Policy-Based Authorization (Current Implementation)

**Program.cs** - Register policies:
```csharp
builder.Services.AddAuthorization(options =>
{
    // Single permission policies
    options.AddPolicy("Permission:user.create", policy =>
        policy.Requirements.Add(new PermissionRequirement("user.create")));
    
    options.AddPolicy("Permission:user.update", policy =>
        policy.Requirements.Add(new PermissionRequirement("user.update")));
    
    // Multiple permissions with AND logic (user must have ALL)
    options.AddPolicy("Permission:user.manage", policy =>
        policy.Requirements.Add(new PermissionRequirement(
            new[] { "user.create", "user.update", "user.delete" },
            PermissionRequirementType.All)));
    
    // Multiple permissions with OR logic (user must have ANY)
    options.AddPolicy("Permission:user.view", policy =>
        policy.Requirements.Add(new PermissionRequirement(
            new[] { "user.read", "user.read_own" },
            PermissionRequirementType.Any)));
});
```

**Controller Usage**:
```csharp
[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // Requires authentication
public class UsersController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "Permission:user.create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        // Only users with "user.create" permission can access
        return Ok();
    }
    
    [HttpGet]
    [Authorize(Policy = "Permission:user.view")]
    public async Task<IActionResult> GetUsers()
    {
        // Users with either "user.read" OR "user.read_own" can access
        return Ok();
    }
    
    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:user.manage")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        // Only users with ALL three permissions can access
        return Ok();
    }
}
```

#### Option 2: Custom Attribute Authorization (Recommended for Future)

**Create Custom Attribute** (not yet implemented):
```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(params string[] permissions)
    {
        Policy = $"Permission:{string.Join(",", permissions)}";
    }
}
```

**Usage**:
```csharp
[HttpPost]
[RequirePermission("user.create")]
public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
{
    return Ok();
}

[HttpPut("{id}")]
[RequirePermission("user.update", "user.delete")] // Requires ALL
public async Task<IActionResult> ManageUser(Guid id)
{
    return Ok();
}
```

### Manual Permission Checks

For complex scenarios, use `PermissionVerificationService` directly:

```csharp
public class WeighingController : ControllerBase
{
    private readonly IPermissionVerificationService _permissionService;
    
    public WeighingController(IPermissionVerificationService permissionService)
    {
        _permissionService = permissionService;
    }
    
    [HttpPost("reweigh")]
    [Authorize]
    public async Task<IActionResult> RequestReweigh([FromBody] ReweighRequest request)
    {
        // Check if user has permission to create reweigh requests
        var canCreateReweigh = await _permissionService
            .UserHasPermissionAsync(HttpContext, "weighing.reweigh_request");
        
        if (!canCreateReweigh)
        {
            return Forbid(); // 403 Forbidden
        }
        
        // Additional logic: Only station officers at same station can reweigh
        var userStationId = HttpContext.User.FindFirst("station_id")?.Value;
        if (userStationId != request.StationId)
        {
            return Forbid();
        }
        
        // Process reweigh request
        return Ok();
    }
    
    [HttpGet("dashboard")]
    [Authorize]
    public async Task<IActionResult> GetDashboard()
    {
        // Check if user has ANY of the analytics permissions
        var canViewAnalytics = await _permissionService
            .UserHasAnyPermissionAsync(HttpContext, new[] 
            { 
                "analytics.view_reports", 
                "analytics.view_dashboard" 
            });
        
        if (!canViewAnalytics)
        {
            return Forbid();
        }
        
        return Ok();
    }
}
```

### Permission Extraction from JWT

The `PermissionVerificationService` extracts permissions directly from JWT claims:

```csharp
public class PermissionVerificationService : IPermissionVerificationService
{
    public async Task<IEnumerable<string>> GetUserPermissionsAsync(HttpContext httpContext)
    {
        // Caching: Check if permissions already extracted in this request
        if (httpContext.Items.TryGetValue("UserPermissions", out var cachedPermissions))
        {
            return (IEnumerable<string>)cachedPermissions;
        }

        var principal = httpContext.User;
        if (principal?.Identity == null || !principal.Identity.IsAuthenticated)
        {
            return Enumerable.Empty<string>();
        }

        // Extract all "permission" claims from JWT token
        var permissions = principal.FindAll("permission")
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        // Cache for this request
        httpContext.Items["UserPermissions"] = permissions;
        
        return permissions;
    }
    
    public async Task<bool> UserHasPermissionAsync(HttpContext httpContext, string permission)
    {
        var permissions = await GetUserPermissionsAsync(httpContext);
        return permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
    
    public async Task<bool> UserHasAllPermissionsAsync(HttpContext httpContext, IEnumerable<string> requiredPermissions)
    {
        var userPermissions = await GetUserPermissionsAsync(httpContext);
        return requiredPermissions.All(required => 
            userPermissions.Contains(required, StringComparer.OrdinalIgnoreCase));
    }
    
    public async Task<bool> UserHasAnyPermissionAsync(HttpContext httpContext, IEnumerable<string> requiredPermissions)
    {
        var userPermissions = await GetUserPermissionsAsync(httpContext);
        return requiredPermissions.Any(required => 
            userPermissions.Contains(required, StringComparer.OrdinalIgnoreCase));
    }
}
```

### Authorization Handler

The `PermissionRequirementHandler` validates permissions against JWT claims:

```csharp
public class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionVerificationService _permissionVerificationService;
    private readonly ILogger<PermissionRequirementHandler> _logger;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity == null || !context.User.Identity.IsAuthenticated)
        {
            _logger.LogWarning("Authorization failed: User is not authenticated");
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext is null");
            return;
        }

        bool hasPermission;

        if (requirement.RequirementType == PermissionRequirementType.All)
        {
            // User must have ALL permissions
            hasPermission = await _permissionVerificationService
                .UserHasAllPermissionsAsync(httpContext, requirement.Permissions);
        }
        else
        {
            // User must have ANY permission
            hasPermission = await _permissionVerificationService
                .UserHasAnyPermissionAsync(httpContext, requirement.Permissions);
        }

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("Authorization failed: User lacks required permissions");
        }
    }
}
```

## Frontend Integration

### 1. Store Authentication State

**Using Zustand (Recommended)**:

```typescript
// stores/authStore.ts
import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface User {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
  permissions: string[];
  organizationId?: string;
  stationId?: string;
  departmentId?: string;
}

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: User | null;
  permissions: string[];
  
  // Actions
  setAuth: (data: { accessToken: string; refreshToken: string; user: User }) => void;
  clearAuth: () => void;
  hasPermission: (permission: string) => boolean;
  hasAllPermissions: (permissions: string[]) => boolean;
  hasAnyPermission: (permissions: string[]) => boolean;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      user: null,
      permissions: [],
      
      setAuth: (data) => set({
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        user: data.user,
        permissions: data.user.permissions,
      }),
      
      clearAuth: () => set({
        accessToken: null,
        refreshToken: null,
        user: null,
        permissions: [],
      }),
      
      hasPermission: (permission) => {
        const { permissions } = get();
        return permissions.includes(permission);
      },
      
      hasAllPermissions: (requiredPermissions) => {
        const { permissions } = get();
        return requiredPermissions.every(p => permissions.includes(p));
      },
      
      hasAnyPermission: (requiredPermissions) => {
        const { permissions } = get();
        return requiredPermissions.some(p => permissions.includes(p));
      },
    }),
    {
      name: 'truload-auth',
      partialize: (state) => ({
        accessToken: state.accessToken,
        refreshToken: state.refreshToken,
        user: state.user,
        permissions: state.permissions,
      }),
    }
  )
);
```

### 2. Login Implementation

```typescript
// services/authService.ts
import { useAuthStore } from '@/stores/authStore';

interface LoginRequest {
  email: string;
  password: string;
}

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: {
    id: string;
    email: string;
    fullName: string;
    roles: string[];
    permissions: string[];
    organizationId?: string;
    stationId?: string;
    departmentId?: string;
  };
}

export async function login(credentials: LoginRequest): Promise<void> {
  const response = await fetch('/api/v1/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(credentials),
  });

  if (!response.ok) {
    throw new Error('Login failed');
  }

  const data: LoginResponse = await response.json();
  
  // Store in Zustand
  useAuthStore.getState().setAuth({
    accessToken: data.accessToken,
    refreshToken: data.refreshToken,
    user: data.user,
  });
}
```

### 3. Permission Hooks

```typescript
// hooks/usePermissions.ts
import { useAuthStore } from '@/stores/authStore';

export function usePermission(permission: string): boolean {
  return useAuthStore((state) => state.hasPermission(permission));
}

export function usePermissions(permissions: string[]): {
  hasAll: boolean;
  hasAny: boolean;
  hasPermission: (permission: string) => boolean;
} {
  const hasAllPermissions = useAuthStore((state) => 
    state.hasAllPermissions(permissions)
  );
  const hasAnyPermission = useAuthStore((state) => 
    state.hasAnyPermission(permissions)
  );
  const hasPermission = useAuthStore((state) => state.hasPermission);

  return {
    hasAll: hasAllPermissions,
    hasAny: hasAnyPermission,
    hasPermission,
  };
}
```

### 4. Permission Guard Component

```typescript
// components/guards/PermissionGuard.tsx
import { ReactNode } from 'react';
import { usePermissions } from '@/hooks/usePermissions';
import { Navigate } from 'react-router-dom';

interface PermissionGuardProps {
  permission?: string;
  permissions?: string[];
  requireAll?: boolean; // Default: true (AND logic)
  fallback?: ReactNode;
  redirectTo?: string;
  children: ReactNode;
}

export function PermissionGuard({
  permission,
  permissions = [],
  requireAll = true,
  fallback,
  redirectTo = '/unauthorized',
  children,
}: PermissionGuardProps) {
  const { hasAll, hasAny, hasPermission } = usePermissions(permissions);

  let hasAccess = false;

  if (permission) {
    hasAccess = hasPermission(permission);
  } else if (permissions.length > 0) {
    hasAccess = requireAll ? hasAll : hasAny;
  }

  if (!hasAccess) {
    if (fallback) {
      return <>{fallback}</>;
    }
    return <Navigate to={redirectTo} replace />;
  }

  return <>{children}</>;
}
```

### 5. Route Protection

```typescript
// app/routes/users/page.tsx
import { PermissionGuard } from '@/components/guards/PermissionGuard';
import { UserList } from '@/components/users/UserList';

export default function UsersPage() {
  return (
    <PermissionGuard permission="user.read">
      <UserList />
    </PermissionGuard>
  );
}

// app/routes/users/create/page.tsx
export default function CreateUserPage() {
  return (
    <PermissionGuard permission="user.create">
      <CreateUserForm />
    </PermissionGuard>
  );
}

// app/routes/weighing/page.tsx
export default function WeighingPage() {
  return (
    <PermissionGuard
      permissions={["weighing.read", "weighing.read_own"]}
      requireAll={false} // OR logic - user needs ANY permission
    >
      <WeighingDashboard />
    </PermissionGuard>
  );
}
```

### 6. Conditional UI Rendering

```typescript
// components/users/UserActions.tsx
import { usePermission } from '@/hooks/usePermissions';
import { Button } from '@/components/ui/button';

export function UserActions({ userId }: { userId: string }) {
  const canUpdate = usePermission('user.update');
  const canDelete = usePermission('user.delete');

  return (
    <div className="flex gap-2">
      {canUpdate && (
        <Button onClick={() => handleUpdate(userId)}>
          Edit User
        </Button>
      )}
      
      {canDelete && (
        <Button variant="destructive" onClick={() => handleDelete(userId)}>
          Delete User
        </Button>
      )}
    </div>
  );
}
```

### 7. Reusable Permission Button Component

```typescript
// components/ui/PermissionButton.tsx
import { ReactNode } from 'react';
import { Button, ButtonProps } from '@/components/ui/button';
import { usePermission } from '@/hooks/usePermissions';

interface PermissionButtonProps extends ButtonProps {
  permission: string;
  children: ReactNode;
  fallback?: ReactNode;
}

export function PermissionButton({
  permission,
  children,
  fallback,
  ...buttonProps
}: PermissionButtonProps) {
  const hasPermission = usePermission(permission);

  if (!hasPermission) {
    return fallback ? <>{fallback}</> : null;
  }

  return <Button {...buttonProps}>{children}</Button>;
}

// Usage
import { PermissionButton } from '@/components/ui/PermissionButton';

<PermissionButton permission="user.create" onClick={handleCreate}>
  Create User
</PermissionButton>

<PermissionButton 
  permission="user.delete" 
  variant="destructive"
  fallback={<span className="text-muted">No permission</span>}
>
  Delete User
</PermissionButton>
```

### 8. Navigation Menu with Permissions

```typescript
// components/layout/NavigationMenu.tsx
import { usePermission } from '@/hooks/usePermissions';
import { Link } from 'react-router-dom';
import {
  Users,
  Scale,
  Gavel,
  BarChart,
  Settings,
} from 'lucide-react';

interface MenuItem {
  label: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  permission?: string;
}

const menuItems: MenuItem[] = [
  {
    label: 'Users',
    href: '/users',
    icon: Users,
    permission: 'user.read',
  },
  {
    label: 'Weighing',
    href: '/weighing',
    icon: Scale,
    permission: 'weighing.read',
  },
  {
    label: 'Cases',
    href: '/cases',
    icon: Gavel,
    permission: 'case.read',
  },
  {
    label: 'Analytics',
    href: '/analytics',
    icon: BarChart,
    permission: 'analytics.view_reports',
  },
  {
    label: 'Settings',
    href: '/settings',
    icon: Settings,
    permission: 'system.view_config',
  },
];

export function NavigationMenu() {
  return (
    <nav className="space-y-2">
      {menuItems.map((item) => (
        <NavItem key={item.href} item={item} />
      ))}
    </nav>
  );
}

function NavItem({ item }: { item: MenuItem }) {
  const hasPermission = usePermission(item.permission || '');

  if (item.permission && !hasPermission) {
    return null; // Hide menu item if no permission
  }

  const Icon = item.icon;

  return (
    <Link
      to={item.href}
      className="flex items-center gap-3 px-4 py-2 rounded-lg hover:bg-accent"
    >
      <Icon className="h-5 w-5" />
      <span>{item.label}</span>
    </Link>
  );
}
```

### 9. Data Table with Action Permissions

```typescript
// components/users/UserTable.tsx
import { usePermissions } from '@/hooks/usePermissions';
import { DataTable } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { Edit, Trash } from 'lucide-react';

export function UserTable({ users }: { users: User[] }) {
  const { hasPermission } = usePermissions(['user.update', 'user.delete']);

  const columns = [
    { accessorKey: 'email', header: 'Email' },
    { accessorKey: 'fullName', header: 'Full Name' },
    { accessorKey: 'roles', header: 'Roles' },
    {
      id: 'actions',
      cell: ({ row }) => {
        const user = row.original;

        return (
          <div className="flex gap-2">
            {hasPermission('user.update') && (
              <Button
                size="sm"
                variant="ghost"
                onClick={() => handleEdit(user.id)}
              >
                <Edit className="h-4 w-4" />
              </Button>
            )}
            
            {hasPermission('user.delete') && (
              <Button
                size="sm"
                variant="ghost"
                onClick={() => handleDelete(user.id)}
              >
                <Trash className="h-4 w-4" />
              </Button>
            )}
          </div>
        );
      },
    },
  ];

  return <DataTable columns={columns} data={users} />;
}
```

### 10. API Client with JWT

```typescript
// lib/apiClient.ts
import { useAuthStore } from '@/stores/authStore';

export async function apiRequest<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const { accessToken } = useAuthStore.getState();

  const headers = {
    'Content-Type': 'application/json',
    ...(accessToken && { Authorization: `Bearer ${accessToken}` }),
    ...options.headers,
  };

  const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL}${endpoint}`, {
    ...options,
    headers,
  });

  if (response.status === 401) {
    // Token expired, try refresh or redirect to login
    useAuthStore.getState().clearAuth();
    window.location.href = '/login';
    throw new Error('Unauthorized');
  }

  if (response.status === 403) {
    // Forbidden - user lacks permission
    throw new Error('You do not have permission to perform this action');
  }

  if (!response.ok) {
    throw new Error('API request failed');
  }

  return response.json();
}
```

## Complete Permissions List (77 Permissions)

### User Management (7)
- `user.create` - Create new users
- `user.read` - View all users
- `user.read_own` - View own profile only
- `user.update` - Update any user
- `user.update_own` - Update own profile only
- `user.delete` - Delete users
- `user.manage_roles` - Assign roles to users

### Weighing Operations (9)
- `weighing.create` - Create weighing records
- `weighing.read` - View weighing records
- `weighing.update` - Modify weighing records
- `weighing.delete` - Delete weighing records
- `weighing.reweigh_request` - Request reweighing
- `weighing.approve_reweigh` - Approve reweigh requests
- `weighing.void` - Void weighing records
- `weighing.export` - Export weighing data
- `weighing.view_history` - View weighing history

### Case Management (8)
- `case.create` - Create new cases
- `case.read` - View cases
- `case.update` - Update case details
- `case.delete` - Delete cases
- `case.assign` - Assign cases to officers
- `case.close` - Close cases
- `case.reopen` - Reopen closed cases
- `case.export` - Export case data

### Prosecution (7)
- `prosecution.initiate` - Initiate prosecution
- `prosecution.read` - View prosecutions
- `prosecution.update` - Update prosecution status
- `prosecution.withdraw` - Withdraw prosecutions
- `prosecution.assign_prosecutor` - Assign prosecutors
- `prosecution.record_verdict` - Record court verdicts
- `prosecution.generate_documents` - Generate prosecution documents

### Organization Management (6)
- `organization.create` - Create organizations
- `organization.read` - View organizations
- `organization.update` - Update organizations
- `organization.delete` - Delete organizations
- `organization.manage_stations` - Manage stations
- `organization.manage_departments` - Manage departments

### Station Management (7)
- `station.create` - Create stations
- `station.read` - View stations
- `station.update` - Update station details
- `station.delete` - Delete stations
- `station.manage` - Full station management
- `station.calibrate_scale` - Calibrate weighing scales
- `station.test_scale` - Test scale accuracy

### Role & Permission Management (6)
- `role.create` - Create roles
- `role.read` - View roles
- `role.update` - Update roles
- `role.delete` - Delete roles
- `permission.read` - View permissions
- `permission.assign` - Assign permissions to roles

### Configuration (8)
- `configuration.tolerances` - Manage FRD tolerances
- `configuration.permits` - Manage permit settings
- `configuration.reweigh_rules` - Configure reweigh rules
- `configuration.email` - Configure email settings
- `configuration.notifications` - Manage notification settings
- `configuration.integrations` - Manage integrations
- `configuration.backup` - Manage backups
- `configuration.audit_retention` - Configure audit retention

### Analytics & Reports (9)
- `analytics.view_reports` - View analytics reports
- `analytics.view_dashboard` - View dashboards
- `analytics.export_data` - Export analytics data
- `analytics.create_reports` - Create custom reports
- `analytics.schedule_reports` - Schedule automated reports
- `analytics.nl_query` - Use natural language queries
- `analytics.embeddings` - Access AI embeddings
- `analytics.predictions` - View ML predictions
- `analytics.data_explorer` - Use data explorer

### System Administration (10)
- `system.view_config` - View system configuration
- `system.manage_roles` - Manage roles
- `system.audit_logs` - View audit logs
- `system.manage_users` - Full user management
- `system.manage_organizations` - Full organization management
- `system.backup_restore` - Backup and restore system
- `system.database_access` - Direct database access
- `system.api_management` - Manage API settings
- `system.security_settings` - Manage security settings
- `system.integrations` - Manage system integrations

## Security Best Practices

### Backend
1. **Never trust frontend validation** - Always verify permissions in backend via JWT claims
2. **Use policy-based authorization** - Leverage ASP.NET Core's built-in mechanisms
3. **Log authorization failures** - Track unauthorized access attempts
4. **Validate JWT on every request** - Middleware automatically validates token signature and expiration
5. **Use HTTPS** - Always use TLS in production
6. **Implement rate limiting** - Prevent brute force attacks
7. **Rotate JWT secrets** - Periodically update signing keys
8. **Short token lifetimes** - Default 60 minutes, use refresh tokens for longer sessions

### Frontend
1. **Permissions are for UX only** - Backend always validates
2. **Never expose sensitive logic** - Permission checks hide UI elements, not security
3. **Store tokens securely** - Use httpOnly cookies or secure storage
4. **Clear auth state on logout** - Remove all tokens and user data
5. **Handle token expiration** - Implement automatic token refresh
6. **Show permission errors clearly** - User-friendly "Access Denied" messages
7. **Don't hardcode permissions** - Load from backend or configuration

## Testing

### Backend Unit Tests

```csharp
[Fact]
public async Task UserHasPermissionAsync_ValidPermission_ReturnsTrue()
{
    // Arrange
    var claims = new List<Claim>
    {
        new Claim("permission", "user.create"),
        new Claim("permission", "user.read"),
    };
    var identity = new ClaimsIdentity(claims, "TestAuthType");
    var principal = new ClaimsPrincipal(identity);
    var httpContext = new DefaultHttpContext { User = principal };

    // Act
    var result = await _permissionVerificationService
        .UserHasPermissionAsync(httpContext, "user.create");

    // Assert
    Assert.True(result);
}

[Fact]
public async Task UserHasAllPermissionsAsync_MissingPermission_ReturnsFalse()
{
    // Arrange
    var claims = new List<Claim>
    {
        new Claim("permission", "user.create"),
    };
    var identity = new ClaimsIdentity(claims, "TestAuthType");
    var principal = new ClaimsPrincipal(identity);
    var httpContext = new DefaultHttpContext { User = principal };

    // Act
    var result = await _permissionVerificationService
        .UserHasAllPermissionsAsync(httpContext, new[] { "user.create", "user.delete" });

    // Assert
    Assert.False(result);
}
```

### Frontend Component Tests

```typescript
import { render, screen } from '@testing-library/react';
import { PermissionButton } from '@/components/ui/PermissionButton';
import { useAuthStore } from '@/stores/authStore';

describe('PermissionButton', () => {
  it('renders button when user has permission', () => {
    // Arrange
    useAuthStore.setState({
      permissions: ['user.create'],
    });

    // Act
    render(
      <PermissionButton permission="user.create">
        Create User
      </PermissionButton>
    );

    // Assert
    expect(screen.getByText('Create User')).toBeInTheDocument();
  });

  it('hides button when user lacks permission', () => {
    // Arrange
    useAuthStore.setState({
      permissions: [],
    });

    // Act
    render(
      <PermissionButton permission="user.create">
        Create User
      </PermissionButton>
    );

    // Assert
    expect(screen.queryByText('Create User')).not.toBeInTheDocument();
  });

  it('shows fallback when provided and no permission', () => {
    // Arrange
    useAuthStore.setState({
      permissions: [],
    });

    // Act
    render(
      <PermissionButton
        permission="user.create"
        fallback={<span>No Permission</span>}
      >
        Create User
      </PermissionButton>
    );

    // Assert
    expect(screen.getByText('No Permission')).toBeInTheDocument();
  });
});
```

## Troubleshooting

### Common Issues

**Issue**: User not seeing permissions in login response
- **Cause**: Frontend calling old API or caching old response
- **Solution**: Clear browser cache, verify `/api/v1/auth/login` returns `permissions` array

**Issue**: Permissions missing from JWT token
- **Cause**: JwtService not called with permissions parameter
- **Solution**: Verify `AuthController.Login()` calls `_jwtService.GenerateAccessToken(user, roles, uniquePermissions)`

**Issue**: Authorization always fails despite correct permissions
- **Cause**: `PermissionVerificationService` not registered or misconfigured
- **Solution**: Verify `Program.cs` registers `IPermissionVerificationService` and `IAuthorizationHandler`

**Issue**: Policies not working
- **Cause**: Policy not registered in `Program.cs`
- **Solution**: Add policy with `options.AddPolicy()` or use custom attribute approach

**Issue**: Frontend shows UI but backend blocks request
- **Cause**: Permissions changed after login, token not refreshed
- **Solution**: Implement token refresh on permission change or force re-login

## Migration Path

### Phase 1: Current State (Completed)
- ✅ ASP.NET Core Identity with local authentication
- ✅ JWT token generation with embedded permissions
- ✅ Login response includes permissions array
- ✅ PermissionVerificationService extracts from JWT claims
- ✅ PermissionRequirementHandler validates requests
- ✅ 6 basic policies configured

### Phase 2: Backend Expansion (Recommended)
- [ ] Add remaining 71 policies in Program.cs OR
- [ ] Implement `[RequirePermission]` custom attribute
- [ ] Add permission checks to all endpoints
- [ ] Add audit logging for authorization failures
- [ ] Implement resource-based authorization (e.g., "user.update_own" checks user ID match)

### Phase 3: Frontend Implementation (Recommended)
- [ ] Create Zustand auth store with permissions
- [ ] Implement usePermission hooks
- [ ] Create PermissionGuard component
- [ ] Create PermissionButton component
- [ ] Add permission checks to all routes
- [ ] Add permission-based navigation menu
- [ ] Implement API client with JWT

### Phase 4: Advanced Features (Future)
- [ ] Dynamic permission loading from backend
- [ ] Permission inheritance (role hierarchies)
- [ ] Conditional permissions (time-based, location-based)
- [ ] Permission delegation (temporary access grants)
- [ ] Multi-tenancy permission isolation
- [ ] Fine-grained resource permissions (document-level, field-level)

## Related Documentation

- [Authentication Audit](./AUTHENTICATION_AUDIT.md) - SSO removal and Identity migration details
- [ERD](./erd.md) - Database schema including permission tables
- [Integration Guide](./integration.md) - Frontend integration patterns
- [RBAC Implementation Plan](./RBAC_IMPLEMENTATION_PLAN.md) - Original implementation plan

---

**Last Updated**: January 2025  
**Version**: 1.0  
**Status**: Production-ready for backend, frontend patterns documented
