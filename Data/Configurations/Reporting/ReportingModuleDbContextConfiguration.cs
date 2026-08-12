using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Reporting;

namespace TruLoad.Backend.Data.Configurations.Reporting;

/// <summary>
/// Reporting Module DbContext Configuration
/// Contains configurations for structured custom-report-builder saved configs.
/// </summary>
public static class ReportingModuleDbContextConfiguration
{
    public static ModelBuilder ApplyReportingConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SavedReportConfig>(entity =>
        {
            entity.ToTable("saved_report_configs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id")
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.Module)
                .HasColumnName("module")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ReportType)
                .HasColumnName("report_type")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.ColumnsJson)
                .HasColumnName("columns_json")
                .HasDefaultValue("[]")
                .IsRequired();

            entity.Property(e => e.ChartType)
                .HasColumnName("chart_type")
                .HasMaxLength(50);

            entity.Property(e => e.FiltersJson)
                .HasColumnName("filters_json");

            entity.Property(e => e.IsDefault)
                .HasColumnName("is_default")
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedByUserId)
                .HasColumnName("created_by_user_id")
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            entity.HasIndex(e => new { e.OrganizationId, e.Module, e.ReportType })
                .HasDatabaseName("idx_saved_report_configs_org_module_report_type");
        });

        return modelBuilder;
    }
}
