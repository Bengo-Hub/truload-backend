using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;
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
    /// Creates a ReportResult for PDF output, applying tenant org context from filters before generating.
    /// </summary>
    protected static ReportResult PdfResult(
        BaseReportDocument doc, ReportFilterParams filters, string reportName, DateTime? dateFrom, DateTime? dateTo)
    {
        ApplyOrgContext(doc, filters);
        return PdfResult(doc.Generate(), reportName, dateFrom, dateTo);
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
        var to = filters.DateTo.HasValue
            ? DateTime.SpecifyKind(filters.DateTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : DateTime.UtcNow;
        var from = filters.DateFrom.HasValue
            ? DateTime.SpecifyKind(filters.DateFrom.Value.Date, DateTimeKind.Utc)
            : to.AddDays(-30);
        return (from, to);
    }

    /// <summary>
    /// Generates an Excel workbook from headers and rows with professional formatting.
    /// </summary>
    protected static byte[] GenerateExcel(string reportTitle, string[] headers, IEnumerable<string[]> rows,
        DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Report");

        // Title row
        worksheet.Cell(1, 1).Value = reportTitle;
        worksheet.Range(1, 1, 1, headers.Length).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml(BrandingConstants.Colors.KuraBlue);

        // Date range row
        var dateRange = dateFrom.HasValue && dateTo.HasValue
            ? $"Period: {dateFrom.Value:dd/MM/yyyy} - {dateTo.Value:dd/MM/yyyy}"
            : $"Generated: {DateTime.UtcNow:dd/MM/yyyy HH:mm}";
        worksheet.Cell(2, 1).Value = dateRange;
        worksheet.Range(2, 1, 2, headers.Length).Merge();
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 9;

        // Header row
        var headerRow = 4;
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(BrandingConstants.Colors.KuraBlue);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        // Data rows with alternating colors
        var dataRow = headerRow + 1;
        foreach (var row in rows)
        {
            for (var i = 0; i < row.Length && i < headers.Length; i++)
            {
                var cell = worksheet.Cell(dataRow, i + 1);
                cell.Value = row[i];
                cell.Style.Font.FontSize = 10;
            }

            if (dataRow % 2 == 0)
            {
                var range = worksheet.Range(dataRow, 1, dataRow, headers.Length);
                range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
            }

            dataRow++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents(4, dataRow);

        // Add borders to data range
        if (dataRow > headerRow + 1)
        {
            var dataRange = worksheet.Range(headerRow, 1, dataRow - 1, headers.Length);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Creates a ReportResult for Excel output.
    /// </summary>
    protected static ReportResult ExcelResult(byte[] data, string reportName, DateTime? dateFrom, DateTime? dateTo)
    {
        var suffix = dateFrom.HasValue && dateTo.HasValue
            ? $"_{dateFrom.Value:yyyyMMdd}_to_{dateTo.Value:yyyyMMdd}"
            : $"_{DateTime.UtcNow:yyyyMMdd}";
        return new ReportResult
        {
            Content = data,
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = $"{reportName}{suffix}.xlsx"
        };
    }

    protected ReportDefinitionDto Def(string id, string name, string description, string[]? formats = null)
        => new()
        {
            Id = id,
            Name = name,
            Description = description,
            Module = Module,
            SupportedFormats = formats ?? ["pdf", "csv", "xlsx"]
        };

    /// <summary>
    /// Applies tenant org context (name, logo, enforcement flag) from filters to a report document.
    /// Call this after creating the document to set branding properties.
    /// </summary>
    protected static void ApplyOrgContext(BaseReportDocument doc, ReportFilterParams filters)
    {
        doc.OrganizationName = filters.OrganizationName;
        doc.IsEnforcement = filters.IsEnforcement;
        if (!string.IsNullOrEmpty(filters.OrgLogoFile))
            doc.OrgLogoFile = filters.OrgLogoFile;
        // Commercial tenants don't show coat of arms as secondary logo
        if (!filters.IsEnforcement)
            doc.SecondaryLogoFile = null;
    }
}
