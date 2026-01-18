using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;

namespace TruLoad.Backend.Data.Configurations.Traffic;

/// <summary>
/// Traffic Module DbContext Configuration
/// Contains configurations for traffic-related entities including:
/// - Driver, DriverDemeritRecord
/// </summary>
public static class TrafficModuleDbContextConfiguration
{
    /// <summary>
    /// Applies traffic module configurations to the model builder
    /// </summary>
    public static ModelBuilder ApplyTrafficConfigurations(this ModelBuilder modelBuilder)
    {
        // Configuration moved to WeighingModuleDbContextConfiguration.cs as Driver entities are now part of Weighing Core.
        // Keeping this method stub to prevent breaking TruLoadDbContext until full refactor.
        
        return modelBuilder;
    }
}
