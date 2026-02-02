# TruLoad Backend - Collaboration Guidelines

This document outlines conventions and best practices for collaborating on the TruLoad Backend project.

## 📝 Naming Conventions

### C# Code Conventions

#### Classes and Interfaces
```csharp
// Classes: PascalCase
public class WeighingService { }
public class VehicleRegistrationValidator { }

// Interfaces: PascalCase with 'I' prefix
public interface IWeighingService { }
public interface IVehicleRepository { }

// Abstract classes: PascalCase with 'Base' suffix (optional)
public abstract class BaseRepository<T> { }
```

#### Methods and Properties
```csharp
// Methods: PascalCase, verb-based
public async Task<Weighing> CreateWeighingAsync(CreateWeighingDto dto) { }
public bool ValidatePermit(Permit permit) { }

// Properties: PascalCase, noun-based
public string VehicleRegistration { get; set; }
public DateTime CreatedAt { get; set; }
public long TotalWeight { get; set; }
```

#### Variables and Parameters
```csharp
// Local variables: camelCase
var weighingResult = await _service.GetWeighingAsync(id);
int axleCount = vehicle.Axles.Count;

// Parameters: camelCase
public Task ProcessWeighing(long weighingId, string userId) { }

// Private fields: _camelCase with underscore prefix
private readonly ILogger<WeighingController> _logger;
private readonly IWeighingService _weighingService;
```

#### Constants and Enums
```csharp
// Constants: UPPER_CASE with underscores
public const int MAX_REWEIGH_CYCLES = 8;
public const string DEFAULT_CURRENCY = "KES";

// Enums: PascalCase for type and members
public enum WeighingMode
{
    Static,
    WeighInMotion,
    Axle
}

public enum PermitStatus
{
    Active,
    Expired,
    Suspended,
    Cancelled
}
```

### Database Conventions

```sql
-- Tables: snake_case, plural
weighing_transactions
weighing_axles
vehicle_permits
prosecution_cases

-- Columns: snake_case
vehicle_registration
gross_vehicle_mass
permit_expiry_date
created_at

-- Primary Keys
id (BIGSERIAL)

-- Foreign Keys: {table}_id
vehicle_id
permit_id
station_id

-- Junction Tables: {table1}_{table2}
user_roles
role_permissions

-- Indexes: idx_{table}_{columns}
idx_weighings_station_date
idx_vehicles_registration

-- Constraints: {table}_{column}_check
weighings_status_check
permits_expiry_check
```

### File and Folder Conventions

```
Controllers/
  WeighingOperations/
    WeighingController.cs          # PascalCase
    VehicleController.cs
  CaseManagement/
    ProsecutionController.cs

Services/
  Interfaces/
    IWeighingService.cs           # 'I' prefix for interfaces
  Implementations/
    WeighingService.cs            # Implementation without 'I'

DTOs/
  Weighing/
    CreateWeighingDto.cs          # Suffix with 'Dto'
    WeighingResponseDto.cs
    UpdateWeighingDto.cs

Models/
  Weighing.cs                     # Singular, matches entity name
  Vehicle.cs
  Permit.cs
```

## 🌿 Branch Naming Conventions

### Format
```
<type>/<scope>-<short-description>
```

### Types
- `feature/` - New features or enhancements
- `fix/` - Bug fixes
- `hotfix/` - Critical production fixes
- `refactor/` - Code refactoring
- `docs/` - Documentation changes
- `test/` - Adding or updating tests
- `chore/` - Maintenance tasks

### Examples
```bash
feature/weighing-wim-mode
feature/prosecution-special-release
fix/permit-expiry-validation
fix/reweigh-cycle-limit
hotfix/jwt-token-expiration
refactor/weighing-service-logic
docs/update-erd
test/add-weighing-integration-tests
chore/upgrade-ef-core-10
```

### Branch Lifecycle
```bash
# Create feature branch from develop
git checkout develop
git pull origin develop
git checkout -b feature/weighing-wim-mode

# Work on feature
git add .
git commit -m "feat(weighing): add WIM mode support"

# Keep branch updated
git fetch origin
git rebase origin/develop

# Push to remote
git push origin feature/weighing-wim-mode

# After merge, delete branch
git branch -d feature/weighing-wim-mode
git push origin --delete feature/weighing-wim-mode
```

## 💬 Commit Message Conventions

### Format
```
<type>(<scope>): <subject>

<body>

<footer>
```

### Type
- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation changes
- `style` - Code formatting (no logic change)
- `refactor` - Code refactoring
- `perf` - Performance improvement
- `test` - Adding/updating tests
- `chore` - Build/tooling changes
- `ci` - CI/CD changes
- `revert` - Reverting previous commit

