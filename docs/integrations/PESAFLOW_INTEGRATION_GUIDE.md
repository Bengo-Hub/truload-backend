# Pesaflow (eCitizen) Payment Integration Guide - TruLoad

## Document Status
**Version:** 2.0  
**Last Updated:** February 11, 2026  
**Status:** Production Ready  
**Previous Version:** ~~ecitizen_test_pesaflow_payment_integration_tru_load.md~~ (deprecated)

---

## 1. Overview

This guide documents the **correct** Pesaflow integration workflow for TruLoad, based on validated API testing and production requirements.

### Key Changes from Previous Implementation
- ✅ **Invoice creation via iframe endpoint** (not `/api/invoice/create`)
- ✅ **Proper field mapping** from Pesaflow response
- ✅ **Background sync queue** for Pesaflow unreachability
- ✅ **Payment confirmation via webhooks** + polling fallback
- ✅ **Complete callback URLs** (success, failure, timeout)

---

## 2. Pesaflow Invoice Creation Flow (Correct Approach)

### 2.1 Endpoint
**POST** `https://test.pesaflow.com/PaymentAPI/iframev2.1.php`  
**POST** `https://pesaflow.ecitizen.go.ke/PaymentAPI/iframev2.1.php` (production)

### 2.2 Request Format
`Content-Type: application/x-www-form-urlencoded`  
**CamelCase keys required!**

```plaintext
apiClientID=588
serviceID=235330
billDesc=Overload Fine
currency=KES
billRefNumber=INV-20260211-001
clientMSISDN=254700000000
clientName=John Doe
clientIDNumber=12345678
clientEmail=john@example.com
amountExpected=100.00
callBackURLOnSuccess=https://api.truload.co.ke/api/v1/payments/callback/success
callBackURLOnFailure=https://api.truload.co.ke/api/v1/payments/callback/failure
callBackURLOnTimeout=https://api.truload.co.ke/api/v1/payments/callback/timeout
notificationURL=https://api.truload.co.ke/api/v1/payments/webhook/ecitizen-pesaflow
secureHash=YmY5ZjM2YzAzZGUyMDI5M2M0NDE0YmRiMWYyNTA4OWVkZDRjMWQ0YTY3MzQzYmMyZDZlNTFhNTdiNWE4MzE3Mg==
format=json
sendSTK=false
```

### 2.3 Secure Hash Computation
```
data_string = apiClientID + amount + serviceID + clientIDNumber + currency + billRefNumber + billDesc + clientName + secret

hmac = HMAC-SHA256(key=ApiKey, data=data_string)
hex_string = hmac.hex() // lowercase hex
secureHash = Base64(hex_string.encode('utf-8'))
```

### 2.4 Response (HTTP 200)
```json
{
  "invoice_number": "GWLQKD",
  "invoice_link": "https://test.pesaflow.com/checkout?request_id=d8HbuZT_0nO3XLjH7nRy",
  "commission": "5.00",
  "amount_net": "100.00",
  "amount_expected": "105.00"
}
```

### 2.5 Field Mapping to TruLoad Invoice Model

| Pesaflow Field | TruLoad Invoice Property | Description |
|---|---|---|
| `invoice_number` | `PesaflowInvoiceNumber` | Pesaflow's unique invoice ref |
| `invoice_link` | `PesaflowPaymentLink` | Payment URL for customer |
| `commission` | `PesaflowGatewayFee` | Gateway fee added by Pesaflow |
| `amount_net` | `PesaflowAmountNet` | Original amount (before fees) |
| `amount_expected` | `PesaflowTotalAmount` | Total amount (net + commission) |

---

## 3. Local Invoice Creation Workflow

```
1. Generate local Invoice (Status: pending)
   ↓
2. POST to Pesaflow iframe endpoint
   ↓
3. Success? → Map response fields to Invoice model
   ↓         → Set PesaflowSyncStatus = "synced"
   ↓         → Save Invoice with complete Pesaflow details
   ↓
4. Failure? → Set PesaflowSyncStatus = "pending"
   ↓         → Queue background task to retry
   ↓         → Save Invoice with local fields only
   ↓
5. Return payment_link to frontend for customer payment
```

### 3.1 Fallback Logic
When Pesaflow is unreachable during invoice creation:
- Save invoice locally with `PesaflowSyncStatus = "pending"`
- Queue a background job to retry invoice creation
- Background worker polls pending invoices and retries sync
- Once synced, update invoice with Pesaflow details

---

## 4. Payment Confirmation Flow

### 4.1 Primary: IPN Webhook
**Pesaflow posts to:** `notificationURL` when payment is completed

**Request Body:**
```json
{
  "payment_channel": "MPESA",
  "client_invoice_ref": "INV-20260211-001",
  "payment_reference": "PF123456789",
  "currency": "KES",
  "amount_paid": 105,
  "invoice_amount": 105,
  "status": "SUCCESS",
  "invoice_number": "GWLQKD",
  "payment_date": "2026-02-11T14:30:00Z",
  "token_hash": "ABC123...",
  "last_payment_amount": 105
}
```

