# Pesaflow Payment Integration – TruLoad (.NET 10)

## 1. Purpose of This Document

This document summarizes **Pesaflow payment capabilities** and how they should be integrated into the **TruLoad system built on .NET 10**. It is intended for backend engineers and architects designing invoice, payment, and receipt workflows.

The goal is to enable TruLoad to:
- Raise invoices
- Support **pay-now** and **pay-later (eCitizen)** flows
- Reliably detect payment completion
- Trigger **receipt generation** once payment is confirmed

---

## 2. High-Level Integration Overview

Pesaflow acts as a **payment orchestration platform** for eCitizen payments. TruLoad integrates as a **third-party merchant system**.

Key integration points:

1. **Invoice Creation API** – raises payable invoices
2. **Checkout / STK APIs** – optional immediate payment
3. **IPN Webhook** – authoritative payment confirmation
4. **Payment Status API** – fallback reconciliation

TruLoad should be designed as an **event-driven system**, where payment events trigger business actions (e.g., receipt issuance).

---

## 3. Authentication & Security Model

### 3.1 OAuth Bearer Token (Required)

Used for:
- Creating invoices

Flow:
1. Generate token using **Consumer Key & Consumer Secret**
2. Include token in request header:

```
Authorization: Bearer {access_token}
```

Tokens should be:
- Cached in memory or Redis
- Automatically refreshed on expiry

---

## 4. Invoice Lifecycle in TruLoad

### 4.1 Invoice States (Recommended)

```
CREATED → UNPAID → PAID → RECEIPT_GENERATED
              ↘ PARTIAL
```

State transitions must be driven by **Pesaflow notifications**, not frontend assumptions.

---

## 5. Scenario 1: Pay Immediately (Pay-on-Create)

### 5.1 Use Case

- Customer is present (web / assisted flow)
- Payment is expected immediately

### 5.2 Supported Capabilities

✔ Invoice creation
✔ Immediate checkout (IFRAME)
✔ Optional STK Push
✔ Real-time payment confirmation

### 5.3 Recommended Flow

1. TruLoad creates invoice via API
2. TruLoad launches Pesaflow checkout (iframe or redirect)
3. Customer pays immediately
4. Pesaflow sends **IPN webhook** to TruLoad
5. TruLoad marks invoice as PAID
6. TruLoad triggers **receipt generation**

### 5.4 .NET 10 Considerations

- Use `HttpClientFactory` for API calls
- Securely compute `secureHash` using `System.Security.Cryptography`
- Do **not** trust frontend callbacks for payment success

---

## 6. Scenario 2: Pay Later via eCitizen Portal

### 6.1 Use Case

- Invoice is raised today
- Customer pays later via eCitizen
- No real-time interaction

### 6.2 Supported Capabilities

✔ Invoice creation
✔ Deferred settlement
✔ Payment via eCitizen portal
✔ Backend webhook notification

### 6.3 Recommended Flow

1. TruLoad creates invoice
2. Invoice remains UNPAID in TruLoad
3. Customer logs into eCitizen and pays
4. Pesaflow sends IPN webhook
5. TruLoad updates invoice status
6. Receipt generation is triggered

### 6.4 Key Notes

- TruLoad **does not control** the payment UI
- Webhook is the primary payment signal
- Payment timing is asynchronous

---

## 7. Instant Payment Notification (Webhook)

### 7.1 Purpose

The IPN webhook is the **single source of truth** for payment completion.

### 7.2 What IPN Enables

✔ Confirms full or partial payment
✔ Identifies invoice and payment channel
✔ Triggers downstream actions (receipts)

### 7.3 TruLoad Webhook Responsibilities

- Validate request authenticity (`token_hash`)
- Match invoice reference
- Ensure idempotency (no double receipts)
- Persist payment event

### 7.4 .NET 10 Best Practices

- Minimal API or Controller endpoint
- HMAC verification using `SHA256`
- Idempotency keys (invoice + payment ref)
- Async background processing (Channels / Queues)

---

## 8. Payment Status Query (Reconciliation)

### 8.1 Purpose

Used when:
- Webhook delivery fails
- Manual reconciliation is required
- Scheduled verification jobs

### 8.2 Usage Guidelines

- Should **not** replace webhooks
- Use for retry / audit jobs
- Safe to run as a background service

---

## 9. Receipt Generation in TruLoad

