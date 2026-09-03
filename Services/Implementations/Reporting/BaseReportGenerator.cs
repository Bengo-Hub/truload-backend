using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;
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
    /// Public (not just protected) so callers outside the report generator hierarchy - e.g.
    /// <c>WeighingController</c>'s ticket-list export - can reuse the same CSV writer instead of
    /// duplicating it.
    /// </summary>
    public static byte[] GenerateCsv(string[] headers, IEnumerable<string[]> rows)
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
    /// <summary>
    /// Resolves the report's date range as `[from, to]` (inclusive end, this method's long-standing
    /// contract), delegating the actual EAT-aware day-boundary math to the same
    /// <see cref="TruLoad.Backend.Common.WeighingQueryHelpers.ResolveEatDayRange"/> the dashboard/
    /// stats endpoints use - a 2026-08-12 audit found this previously did its own
    /// <c>DateTime.SpecifyKind(...,Utc)</c>, which only relabels the client's calendar date as UTC
    /// without converting it, shifting every "selected day" by Nairobi's UTC+3 offset. Centralizing
    /// here fixes every report generator that calls this (all ~30 report types) in one place.
    /// </summary>
    protected static (DateTime from, DateTime to) GetDateRange(ReportFilterParams filters)
    {
        var (fromUtc, toUtcExclusive) = TruLoad.Backend.Common.WeighingQueryHelpers.ResolveEatDayRange(
            filters.DateFrom, filters.DateTo);
        return (fromUtc, toUtcExclusive.AddTicks(-1));
    }

    /// <summary>
    /// Generates a flat Excel workbook from headers and rows with professional formatting.
    /// Thin pass-through onto <see cref="GenerateExcel(ExcelReportRequest)"/> so the ~30 existing
    /// call sites across the module generators keep compiling unchanged; migrate a report to the
    /// richer overload (status colouring, legend, summary tables, branding) individually.
    /// </summary>
    protected static byte[] GenerateExcel(string reportTitle, string[] headers, IEnumerable<string[]> rows,
        DateTime? dateFrom = null, DateTime? dateTo = null)
        => GenerateExcel(new ExcelReportRequest
        {
            ReportTitle = reportTitle,
            Headers = headers,
            Rows = rows,
            DateFrom = dateFrom,
            DateTo = dateTo
        });

    /// <summary>
    /// Generates an Excel workbook with professional formatting, optionally including
    /// status-driven row colouring, a "Key:" legend, one or more titled summary/breakdown
    /// tables, and tenant branding (org name + logo) - the shared primitives every report can
    /// opt into via <see cref="ExcelReportRequest"/> without duplicating this rendering logic.
    /// </summary>
    protected static byte[] GenerateExcel(ExcelReportRequest request)
    {
        var headers = request.Headers;

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Report");

        // Title row (org name prefixed when known, for tenant-branding-aware Excel exports)
        var titleText = string.IsNullOrWhiteSpace(request.OrgName)
            ? request.ReportTitle
            : $"{request.OrgName} - {request.ReportTitle}";
        worksheet.Cell(1, 1).Value = titleText;
        worksheet.Range(1, 1, 1, headers.Length).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml(BrandingConstants.Colors.KuraBlue);

        // Date range row
        var dateRange = request.DateFrom.HasValue && request.DateTo.HasValue
            ? $"Period: {request.DateFrom.Value:dd/MM/yyyy} - {request.DateTo.Value:dd/MM/yyyy}"
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

        // Data rows: status-driven full-row colouring (when a status column is designated and the
        // status is an "exception" state) takes precedence over the default alternating banding -
        // verified against the sample KURA NRB template, whose tolerance/overloaded rows are
        // filled edge-to-edge across every column, not just the Status cell.
        var dataRow = headerRow + 1;
        foreach (var row in request.Rows)
        {
            ReportStatusColors.StatusStyle? highlight = null;
            if (request.ConditionalStatusColumnIndex is { } statusIdx && statusIdx < row.Length
                && ReportStatusColors.ShouldHighlightRow(row[statusIdx]))
            {
                highlight = ReportStatusColors.Resolve(row[statusIdx]);
            }

            for (var i = 0; i < row.Length && i < headers.Length; i++)
            {
                var cell = worksheet.Cell(dataRow, i + 1);
                cell.Value = row[i];
                cell.Style.Font.FontSize = 10;

                if (highlight != null)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml(highlight.ExcelFillHex);
                    cell.Style.Font.FontColor = XLColor.FromHtml(highlight.ExcelFontHex);
                    cell.Style.Font.Bold = true;
                }
            }

            if (highlight == null && dataRow % 2 == 0)
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

            if (request.IncludeChartVisuals && request.PercentageDataBarColumnIndexes is { Length: > 0 } pctCols)
            {
                foreach (var colIdx in pctCols)
                {
                    if (colIdx >= 0 && colIdx < headers.Length)
                        ApplyPercentageDataBar(worksheet.Range(headerRow + 1, colIdx + 1, dataRow - 1, colIdx + 1));
                }
            }
        }

        var nextRow = dataRow + 1;

        if (request.Legend is { Length: > 0 } legend)
            nextRow = WriteLegend(worksheet, legend, nextRow);

        if (request.SummaryTables is { Length: > 0 } summaryTables)
        {
            foreach (var table in summaryTables)
                nextRow = WriteSummaryTable(worksheet, table, nextRow, request.IncludeChartVisuals);
        }

        if (!string.IsNullOrWhiteSpace(request.OrgLogoFile))
            TryEmbedLogo(worksheet, request.OrgLogoFile!, headers.Length);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Writes the "Key:" legend block (fill-colour swatch + label per row) below the data table.</summary>
    private static int WriteLegend(IXLWorksheet worksheet, (string colorHex, string label)[] legend, int startRow)
    {
        var row = startRow + 1;
        worksheet.Cell(row, 1).Value = "Key:";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        foreach (var (colorHex, label) in legend)
        {
            var swatch = worksheet.Cell(row, 1);
            swatch.Style.Fill.BackgroundColor = XLColor.FromHtml(colorHex);
            swatch.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            worksheet.Cell(row, 2).Value = label;
            row++;
        }

        return row;
    }

    /// <summary>Writes one titled summary/breakdown table (bold title, header row, data rows, borders).</summary>
    private static int WriteSummaryTable(
        IXLWorksheet worksheet, ExcelSummaryTable table, int startRow, bool includeChartVisuals = true)
    {
        var row = startRow + 1;
        worksheet.Cell(row, 1).Value = table.Title;
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(BrandingConstants.Colors.KuraBlue);
        row++;

        var headerRowIndex = row;
        for (var i = 0; i < table.Headers.Length; i++)
        {
            var cell = worksheet.Cell(row, i + 1);
            cell.Value = table.Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(BrandingConstants.Colors.KuraBlue);
        }
        row++;

        var startDataRow = row;
        foreach (var dataRow in table.Rows)
        {
            for (var i = 0; i < dataRow.Length && i < table.Headers.Length; i++)
                worksheet.Cell(row, i + 1).Value = dataRow[i];
            row++;
        }

        if (row > startDataRow)
        {
            var range = worksheet.Range(headerRowIndex, 1, row - 1, table.Headers.Length);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Native in-cell "chart": ClosedXML's DataBar conditional format on the last column
            // when it reads as a percentage share (e.g. "% OF TOTAL", "% Overloaded by GVW") -
            // a real Excel visual, no image-embedding or new package needed.
            if (includeChartVisuals && table.Headers.Length > 0 && table.Headers[^1].Contains('%') && row > startDataRow)
            {
                var pctRange = worksheet.Range(startDataRow, table.Headers.Length, row - 1, table.Headers.Length);
                ApplyPercentageDataBar(pctRange);
            }
        }

        return row + 1;
    }

    /// <summary>
    /// Applies ClosedXML's native DataBar conditional formatting to a percentage column. Every
    /// percentage cell in this codebase is written as a pre-formatted DISPLAY STRING (e.g.
    /// "45.00%", built via <c>$"{value:F2}%"</c>) so the plain CSV/table output reads correctly -
    /// but a text cell has no numeric value for DataBar to size a bar against, so ClosedXML/Excel
    /// silently renders no bar at all on a text-formatted range (confirmed bug, not a guess -
    /// DataBar conditional formatting only operates on genuine numbers). Converts each cell to its
    /// real underlying fraction (e.g. 0.45) with a "0.00%" number format first, which keeps the
    /// exact same "45.00%" on-screen display while giving DataBar an actual number to work with.
    /// </summary>
    protected static void ApplyPercentageDataBar(IXLRange range)
    {
        foreach (var cell in range.Cells())
        {
            var text = cell.GetString();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var trimmed = text.Trim().TrimEnd('%');
            if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var percentValue))
            {
                cell.Value = percentValue / 100m;
                cell.Style.NumberFormat.Format = "0.00%";
            }
        }

        range.AddConditionalFormat().DataBar(XLColor.FromHtml(BrandingConstants.Colors.KuraBlue));
    }

    /// <summary>
    /// Embeds the resolved org/fallback logo (same wwwroot/images/ resolution the PDF engine
    /// uses via <see cref="BaseDocument.LoadLogo"/>) at a fixed anchor to the right of the title
    /// row, so Excel exports get the same tenant branding PDF exports already have.
    /// </summary>
    private static void TryEmbedLogo(IXLWorksheet worksheet, string orgLogoFile, int headerCount)
    {
        var resolvedFile = BaseDocument.ResolveOrgLogo(orgLogoFile);
        var bytes = BaseDocument.LoadLogo(resolvedFile);
        if (bytes == null || bytes.Length == 0)
            return;

        using var imageStream = new MemoryStream(bytes);
        worksheet.AddPicture(imageStream)
            .MoveTo(worksheet.Cell(1, headerCount + 2))
            .Scale(0.25);
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

    protected ReportDefinitionDto Def(string id, string name, string description, string[]? formats = null, string[]? allowedRoles = null, string[]? allowedVerticals = null)
        => new()
        {
            Id = id,
            Name = name,
            Description = description,
            Module = Module,
            SupportedFormats = formats ?? ["pdf", "csv", "xlsx"],
            AllowedRoles = allowedRoles,
            AllowedVerticals = allowedVerticals
        };

    /// <summary>
    /// Overload for report types that opt into the structured custom-report builder - adds a
    /// curated column/chart catalog to the definition (see <see cref="ReportColumnDefinition"/>).
    /// </summary>
    protected ReportDefinitionDto Def(
        string id, string name, string description,
        List<ReportColumnDefinition> columns, List<ReportChartOption>? chartOptions = null,
        string[]? formats = null, string[]? allowedRoles = null, string[]? allowedVerticals = null)
    {
        var def = Def(id, name, description, formats, allowedRoles, allowedVerticals);
        def.Columns = columns;
        def.ChartOptions = chartOptions;
        return def;
    }

    /// <summary>
    /// Overload for report types that additionally opt into the structured filter catalog (see
    /// <see cref="ReportFilterDefinition"/>) so the builder UI shows only the filters this report
    /// actually supports, instead of one fixed generic set.
    /// </summary>
    protected ReportDefinitionDto Def(
        string id, string name, string description,
        List<ReportColumnDefinition>? columns, List<ReportFilterDefinition> filters,
        List<ReportChartOption>? chartOptions = null, string[]? formats = null, string[]? allowedRoles = null, string[]? allowedVerticals = null)
    {
        var def = Def(id, name, description, formats, allowedRoles, allowedVerticals);
        def.Columns = columns;
        def.ChartOptions = chartOptions;
        def.Filters = filters;
        return def;
    }

    /// <summary>
    /// Filters a report's header/row output down to a caller-selected subset of columns, matched
    /// by header text (each report's headers are already its stable column identifiers - see
    /// <see cref="ReportColumnDefinition.Key"/>). Every report generator keeps building its full
    /// existing projection unchanged; this is the one shared point where a structured builder
    /// selection is actually applied, so no report needs N query variants for N column combinations.
    /// Returns the original headers/rows unchanged when <paramref name="selectedKeys"/> is null/empty
    /// or matches nothing (fails safe to "show everything" rather than silently emptying a report).
    /// </summary>
    protected static (string[] Headers, List<string[]> Rows) ApplyColumnSelection(
        string[] headers, IEnumerable<string[]> rows, string[]? selectedKeys)
    {
        var rowList = rows is List<string[]> list ? list : rows.ToList();

        if (selectedKeys == null || selectedKeys.Length == 0)
            return (headers, rowList);

        var indices = selectedKeys
            .Select(key => Array.FindIndex(headers, h => string.Equals(h, key, StringComparison.OrdinalIgnoreCase)))
            .Where(i => i >= 0)
            .Distinct()
            .ToList();

        if (indices.Count == 0)
            return (headers, rowList);

        var newHeaders = indices.Select(i => headers[i]).ToArray();
        var newRows = rowList
            .Select(r => indices.Select(i => i < r.Length ? r[i] : "-").ToArray())
            .ToList();

        return (newHeaders, newRows);
    }

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
