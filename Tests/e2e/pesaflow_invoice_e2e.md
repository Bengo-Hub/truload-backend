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