### 9.1 Trigger Point

Receipts must be generated **only when**:

```
Invoice Status = PAID (Confirmed via IPN)
```

### 9.2 Recommended Design

- Event-driven handler: `InvoicePaidEvent`
- Receipt generation as a separate service
- Persist receipt reference back to invoice

### 9.3 Avoid

✖ Generating receipts on invoice creation
✖ Generating receipts from UI confirmation
✖ Assuming synchronous payment

---

## 10. Supported vs Unsupported Features

### Supported

- Invoice creation
- Immediate payment (iframe / STK)
- Deferred payment (eCitizen)
- Partial payments
- Webhook-based confirmation
- Status polling

### Not Supported (Per API Spec)

- Refunds
- Invoice cancellation
- Payment reversal APIs
- Subscription billing

---

## 11. Recommended TruLoad Architecture (.NET 10)

```
Controllers / Minimal APIs
        ↓
Application Services
        ↓
Domain Events (InvoicePaid)
        ↓
Background Workers
        ↓
Receipt Service
```

Key principles:
- Webhook-driven
- Idempotent
- Async-first
- Auditable

---

## 12. API Reference (Production-Ready)

### 12.1 Create Invoice API

**URL:** `POST {BaseUrl}/api/invoice/create`
**Auth:** Bearer token (from OAuth endpoint)
**Content-Type:** `application/json`

**Request Body:**
```json
{
  "account_id": "588",
  "amount_expected": "100.00",
  "amount_net": "100.00",
  "amount_settled_offline": "0",
  "callback_url": "https://your-domain/api/v1/payments/callback/ecitizen-pesaflow",
  "client_invoice_ref": "INV-2026-000001",
  "commission": "0",
  "currency": "USD",
  "email": "driver@example.com",
  "format": "json",
  "id_number": "12345678",
  "items": [
    {
      "account_id": "588",
      "desc": "Overload Fine",
      "item_ref": "INV-2026-000001",
      "price": "100.00",
      "quantity": "1",
      "require_settlement": "true",
      "currency": "USD"
    }
  ],
  "msisdn": "254700000000",
  "name": "John Doe",
  "notification_url": "https://your-domain/api/v1/payments/webhook/ecitizen-pesaflow"
}
```

