# Password Hashing - Cross-Service Compatibility

## Overview

All BengoBox services use **Argon2id** password hashing with **identical parameters** to ensure password hashes can be verified across services. This is critical for bidirectional user sync between local services and the centralized auth-service.

## Implementation

### Go Services (Recommended)

Use the shared Go library:

```bash
go get github.com/Bengo-Hub/shared-password-hasher@latest
```

```go
import passwordhasher "github.com/Bengo-Hub/shared-password-hasher"

hasher := passwordhasher.NewHasher()

// Hash password
hash, err := hasher.Hash("ChangeMe123!")

// Verify password
err = hasher.Verify("ChangeMe123!", hash)
if err == passwordhasher.ErrPasswordMismatch {
    // Invalid password
}
```

**Services using Go library:**
- auth-service (SSO)
- ordering-service
- notifications-service
- Any other Go microservices

### .NET Services

Use the `PasswordHasher` class in `Infrastructure/Security`:

```csharp
using TruLoad.Backend.Infrastructure.Security;

var hasher = new PasswordHasher();

// Hash password
string hash = hasher.HashPassword("ChangeMe123!");

// Verify password
bool isValid = hasher.VerifyPassword("ChangeMe123!", hash);
```

**Services using .NET implementation:**
- TruLoad backend
- ERP backend (if .NET)
- Any other C# microservices

## Hash Format

Both implementations produce **identical** PHC string format:

```
$argon2id$v=19$m=65536,t=3,p=2$<base64-salt>$<base64-hash>
```

Parameters:
- **Algorithm**: Argon2id (memory-hard, side-channel resistant)
- **Version**: 19 (Argon2 v1.3)
- **Memory**: 65536 KB (64 MiB)
- **Iterations**: 3 (time cost)
- **Parallelism**: 2 threads
- **Key Length**: 32 bytes
- **Salt Length**: 16 bytes (random)
- **Encoding**: Base64 without padding

## Cross-Service Verification

A password hashed by **any** service can be verified by **any other** service:

```
Go Service → Hash → .NET Service → ✅ Verify
.NET Service → Hash → Go Service → ✅ Verify
Auth-Service → Hash → TruLoad → ✅ Verify
```

## Bidirectional Sync Pattern

### Scenario 1: User exists locally but NOT in auth-service

```csharp
// TruLoad backend syncs user TO auth-service
var hasher = new PasswordHasher();
var passwordHash = hasher.HashPassword(user.PlaintextPassword);

// Send to auth-service with pre-hashed password
await _authServiceClient.CreateUserAsync(new CreateUserRequest
{
    Id = user.Id,  // Same UUID
    Email = user.Email,
    PasswordHash = passwordHash,  // Already hashed
    TenantSlug = "kura"
});
```

### Scenario 2: User exists in auth-service but NOT locally

```csharp
// Auth-service JWT contains user info
// TruLoad syncs user FROM auth-service
var localUser = await _userSyncService.SyncUserFromSsoAsync(
    ssoUserId: claims.Sub,
    email: claims.Email,
    tenantSlug: claims.TenantSlug
);

// Password hash is managed by auth-service (NOT synced to local DB)
```

## Security Best Practices

1. **Never store plaintext passwords**: Always hash before storage
2. **Never log passwords**: Not even hashed passwords
3. **Use strong passwords**: Enforce minimum 12 characters
4. **Random salts**: Generated automatically (16 bytes cryptographically random)
5. **Constant-time comparison**: Prevents timing attacks
6. **Pre-hash for sync**: When creating users in auth-service, hash locally first

## Testing

### Go Library

```bash
cd shared/password-hasher/
go test -v
go test -bench=. -benchmem
```

### .NET Implementation

```csharp
[Fact]
public void TestPasswordHashing()
{
    var hasher = new PasswordHasher();
    var password = "ChangeMe123!";
    
    var hash = hasher.HashPassword(password);
    Assert.True(hasher.VerifyPassword(password, hash));
    Assert.False(hasher.VerifyPassword("WrongPassword", hash));
}

[Fact]
public void TestCrossServiceCompatibility()
{
    // Hash from Go service (example)
    var goHash = "$argon2id$v=19$m=65536,t=3,p=2$...";
    
    var hasher = new PasswordHasher();
    Assert.True(hasher.VerifyPassword("ChangeMe123!", goHash));
}
```

## Updating Parameters

⚠️ **CRITICAL**: If changing Argon2id parameters, update **BOTH** implementations:

1. Update Go library: `shared/password-hasher/hasher.go`
2. Update .NET class: `TruLoad/truload-backend/Infrastructure/Security/PasswordHasher.cs`
3. Tag new version: `v0.2.0`
4. Update all services
5. Test cross-service verification

## Package Dependencies

### Go
```go
require (
    golang.org/x/crypto v0.40.0
)
```

### .NET
```xml
<PackageReference Include="Konscious.Security.Cryptography.Argon2" Version="1.3.0" />
```

## References

- [Argon2 RFC 9106](https://www.rfc-editor.org/rfc/rfc9106.html)
- [PHC String Format](https://github.com/P-H-C/phc-string-format/blob/master/phc-sf-spec.md)
- [Go shared-password-hasher](https://github.com/Bengo-Hub/shared-password-hasher)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)

## Troubleshooting

### Password verification fails between services

1. Check hash format matches: `$argon2id$v=19$m=65536,t=3,p=2$...$...`
2. Verify base64 encoding (no padding)
3. Ensure parameters match (m, t, p)
4. Check for trailing whitespace

### Performance concerns

Argon2id with m=65536, t=3, p=2 takes ~15-20ms per hash. This is intentional for security. For high-throughput scenarios:

1. Cache verification results (with TTL)
2. Use async/background hashing
3. Scale horizontally
4. Consider rate limiting login attempts
