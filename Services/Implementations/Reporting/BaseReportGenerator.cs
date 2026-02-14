using System.Globalization;
using System.Text;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Interfaces.Reporting;

namespace TruLoad.Backend.Services.Implementations.Reporting;

/// <summary>
/// Shared base class for all module report generators.
/// Provides CSV serialization, date filtering helpers, and common report metadata.
/// </summary>
public abstract class BaseReportGenerator : IModuleReportGenerator
{
    public abstract string Module { get; }
    public abstract List<ReportDefinitionDto> GetDefinitions();
    public abstract Task<ReportResult> GenerateAsync(
        string reportType, ReportFilterParams filters, string format, CancellationToken ct = default);

    /// <summary>
    /// Generates a CSV byte array from a list of column headers and row data.
    /// </summary>
    protected static byte[] GenerateCsv(string[] headers, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>
    /// Wraps a CSV value in quotes if needed.
    /// </summary>
    protected static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    /// <summary>
    /// Creates a ReportResult for CSV output.
    /// </summary>
    protected static ReportResult CsvResult(byte[] data, string reportName, DateTime? dateFrom, DateTime? dateTo)
    {
        var suffix = dateFrom.HasValue && dateTo.HasValue
            ? $"_{dateFrom.Value:yyyyMMdd}_to_{dateTo.Value:yyyyMMdd}"
            : $"_{DateTime.UtcNow:yyyyMMdd}";
        return new ReportResult
        {
            Content = data,
            ContentType = "text/csv",
            FileName = $"{reportName}{suffix}.csv"
        };
    }

    /// <summary>
    /// Creates a ReportResult for PDF output.
    /// </summary>
    protected static ReportResult PdfResult(byte[] data, string reportName, DateTime? dateFrom, DateTime? dateTo)
    {
        var suffix = dateFrom.HasValue && dateTo.HasValue
            ? $"_{dateFrom.Value:yyyyMMdd}_to_{dateTo.Value:yyyyMMdd}"
            : $"_{DateTime.UtcNow:yyyyMMdd}";
        return new ReportResult
        {
            Content = data,
            ContentType = "application/pdf",
            FileName = $"{reportName}{suffix}.pdf"
        };
    }

    /// <summary>
    /// Formats a nullable date.
    /// </summary>
    protected static string FormatDate(DateTime? date, string fallback = "-")
        => date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? fallback;

    /// <summary>
    /// Formats a number with thousands separator.
    /// </summary>
    protected static string FormatNumber(decimal value)
        => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats currency in KES.
    /// </summary>
    protected static string FormatKes(decimal value)
        => $"KES {value:N0}";

    /// <summary>
    /// Gets effective date range (defaults to last 30 days if not specified).
    /// </summary>
    protected static (DateTime from, DateTime to) GetDateRange(ReportFilterParams filters)
    {
        var to = filters.DateTo?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow;
        var from = filters.DateFrom?.Date ?? to.AddDays(-30);
        return (from, to);
    }

    protected ReportDefinitionDto Def(string id, string name, string description, string[]? formats = null)
        => new()
        {
            Id = id,
            Name = name,
            Description = description,
            Module = Module,
            SupportedFormats = formats ?? ["pdf", "csv"]
        };
}
