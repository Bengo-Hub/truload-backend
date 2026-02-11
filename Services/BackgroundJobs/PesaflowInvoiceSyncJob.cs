using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Background job that syncs pending/failed invoices with Pesaflow.
/// Runs as a recurring Hangfire job every 5 minutes.
/// </summary>
public class PesaflowInvoiceSyncJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PesaflowInvoiceSyncJob> _logger;

    public PesaflowInvoiceSyncJob(
        IServiceScopeFactory scopeFactory,
        ILogger<PesaflowInvoiceSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Processes all invoices with PesaflowSyncStatus = 'pending' or 'failed'.
    /// Retries Pesaflow invoice creation and updates sync status.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var eCitizenService = scope.ServiceProvider.GetRequiredService<IECitizenService>();

        _logger.LogInformation("[PesaflowInvoiceSyncJob] Starting invoice sync job");

        var pendingInvoices = await context.Invoices
            .Include(i => i.CaseRegister)
            .Include(i => i.Weighing)
            .Where(i => i.PesaflowSyncStatus == "pending" || i.PesaflowSyncStatus == "failed")
            .Where(i => i.DeletedAt == null)
            .ToListAsync(ct);

        if (!pendingInvoices.Any())
        {
            _logger.LogInformation("[PesaflowInvoiceSyncJob] No pending invoices to sync");
            return;
        }

        _logger.LogInformation("[PesaflowInvoiceSyncJob] Found {Count} pending invoices", pendingInvoices.Count);

        var successCount = 0;
        var failureCount = 0;

        foreach (var invoice in pendingInvoices)
        {
            try
            {
                _logger.LogInformation(
                    "[PesaflowInvoiceSyncJob] Attempting sync for invoice {InvoiceNo} (ID: {InvoiceId})",
                    invoice.InvoiceNo, invoice.Id);

                // Construct sync request from invoice data
                var request = new CreatePesaflowInvoiceRequest
                {
                    LocalInvoiceId = invoice.Id,
                    ClientName = "TruLoad Invoice Sync",
                    ClientEmail = null,
                    ClientMsisdn = null,
                    ClientIdNumber = null,
                    SendStk = false // Don't send STK for background retries
                };

                var result = await eCitizenService.CreatePesaflowInvoiceAsync(request, ct);

                if (result.Success)
                {
                    // Invoice already marked as 'synced' by ECitizenService
                    successCount++;
                    _logger.LogInformation(
                        "[PesaflowInvoiceSyncJob] Successfully synced invoice {InvoiceNo}. Pesaflow Invoice: {PesaflowInvoice}",
                        invoice.InvoiceNo, result.PesaflowInvoiceNumber);
                }
                else
                {
                    // Invoice already marked as 'failed' by ECitizenService
                    failureCount++;
                    _logger.LogWarning(
                        "[PesaflowInvoiceSyncJob] Failed to sync invoice {InvoiceNo}: {Message}",
                        invoice.InvoiceNo, result.Message);
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError(ex,
                    "[PesaflowInvoiceSyncJob] Error processing invoice {InvoiceNo}",
                    invoice.InvoiceNo);

                // Mark as failed if exception occurred
                invoice.PesaflowSyncStatus = "failed";
                invoice.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[PesaflowInvoiceSyncJob] Sync job completed. Success: {Success}, Failures: {Failures}",
            successCount, failureCount);
    }
}
