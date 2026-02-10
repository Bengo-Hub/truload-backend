======================================================================
  PESAFLOW API DIRECT TEST
  Base URL:      https://test.pesaflow.com
  API Client ID: 588
  API Key:       hkW0lc/+xu9GA5Di
  API Secret:    tgia2h6QEc...
  Retries:       1
  Token cache:   D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\.pesaflow_token_cache.json
  Timestamp:     2026-02-10T13:46:31.193486+00:00
======================================================================        

======================================================================        
  STEP 1: Get Pesaflow OAuth Token
======================================================================        
  [CACHE] Reusing cached token (expires in 3423s)

======================================================================        
  STEP 2: Create Dummy Invoice on Pesaflow
======================================================================        
  URL:         https://test.pesaflow.com/api/invoice/create
  Auth:        Bearer token
  Invoice Ref: TEST-20260210134631
  Amount:      100.00 KES
  Payload:
{
    "account_id": "588",
    "amount_expected": "100.00",
    "amount_net": "100.00",
    "amount_settled_offline": "0",
    "callback_url": "http://localhost:4000/api/v1/payments/callback/ecitizen-pesaflow",
    "client_invoice_ref": "TEST-20260210134631",
    "commission": "0",
    "currency": "KES",
    "email": "test@truload-e2e.co.ke",
    "format": "json",
    "id_number": "TEST-ID-001",
    "items": [
        {
            "account_id": "588",
            "desc": "Test Overload Fine (Pesaflow API Test)",
            "item_ref": "TEST-20260210134631",
            "price": "100.00",
            "quantity": "1",
            "require_settlement": "true",
            "currency": "KES"
        }
    ],
    "msisdn": "254700000000",
    "name": "Pesaflow API Test User",
    "notification_url": "http://localhost:4000/api/v1/payments/webhook/ecitizen-pesaflow"
}

  HTTP Status: 401
  Response:    {"status":401,"message":"Access to this resource Pesaflow.Invoice has not been granted"}

  [FAIL] Create Invoice failed with HTTP 401
         Error: {
  "status": 401,
  "message": "Access to this resource Pesaflow.Invoice has not been granted"  
}

======================================================================        
  STEP 3: Query Payment Status
======================================================================        
  Hash data:  '588TEST-20260210134631'
  Hash:       OTQ0YTdiOWRlODZhOGQwZTRjODdjMTRmZmU3ZGQzZDhlODIxZjRlMDU4MmJiNzg4NTBiMjAxNmI2MTgzMTRiYg==
  URL:        https://test.pesaflow.com/api/invoice/payment/status?api_client_id=588&ref_no=TEST-20260210134631&secure_hash=OTQ0YTdiOWRlODZhOGQwZTRjODdjMTRmZmU3ZGQzZDhlODIxZjRlMDU4MmJiNzg4NTBiMjAxNmI2MTgzMTRiYg%3D%3D

  HTTP Status: 404
  Response:    {"error":"Invoice Not Found"}

  [INFO] Status query returned HTTP 404

======================================================================        
  STEP 4: Test Online Checkout (iframe)
======================================================================        
  URL:        https://test.pesaflow.com/PaymentAPI/iframev2.1.php
  Hash data:  '588100.00588TEST-ID-001KESTEST-20260210134631Test Overload FineTest Usertgia2h6Q...'
  Hash:       NDY5MTJhNzdkODZkZGFiYjk2NDAzYzE3OGVmYzIxYzU3YTY3YjE3MWUxMWVmZGYyMzgyNDE1NmQxMjc4ZDQ4Nw==

  HTTP Status: 422
  Response:    {"error":"invalid params on parsing %{\"amountExpected\" => \"100.00\", \"apiClientID\" => \"588\", \"billDesc\" => \"Test Overload Fine\", \"billRefNumber\" => \"TEST-20260210134631\", \"callBackURLONSuccess\" => \"http://localhost:4000/api/v1/payments/callback/ecitizen-pesaflow\", \"clientEmail\" => \"test@truload-e2e.co.ke\", \"clientIDNumber\" => \"TEST-ID-001\", \"clientMSISDN\" => \"254700000000\", \"clientName\" => \"Test User\", \"currency\" => \"KES\", \"format\" => \"json\", \"notificationURL\" => \"http://localhost:4000/api/v1/payments/webhook/ecitizen-pesaflow\", \"payment_gateway_id\" => 1, \"secureHash\" => \"NDY5MTJhNzdkODZkZGFiYjk2NDAzYzE3OGVmYzIxYzU3YTY3YjE3MWUxMWVmZGYyMzgyNDE1NmQxMjc4ZDQ4Nw==\", \"sendSTK\" => \"false\", \"serviceID\" => \"588\"}"}

  [INFO] Checkout returned HTTP 422

======================================================================        
  SUMMARY
======================================================================        
  [PASS] oauth: PASS
  [FAIL] create_invoice: FAIL
  [TEST] payment_status: TESTED
  [TEST] online_checkout: TESTED
======================================================================