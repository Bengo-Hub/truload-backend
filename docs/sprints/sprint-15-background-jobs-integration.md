# Sprint 15: Background Jobs & Payment Integration
**Duration:** 1 week  
**Priority:** High  
**Status:** 75% Complete (Core implementation done, tests pending)  
**Target Completion:** February 18, 2026

---

## Sprint Goals
Implement production-ready background job processing using Hangfire for asynchronous tasks, with focus on Pesaflow invoice synchronization and payment webhook handling.

---

## User Stories

### US-15.1: Hangfire Background Job Framework
**As a** system administrator  
**I want** a reliable background job processing framework  
**So that** asynchronous tasks can be executed, monitored, and retried automatically

**Acceptance Criteria:**
- [x] Hangfire installed and configured with PostgreSQL storage
- [x] Hangfire dashboard accessible at `/hangfire` (admin-only)
- [x] Job retry policies configured (exponential backoff)
- [x] Recurring job scheduler configured
- [ ] Health check endpoint for Hangfire status
- [x] Authorization policy restricts dashboard access

### US-15.2: Pesaflow Invoice Sync Background Worker
**As a** finance officer  
**I want** invoices to automatically sync with Pesaflow when the gateway is unavailable  
**So that** payment processing continues seamlessly without manual intervention

**Acceptance Criteria:**
- [x] Background job processes invoices with `PesaflowSyncStatus = 'pending'` or `'failed'`
- [x] Recurring job runs every 5 minutes
- [x] Retry logic with exponential backoff (max 10 attempts over 24 hours)
- [x] Invoice status updated to `'synced'` on success, `'failed'` after max retries
- [x] Audit log entries created for each sync attempt
- [ ] Metrics tracked (sync success rate, retry count, average sync duration)

### US-15.3: Payment Callback Webhook Endpoints
**As a** payment gateway  
**I want** to send payment notifications to TruLoad  
**So that** invoices are automatically marked as paid when customers complete payment

**Acceptance Criteria:**
- [x] `POST /api/v1/payments/callback/success` endpoint created
- [x] `POST /api/v1/payments/callback/failure` endpoint created
- [x] `POST /api/v1/payments/callback/timeout` endpoint created
- [x] Webhook signature verification implemented (HMAC-SHA256)
- [x] Idempotency check prevents duplicate payment processing
- [x] Invoice status updated based on callback type
- [x] Receipt generation triggered on successful payment
- [x] Audit trail created for all webhook events (PaymentCallback model)
- [ ] Rate limiting applied (100 req/min per IP)

### US-15.4: Payment Webhook IPN Handler
**As a** Pesaflow gateway  
**I want** to send instant payment notifications (IPN)  
**So that** TruLoad is immediately informed when payments are completed

**Acceptance Criteria:**
- [x] `POST /api/v1/payments/webhook/ecitizen-pesaflow` endpoint processes IPN
- [x] Token hash verification using Pesaflow API key
- [x] Invoice lookup by `client_invoice_ref` or `invoice_number`
- [x] Amount validation (payment amount matches invoice amount)
- [x] Payment record created with transaction details
- [x] Invoice status changed from `pending` to `paid`
- [x] Receipt auto-generated and linked to invoice
- [ ] Email notification sent to customer (if email provided)
- [x] Duplicate IPN detection (check existing payment records)
- [x] PaymentCallback audit trail for all IPN events

---

## Technical Implementation

### 1. Hangfire Installation
```bash
dotnet add package Hangfire.Core
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.PostgreSql
```

### 2. Hangfire Configuration (Program.cs)
```csharp
// Hangfire PostgreSQL storage
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));

// Hangfire server
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 5;
    options.Queues = new[] { "default", "critical", "payments" };
});

// Hangfire dashboard with authorization
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});
```

### 3. Invoice Sync Background Job
**File:** `Services/BackgroundJobs/PesaflowInvoiceSyncJob.cs`

```csharp
public class PesaflowInvoiceSyncJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PesaflowInvoiceSyncJob> _logger;

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var eCitizenService = scope.ServiceProvider.GetRequiredService<IECitizenService>();

        var pendingInvoices = await context.Invoices
            .Where(i => i.PesaflowSyncStatus == "pending" || i.PesaflowSyncStatus == "failed")
            .Where(i => i.DeletedAt == null)
            .ToListAsync(ct);

        foreach (var invoice in pendingInvoices)
        {
            try
            {
                // Retry Pesaflow sync
                var request = new CreatePesaflowInvoiceRequest
                {
                    LocalInvoiceId = invoice.Id,
                    ClientName = invoice.CaseRegister?.VehicleOwnerName ?? "Unknown",
                    // ... other fields from original invoice
                };

                var result = await eCitizenService.CreatePesaflowInvoiceAsync(request, ct);
                
                if (result.Success)
                {
                    invoice.PesaflowSyncStatus = "synced";
                    _logger.LogInformation("Invoice {InvoiceNo} synced successfully", invoice.InvoiceNo);
                }
                else
                {
                    invoice.PesaflowSyncStatus = "failed";
                    _logger.LogWarning("Invoice {InvoiceNo} sync failed: {Message}", 
                        invoice.InvoiceNo, result.Message);
                }
                
                invoice.UpdatedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing invoice {InvoiceNo}", invoice.InvoiceNo);
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
```

