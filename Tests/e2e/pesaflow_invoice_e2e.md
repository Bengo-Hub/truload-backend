======================================================================
  PESAFLOW INVOICE E2E TEST (via Backend API)
  Backend URL:   https://kuraweightapiest.masterspace.co.ke
  Email:         gadmin@masterspace.co.ke
  Timestamp:     2026-04-14T16:13:59.762901+00:00
======================================================================

======================================================================
  STEP 1: Authenticate with Backend
======================================================================
  URL:      https://kuraweightapiest.masterspace.co.ke/api/v1/auth/login
  Email:    gadmin@masterspace.co.ke
  Password: ************

  HTTP Status: 0

  [FAIL] Login failed with HTTP 0
         Body: [Errno 11001] getaddrinfo failed

  [ABORT] Cannot proceed without authentication

======================================================================
  SUMMARY
======================================================================
  [FAIL] auth: FAIL
======================================================================
======================================================================
  PESAFLOW INVOICE E2E TEST (via Backend API)
  Backend URL:   https://kuraweighapitest.masterspace.co.ke
  Email:         gadmin@masterspace.co.ke
  Timestamp:     2026-04-14T16:32:04.074046+00:00
======================================================================

======================================================================
  STEP 1: Authenticate with Backend
======================================================================
  URL:      https://kuraweighapitest.masterspace.co.ke/api/v1/auth/login
  Email:    gadmin@masterspace.co.ke
  Password: ************

  HTTP Status: 401

  [FAIL] Login failed with HTTP 401
         Body: {"message":"Invalid email or password"}

  [ABORT] Cannot proceed without authentication

======================================================================
  SUMMARY
======================================================================
  [FAIL] auth: FAIL
======================================================================
======================================================================
  PESAFLOW INVOICE E2E TEST (via Backend API)
  Backend URL:   https://kuraweighapitest.masterspace.co.ke
  Email:         gadmin@masterspace.co.ke
  Timestamp:     2026-04-14T16:41:47.410062+00:00
======================================================================

======================================================================
  STEP 1: Authenticate with Backend
======================================================================
  URL:      https://kuraweighapitest.masterspace.co.ke/api/v1/auth/login
  Email:    gadmin@masterspace.co.ke
  Password: ************

  HTTP Status: 401

  [FAIL] Login failed with HTTP 401
         Body: {"message":"Account is locked out"}

  [ABORT] Cannot proceed without authentication

======================================================================
  SUMMARY
======================================================================
  [FAIL] auth: FAIL
======================================================================
======================================================================
  PESAFLOW INVOICE E2E TEST (via Backend API)
  Backend URL:   https://kuraweighapitest.masterspace.co.ke
  Email:         gadmin@masterspace.co.ke
  Timestamp:     2026-04-14T16:57:38.310780+00:00
======================================================================

======================================================================
  STEP 1: Authenticate with Backend
======================================================================
  URL:      https://kuraweighapitest.masterspace.co.ke/api/v1/auth/login
  Email:    gadmin@masterspace.co.ke
  Password: ************

  HTTP Status: FAIL

  [FAIL] Login failed
         Body: Login failed: 500 {"error":{"code":"INTERNAL_SERVER_ERROR","message":"An unexpected error occurred","details":null,"traceId":"0HNKQ8OR3017M:00000001","timestamp":"2026-04-14T16:57:43.6396275Z"}}

  [ABORT] Cannot proceed without authentication

======================================================================
  SUMMARY
======================================================================
  [FAIL] auth: FAIL
======================================================================
======================================================================
  PESAFLOW INVOICE E2E TEST (via Backend API)
  Backend URL:   https://kuraweighapitest.masterspace.co.ke
  Email:         gadmin@masterspace.co.ke
  Timestamp:     2026-04-14T16:59:25.329712+00:00
======================================================================

======================================================================
  STEP 1: Authenticate with Backend
======================================================================
  URL:      https://kuraweighapitest.masterspace.co.ke/api/v1/auth/login
  Email:    gadmin@masterspace.co.ke
  Password: ************

  HTTP Status: CACHE

  [PASS] Login successful
         User:        Global Administrator (gadmin@masterspace.co.ke)
         isSuperUser: True
         Token:       eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc...

======================================================================
  STEP 2: Find Invoice to Push to Pesaflow
======================================================================
  URL:     https://kuraweighapitest.masterspace.co.ke/api/v1/invoices/search
  Search:  All invoices (most recent first)

  HTTP Status: 200
  Total invoices found: 8

  Selected invoice:
    ID:               926bea69-df38-42b5-afe8-e9294197c95e
    Invoice No:       INV-2026-000008
    Amount Due:       1.0 KES
    Status:           paid
    Pesaflow Status:  failed
    Pesaflow Invoice: PJLVXY

======================================================================
  STEP 3: Push Invoice to Pesaflow
