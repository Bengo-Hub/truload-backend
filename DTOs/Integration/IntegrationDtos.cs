namespace TruLoad.Backend.DTOs.Integration;

/// <summary>
/// Result from KeNHA vehicle tag verification API.
/// </summary>
public class KeNHATagVerificationResult
{
    /// <summary>Whether a tag was found for this vehicle.</summary>
    public bool HasTag { get; set; }

    /// <summary>Tag status: open, closed, expired.</summary>
    public string? TagStatus { get; set; }

    /// <summary>Vehicle registration number searched.</summary>
    public string RegNo { get; set; } = string.Empty;

    /// <summary>Tag category/reason from KeNHA (e.g., habitual offender, overload, stolen).</summary>
    public string? TagCategory { get; set; }

    /// <summary>Reason description from KeNHA.</summary>
    public string? Reason { get; set; }

    /// <summary>Station where tag was created.</summary>
    public string? Station { get; set; }

    /// <summary>Date the tag was created.</summary>
    public DateTime? TagDate { get; set; }

    /// <summary>Unique tag identifier from KeNHA system.</summary>
    public string? TagUid { get; set; }

    /// <summary>Raw JSON response from KeNHA API for debugging.</summary>
    public string? RawResponse { get; set; }
}

/// <summary>
/// Result from NTSA vehicle search API.
/// Based on KenLoad V2 NTSA response structure.
/// </summary>
public class NTSAVehicleSearchResult
{
    /// <summary>Whether vehicle was found in NTSA records.</summary>
    public bool Found { get; set; }

    /// <summary>Vehicle registration number.</summary>
    public string RegNo { get; set; } = string.Empty;

    // Owner Information
    public string? OwnerFirstName { get; set; }
    public string? OwnerLastName { get; set; }
    public string? OwnerType { get; set; }
    public string? OwnerAddress { get; set; }
    public string? OwnerTown { get; set; }
    public string? OwnerPhone { get; set; }

    // Vehicle Information
    public string? ChassisNo { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? BodyType { get; set; }
    public int? PassengerCapacity { get; set; }
    public int? YearOfManufacture { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? LogbookNumber { get; set; }

    // Inspection Information
    public string? InspectionCenter { get; set; }
    public DateTime? InspectionDate { get; set; }
    public DateTime? InspectionExpiryDate { get; set; }
    public string? InspectionStatus { get; set; }

    // Caveat Information
    public string? CaveatReason { get; set; }
    public string? CaveatStatus { get; set; }
    public string? CaveatType { get; set; }

    /// <summary>Raw JSON response from NTSA API for debugging.</summary>
    public string? RawResponse { get; set; }
}

/// <summary>
/// Health check result for integration connectivity tests.
/// </summary>
public class IntegrationHealthResult
{
    public bool IsHealthy { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int? ResponseTimeMs { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
