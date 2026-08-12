using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

/// <summary>
/// PDF rendering for the "Axle Load Data Analysis" report - matches the KURA NRB sample
/// template: a per-vehicle data table with a status-highlighted row (blue "Within Permissible
/// Tolerance" / red "Overloaded and charged"), a "Key:" legend, and two summary/breakdown tables.
/// </summary>
internal sealed class AxleLoadAnalysisDocument : BaseReportDocument
{
    public string[] Headers { get; set; } = [];
    public string[][] Rows { get; set; } = [];
    public int? StatusColumnIndex { get; set; }
    public (string colorHex, string label)[] Legend { get; set; } = [];
    public (string title, string[] headers, string[][] rows)[] SummaryTables { get; set; } = [];

    /// <summary>Vehicle-type-by-axle-count proportions rendered as a stacked bar, when <see cref="IncludeChartVisuals"/>.</summary>
    public (string label, decimal value, string colorHex)[] VehicleTypeProportions { get; set; } = [];

    /// <summary>Mirrors the structured builder's "Tables only, no visual emphasis" chart-option toggle.</summary>
    public bool IncludeChartVisuals { get; set; } = true;

    public AxleLoadAnalysisDocument()
    {
        ReportTitle = "Axle Load Data Analysis";
    }

    protected override void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(6);

            col.Item().Element(c => ComposeDataTable(c, Headers, Rows, conditionalStatusColumnIndex: StatusColumnIndex));
            col.Item().Element(c => ComposeLegend(c, Legend));

            if (IncludeChartVisuals && VehicleTypeProportions.Length > 0)
                col.Item().Element(c => ComposeProportionBar(c, VehicleTypeProportions));

            foreach (var table in SummaryTables)
                col.Item().Element(c => ComposeTitledTable(c, table.title, table.headers, table.rows));
        });
    }
}