### Scope
Module or feature area affected:
- `weighing` - Weighing operations
- `prosecution` - Case management
- `auth` - Authentication/authorization
- `user` - User management
- `vehicle` - Vehicle management
- `permit` - Permit handling
- `yard` - Yard operations
- `config` - Configuration
- `db` - Database/migrations
- `api` - API changes

### Subject
- Use imperative mood ("add" not "added")
- Don't capitalize first letter
- No period at the end
- Maximum 50 characters

### Body (Optional)
- Explain what and why, not how
- Wrap at 72 characters
- Separate from subject with blank line

### Footer (Optional)
- Reference issues: `Closes #123`, `Fixes #456`
- Breaking changes: `BREAKING CHANGE: description`

### Examples

#### Feature Addition
```
feat(weighing): add WIM mode support

Implemented Weigh-In-Motion capturing with auto-detection
of stable weights per axle as vehicle moves over scale.

- Added WeighingMode.WeighInMotion enum
- Implemented auto-stabilization algorithm
- Updated weighing service with WIM logic

Closes #42
```

#### Bug Fix
```
fix(prosecution): correct EAC charge calculation for permits

Fixed issue where permit vehicles were charged against base
limits instead of permit-extended limits.

Fixes #87
```

#### Breaking Change
```
feat(api): update weighing endpoint response structure

Changed weighing transaction response to include nested
axle data instead of flat structure for better clarity.

BREAKING CHANGE: WeighingTransactionDto now includes
`axles` array property instead of separate axle IDs.
Clients must update their parsing logic.

Closes #156
```

#### Simple Fix
```
fix(auth): handle null refresh token gracefully
```

#### Documentation
```
docs(erd): update vehicle entity relationships
```

#### Refactoring
```
refactor(weighing): extract compliance calculation logic

Moved EAC and Traffic Act calculation logic into separate
service for better testability and reusability.
```

### Commit Frequency

- **Commit often**: Small, logical chunks
- **One concept per commit**: Don't mix unrelated changes
- **Working state**: Each commit should build and pass tests
- **Meaningful messages**: Explain context, not just what changed

## 🔀 Pull Request Guidelines

### PR Title Format
Use same format as commit messages:
```
feat(weighing): add WIM mode support
fix(prosecution): correct EAC charge calculation
```

### PR Description Template
```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Feature
- [ ] Bug fix
- [ ] Hotfix
- [ ] Refactoring
- [ ] Documentation
- [ ] Test

## Related Issues
Closes #123
Related to #456

## Changes Made
- Change 1
- Change 2
- Change 3

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing performed
- [ ] All tests passing

## Screenshots (if applicable)

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Comments added for complex logic
- [ ] Documentation updated
- [ ] No new warnings generated
- [ ] Tests added/updated
- [ ] Database migrations created (if needed)
- [ ] No secrets committed
```

### PR Review Process

1. **Self-Review**: Review your own changes first
2. **Request Review**: Assign 1-2 reviewers
3. **Address Feedback**: Respond to all comments
4. **Update PR**: Push additional commits or squash
5. **Approval**: Wait for at least 1 approval
6. **Merge**: Squash and merge to develop/main

### PR Best Practices

- Keep PRs small and focused (< 500 lines ideally)
- Include tests for new features
- Update documentation
- Link related issues
- Add screenshots for UI changes
- Ensure CI checks pass
- Resolve conflicts before requesting review

## 🏷️ Code Review Guidelines

### What to Look For

#### Functionality
- Does it work as intended?
- Are edge cases handled?
- Is error handling appropriate?

#### Code Quality
- Is it readable and maintainable?
- Does it follow conventions?
- Is there unnecessary complexity?
- Are there code smells?

#### Performance
- Are there N+1 query issues?
- Is caching used appropriately?
- Are there memory leaks?

#### Security
- Is input validated?
- Are SQL injections prevented?
- Are secrets handled properly?
- Is authentication/authorization correct?

#### Testing
- Are there adequate tests?
- Do tests cover edge cases?
- Are tests meaningful?

### Giving Feedback

- **Be Kind**: Assume positive intent
- **Be Specific**: Point to exact lines
- **Explain Why**: Don't just say "change this"
- **Suggest Solutions**: Offer alternatives
- **Praise Good Work**: Acknowledge good patterns

### Receiving Feedback

- **Don't Take it Personally**: It's about code, not you
- **Ask Questions**: Clarify if unsure
- **Be Open**: Consider different perspectives
- **Say Thanks**: Appreciate the time reviewers spend

## 🔒 Security Guidelines

### Secrets Management
```csharp
// ❌ NEVER do this
public const string ApiKey = "sk-12345...";
var connString = "Server=localhost;Password=mysecret;";

// ✅ Use configuration
private readonly string _apiKey = configuration["ExternalApi:ApiKey"];
var connString = configuration.GetConnectionString("DefaultConnection");
```

