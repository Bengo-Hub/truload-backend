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

## 12. Final Summary

Pesaflow fully supports TruLoad’s required payment models:

✔ Pay-now invoices
✔ Pay-later via eCitizen
✔ Reliable backend confirmation
✔ Clean receipt triggering

When integrated correctly, TruLoad can treat Pesaflow as a **trusted external payment authority**, while maintaining full control over invoice and receipt lifecycles.

---

**Audience:** TruLoad Engineering Team  
**Target Runtime:** .NET 10  
**Integration Style:** Secure, Event-Driven, Asynchronous

