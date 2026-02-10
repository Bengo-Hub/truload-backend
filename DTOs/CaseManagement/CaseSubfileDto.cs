using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Case Subfile Data Transfer Object
/// </summary>
public class CaseSubfileDto
{
    public Guid Id { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string? CaseNo { get; set; }
    public Guid SubfileTypeId { get; set; }
    public string? SubfileTypeName { get; set; }
    public string? SubfileName { get; set; }
    public string? DocumentType { get; set; }
    public string? Content { get; set; }
    public string? FilePath { get; set; }
    public string? FileUrl { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? Checksum { get; set; }
    public Guid? UploadedById { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create Case Subfile Request
/// </summary>
public class CreateCaseSubfileRequest
{
    public Guid CaseRegisterId { get; set; }
    public Guid SubfileTypeId { get; set; }
    public string? SubfileName { get; set; }
    public string? DocumentType { get; set; }
    public string? Content { get; set; }
    public string? FilePath { get; set; }
    public string? FileUrl { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? Metadata { get; set; }
}

/// <summary>
/// Update Case Subfile Request
/// </summary>
public class UpdateCaseSubfileRequest
{
    public string? SubfileName { get; set; }
    public string? Content { get; set; }
    public string? FilePath { get; set; }
    public string? FileUrl { get; set; }
    public string? Metadata { get; set; }
}

/// <summary>
/// Search criteria for case subfiles
/// </summary>
public class CaseSubfileSearchCriteria : PagedRequest
{
    public Guid? CaseRegisterId { get; set; }
    public Guid? SubfileTypeId { get; set; }
    public string? DocumentType { get; set; }
}

/// <summary>
/// Subfile completion status for a case (which subfile types are present)
/// </summary>
public class SubfileCompletionDto
{
    public Guid CaseRegisterId { get; set; }
    public List<SubfileTypeCompletionItem> Items { get; set; } = new();
    public int TotalTypes { get; set; }
    public int CompletedTypes { get; set; }
}

public class SubfileTypeCompletionItem
{
    public Guid SubfileTypeId { get; set; }
    public string SubfileTypeCode { get; set; } = string.Empty;
    public string SubfileTypeName { get; set; } = string.Empty;
    public bool HasDocuments { get; set; }
    public int DocumentCount { get; set; }
}