**Verification:**
```
data_string = invoice_number + amount + secret
expected_hash = Base64(hex(HMAC-SHA256(ApiKey, data_string)))

if token_hash == expected_hash:
    # Valid payment
else:
    # Reject as fraudulent
```

**TruLoad Actions:**
1. Verify `token_hash`
2. Find Invoice by `client_invoice_ref` or `invoice_number`
3. Update Invoice status to "paid"
4. Save `payment_reference` to Invoice
5. Trigger Receipt generation
6. Return HTTP 200 to Pesaflow

### 4.2 Fallback: Payment Status Polling

**Use when:** IPN webhook not received (network issues, timeouts)

**Endpoint:** `GET https://test.pesaflow.com/api/invoice/payment/status`

**Query Params:**
```
api_client_id=588
ref_no=GWLQKD
secure_hash=<Base64(hex(HMAC-SHA256(ApiKey, "588GWLQKD")))>
```

**Response:**
```json
{
  "status": "paid",
  "ref_no": "GWLQKD",
  "payment_date": "2026-02-11T14:30:00",
  "name": "John Doe",
  "currency": "KES",
  "client_invoice_ref": "INV-20260211-001",
  "amount_paid": "105.00",
  "amount_expected": "105.00"
}
```

**TruLoad Background Worker:**
- Poll unpaid invoices with `PesaflowInvoiceNumber` every 5-10 minutes
- Query Pesaflow payment status
- If `status == "paid"`, update Invoice and generate Receipt

### 4.3 Manual Reconciliation

**Endpoint:** `https://pesaflow.ecitizen.go.ke/mpesa_recon_alpha.php`

**Use when:** Manual verification needed

**Request (form-urlencoded):**
```
mpesa_reference=PF123456789
pesaflow_invoice=GWLQKD
```

---

## 5. Integration Settings (appsettings.json / Database)

### 5.1 eCitizen IntegrationConfig Record

```json
{
  "ProviderName": "ecitizen_pesaflow",
  "DisplayName": "eCitizen Pesaflow",
  "BaseUrl": "https://test.pesaflow.com",
  "Environment": "test",
  "EncryptedCredentials": {
    "ApiKey": "hkW0lc/+xu9GA5Di",
    "ApiSecret": "tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X",
    "ApiClientId": "588",
    "ServiceId": "235330"
  },
  "WebhookUrl": "https://api.truload.co.ke/api/v1/payments/webhook/ecitizen-pesaflow",
  "CallbackUrl": "https://api.truload.co.ke/api/v1/payments/callback/success",
  "CallbackFailureUrl": "https://api.truload.co.ke/api/v1/payments/callback/failure",
  "CallbackTimeoutUrl": "https://api.truload.co.ke/api/v1/payments/callback/timeout",
  "PaymentPollingEndpoint": "https://test.pesaflow.com/api/payment/co/getStatus",
  "PaymentConfirmationEndpoint": "https://pesaflow.ecitizen.go.ke/mpesa_recon_alpha.php",
  "EndpointsJson": {
    "OAuth": "/api/oauth/generate/token",
    "InvoiceCreate": "/PaymentAPI/iframev2.1.php",
    "PaymentStatus": "/api/invoice/payment/status"
  }
}
```

### 5.2 Production URLs

| Environment | BaseUrl |
|---|---|
| Test | `https://test.pesaflow.com` |
| Production | `https://pesaflow.ecitizen.go.ke` |

---

## 6. Backend Implementation Summary

### 6.1 ECitizenService.CreatePesaflowInvoiceAsync

**Flow:**
1. Fetch local Invoice from database
2. Build iframe form data with camelCase keys
3. Compute secureHash
4. POST to `/PaymentAPI/iframev2.1.php`
5. Parse response JSON
6. Map `invoice_number`, `invoice_link`, `commission`, `amount_net`, `amount_expected` to Invoice model
7. Set `PesaflowSyncStatus = "synced"`
8. Save Invoice
9. Return response with `PaymentLink` to frontend

**Error Handling:**
- If Pesaflow returns 4xx/5xx: set `PesaflowSyncStatus = "failed"`, queue retry
- If network timeout/exception: set `PesaflowSyncStatus = "pending"`, queue retry

### 6.2 Webhook Controller

**Route:** `POST /api/v1/payments/webhook/ecitizen-pesaflow`

**Actions:**
1. Parse IPN payload
2. Verify `token_hash`
3. Find Invoice by `client_invoice_ref` or `invoice_number`
4. Check idempotency (already processed?)
5. Update Invoice: `Status = "paid"`, `PesaflowPaymentReference = payment_reference`
6. Trigger Receipt generation
7. Return 200 OK

### 6.3 Background Sync Worker

