# Data Layer

This folder contains all database-related code for the TruLoad backend.

## Structure

```
Data/
├── TruLoadDbContext.cs              # Main DbContext with all DbSets
├── Configurations/                   # Entity type configurations
│   ├── UserConfiguration.cs
│   ├── WeighSessionConfiguration.cs
│   ├── ProsecutionCaseConfiguration.cs
│   └── ...
├── Migrations/                       # EF Core migrations (auto-generated)
│   ├── 20250101000000_InitialCreate.cs
│   └── ...
└── Seed/                            # Seed data classes
    ├── RoleSeed.cs
    ├── AxleConfigurationSeed.cs
    ├── EacActFeeSeed.cs
    └── TrafficActFeeSeed.cs
```

## DbContext: `TruLoadDbContext`

The main database context that:
- Inherits from `IdentityDbContext<IdentityUser>` for built-in user management
- Contains all `DbSet<T>` properties for entities
- Configures entity relationships in `OnModelCreating()`
- Seeds initial reference data

## Entity Configurations

Following EF Core best practices, each entity should have its own configuration class implementing `IEntityTypeConfiguration<T>`. This keeps the DbContext clean and makes configurations reusable.

Example:
```csharp
public class WeighSessionConfiguration : IEntityTypeConfiguration<WeighSession>
{
    public void Configure(EntityTypeBuilder<WeighSession> builder)
    {
        builder.ToTable("WeighSessions");
        
        builder.HasKey(w => w.Id);
        
        builder.Property(w => w.RegistrationNumber)
            .IsRequired()
            .HasMaxLength(20);
            
        builder.HasIndex(w => w.RegistrationNumber);
        builder.HasIndex(w => w.WeighDate);
        
        builder.HasOne(w => w.Vehicle)
            .WithMany(v => v.WeighSessions)
            .HasForeignKey(w => w.VehicleId);
    }
}
```

Then in `TruLoadDbContext.OnModelCreating()`:
```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(TruLoadDbContext).Assembly);
```

## Migrations

To create and apply migrations:

```bash
# Create a new migration
dotnet ef migrations add MigrationName

# Apply migrations to database
dotnet ef database update

# Rollback to a specific migration
dotnet ef database update PreviousMigrationName

# Remove last migration (if not applied)
dotnet ef migrations remove

# Generate SQL script for production
dotnet ef migrations script -o migration.sql
```

## Seed Data

Initial reference data should be seeded in `OnModelCreating()` or using separate seed classes:

- **Roles**: Administrator, Operator, Supervisor, etc.
- **Axle Configurations**: 2A, 3A, 4A, 5A, 6A as per EAC/Traffic Act
- **EAC Act Fees**: Fee bands for overload charges
- **Traffic Act Fees**: GVW-based fee structure
- **System Settings**: Default values for tolerances, thresholds
- **Stations**: Initial weighbridge stations
- **Default Admin User**: For initial login

## Design Principles

1. **Separation of Concerns**: Keep data access logic separate from business logic
2. **Entity Configurations**: Use `IEntityTypeConfiguration<T>` for complex configurations
3. **Naming Conventions**: Use Pascal case for entity names and properties
4. **Indexes**: Add indexes on frequently queried columns (registration, dates, foreign keys)
5. **Constraints**: Define proper constraints (required, max length, unique, etc.)
6. **Relationships**: Explicitly configure relationships with `HasOne`, `HasMany`, `WithMany`
7. **Soft Deletes**: Consider using `IsDeleted` flag instead of hard deletes for auditing
8. **Audit Fields**: Include `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on all tables
9. **Concurrency**: Use `[Timestamp]` or `RowVersion` for optimistic concurrency
10. **Partitioning**: Consider table partitioning for high-volume tables (WeighSessions, AuditLogs)

## Performance Considerations

- Use `.AsNoTracking()` for read-only queries
- Use `.Include()` judiciously to avoid N+1 queries
- Consider projections (`.Select()`) for large entities
- Use pagination for large result sets
- Add database indexes on foreign keys and frequently queried fields
- Use compiled queries for frequently executed queries
- Consider read replicas for reporting queries

## Security

- Never expose the DbContext directly to controllers
- Use repository pattern or services to encapsulate data access
- Validate all user input before querying
- Use parameterized queries (EF Core does this by default)
- Implement proper authorization checks in business layer
- Encrypt sensitive data in the database (PII, financial data)
- Log all data access operations to audit table

