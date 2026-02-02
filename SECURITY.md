# Security Policy

## Supported Versions

We release patches for security vulnerabilities in the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 0.2.x   | :white_check_mark: |
| 0.1.x   | :x:                |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

If you discover a security vulnerability in TruLoad Backend, please report it by emailing:

**security@truload.example.com** (or contact repository maintainers privately)

Include the following information:

- **Type of vulnerability** (e.g., SQL injection, XSS, authentication bypass)
- **Full paths** of source files related to the vulnerability
- **Location** of the affected code (tag/branch/commit or direct URL)
- **Step-by-step instructions** to reproduce the issue
- **Proof-of-concept or exploit code** (if possible)
- **Impact** of the vulnerability
- **Suggested fix** (if you have one)

### What to Expect

- **Acknowledgment**: Within 48 hours of report submission
- **Initial Assessment**: Within 5 business days
- **Regular Updates**: Every 7 days until resolved
- **Resolution**: Timeline depends on severity and complexity
- **Credit**: Public acknowledgment in CHANGELOG (unless you prefer anonymity)

## Security Best Practices

### For Contributors

#### 1. Authentication & Authorization

- **Never commit credentials** to the repository
- **Use ASP.NET Core Identity** for user authentication
- **Implement permission-based authorization** on all endpoints
- **Validate JWT tokens** on every protected request
- **Enforce password policies** via Identity configuration
- **Use httpOnly cookies** for token storage

```csharp
// ✅ Good: Permission-based authorization
[Authorize(Policy = "Weighing.Create")]
public async Task<IActionResult> CreateWeighing([FromBody] CreateWeighingDto dto)

// ❌ Bad: No authorization check
public async Task<IActionResult> CreateWeighing([FromBody] CreateWeighingDto dto)
```

#### 2. Input Validation

- **Validate all input** using FluentValidation
- **Sanitize user input** before database queries
- **Use parameterized queries** (EF Core does this automatically)
- **Validate file uploads** (type, size, content)
- **Implement rate limiting** on authentication endpoints

```csharp
// ✅ Good: Input validation
public class CreateWeighingValidator : AbstractValidator<CreateWeighingDto>
{
    public CreateWeighingValidator()
    {
        RuleFor(x => x.VehicleRegistration)
            .NotEmpty()
            .MaximumLength(20)
            .Matches("^[A-Z0-9\\s]+$");
    }
}

// ❌ Bad: No validation
await _context.Weighings.AddAsync(new Weighing { 
    VehicleRegistration = dto.VehicleRegistration 
});
```

#### 3. Secrets Management

- **Never hardcode secrets** in source code
- **Use appsettings.json** for configuration (excluded from git)
- **Use environment variables** in production
- **Use Azure Key Vault** or similar for production secrets
- **Rotate secrets regularly**

```csharp
// ✅ Good: Configuration-based
var apiKey = _configuration["ExternalApi:ApiKey"];

// ❌ Bad: Hardcoded secret
var apiKey = "sk-1234567890abcdef";
```

#### 4. Logging & Monitoring

- **Never log sensitive data** (passwords, tokens, PII)
- **Log authentication attempts** (success and failure)
- **Log authorization failures**
- **Implement structured logging** with Serilog
- **Set up alerts** for suspicious activity

```csharp
// ✅ Good: Safe logging
_logger.LogInformation("User {UserId} logged in successfully", user.Id);

// ❌ Bad: Logging sensitive data
_logger.LogInformation("User {Email} logged in with password {Password}", 
    user.Email, password);
```

#### 5. Database Security

- **Use least privilege** for database users
- **Enable SSL/TLS** for database connections
- **Implement soft deletes** instead of hard deletes
- **Encrypt sensitive columns** (if storing PII)
- **Regular backups** with encryption
- **Audit trail** for sensitive operations

```csharp
// ✅ Good: Soft delete
entity.DeletedAt = DateTime.UtcNow;
await _context.SaveChangesAsync();

// Consider: Hard delete only for GDPR right to erasure
```

#### 6. API Security

- **Use HTTPS only** (enforce in production)
- **Implement CORS** restrictions
- **Add security headers** (HSTS, X-Content-Type-Options, etc.)
- **Implement rate limiting**
- **Version APIs** to maintain backward compatibility
- **Document security requirements** in Swagger/OpenAPI

```csharp
// Program.cs security configuration
app.UseHsts();
app.UseHttpsRedirection();

app.UseCors(policy => policy
    .WithOrigins("https://truload.example.com")
    .AllowCredentials()
    .AllowAnyHeader()
    .AllowAnyMethod());

app.UseAuthentication();
app.UseAuthorization();
```

#### 7. Dependency Management

- **Keep dependencies updated** (security patches)
- **Review NuGet packages** before adding
- **Monitor for vulnerabilities** (GitHub Dependabot)
- **Audit transitive dependencies**
- **Use trusted sources only**

