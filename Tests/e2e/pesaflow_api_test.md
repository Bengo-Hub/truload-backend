======================================================================
  PESAFLOW API DIRECT TEST
  Base URL:      https://test.pesaflow.com
  API Client ID: 588
  API Key:       hkW0lc/+xu9GA5Di
  API Secret:    tgia2h6QEc...
  Retries:       1
  Token cache:   D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\.pesaflow_token_cache.json
  Timestamp:     2026-02-11T16:31:39.119500+00:00
======================================================================

======================================================================
  STEP 1: Get Pesaflow OAuth Token
======================================================================
  [CACHE] Cached token expired, requesting new one
  URL:          https://test.pesaflow.com/api/oauth/generate/token
  Method:       POST (JSON body)
  ApiKey:       hkW0lc/+xu9GA5Di
  ApiSecret:    tgia2h6QEc...
  Payload:      {"key": "hkW0lc/+xu9GA5Di", "secret": "tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X"}

  HTTP Status:  200
  Response:     {"token":"eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpZCI6IjU4ODIwMjYwMjExMTYwMjE3NzA4Mjc1MDAifQ.-RWGNftJuvXlor2fH5Kyc8ydv7Gq0xiP8H-I7vqCVJc","expiry":3599}

  [PASS] Token obtained successfully
         Token:      eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpZCI6IjU4O...
         Expires in: 3599s
  [CACHE] Token cached to .pesaflow_token_cache.json (valid for 3539s)

======================================================================
  STEP 2: Create Dummy Invoice on Pesaflow