### 4. Webhook Callback Endpoints
**File:** `Controllers/Financial/PaymentCallbackController.cs`

```csharp
[ApiController]
[Route("api/v1/payments/callback")]
public class PaymentCallbackController : ControllerBase
{
    [HttpPost("success")]
    public async Task<IActionResult> Success([FromForm] PesaflowCallbackDto callback)
    {
        // Verify signature, update invoice, generate receipt
        return Ok();
    }

    [HttpPost("failure")]
    public async Task<IActionResult> Failure([FromForm] PesaflowCallbackDto callback)
    {
        // Update invoice status to failed
        return Ok();
    }

    [HttpPost("timeout")]
    public async Task<IActionResult> Timeout([FromForm] PesaflowCallbackDto callback)
    {
        // Update invoice status to timeout
        return Ok();
    }
}
```

---

## Database Changes
**Migration:** `AddPaymentCallbackTracking`

### New Table: `payment_callbacks`
```sql
CREATE TABLE payment_callbacks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_id UUID REFERENCES invoices(id),
    callback_type VARCHAR(20) NOT NULL, -- success, failure, timeout
    pesaflow_invoice_number VARCHAR(100),
    payment_reference VARCHAR(100),
    amount DECIMAL(18,2),
    currency VARCHAR(3),
    payment_date TIMESTAMP,
    raw_payload TEXT,
    signature_verified BOOLEAN,
    processed_at TIMESTAMP DEFAULT NOW(),
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_payment_callbacks_invoice_id ON payment_callbacks(invoice_id);
CREATE INDEX idx_payment_callbacks_type ON payment_callbacks(callback_type);
CREATE INDEX idx_payment_callbacks_payment_ref ON payment_callbacks(payment_reference);
```

---

## Testing Requirements

### Unit Tests
- [ ] `PesaflowInvoiceSyncJobTests` - verify sync logic and retry behavior
- [ ] `PaymentCallbackControllerTests` - test all callback endpoint scenarios
- [ ] `WebhookSignatureVerificationTests` - ensure signature validation works

### Integration Tests
- [ ] Hangfire job enqueuing and execution
- [ ] Database transaction handling in background jobs
- [ ] Webhook signature verification with test keys
- [ ] Invoice status transitions (pending → synced → paid)

### E2E Tests
- [ ] Full payment flow: invoice creation → Pesaflow → webhook → receipt
- [ ] Retry scenario: Pesaflow down → background sync → success
- [ ] Duplicate webhook prevention

---

## Performance Targets
- **Invoice Sync Job:** Process 100 pending invoices in < 30 seconds
- **Webhook Response Time:** < 200ms (99th percentile)
- **Job Retry Delay:** 1min, 5min, 15min, 30min, 1hr, 2hr, 4hr, 8hr, 16hr, 24hr
- **Hangfire Dashboard Load Time:** < 2 seconds

---

## Security Considerations
- Webhook signature verification mandatory (reject unsigned requests)
- Hangfire dashboard requires `financial.admin` permission
- Rate limiting on webhook endpoints (100 req/min)
- Sensitive payment data encrypted in database
- Audit log for all payment state changes

---

## Dependencies
- Hangfire.Core >= 1.8.0
- Hangfire.AspNetCore >= 1.8.0
- Hangfire.PostgreSql >= 1.20.0
- Existing ECitizenService
- Existing InvoiceService
- ReceiptService (to be created if not exists)

---

## Rollout Plan
1. **Day 1-2:** Install Hangfire, configure dashboard, basic job scheduling
2. **Day 3:** Implement PesaflowInvoiceSyncJob with unit tests
3. **Day 4:** Create PaymentCallbackController endpoints
4. **Day 5:** Implement webhook IPN handler with signature verification
5. **Day 6:** Integration testing and E2E payment flow validation
6. **Day 7:** Performance testing, security audit, deployment to staging

---

## Success Metrics
- ✅ 0 manual invoice sync interventions required
- ✅ > 99% webhook delivery success rate
- ✅ < 5 minute average time from payment to receipt generation
- ✅ 100% webhook signature verification success
- ✅ 0 duplicate payment processing incidents

---

## Risks & Mitigations
| Risk | Impact | Mitigation |
|------|--------|------------|
| Hangfire storage fails | High | Use PostgreSQL with replication; health checks |
| Webhook replay attacks | High | Implement idempotency keys; signature verification |
| Background job memory leaks | Medium | Proper disposal of scoped services; monitoring |
| Payment amount mismatch | High | Strict validation; manual review queue for mismatches |

---

## Status Tracking
- [x] Sprint planning complete
- [x] Hangfire installation and configuration
- [x] Invoice sync background job implemented
- [x] Webhook callback endpoints created
- [x] IPN handler implemented
- [x] PaymentCallback audit model created
- [ ] Unit tests passing
- [ ] Integration tests passing
- [ ] E2E tests passing
- [ ] Code review approved
- [ ] Deployed to staging
- [ ] Production deployment