**Trigger:** Every 10 minutes (or on-demand)

**Actions:**
1. Query Invoices where `PesaflowSyncStatus IN ('pending', 'failed')` AND `Status = 'pending'`
2. For each invoice:
   - Retry `CreatePesaflowInvoiceAsync`
   - If success: update sync status to "synced"
   - If failure: increment retry count, back off

---

## 7. Frontend Integration

### 7.1 Invoice Generation

**Request:**
```http
POST /api/v1/invoices/{invoiceId}/pesaflow
{
  "clientName": "John Doe",
  "clientEmail": "john@example.com",
  "clientMsisdn": "254700000000",
  "clientIdNumber": "12345678"
}
```

**Response:**
```json
{
  "success": true,
  "pesaflowInvoiceNumber": "GWLQKD",
  "paymentLink": "https://test.pesaflow.com/checkout?request_id=...",
  "gatewayFee": 5.00,
  "amountNet": 100.00,
  "totalAmount": 105.00,
  "currency": "KES",
  "message": "Invoice created on Pesaflow via iframe endpoint"
}
```

### 7.2 Payment Initiation

Frontend displays `paymentLink` to customer:
- Option 1: Open in new tab
- Option 2: Embed in iframe
- Option 3: Redirect user

Customer completes payment on Pesaflow portal.

### 7.3 Payment Confirmation

**Webhook handles confirmation automatically** - frontend should poll Invoice status:

```http
GET /api/v1/invoices/{invoiceId}
```

```json
{
  "status": "paid",
  "pesaflowPaymentReference": "PF123456789",
  "receiptId": "uuid-here"
}
```

---

## 8. Testing

### 8.1 E2E Test Script

Run: `python Tests/e2e/pesaflow_api_test.py`

**Steps:**
1. Get OAuth token (cached)
2. Create invoice via iframe endpoint
3. Verify response fields
4. Save local invoice with Pesaflow details
5. Query payment status

**Expected Output:**
```
[PASS] oauth: PASS
[PASS] create_invoice: PASS
[PASS] save_local_invoice: PASS
[TEST] payment_status: TESTED
```

### 8.2 Test Credentials

```
Base URL: https://test.pesaflow.com
API Key: hkW0lc/+xu9GA5Di
API Secret: tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X
API Client ID: 588
Service ID: 235330
```

---

## 9. Security Best Practices

1. **Never log** `ApiKey`, `ApiSecret`, or `token_hash` in plaintext
2. **Always verify** `token_hash` on IPN webhooks
3. **Use HTTPS** for all callbacks and webhooks
4. **Encrypt credentials** at rest (EncryptedCredentials field)
5. **Rate limit** webhook endpoints to prevent DoS
6. **Idempotency** - check if payment already processed before creating receipt

---

## 10. Troubleshooting

### 10.1 HTTP 422 on Invoice Creation

**Cause:** Incorrect field names or missing `secureHash`  
**Fix:** Use exact camelCase keys: `callBackURLOnSuccess` (not `callBackURLONSuccess`)

### 10.2 Invalid Secure Hash

**Cause:** Wrong data_string order or encoding  
**Fix:** Use exact order: `apiClientID + amount + serviceID + clientIDNumber + currency + billRefNumber + billDesc + clientName + secret`

### 10.3 Webhook Not Received

**Cause:** Network firewall, wrong URL, or Pesaflow issue  
**Fix:** Enable fallback polling; check webhook URL is publicly accessible

### 10.4 Invoice Not Synced to Pesaflow

**Cause:** Pesaflow unavailable during creation  
**Fix:** Background worker will retry; check `PesaflowSyncStatus`

---

## 11. Migration from Old Implementation

**Deprecated Endpoints/Approaches:**
- ❌ `/api/invoice/create` with Bearer token (Section 6 of old docs)
- ❌ `InitiateCheckoutAsync` as separate step
- ❌ `PesaflowCheckoutUrl` field

**New Approach:**
- ✅ Single iframe POST creates invoice + returns payment link
- ✅ `PesaflowPaymentLink` field replaces `PesaflowCheckoutUrl`
- ✅ Gateway fees tracked separately

**Database Migration:**
- Run migration: `20260211_AddPesaflowInvoiceFields`
- Adds: `PesaflowPaymentLink`, `PesaflowGatewayFee`, `PesaflowAmountNet`, `PesaflowTotalAmount`, `PesaflowSyncStatus`
- Removes: `PesaflowCheckoutUrl`

---

## 12. References

- Original API Doc: `docs/integrations/ecitizen-original-api-doc.md`
- E2E Test: `Tests/e2e/pesaflow_api_test.py`
- Backend Service: `Services/Implementations/Financial/ECitizenService.cs`
- DTOs: `DTOs/Financial/ECitizenDtos.cs`
- Models: `Models/Financial/Invoice.cs`, `Models/System/IntegrationConfig.cs`

---

**End of Document**
