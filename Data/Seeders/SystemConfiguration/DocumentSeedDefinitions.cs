using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Single source of truth for document types that have both a naming convention and a sequence.
/// Used by DocumentConventionSeeder and DocumentSequenceSeeder so conventions and sequences stay aligned
/// (same document types, matching ResetFrequency). DocumentNumberService links them by OrganizationId + DocumentType.
/// </summary>
public static class DocumentSeedDefinitions
{
    /// <summary>
    /// Reset frequency for the sequence (and for the convention, so runtime-created sequences use the same value).
    /// </summary>
    public const string Daily = "daily";
    public const string Monthly = "monthly";
    public const string Never = "never";

    /// <summary>
    /// All document types with their convention defaults and sequence reset frequency.
    /// Order matches display preference; sequences and conventions are seeded for each.
    /// </summary>
    public static readonly IReadOnlyList<DocumentSeedEntry> All =
    [
        new DocumentSeedEntry(DocumentTypes.WeightTicket, "Weight Ticket", "", Daily,
            IncludeStationCode: true, IncludeBound: true, IncludeDate: true, DateFormat: "yyyyMMdd", IncludeVehicleReg: true),
        new DocumentSeedEntry(DocumentTypes.ReweighTicket, "Reweigh Ticket", "RWG", Daily,
            IncludeStationCode: true, IncludeBound: true, IncludeDate: true, DateFormat: "yyyyMMdd", IncludeVehicleReg: true),
        new DocumentSeedEntry(DocumentTypes.Invoice, "Invoice", "INV", Never,
            IncludeStationCode: false, IncludeBound: false, IncludeDate: true, DateFormat: "ddMMyy", IncludeVehicleReg: false),
        new DocumentSeedEntry(DocumentTypes.Receipt, "Receipt", "RCP", Never,
            IncludeStationCode: false, IncludeBound: false, IncludeDate: true, DateFormat: "ddMMyy", IncludeVehicleReg: false),
        new DocumentSeedEntry(DocumentTypes.ChargeSheet, "Charge Sheet", "CS", Monthly,
            IncludeStationCode: true, IncludeBound: false, IncludeDate: true, DateFormat: "yyyyMMdd", IncludeVehicleReg: false),
        new DocumentSeedEntry(DocumentTypes.ComplianceCertificate, "Compliance Certificate", "CC", Monthly,
            IncludeStationCode: true, IncludeBound: false, IncludeDate: true, DateFormat: "yyyyMMdd", IncludeVehicleReg: false),
        new DocumentSeedEntry(DocumentTypes.ProhibitionOrder, "Prohibition Order", "PO", Monthly,
            IncludeStationCode: true, IncludeBound: false, IncludeDate: true, DateFormat: "yyyyMMdd", IncludeVehicleReg: false),
        new DocumentSeedEntry(DocumentTypes.SpecialRelease, "Special Release Certificate", "SR", Monthly,
            IncludeStationCode: true, IncludeBound: false, IncludeDate: true, DateFormat: "yyyyMMdd", IncludeVehicleReg: false),
        new DocumentSeedEntry(DocumentTypes.LoadCorrectionMemo, "Load Correction Memo", "LCM", Monthly,
            IncludeStationCode: true, IncludeBound: false, IncludeDate: true, DateFormat: "yyyyMMdd", IncludeVehicleReg: false),
        new DocumentSeedEntry(DocumentTypes.CourtMinutes, "Court Minutes", "CM", Monthly,
            IncludeStationCode: true, IncludeBound: false, IncludeDate: true, DateFormat: "yyyyMMdd", IncludeVehicleReg: false),
        new DocumentSeedEntry(DocumentTypes.Permit, "Transport Permit", "PRM", Never,
            IncludeStationCode: false, IncludeBound: false, IncludeDate: true, DateFormat: "yyyyMMdd", IncludeVehicleReg: true),
    ];

    public sealed record DocumentSeedEntry(
        string DocumentType,
        string DisplayName,
        string Prefix,
        string ResetFrequency,
        bool IncludeStationCode,
        bool IncludeBound,
        bool IncludeDate,
        string DateFormat,
        bool IncludeVehicleReg,
        int SequencePadding = 4,
        string Separator = "-");
}