**Response:** Returns `invoice_number` (Pesaflow's unique reference).

**Key Notes:**
- `account_id` = Pesaflow service ID (provided on activation)
- `client_invoice_ref` = TruLoad's InvoiceNo (used to match IPN callbacks)
- `notification_url` = IPN webhook endpoint (Pesaflow sends payment confirmations here)
- `callback_url` = redirect URL after user completes payment in browser
- `items` array is mandatory with at least one line item
- Bearer token is required (no `secure_hash` needed for this endpoint)

### 12.2 Online Checkout API (Iframe)

**URL:** `POST {BaseUrl}/PaymentAPI/iframev2.1.php`
**Content-Type:** `application/x-www-form-urlencoded`

**Parameters:**

| Parameter | Description |
|-----------|-------------|
| `apiClientID` | API client ID (provided on activation) |
| `serviceID` | Service ID (provided on activation) |
| `billDesc` | Description of the bill |
| `currency` | KES or USD |
| `billRefNumber` | Invoice reference number (unique) |
| `clientMSISDN` | Customer phone (e.g. 254700000000) |
| `clientName` | Customer name |
| `clientIDNumber` | Customer ID/passport |
| `clientEmail` | Customer email |
| `amountExpected` | Total amount |
| `callBackURLONSuccess` | Redirect URL after payment |
| `notificationURL` | IPN webhook URL |
| `secureHash` | HMAC-SHA256 signature (see below) |
| `format` | "json" or "iframe" |
| `sendSTK` | "true" or "false" (push STK on create) |

### 12.3 Secure Hash Computation

**Algorithm:** `Base64(hex(HMAC-SHA256(key, data)))`

**For Online Checkout:**
```
data = apiClientID + amount + serviceID + clientIDNumber + currency +
       billRefNumber + billDesc + clientName + secret
key  = Consumer Key (ApiKey)
```

**For Payment Status Query:**
```
data = api_client_id + ref_no
key  = Consumer Key (ApiKey)
```

**C# Implementation:**
```csharp
var keyBytes = Encoding.UTF8.GetBytes(apiKey);
var dataBytes = Encoding.UTF8.GetBytes(dataString);
using var hmac = new HMACSHA256(keyBytes);
var hashBytes = hmac.ComputeHash(dataBytes);
var hexHash = Convert.ToHexStringLower(hashBytes);
return Convert.ToBase64String(Encoding.UTF8.GetBytes(hexHash));
```

### 12.4 IPN Webhook Payload

Pesaflow sends HTTPS POST to `notification_url` with these fields:

| Field | Description |
|-------|-------------|
| `payment_channel` | MPESA, CARD, BANK, AIRTEL, etc. |
| `client_invoice_ref` | Our InvoiceNo (from `billRefNumber` / `client_invoice_ref`) |
| `payment_reference` | Pesaflow's unique payment ID |
| `currency` | Transaction currency |
| `amount_paid` | Total paid to date (includes partial payments) |
| `invoice_amount` | Original invoice amount |
| `status` | "PAID" or "SUCCESS" |
| `invoice_number` | Pesaflow's invoice reference |
| `payment_date` | Payment timestamp |
| `token_hash` | HMAC signature for verification (mandatory) |
| `last_payment_amount` | Current payment amount |

**Webhook Signature Verification:**
```
verification_data = invoice_number + amount_paid + secret
expected_hash = Base64(hex(HMAC-SHA256(ApiKey, verification_data)))
```
If `token_hash` does not match, reject the webhook.

### 12.5 Payment Status Query

**URL:** `GET {BaseUrl}/api/invoice/payment/status`
**Parameters:** `api_client_id`, `ref_no`, `secure_hash`

Used for manual reconciliation when webhooks fail.

---

## 13. Production Configuration

### 13.1 Credential Sources

| Environment | Source |
|-------------|--------|
| Development | `appsettings.Development.json` (seeded to DB by `IntegrationConfigSeeder`) |
| Staging | Environment variables or K8s secrets |
| Production | Azure Key Vault / K8s secrets (never appsettings) |

### 13.2 URL Switching

| Environment | BaseUrl |
|-------------|---------|
| Test | `https://test.pesaflow.com` |
| Production | `https://api.pesaflow.com` (confirm with Pesaflow) |

### 13.3 Required Credentials

- **Consumer Key** (`ApiKey`) - HMAC signing key and Basic auth username
- **Consumer Secret** (`ApiSecret`) - Appended to hash data strings and Basic auth password
- **API Client ID** (`ApiClientId`) - Service identifier (e.g. "588")

All stored encrypted (AES-256-GCM) in `IntegrationConfig.EncryptedCredentials`.

---

## 14. TruLoad Integration Points

### 14.1 Prosecution Workflow Integration

```
Overload Detected
    -> Case Register auto-created
    -> Yard Entry auto-created
    -> Prosecution created (charges calculated)
    -> Invoice generated
    -> Invoice pushed to Pesaflow (Create Invoice API)
    -> Payment received (IPN webhook or manual cash)
    -> Receipt generated
    -> Load Correction Memo auto-created
    -> Reweigh initiated (with relief truck)
    -> Compliant reweigh triggers:
       - Case auto-closed (with payment narration)
       - Yard entry auto-released
       - Compliance certificate generated
```

### 14.2 Auto-Trigger Chain

| Event | Trigger | Service |
|-------|---------|---------|
| Invoice paid | Auto-create Load Correction Memo | `ReceiptService.RecordPaymentAsync` |
| Invoice paid | Update ProsecutionCase.Status to "paid" | `ReceiptService.RecordPaymentAsync` |
| Compliant reweigh | Auto-close case + release yard + issue cert | `WeighingService.CalculateComplianceAsync` |

---

## 15. Final Summary

Pesaflow fully supports TruLoad's required payment models:

- Pay-now invoices (Online Checkout with iframe/STK)
- Pay-later via eCitizen portal (Create Invoice API)
- Reliable backend confirmation (IPN webhook with mandatory signature verification)
- Clean receipt triggering (event-driven, idempotent)
- Payment reconciliation (Status Query API for failed webhooks)

When integrated correctly, TruLoad treats Pesaflow as a **trusted external payment authority**, while maintaining full control over invoice and receipt lifecycles.

---

**Audience:** TruLoad Engineering Team
**Target Runtime:** .NET 10
**Integration Style:** Secure, Event-Driven, Asynchronous
**Last Updated:** February 2026