### Sensitive Data in Logs
```csharp
// ❌ Don't log sensitive data
_logger.LogInformation($"User {user.Email} logged in with password {password}");

// ✅ Log safe information
_logger.LogInformation($"User {user.Id} logged in successfully");
```

### Input Validation
```csharp
// Always validate and sanitize input
public async Task<IActionResult> CreateWeighing([FromBody] CreateWeighingDto dto)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    // Additional business validation
    var validationResult = await _validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
        return BadRequest(validationResult.Errors);
    
    // Process request
}
```

## 📊 Database Guidelines

### Migrations
```bash
# Create migration
dotnet ef migrations add AddWeighingWimMode

# Review migration before applying
# Check Up and Down methods

# Apply migration
dotnet ef database update

# Rollback if needed
dotnet ef database update PreviousMigration
```

### Migration Best Practices

- **Descriptive Names**: Use clear, action-based names
- **Test Rollback**: Ensure Down() method works
- **Seed Data**: Include reference data in migrations
- **Avoid Data Loss**: Be careful with column drops
- **Index Creation**: Create indexes for foreign keys and frequent queries

## 🧪 Testing Standards

### Test Naming
```csharp
// Pattern: MethodName_Scenario_ExpectedResult
[Fact]
public async Task CreateWeighing_ValidData_ReturnsWeighingId()
{
    // Arrange
    var dto = new CreateWeighingDto { /* ... */ };
    
    // Act
    var result = await _service.CreateWeighingAsync(dto);
    
    // Assert
    result.Should().NotBeNull();
    result.Id.Should().BeGreaterThan(0);
}

[Fact]
public async Task CreateWeighing_InvalidVehicle_ThrowsValidationException()
{
    // Arrange
    var dto = new CreateWeighingDto { VehicleRegistration = "" };
    
    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(
        () => _service.CreateWeighingAsync(dto)
    );
}
```

### Test Coverage Goals

- **Unit Tests**: 80%+ coverage for services and business logic
- **Integration Tests**: Cover critical workflows
- **Edge Cases**: Test boundary conditions
- **Error Paths**: Test failure scenarios

## 📚 Documentation Standards

### Code Comments
```csharp
// ✅ Good: Explain WHY, not WHAT
// Permit vehicles get extended GVM limits per EAC regulations
var allowedGvm = vehicle.HasPermit 
    ? vehicle.Permit.ExtendedGvm 
    : axleConfig.LegalGvm;

// ❌ Bad: Obvious statement
// Check if vehicle has permit
if (vehicle.HasPermit) { }
```

### XML Documentation
```csharp
/// <summary>
/// Calculates compliance status against EAC or Traffic Act regulations.
/// </summary>
/// <param name="weighingId">The weighing transaction ID</param>
/// <param name="applicableAct">EAC or Traffic Act</param>
/// <returns>Compliance result with charges and violations</returns>
/// <exception cref="NotFoundException">Thrown when weighing not found</exception>
public async Task<ComplianceResult> CalculateComplianceAsync(
    long weighingId, 
    ApplicableAct applicableAct)
{
    // Implementation
}
```

## 🎯 Performance Guidelines

### Database Queries
```csharp
// ✅ Use AsNoTracking for read-only queries
var weighings = await _context.Weighings
    .AsNoTracking()
    .Where(w => w.StationId == stationId)
    .ToListAsync();

// ✅ Use projections to select only needed columns
var summary = await _context.Weighings
    .Where(w => w.StationId == stationId)
    .Select(w => new { w.Id, w.VehicleRegistration, w.TotalGvm })
    .ToListAsync();

// ❌ Avoid N+1 queries
foreach (var weighing in weighings)
{
    var vehicle = await _context.Vehicles.FindAsync(weighing.VehicleId);
}

// ✅ Use eager loading
var weighings = await _context.Weighings
    .Include(w => w.Vehicle)
    .Include(w => w.Axles)
    .Where(w => w.StationId == stationId)
    .ToListAsync();
```

### Caching Strategy
```csharp
// Cache frequently accessed, rarely changing data
var axleConfigs = await _cache.GetOrCreateAsync(
    "axle-configurations",
    async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
        return await _context.AxleConfigurations.ToListAsync();
    });
```

## 🤝 Communication

### Channels
- **GitHub Issues**: Bug reports, feature requests
- **Pull Requests**: Code review discussions
- **Discussions**: Architecture decisions, questions

### Response Times
- **Critical Issues**: Within 24 hours
- **PR Reviews**: Within 48 hours
- **Questions**: Within 72 hours

---

**Last Updated**: February 2, 2026

For questions or clarifications, please open a GitHub Discussion or contact the maintainers.