```bash
# Check for vulnerabilities
dotnet list package --vulnerable
dotnet list package --outdated
```

#### 8. Error Handling

- **Don't expose stack traces** in production
- **Use generic error messages** for users
- **Log detailed errors** server-side
- **Implement global exception handler**

```csharp
// ✅ Good: Generic error response
catch (Exception ex)
{
    _logger.LogError(ex, "Error creating weighing transaction");
    return StatusCode(500, new { 
        message = "An error occurred processing your request" 
    });
}

// ❌ Bad: Exposing details
catch (Exception ex)
{
    return BadRequest(new { 
        message = ex.Message, 
        stackTrace = ex.StackTrace 
    });
}
```

## Security Features

### Implemented

- ✅ JWT-based authentication with refresh tokens
- ✅ Permission-based authorization (77 permissions)
- ✅ Password hashing with ASP.NET Core Identity
- ✅ HTTPS enforcement in production
- ✅ Input validation with FluentValidation
- ✅ Parameterized database queries via EF Core
- ✅ Structured logging with Serilog
- ✅ Global exception handling middleware
- ✅ CORS configuration
- ✅ Health check endpoints
- ✅ Soft delete for data retention
- ✅ Audit trails for sensitive operations

### Planned

- ⏳ Rate limiting on authentication endpoints
- ⏳ IP whitelisting for admin endpoints
- ⏳ Two-factor authentication (2FA)
- ⏳ Security headers middleware (CSP, etc.)
- ⏳ Automated security scanning in CI/CD
- ⏳ Penetration testing
- ⏳ GDPR compliance tooling

## Common Vulnerabilities & Mitigations

### SQL Injection

**Risk**: Malicious SQL code execution via user input

**Mitigation**: 
- Use EF Core (parameterized queries by default)
- Never concatenate SQL with user input
- Validate all input

### Cross-Site Scripting (XSS)

**Risk**: Malicious scripts executed in user's browser

**Mitigation**:
- Frontend sanitizes all user input
- API returns JSON (not HTML)
- Content-Type headers properly set

### Cross-Site Request Forgery (CSRF)

**Risk**: Unauthorized actions on behalf of authenticated user

**Mitigation**:
- Use SameSite cookies
- Implement anti-forgery tokens for forms
- Validate Origin/Referer headers

### Broken Authentication

**Risk**: Unauthorized access due to weak authentication

**Mitigation**:
- ASP.NET Core Identity for secure authentication
- Strong password policies enforced
- JWT tokens with short expiration
- Secure token storage (httpOnly cookies)
- Account lockout after failed attempts

### Sensitive Data Exposure

**Risk**: Unauthorized access to sensitive data

**Mitigation**:
- HTTPS only in production
- Don't log sensitive data
- Encrypt data at rest (database encryption)
- Encrypt data in transit (TLS 1.2+)

### Broken Access Control

**Risk**: Users accessing unauthorized resources

**Mitigation**:
- Permission-based authorization on all endpoints
- Validate user ownership before operations
- Implement tenant isolation
- Regular permission audits

### Security Misconfiguration

**Risk**: Insecure default configurations

**Mitigation**:
- Remove unused features/endpoints
- Disable directory listing
- Remove default accounts
- Keep frameworks updated
- Regular security reviews

### Insufficient Logging & Monitoring

**Risk**: Security incidents go undetected

**Mitigation**:
- Structured logging with Serilog
- Log all authentication/authorization events
- Set up monitoring alerts
- Regular log reviews
- Centralized log management

## Security Checklist for PRs

Before submitting a PR, ensure:

- [ ] No secrets or credentials committed
- [ ] All new endpoints have authorization checks
- [ ] Input validation implemented
- [ ] Error handling doesn't expose sensitive info
- [ ] No sensitive data in logs
- [ ] Database queries are parameterized
- [ ] Dependencies are up to date
- [ ] Security implications documented
- [ ] Tests cover security scenarios

## Incident Response

In case of a security incident:

1. **Contain**: Isolate affected systems
2. **Assess**: Determine scope and impact
3. **Notify**: Contact security team immediately
4. **Document**: Record all details
5. **Patch**: Develop and deploy fix
6. **Review**: Conduct post-incident analysis
7. **Communicate**: Notify affected users (if applicable)

## Compliance

TruLoad handles weighing enforcement data and must comply with:

- **Data Protection**: Personal data handling (GDPR principles)
- **Audit Requirements**: All enforcement actions must be auditable
- **Legal Compliance**: EAC Vehicle Load Control Act (2016), Kenya Traffic Act

## Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [.NET Security Best Practices](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [Entity Framework Core Security](https://learn.microsoft.com/en-us/ef/core/miscellaneous/nullable-reference-types#non-nullable-reference-types-and-entity-framework-core)

## Contact

For security concerns, contact:
- **Email**: security@truload.example.com
- **Maintainers**: [See CODEOWNERS]

---

**Last Updated**: February 2, 2026

This security policy is subject to change. Please check regularly for updates.
