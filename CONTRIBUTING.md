# Contributing to TruLoad Backend

Thank you for considering contributing to the TruLoad Backend project! This document provides guidelines and standards for contributing.

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Commit Message Guidelines](#commit-message-guidelines)

## 📜 Code of Conduct

- Be respectful and inclusive
- Welcome newcomers and help them get started
- Focus on what is best for the community
- Show empathy towards other community members

## 🚀 Getting Started

### Prerequisites

- .NET 8 SDK or later
- Docker Desktop
- Git
- IDE: Visual Studio 2022, VS Code, or JetBrains Rider
- PostgreSQL 16+ (or use Docker Compose)
- Redis 7+ (or use Docker Compose)

### Setting Up Development Environment

1. **Fork and Clone**
   ```bash
   git clone https://github.com/YOUR_USERNAME/truload-backend.git
   cd truload-backend
   ```

2. **Install Dependencies**
   ```bash
   dotnet restore
   ```

3. **Set Up Local Database**
   ```bash
   # Option 1: Use Docker Compose (recommended)
   docker-compose up -d postgres redis rabbitmq
   
   # Option 2: Install locally and update appsettings.Development.json
   ```

4. **Run Migrations**
   ```bash
   dotnet ef database update
   ```

5. **Run the Application**
   ```bash
   dotnet run
   ```

6. **Access Swagger UI**
   Navigate to `https://localhost:7001/swagger`

## 🔄 Development Workflow

### Branching Strategy

- `main` - Production-ready code
- `develop` - Integration branch for features
- `feature/{module-name}` - Feature branches
- `fix/{issue-number}` - Bug fix branches
- `hotfix/{issue}` - Critical production fixes

### Creating a Feature Branch

```bash
git checkout develop
git pull origin develop
git checkout -b feature/weighing-module
```

## 🎨 Coding Standards

### C# Style Guidelines

Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions):

- **Naming:**
  - PascalCase for classes, methods, properties, public fields
  - camelCase for local variables, parameters, private fields
  - Prefix interfaces with `I` (e.g., `IWeighingService`)
  - Suffix async methods with `Async`

- **File Organization:**
  - One class per file
  - File name matches class name
  - Organize usings: System namespaces first, then third-party, then project namespaces

- **Code Structure:**
  ```csharp
  // ✅ Good
  public class WeighingService : IWeighingService
  {
      private readonly ILogger<WeighingService> _logger;
      private readonly ApplicationDbContext _context;
      
      public WeighingService(ILogger<WeighingService> logger, ApplicationDbContext context)
      {
          _logger = logger;
          _context = context;
      }
      
      public async Task<WeighingResult> CreateWeighingAsync(CreateWeighingCommand command)
      {
          // Implementation
      }
  }
  ```

### Module Structure

Each module follows vertical slice architecture:

```
Modules/
├── Weighing/
│   ├── Commands/
│   │   └── CreateWeighingCommand.cs
│   ├── Queries/
│   │   └── GetWeighingByIdQuery.cs
│   ├── Handlers/
│   │   ├── CreateWeighingHandler.cs
│   │   └── GetWeighingByIdHandler.cs
│   ├── Validators/
│   │   └── CreateWeighingValidator.cs
│   ├── DTOs/
│   │   └── WeighingDto.cs
│   └── WeighingController.cs
```

### Database Conventions

- **Table Names:** snake_case, plural (e.g., `weighing_axles`)
- **Column Names:** snake_case
- **Primary Keys:** `id` (BIGSERIAL)
- **Foreign Keys:** `{entity}_id`
- **Timestamps:** Always include `created_at`, `updated_at`
- **Soft Deletes:** Use `deleted_at` (nullable)

### API Conventions

- **Versioning:** `/api/v1/...`
- **HTTP Methods:**
  - GET - Retrieve resources
  - POST - Create resources
  - PUT - Full update
  - PATCH - Partial update
  - DELETE - Remove resource