======================================================================
  URL:         https://test.pesaflow.com/api/invoice/create
  Auth:        Bearer token
  Invoice Ref: TEST-20260211163139
  Amount:      100.00 KES
  Payload:
{
    "account_id": "588",
    "amount_expected": "100.00",
    "amount_net": "100.00",
    "amount_settled_offline": "0",
    "callback_url": "http://localhost:4000/api/v1/payments/callback/ecitizen-pesaflow",
    "client_invoice_ref": "TEST-20260211163139",
    "commission": "0",
    "currency": "KES",
    "email": "test@truload-e2e.co.ke",
    "format": "json",
    "id_number": "TEST-ID-001",
    "items": [
        {
            "account_id": "588",
            "desc": "Test Overload Fine (Pesaflow API Test)",
            "item_ref": "TEST-20260211163139",
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

  [FALLBACK] Server-side invoice create unauthorized — using local invoice + iframe flow
  [LOCAL] Dummy invoice saved: D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\pesaflow_local_invoice_LOCAL-20260211163139.json

======================================================================
  STEP 4: Test Online Checkout (iframe)
======================================================================
  URL:        https://test.pesaflow.com/PaymentAPI/iframev2.1.php
  Hash data:  '588100.00235330TEST-ID-001KESLOCAL-20260211163139Test Overload FineTest Usertgia...'
  Hash:       YTZkYjZkMjU0MzgxMWZlNjAzYTQ3ZjQ2YTY5YmI2YThiNDgwNTljMzNlOWM4YWY4ZTExMDgwMTcwNTE1YTJmYw==

  HTTP Status: 200
  Response:    {"invoice_number":"RMNWAX","invoice_link":"https://test.pesaflow.com/checkout?request_id=44ihJEXqeGu4_qRTSEQh","commission":"5.00","amount_net":"100.00","amount_expected":"105.00"}

  [PASS] Checkout endpoint responded

======================================================================
  SIMULATE IPN: Saving simulated IPN payload to file (no network POST)
======================================================================
  Saved simulated IPN to: D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\pesaflow_saved_ipn_LOCAL-20260211163139.json

======================================================================
  SUMMARY
======================================================================
  [PASS] oauth: PASS
  [TEST] create_invoice: FALLBACK_LOCAL
======================================================================
======================================================================
  PESAFLOW API DIRECT TEST
  Base URL:      https://test.pesaflow.com
  API Client ID: 588
  API Key:       hkW0lc/+xu9GA5Di
  API Secret:    tgia2h6QEc...
  Retries:       1
  Token cache:   D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\.pesaflow_token_cache.json
  Timestamp:     2026-02-11T16:37:38.584163+00:00
======================================================================

======================================================================
  STEP 1: Get Pesaflow OAuth Token
======================================================================
  [CACHE] Reusing cached token (expires in 3180s)

======================================================================
  STEP 2: Create Dummy Invoice on Pesaflow
======================================================================
  URL:         https://test.pesaflow.com/api/invoice/create
  Auth:        Bearer token
  Invoice Ref: TEST-20260211163738
  Amount:      100.00 KES
  Payload:
{
    "account_id": "588",
    "amount_expected": "100.00",
    "amount_net": "100.00",
    "amount_settled_offline": "0",
    "callback_url": "http://localhost:4000/api/v1/payments/callback/ecitizen-pesaflow",
    "client_invoice_ref": "TEST-20260211163738",
    "commission": "0",
    "currency": "KES",
    "email": "test@truload-e2e.co.ke",
    "format": "json",
    "id_number": "TEST-ID-001",
    "items": [
        {
            "account_id": "588",
            "desc": "Test Overload Fine (Pesaflow API Test)",
            "item_ref": "TEST-20260211163738",
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

  [FALLBACK] Server-side invoice create unauthorized — using local invoice + iframe flow
  [LOCAL] Dummy invoice saved: D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\pesaflow_local_invoice_LOCAL-20260211163739.json

======================================================================
  STEP 4: Test Online Checkout (iframe)
======================================================================
  URL:        https://test.pesaflow.com/PaymentAPI/iframev2.1.php
  Hash data:  '588100.00235330TEST-ID-001KESLOCAL-20260211163739Test Overload FineTest Usertgia...'
  Hash:       NjJmZjhkYzhkYjkxOGM0NDA3NWIxYWRkZGRjM2E2NjJhZTY4YjkxNDkzNDA5NGNkNjQ1MGE4ODc0ZDRlMTQ3Yw==

  HTTP Status: 200
  Response:    {"invoice_number":"BPXQBP","invoice_link":"https://test.pesaflow.com/checkout?request_id=nn5TRUG67BZt8XH6igzU","commission":"5.00","amount_net":"100.00","amount_expected":"105.00"}

  [PASS] Checkout endpoint responded

======================================================================
  SIMULATE IPN: Saving simulated IPN payload to file (no network POST)
======================================================================
  Saved simulated IPN to: D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\pesaflow_saved_ipn_LOCAL-20260211163739.json

======================================================================
  SUMMARY
======================================================================
  [PASS] oauth: PASS
  [TEST] create_invoice: FALLBACK_LOCAL
======================================================================
======================================================================
  PESAFLOW API DIRECT TEST
  Base URL:      https://test.pesaflow.com
  API Client ID: 588
  API Key:       hkW0lc/+xu9GA5Di
  API Secret:    tgia2h6QEc...
  Retries:       1
  Token cache:   D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\.pesaflow_token_cache.json
  Timestamp:     2026-02-11T17:07:40.053389+00:00
======================================================================

======================================================================
  STEP 1: Get Pesaflow OAuth Token
======================================================================
  [CACHE] Reusing cached token (expires in 1378s)

======================================================================
  STEP 2: Create Invoice via Pesaflow iframe endpoint
======================================================================
  URL:        https://test.pesaflow.com/PaymentAPI/iframev2.1.php
  Invoice Ref: TEST-20260211170740
  Amount:      100.00 KES
  Hash data:  '588100.00235330TEST-ID-001KESTEST-20260211170740Test Overload FineTest Usertgia2...'
  Hash:       MDcwYzlhYzBlNzY5MmIyMWU2YzdiODQ0M2ZhMGI3YzE5MDVjZDU2YTM5NmFmNmQzM2RiMGRjMjYzMDA2OWIxNw==

  HTTP Status: 200
  Response:    {"invoice_number":"GWLQKD","invoice_link":"https://test.pesaflow.com/checkout?request_id=d8HbuZT_0nO3XLjH7nRy","commission":"5.00","amount_net":"100.00","amount_expected":"105.00"}

  [PASS] Invoice created on Pesaflow
         Pesaflow Invoice No: GWLQKD
         Payment Link:        https://test.pesaflow.com/checkout?request_id=d8HbuZT_0nO3XLjH7nRy
         Amount Net:          100.00
         Commission:          5.00
         Total Expected:      105.00

======================================================================
  STEP 3: Save Local Invoice
======================================================================
  [LOCAL] Invoice saved: D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\pesaflow_local_invoice_TEST-20260211170740.json
          Pesaflow Invoice: GWLQKD
          Payment Link: https://test.pesaflow.com/checkout?request_id=d8HbuZT_0nO3XLjH7nRy

======================================================================
  STEP 3: Query Payment Status
======================================================================
  Invoice Ref:     TEST-20260211170740
  Pesaflow Inv No: GWLQKD
  Hash data:  '588GWLQKD'
  Hash:       Mzc1ODY1N2QxOTIxY2U0ODYwOGQ2NWU3N2U3ZjdmNjJhMTljODQ0MDVkNjlkMzQ1MTNlZDNkNDA4YzIyMThlMg==
  URL:        https://test.pesaflow.com/api/invoice/payment/status?api_client_id=588&ref_no=GWLQKD&secure_hash=Mzc1ODY1N2QxOTIxY2U0ODYwOGQ2NWU3N2U3ZjdmNjJhMTljODQ0MDVkNjlkMzQ1MTNlZDNkNDA4YzIyMThlMg%3D%3D

  HTTP Status: 200
  Response:    {"status":"pending","ref_no":"GWLQKD","payment_date":null,"name":"Test User","currency":"KES","client_invoice_ref":"TEST-20260211170740","amount_paid":"0.00","amount_expected":"105.00"}

  [PASS] Payment status retrieved

======================================================================
  SUMMARY
======================================================================
  [PASS] oauth: PASS
  [PASS] create_invoice: PASS
  [PASS] save_local_invoice: PASS
  [TEST] payment_status: TESTED
======================================================================
======================================================================
  PESAFLOW API DIRECT TEST
  Base URL:      https://test.pesaflow.com
  API Client ID: 588
  API Key:       hkW0lc/+xu9GA5Di
  API Secret:    tgia2h6QEc...
  Retries:       1
  Token cache:   D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\.pesaflow_token_cache.json
  Timestamp:     2026-02-11T17:18:35.136061+00:00
======================================================================

======================================================================
  STEP 1: Get Pesaflow OAuth Token
======================================================================
  [CACHE] Reusing cached token (expires in 723s)

======================================================================
  STEP 2: Create Invoice via Pesaflow iframe endpoint
======================================================================
  URL:        https://test.pesaflow.com/PaymentAPI/iframev2.1.php
  Invoice Ref: TEST-20260211171835
  Amount:      100.00 KES
  Hash data:  '588100.00235330TEST-ID-001KESTEST-20260211171835Test Overload FineTest Usertgia2...'
  Hash:       NmI2YTcyNjYyZmMxOWJiYzViNjIyMTdkZGM2ODM0NTNhN2MwYjcyZjdjMzUyYzg3OWJhN2ZkMjI0Zjg5MmIyMQ==

  HTTP Status: 200
  Response:    {"invoice_number":"DMDAZZ","invoice_link":"https://test.pesaflow.com/checkout?request_id=hkVVK3tI5ras_BWWpj58","commission":"5.00","amount_net":"100.00","amount_expected":"105.00"}

  [PASS] Invoice created on Pesaflow
         Pesaflow Invoice No: DMDAZZ
         Payment Link:        https://test.pesaflow.com/checkout?request_id=hkVVK3tI5ras_BWWpj58
         Amount Net:          100.00
         Commission:          5.00
         Total Expected:      105.00

======================================================================
  STEP 3: Save Local Invoice
======================================================================
  [LOCAL] Invoice saved: D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\pesaflow_local_invoice_TEST-20260211171835.json
          Pesaflow Invoice: DMDAZZ
          Payment Link: https://test.pesaflow.com/checkout?request_id=hkVVK3tI5ras_BWWpj58

======================================================================
  STEP 3: Query Payment Status
======================================================================
  Invoice Ref:     TEST-20260211171835
  Pesaflow Inv No: DMDAZZ
  Hash data:  '588DMDAZZ'
  Hash:       ZmJkZDZjMDRiMTViYjk4MzdhYzhhM2FhYTE5YzM1MjkyOTdhM2IxYTMwMzQ5ZWQ2MDhiYWVlODNiNzUwYTI3ZQ==
  URL:        https://test.pesaflow.com/api/invoice/payment/status?api_client_id=588&ref_no=DMDAZZ&secure_hash=ZmJkZDZjMDRiMTViYjk4MzdhYzhhM2FhYTE5YzM1MjkyOTdhM2IxYTMwMzQ5ZWQ2MDhiYWVlODNiNzUwYTI3ZQ%3D%3D

  HTTP Status: 200
  Response:    {"status":"pending","ref_no":"DMDAZZ","payment_date":null,"name":"Test User","currency":"KES","client_invoice_ref":"TEST-20260211171835","amount_paid":"0.00","amount_expected":"105.00"}

  [PASS] Payment status retrieved

======================================================================
  SUMMARY
======================================================================
  [PASS] oauth: PASS
  [PASS] create_invoice: PASS
  [PASS] save_local_invoice: PASS
  [TEST] payment_status: TESTED
======================================================================
======================================================================
  PESAFLOW API DIRECT TEST
  Base URL:      https://test.pesaflow.com
  API Client ID: 588
  API Key:       hkW0lc/+xu9GA5Di
  API Secret:    tgia2h6QEc...
  Retries:       1
  Token cache:   D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\.pesaflow_token_cache.json
  Timestamp:     2026-02-13T11:38:48.380150+00:00
======================================================================

======================================================================
  STEP 1: Get Pesaflow OAuth Token
======================================================================
  URL:          https://test.pesaflow.com/api/oauth/generate/token
  Method:       POST (JSON body)
  ApiKey:       hkW0lc/+xu9GA5Di
  ApiSecret:    tgia2h6QEc...
  Payload:      {"key": "hkW0lc/+xu9GA5Di", "secret": "tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X"}

  HTTP Status:  200
  Response:     {"token":"eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpZCI6IjU4ODIwMjYwMjEzMTEwMjE3NzA5ODI3MjgifQ.MHlsyOFetLdY1gpXNKludSR6Us6nF3KwIphD-JYrTmA","expiry":3599}

  [PASS] Token obtained successfully
         Token:      eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpZCI6IjU4O...
         Expires in: 3599s
  [CACHE] Token cached to .pesaflow_token_cache.json (valid for 3539s)

======================================================================
  STEP 2: Create Invoice via Pesaflow iframe endpoint
======================================================================
  URL:        https://test.pesaflow.com/PaymentAPI/iframev2.1.php
  Invoice Ref: TEST-20260213113848
  Amount:      100.00 KES
  Hash data:  '588100.00235330TEST-ID-001KESTEST-20260213113848Test Overload FineTest Usertgia2...'
  Hash:       ZmU5ZmYyYTcyNDQ4MTE5YTI0MzM3MGJkODUxOTdjOTQwYWMyM2QwNGEyYWE4MTcxMjJkNTIyZDhkNDMxZjdiYg==

  HTTP Status: 200
  Response:    {"invoice_number":"RXJABA","invoice_link":"https://test.pesaflow.com/checkout?request_id=1suOXoptUiosUyRkr3OS","commission":"5.00","amount_net":"100.00","amount_expected":"105.00"}

  [PASS] Invoice created on Pesaflow
         Pesaflow Invoice No: RXJABA
         Payment Link:        https://test.pesaflow.com/checkout?request_id=1suOXoptUiosUyRkr3OS
         Amount Net:          100.00
         Commission:          5.00
         Total Expected:      105.00

======================================================================
  STEP 3: Save Local Invoice
======================================================================
  [LOCAL] Invoice saved: D:\Projects\BengoBox\TruLoad\truload-backend\Tests\e2e\pesaflow_local_invoice_TEST-20260213113848.json
          Pesaflow Invoice: RXJABA
          Payment Link: https://test.pesaflow.com/checkout?request_id=1suOXoptUiosUyRkr3OS

======================================================================
  STEP 3: Query Payment Status
======================================================================
  Invoice Ref:     TEST-20260213113848
  Pesaflow Inv No: RXJABA
  Hash data:  '588RXJABA'
  Hash:       NzVmN2IwNmI0ZmQ4OGE4MjA2OGQ3ZmMxYzFkNjRjNjc2NDljNzA1ZWNjNGFkN2NmYjFkYTg0MjIxMDVkYjc0Mg==
  URL:        https://test.pesaflow.com/api/invoice/payment/status?api_client_id=588&ref_no=RXJABA&secure_hash=NzVmN2IwNmI0ZmQ4OGE4MjA2OGQ3ZmMxYzFkNjRjNjc2NDljNzA1ZWNjNGFkN2NmYjFkYTg0MjIxMDVkYjc0Mg%3D%3D

  HTTP Status: 200
  Response:    {"status":"pending","ref_no":"RXJABA","payment_date":null,"name":"Test User","currency":"KES","client_invoice_ref":"TEST-20260213113848","amount_paid":"0.00","amount_expected":"105.00"}

  [PASS] Payment status retrieved

======================================================================
  SUMMARY
======================================================================
  [PASS] oauth: PASS
  [PASS] create_invoice: PASS
  [PASS] save_local_invoice: PASS
  [TEST] payment_status: TESTED
======================================================================
======================================================================
  PESAFLOW API DIRECT TEST
  Base URL:      https://test.pesaflow.com
  API Client ID: 588
  API Key:       hkW0lc/+xu9GA5Di
  API Secret:    tgia2h6QEc...
  Retries:       1
  Token cache:   D:\Projects\Codevertex\TruLoad\truload-backend\Tests\e2e\.pesaflow_token_cache.json
  Timestamp:     2026-04-14T16:14:00.074460+00:00
======================================================================

======================================================================
  STEP 1: Get Pesaflow OAuth Token
======================================================================
  [CACHE] Cached token expired, requesting new one
  URL:          https://test.pesaflow.com/api/oauth/generate/token
  Method:       POST (JSON body)
  ApiKey:       hkW0lc/+xu9GA5Di
  ApiSecret:    tgia2h6QEc...
  Payload:      {"key": "hkW0lc/+xu9GA5Di", "secret": "tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X"}

  HTTP Status:  200
  Response:     {"token":"eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpZCI6IjU4ODIwMjYwNDE0MTYwNDE3NzYxODMyNDAifQ.L9mN3DusIwMvsazUX1BXwa8DE5-HQ6lEW_rBFCXmJZE","expiry":3599}

  [PASS] Token obtained successfully
         Token:      eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpZCI6IjU4O...
         Expires in: 3599s
  [CACHE] Token cached to .pesaflow_token_cache.json (valid for 3539s)

======================================================================
  STEP 2: Create Invoice via Pesaflow iframe endpoint
======================================================================
  URL:        https://test.pesaflow.com/PaymentAPI/iframev2.1.php
  Invoice Ref: TEST-20260414161400
  Amount:      100.00 KES
  Hash data:  '588100.00235330TEST-ID-001KESTEST-20260414161400Test Overload FineTest Usertgia2...'
  Hash:       NDI0ZGRmNzMwNzJiYjlmODNmZDE1Y2E4OTkxYTU1MmZhMWJmMjhlZTUwNjQ2ZjRlOTU4NzFmZjc0YTlkODdlMQ==

  HTTP Status: 200
  Response:    {"invoice_number":"AEVDVM","invoice_link":"https://test.pesaflow.com/checkout?request_id=rpIQWFqgPw4mI6q1UMRU","commission":"5.00","amount_net":"100.00","amount_expected":"105.00"}

  [PASS] Invoice created on Pesaflow
         Pesaflow Invoice No: AEVDVM
         Payment Link:        https://test.pesaflow.com/checkout?request_id=rpIQWFqgPw4mI6q1UMRU
         Amount Net:          100.00
         Commission:          5.00
         Total Expected:      105.00

======================================================================
  STEP 3: Save Local Invoice
======================================================================
  [LOCAL] Invoice saved: D:\Projects\Codevertex\TruLoad\truload-backend\Tests\e2e\pesaflow_local_invoice_TEST-20260414161400.json
          Pesaflow Invoice: AEVDVM
          Payment Link: https://test.pesaflow.com/checkout?request_id=rpIQWFqgPw4mI6q1UMRU

======================================================================
  STEP 3: Query Payment Status
======================================================================
  Invoice Ref:     TEST-20260414161400
  Pesaflow Inv No: AEVDVM
  Hash data:  '588AEVDVM'
  Hash:       MjBjZWZjZGE3N2FhNjIyNzFhYTBjN2I5ODVmNTRlZDA3MTEwODRkYmFlODA4ZDRmZDY2NTVlN2Q2ZmQzZWU5OQ==
  URL:        https://test.pesaflow.com/api/invoice/payment/status?api_client_id=588&ref_no=AEVDVM&secure_hash=MjBjZWZjZGE3N2FhNjIyNzFhYTBjN2I5ODVmNTRlZDA3MTEwODRkYmFlODA4ZDRmZDY2NTVlN2Q2ZmQzZWU5OQ%3D%3D

  HTTP Status: 200
  Response:    {"status":"pending","ref_no":"AEVDVM","payment_date":null,"name":"Test User","currency":"KES","client_invoice_ref":"TEST-20260414161400","amount_paid":"0.00","amount_expected":"105.00"}

  [PASS] Payment status retrieved

======================================================================
  SUMMARY
======================================================================
  [PASS] oauth: PASS
  [PASS] create_invoice: PASS
  [PASS] save_local_invoice: PASS
  [TEST] payment_status: TESTED
======================================================================
======================================================================
  PESAFLOW API DIRECT TEST
  Base URL:      https://test.pesaflow.com
  API Client ID: 588
  API Key:       hkW0lc/+xu9GA5Di
  API Secret:    tgia2h6QEc...
  Retries:       1
  Token cache:   D:\Projects\Codevertex\TruLoad\truload-backend\Tests\e2e\.pesaflow_token_cache.json
  Timestamp:     2026-04-14T16:42:07.145414+00:00
======================================================================

======================================================================
  STEP 1: Get Pesaflow OAuth Token
======================================================================
  [CACHE] Reusing cached token (expires in 1852s)

======================================================================
  STEP 2: Create Invoice via Pesaflow iframe endpoint
======================================================================
  URL:        https://test.pesaflow.com/PaymentAPI/iframev2.1.php
  Invoice Ref: TEST-20260414164207
  Amount:      100.00 KES
  Hash data:  '588100.00235330TEST-ID-001KESTEST-20260414164207Test Overload FineTest Usertgia2...'
  Hash:       MmM3Mzk2ZmZkZTI3NDQ1ZGE2NDkwMTI0MzA3NjdiZGZmMWYzZGU3ODUzNWE5MGI4ZjU1ZTYzZmE0ZWE0ZjUwOQ==

  HTTP Status: 200
  Response:    {"invoice_number":"LYVBVM","invoice_link":"https://test.pesaflow.com/checkout?request_id=wxSNr-DxzQKSiCANCpxj","commission":"5.00","amount_net":"100.00","amount_expected":"105.00"}

  [PASS] Invoice created on Pesaflow
         Pesaflow Invoice No: LYVBVM
         Payment Link:        https://test.pesaflow.com/checkout?request_id=wxSNr-DxzQKSiCANCpxj
         Amount Net:          100.00
         Commission:          5.00
         Total Expected:      105.00

======================================================================
  STEP 3: Save Local Invoice
======================================================================
  [LOCAL] Invoice saved: D:\Projects\Codevertex\TruLoad\truload-backend\Tests\e2e\pesaflow_local_invoice_TEST-20260414164207.json
          Pesaflow Invoice: LYVBVM
          Payment Link: https://test.pesaflow.com/checkout?request_id=wxSNr-DxzQKSiCANCpxj

======================================================================
  STEP 3: Query Payment Status
======================================================================
  Invoice Ref:     TEST-20260414164207
  Pesaflow Inv No: LYVBVM
  Hash data:  '588LYVBVM'
  Hash:       OTgxZWMzODdlNmVhMzVjYzUwMDg2ZmY4MDEwNzRiY2ExMTA1NjBjNTU2MWNmNWIxOGRjM2U3M2IxMGYxN2U4NQ==
  URL:        https://test.pesaflow.com/api/invoice/payment/status?api_client_id=588&ref_no=LYVBVM&secure_hash=OTgxZWMzODdlNmVhMzVjYzUwMDg2ZmY4MDEwNzRiY2ExMTA1NjBjNTU2MWNmNWIxOGRjM2U3M2IxMGYxN2U4NQ%3D%3D

  HTTP Status: 200
  Response:    {"status":"pending","ref_no":"LYVBVM","payment_date":null,"name":"Test User","currency":"KES","client_invoice_ref":"TEST-20260414164207","amount_paid":"0.00","amount_expected":"105.00"}

  [PASS] Payment status retrieved

======================================================================
  SUMMARY
======================================================================
  [PASS] oauth: PASS
  [PASS] create_invoice: PASS
  [PASS] save_local_invoice: PASS
  [TEST] payment_status: TESTED
======================================================================