======================================================================
  URL:        https://kuraweighapitest.masterspace.co.ke/api/v1/invoices/926bea69-df38-42b5-afe8-e9294197c95e/pesaflow
  Invoice ID: 926bea69-df38-42b5-afe8-e9294197c95e
  Invoice No: INV-2026-000008
  Amount:     1.0 KES

  HTTP Status: 400
  Response:
    {
      "success": false,
      "pesaflowInvoiceNumber": null,
      "paymentLink": null,
      "gatewayFee": null,
      "amountNet": null,
      "totalAmount": null,
      "currency": null,
      "message": "Pesaflow API error: UnprocessableEntity - {\"status\":422,\"message\":\"Invoice already paid\"}"
    }

  [FAIL] Push to Pesaflow failed with HTTP 400
         Error: Pesaflow API error: UnprocessableEntity - {"status":422,"message":"Invoice already paid"}

======================================================================
  STEP 4: Query Payment Status
======================================================================
  URL:        https://kuraweighapitest.masterspace.co.ke/api/v1/invoices/926bea69-df38-42b5-afe8-e9294197c95e/payment-status
  Invoice ID: 926bea69-df38-42b5-afe8-e9294197c95e

  HTTP Status: 400
  Payment Status:
    {
      "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
      "title": "One or more validation errors occurred.",
      "status": 400,
      "errors": {
        "invoiceRefNo": [
          "The invoiceRefNo field is required."
        ]
      },
      "traceId": "00-a859f9a075cc6b02e1d4609efc89f853-a9a49f085d6f4952-00"
    }

  [INFO] Payment status query returned HTTP 400

======================================================================
  SUMMARY
======================================================================
  [PASS] auth: PASS
  [PASS] find_invoice: PASS
  [FAIL] push_pesaflow: FAIL
  [TEST] payment_status: TESTED
======================================================================
======================================================================
  PESAFLOW INVOICE E2E TEST (via Backend API)
  Backend URL:   https://kuraweighapitest.masterspace.co.ke
  Email:         gadmin@masterspace.co.ke
  Timestamp:     2026-04-14T17:00:26.872864+00:00
======================================================================

======================================================================
  STEP 1: Authenticate with Backend
======================================================================
  URL:      https://kuraweighapitest.masterspace.co.ke/api/v1/auth/login
  Email:    gadmin@masterspace.co.ke
  Password: ************

  HTTP Status: CACHE

  [PASS] Login successful
         User:        Global Administrator (gadmin@masterspace.co.ke)
         isSuperUser: True
         Token:       eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc...

======================================================================
  STEP 2: Find Invoice to Push to Pesaflow
======================================================================
  URL:     https://kuraweighapitest.masterspace.co.ke/api/v1/invoices/search
  Search:  All invoices (most recent first)

  HTTP Status: 200
  Total invoices found: 8

  Selected invoice:
    ID:               fad25461-b139-4789-a807-f4cd114fc7d9
    Invoice No:       INV-2026-000004
    Amount Due:       2499900.0 KES
    Status:           pending
    Pesaflow Status:  None
    Pesaflow Invoice: None

======================================================================
  STEP 3: Push Invoice to Pesaflow
======================================================================
  URL:        https://kuraweighapitest.masterspace.co.ke/api/v1/invoices/fad25461-b139-4789-a807-f4cd114fc7d9/pesaflow
  Invoice ID: fad25461-b139-4789-a807-f4cd114fc7d9
  Invoice No: INV-2026-000004
  Amount:     2499900.0 KES

  HTTP Status: 200
  Response:
    {
      "success": true,
      "pesaflowInvoiceNumber": "QVBZBW",
      "paymentLink": "https://test.pesaflow.com/checkout?request_id=wb6Y1MyJiJ2w-bWOpP_J",
      "gatewayFee": 50.0,
      "amountNet": 2499900.0,
      "totalAmount": 2499950.0,
      "currency": "KES",
      "message": "Invoice created on Pesaflow via iframe endpoint"
    }

  [PASS] Invoice pushed to Pesaflow successfully
         Pesaflow Invoice: QVBZBW
         Payment Link:     https://test.pesaflow.com/checkout?request_id=wb6Y1MyJiJ2w-bWOpP_J
         Gateway Fee:      50.0
         Amount Net:       2499900.0

======================================================================
  STEP 4: Query Payment Status
======================================================================
  URL:        https://kuraweighapitest.masterspace.co.ke/api/v1/invoices/fad25461-b139-4789-a807-f4cd114fc7d9/payment-status
  Invoice ID: fad25461-b139-4789-a807-f4cd114fc7d9

  HTTP Status: 400
  Payment Status:
    {
      "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
      "title": "One or more validation errors occurred.",
      "status": 400,
      "errors": {
        "invoiceRefNo": [
          "The invoiceRefNo field is required."
        ]
      },
      "traceId": "00-145e46c899d4bfbb87d87fea3ef9ce31-b17c1ca4d65c4549-00"
    }

  [INFO] Payment status query returned HTTP 400

======================================================================
  SUMMARY
======================================================================
  [PASS] auth: PASS
  [PASS] find_invoice: PASS
  [PASS] push_pesaflow: PASS
  [TEST] payment_status: TESTED
======================================================================
