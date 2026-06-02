namespace TruLoad.Backend.Services.Implementations.Shared;

/// <summary>
/// Builds the template-data dictionaries and subjects for TruLoad enforcement workflow emails,
/// using the EXACT variable keys the notifications-api templates expect. Shared by the trigger
/// sites and the resend endpoint so the two never drift. CTA deep-links (case_link, invoice_link,
/// payment_link, receipt_link) are derived centrally in NotificationService.AddDeepLinks from the
/// app_url + slug + the ids (case_id, pesaflow_payment_link) included here.
/// </summary>
public static class TruLoadEmailData
{
    private static string Date(DateTime? dt) => dt.HasValue ? dt.Value.ToString("dd MMM yyyy HH:mm") : "—";
    private static string DateOnly(DateTime? dt) => dt.HasValue ? dt.Value.ToString("dd MMM yyyy") : "—";

    public static (Dictionary<string, object> Data, string Subject) OverloadAlert(
        string vehicleReg, int overloadKg, string ticketNo, string stationName,
        string caseNo, int gvwMeasuredKg, int gvwPermissibleKg, DateTime weighedAt, Guid? caseId)
    {
        var data = new Dictionary<string, object>
        {
            ["vehicle_reg"] = vehicleReg,
            ["overload_kg"] = overloadKg,
            ["ticket_no"] = ticketNo,
            ["station_name"] = stationName,
            ["case_no"] = caseNo,
            ["gvw_measured"] = gvwMeasuredKg,
            ["gvw_permissible"] = gvwPermissibleKg,
            ["weighed_at"] = Date(weighedAt),
        };
        if (caseId.HasValue && caseId.Value != Guid.Empty) data["case_id"] = caseId.Value;
        return (data, $"Overload alert — {vehicleReg} ({overloadKg:N0} kg over)");
    }

    public static (Dictionary<string, object> Data, string Subject) CaseCreated(
        string caseNo, string? vehicleReg, string? violationType, string stationName,
        DateTime createdAt, Guid caseId)
    {
        var data = new Dictionary<string, object>
        {
            ["case_no"] = caseNo,
            ["vehicle_reg"] = vehicleReg ?? "—",
            ["violation_type"] = violationType ?? "—",
            ["station_name"] = stationName,
            ["created_at"] = Date(createdAt),
        };
        if (caseId != Guid.Empty) data["case_id"] = caseId;
        return (data, $"New enforcement case {caseNo}");
    }

    public static (Dictionary<string, object> Data, string Subject) InvoiceIssued(
        string invoiceNo, string? caseNo, string? vehicleReg, decimal amountDue, string currency,
        DateTime? dueDate, DateTime issuedAt, Guid? caseId, string? pesaflowPaymentLink)
    {
        var data = new Dictionary<string, object>
        {
            ["invoice_no"] = invoiceNo,
            ["case_no"] = caseNo ?? "—",
            ["vehicle_reg"] = vehicleReg ?? "—",
            ["amount_due"] = amountDue.ToString("N2"),
            ["currency"] = currency,
            ["due_date"] = DateOnly(dueDate ?? issuedAt.AddDays(30)),
            ["issued_at"] = DateOnly(issuedAt),
        };
        if (caseId.HasValue && caseId.Value != Guid.Empty) data["case_id"] = caseId.Value;
        if (!string.IsNullOrWhiteSpace(pesaflowPaymentLink)) data["pesaflow_payment_link"] = pesaflowPaymentLink!;
        return (data, $"Invoice {invoiceNo} issued");
    }

    public static (Dictionary<string, object> Data, string Subject) InvoicePaid(
        string receiptNo, decimal amountPaid, string currency, string invoiceNo,
        string? caseNo, string? vehicleReg, string? paymentRef, DateTime paidAt, Guid? caseId)
    {
        var data = new Dictionary<string, object>
        {
            ["receipt_no"] = receiptNo,
            ["amount_paid"] = amountPaid.ToString("N2"),
            ["currency"] = currency,
            ["invoice_no"] = invoiceNo,
            ["case_no"] = caseNo ?? "—",
            ["vehicle_reg"] = vehicleReg ?? "—",
            ["payment_ref"] = string.IsNullOrWhiteSpace(paymentRef) ? "—" : paymentRef!,
            ["paid_at"] = Date(paidAt),
        };
        if (caseId.HasValue && caseId.Value != Guid.Empty) data["case_id"] = caseId.Value;
        return (data, $"Payment received — Receipt {receiptNo}");
    }
}
