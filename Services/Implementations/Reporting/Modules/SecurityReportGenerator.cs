using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;

namespace TruLoad.Backend.Services.Implementations.Reporting.Modules;

/// <summary>
/// Generates security and audit reports: audit log and shift report.
/// </summary>
public class SecurityReportGenerator : BaseReportGenerator
{
    private readonly TruLoadDbContext _context;

    // =====================================================================
    // Structured custom-report-builder column catalogs. Each Key MUST match the literal header
    // text built in that report's generation method (see BaseReportGenerator.ApplyColumnSelection
    // - matches by header string). Neither report builds a header via string interpolation with a
    // runtime value, so all columns are included in the catalog.
    // =====================================================================

    private static readonly List<ReportColumnDefinition> AuditLogColumns =
    [
        new() { Key = "Timestamp", Label = "Timestamp" },
        new() { Key = "User", Label = "User" },
        new() { Key = "Action", Label = "Action" },
        new() { Key = "Resource Type", Label = "Resource Type" },
        new() { Key = "Resource", Label = "Resource" },
        new() { Key = "Success", Label = "Success" },
        new() { Key = "Method", Label = "HTTP Method" },
        new() { Key = "Endpoint", Label = "Endpoint" },
        new() { Key = "IP Address", Label = "IP Address" },
        new() { Key = "Denial Reason", Label = "Denial Reason" }
    ];

    private static readonly List<ReportColumnDefinition> ShiftReportColumns =
    [
        new() { Key = "User", Label = "User" },
        new() { Key = "Shift", Label = "Shift" },
        new() { Key = "Code", Label = "Shift Code" },
        new() { Key = "Starts On", Label = "Starts On" },
        new() { Key = "Ends On", Label = "Ends On" },
        new() { Key = "Active", Label = "Active" }
    ];

    public SecurityReportGenerator(TruLoadDbContext context)
    {
        _context = context;
    }