- **Response Format:**
  ```json
  {
    "success": true,
    "data": { ... },
    "meta": {
      "timestamp": "2025-10-28T12:34:56Z",
      "version": "v1"
    },
    "errors": []
  }
  ```

- **Error Codes:**
  - 200 OK
  - 201 Created
  - 400 Bad Request
  - 401 Unauthorized
  - 403 Forbidden
  - 404 Not Found
  - 409 Conflict
  - 500 Internal Server Error

## 🧪 Testing Guidelines

### Unit Tests

- Use xUnit framework
- Place tests in `Tests/{ModuleName}Tests/` directory
- Name test methods: `MethodName_Scenario_ExpectedResult`
- Use FluentAssertions for readable assertions

```csharp
[Fact]
public async Task CreateWeighing_ValidData_ReturnsWeighingId()
{
    // Arrange
    var command = new CreateWeighingCommand { /* ... */ };
    var handler = new CreateWeighingHandler(_context, _logger);
    
    // Act
    var result = await handler.Handle(command, CancellationToken.None);
    
    // Assert
    result.Should().NotBeNull();
    result.WeighingId.Should().BeGreaterThan(0);
}
```

### Integration Tests

- Use Testcontainers for database
- Seed test data via fixtures
- Clean up after each test

### Test Coverage

- Aim for >80% code coverage
- Focus on business logic and validation
- Mock external dependencies (TruConnect, eCitizen)

## 📝 Pull Request Process

1. **Create Feature Branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make Changes**
   - Write clean, documented code
   - Add/update tests
   - Update documentation

3. **Commit Changes**
   ```bash
   git add .
   git commit -m "feat(weighing): add WIM support"
   ```

4. **Push to Fork**
   ```bash
   git push origin feature/your-feature-name
   ```

5. **Open Pull Request**
   - Use PR template
   - Link related issues
   - Request review from maintainers

6. **Address Review Feedback**
   - Make requested changes
   - Push updates to same branch
   - Re-request review

7. **Merge**
   - Squash commits if requested
   - Delete feature branch after merge

## 💬 Commit Message Guidelines

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation changes
- `style` - Code style changes (formatting, etc.)
- `refactor` - Code refactoring
- `perf` - Performance improvements
- `test` - Adding or updating tests
- `chore` - Maintenance tasks
- `ci` - CI/CD changes

### Examples

```
feat(weighing): add support for WIM mode

Implemented Weigh-In-Motion capturing with auto-detection
of stable weights per axle as vehicle moves over scale.

Closes #42
```

```
fix(prosecution): correct EAC charge calculation for permits

Fixed issue where permit vehicles were charged against base
limits instead of permit-extended limits.

Fixes #87
```

## 🔍 Code Review Checklist

Before submitting PR, ensure:

- [ ] Code follows style guidelines
- [ ] All tests pass (`dotnet test`)
- [ ] New features have tests
- [ ] Documentation updated
- [ ] No unnecessary dependencies added
- [ ] No secrets or sensitive data committed
- [ ] Database migrations are reversible
- [ ] API changes are backward compatible (or versioned)
- [ ] Logging is appropriate (no sensitive data in logs)

## 🐛 Reporting Bugs

Create an issue with:

- **Title:** Clear, concise description
- **Description:** Steps to reproduce, expected vs actual behavior
- **Environment:** OS, .NET version, deployment environment
- **Logs:** Relevant log excerpts
- **Screenshots:** If applicable

## 💡 Suggesting Features

Create an issue with:

- **Title:** Feature request summary
- **Problem:** What problem does this solve?
- **Solution:** Proposed implementation
- **Alternatives:** Other approaches considered
- **Additional Context:** Mockups, references, etc.

## 📚 Additional Resources

- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [EAC Vehicle Load Control Act (2016)](docs/BACKEND_IMPLEMENTATION_PLAN.md#5-legal-computation-rules-summary)

## 🙏 Thank You!

Your contributions make TruLoad better for everyone. We appreciate your time and effort!

