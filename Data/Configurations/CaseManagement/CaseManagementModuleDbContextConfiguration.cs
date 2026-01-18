using Microsoft.EntityFrameworkCore;

namespace TruLoad.Backend.Data.Configurations.CaseManagement;

/// <summary>
/// Module coordinator for Case Management entity configurations.
/// Delegates to specialized configuration files for better maintainability.
/// </summary>
public static class CaseManagementModuleDbContextConfiguration
{
    /// <summary>
    /// Applies all case management module entity configurations to the model builder.
    /// </summary>
    public static void ApplyCaseManagementConfigurations(this ModelBuilder modelBuilder)
    {
        // Apply core case management entities configuration
        modelBuilder.ApplyCoreEntitiesConfigurations();

        // Apply extended case management entities configuration (Sprint 11)
        modelBuilder.ApplyExtendedEntitiesConfigurations();

        // Apply configuration/taxonomy entities configuration
        modelBuilder.ApplyConfigurationEntitiesConfigurations();
    }
}
