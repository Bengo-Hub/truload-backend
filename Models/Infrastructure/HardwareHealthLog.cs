using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Infrastructure;

/// <summary>
/// Logs hardware device status checks for weighbridge equipment
/// Supports polling monitoring as per FRD C.1 - Hardware Health Monitoring
/// </summary>
[Table("hardware_health_logs")]
public class HardwareHealthLog : BaseEntity
{

    [Required]
    [Column("device_name")]
    [StringLength(100)]
    public string DeviceName { get; set; } = string.Empty; // e.g., "Weighbridge Scale", "Printer", "Camera"

    [Required]
    [Column("device_type")]
    [StringLength(50)]
    public string DeviceType { get; set; } = string.Empty; // SCALE, PRINTER, CAMERA, INDICATOR, BARRIER

    [Required]
    [Column("station_id")]
    public Guid StationId { get; set; }

    [Column("ip_address")]
    [StringLength(45)] // IPv6 max length
    public string? IpAddress { get; set; }

    [Column("port")]
    public int? Port { get; set; }

    [Required]
    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = "unknown"; // online, offline, error, unknown

    [Column("response_time_ms")]
    public int? ResponseTimeMs { get; set; }

    [Column("error_message")]
    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    [Column("is_critical")]
    public bool IsCritical { get; set; } = false; // Critical devices: Indicator, Camera

    [Required]
    [Column("checked_at")]
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    [Column("checked_by")]
    [StringLength(100)]
    public string? CheckedBy { get; set; } = "system"; // "system" for automated polls, user ID for manual checks

    [Column("metadata")]
    [StringLength(1000)]
    public string? Metadata { get; set; } // JSON string for additional device-specific data

    // Navigation property
    [ForeignKey("StationId")]
    public virtual Station? Station { get; set; }
}

/// <summary>
/// Registry of weighbridge hardware devices for monitoring
/// </summary>
[Table("weighbridge_hardware")]
public class WeighbridgeHardware : BaseEntity
{

    [Required]
    [Column("device_name")]
    [StringLength(100)]
    public string DeviceName { get; set; } = string.Empty;

    [Required]
    [Column("device_type")]
    [StringLength(50)]
    public string DeviceType { get; set; } = string.Empty; // SCALE, PRINTER, CAMERA, INDICATOR, BARRIER

    [Required]
    [Column("station_id")]
    public Guid StationId { get; set; }

    [Column("ip_address")]
    [StringLength(45)]
    public string? IpAddress { get; set; }

    [Column("port")]
    public int? Port { get; set; }

    [Column("manufacturer")]
    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [Column("model")]
    [StringLength(100)]
    public string? Model { get; set; }

    [Column("serial_number")]
    [StringLength(100)]
    public string? SerialNumber { get; set; }

    [Required]
    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = "unknown"; // online, offline, error, maintenance, unknown

    [Column("is_critical")]
    public bool IsCritical { get; set; } = false; // Critical devices: Indicator, Camera

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true; // Whether device should be monitored

    [Column("last_checked_at")]
    public DateTime? LastCheckedAt { get; set; }

    [Column("last_online_at")]
    public DateTime? LastOnlineAt { get; set; }

    [Column("polling_interval_seconds")]
    public int PollingIntervalSeconds { get; set; } = 60; // Default: check every minute

    // Navigation property
    [ForeignKey("StationId")]
    public virtual Station? Station { get; set; }
}