    public override string Module => ReportModules.Security;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("audit-log", "Audit Log",
            "Detailed audit trail of all system actions including user, action type, resource, and outcome.",
            columns: AuditLogColumns),
        Def("shift-report", "Shift Report",
            "Summary of user shift assignments and coverage across stations for the reporting period.",
            columns: ShiftReportColumns)
    ];

    public override async Task<ReportResult> GenerateAsync(
        string reportType, ReportFilterParams filters, string format, CancellationToken ct = default)
    {
        return reportType switch
        {
            "audit-log" => await GenerateAuditLog(filters, format, ct),
            "shift-report" => await GenerateShiftReport(filters, format, ct),
            _ => throw new ArgumentException($"Unknown security report type: {reportType}")
        };
    }

    // ──────────────────────────────────────────────────────────────────
    // audit-log
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateAuditLog(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.AuditLogs
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to);

        if (!string.IsNullOrEmpty(filters.Status))
        {
            // Filter by action type (e.g., CREATE, UPDATE, DELETE, LOGIN, PERMISSION_DENIED)
            query = query.Where(a => a.Action == filters.Status);
        }

        var logs = await query
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Action,
                a.ResourceType,
                a.ResourceName,
                a.ResourceId,
                a.Success,
                a.HttpMethod,
                a.Endpoint,
                a.StatusCode,
                a.IpAddress,
                a.DenialReason,
                a.RequiredPermission,
                UserName = a.User != null ? a.User.FullName : a.UserId.ToString(),
                a.CreatedAt
            })
            .ToListAsync(ct);

        var totalActions = logs.Count;
        var successCount = logs.Count(l => l.Success);
        var failedCount = logs.Count(l => !l.Success);

        // Action type breakdown
        var actionGroups = logs
            .GroupBy(l => l.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToList();

        string[] headers =
        [
            "Timestamp", "User", "Action", "Resource Type", "Resource",
            "Success", "Method", "Endpoint", "IP Address", "Denial Reason"
        ];
        var rows = logs.Select(l => new[]
        {
            l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
            l.UserName,
            l.Action,
            l.ResourceType,
            l.ResourceName ?? l.ResourceId?.ToString() ?? "-",
            l.Success ? "Yes" : "No",
            l.HttpMethod ?? "-",
            l.Endpoint ?? "-",
            l.IpAddress ?? "-",
            l.DenialReason ?? "-"
        });

        var outputHeaders = headers;
        List<string[]> outputRows = rows.ToList();
        int? effectiveStatusColumnIndex = 5; // "Success" (Yes/No)

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Success");
            effectiveStatusColumnIndex = idx >= 0 ? idx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "audit_log", from, to);

        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Audit Log Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "audit_log", from, to);
        }

        var summaryItems = new List<(string label, string value)>
        {
            ("Total Actions", totalActions.ToString()),
            ("Successful", successCount.ToString()),
            ("Failed/Denied", failedCount.ToString()),
            ("Unique Users", logs.Select(l => l.UserName).Distinct().Count().ToString())
        };
        foreach (var ag in actionGroups.Take(4))
        {
            summaryItems.Add(($"{ag.Action}", ag.Count.ToString()));
        }

        var doc = new AuditLogDocument
        {
            ReportTitle = "Audit Log Report",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows,
            StatusColumnIndex = effectiveStatusColumnIndex,
            SummaryItems = summaryItems.ToArray()
        };
        return PdfResult(doc, filters, "audit_log", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // shift-report
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateShiftReport(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);
        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);

        // Get user shift assignments that overlap with the reporting period
        var userShifts = await _context.UserShifts
            .Where(us => us.StartsOn <= toDate)
            .Where(us => us.EndsOn == null || us.EndsOn >= fromDate)
            .Include(us => us.User)
            .Include(us => us.WorkShift)
            .OrderBy(us => us.StartsOn)
            .ThenBy(us => us.User.FullName)
            .Select(us => new
            {
                UserName = us.User.FullName,
                us.UserId,
                ShiftName = us.WorkShift != null ? us.WorkShift.Name : "Rotation-based",
                ShiftCode = us.WorkShift != null ? us.WorkShift.Code : "-",
                us.StartsOn,
                us.EndsOn,
                IsActive = us.EndsOn == null || us.EndsOn >= toDate
            })
            .ToListAsync(ct);

        // Get active work shifts for reference
        var activeShifts = await _context.WorkShifts
            .Where(ws => ws.DeletedAt == null && ws.IsActive)
            .Select(ws => new { ws.Name, ws.Code, ws.TotalHoursPerWeek })
            .ToListAsync(ct);

        string[] headers = ["User", "Shift", "Code", "Starts On", "Ends On", "Active"];
        var rows = userShifts.Select(us => new[]
        {
            us.UserName,
            us.ShiftName,
            us.ShiftCode,
            us.StartsOn.ToString("dd/MM/yyyy"),
            us.EndsOn?.ToString("dd/MM/yyyy") ?? "Ongoing",
            us.IsActive ? "Yes" : "No"
        });

        var outputHeaders = headers;
        List<string[]> outputRows = rows.ToList();

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "shift_report", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Shift Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "shift_report", from, to);

        var totalAssignments = userShifts.Count;
        var activeAssignments = userShifts.Count(us => us.IsActive);
        var uniqueUsers = userShifts.Select(us => us.UserId).Distinct().Count();

        var summaryItems = new List<(string label, string value)>
        {
            ("Total Assignments", totalAssignments.ToString()),
            ("Active Assignments", activeAssignments.ToString()),
            ("Unique Users", uniqueUsers.ToString()),
            ("Defined Shifts", activeShifts.Count.ToString())
        };
        foreach (var shift in activeShifts)
        {
            var assignedCount = userShifts.Count(us => us.ShiftCode == shift.Code && us.IsActive);
            summaryItems.Add(($"{shift.Name} ({shift.Code})", $"{assignedCount} users | {shift.TotalHoursPerWeek}h/week"));
        }

        var doc = new ShiftReportDocument
        {
            ReportTitle = "Shift Report",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows,
            SummaryItems = summaryItems.ToArray()
        };
        return PdfResult(doc, filters, "shift_report", from, to);
    }

    // ══════════════════════════════════════════════════════════════════
    // Inner PDF document classes
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// PDF document for the audit log with action breakdown summary.
    /// </summary>
    private sealed class AuditLogDocument : BaseReportDocument
    {
        public required string[] Headers { get; init; }
        public required List<string[]> Rows { get; init; }
        public required (string label, string value)[] SummaryItems { get; init; }
        public int? StatusColumnIndex { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));
                col.Item().Element(c => ComposeDataTable(c, Headers, Rows,
                    summaryLabel: "Total Log Entries",
                    summaryValue: Rows.Count.ToString(),
                    conditionalStatusColumnIndex: StatusColumnIndex));
            });
        }
    }

    /// <summary>
    /// PDF document for shift assignments with coverage summary.
    /// </summary>
    private sealed class ShiftReportDocument : BaseReportDocument
    {
        public required string[] Headers { get; init; }
        public required List<string[]> Rows { get; init; }
        public required (string label, string value)[] SummaryItems { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));
                col.Item().Element(c => ComposeDataTable(c, Headers, Rows,
                    summaryLabel: "Total Shift Assignments",
                    summaryValue: Rows.Count.ToString()));
            });
        }
    }
}
