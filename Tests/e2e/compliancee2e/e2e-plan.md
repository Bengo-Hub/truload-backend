Run a comprehensive E2E test of the TruLoad backend on http://localhost:4000. Execute each step sequentially, saving IDs from each response for use in subsequent steps. Report results for each step clearly.

IMPORTANT: For JSON bodies containing exclamation marks, write to a file using Python first, then use `@filename` with curl. Don't use quotes around `!` in bash.

Step 1: Login
- Write JSON to file: {"email":"gadmin@masterspace.co.ke","password":"ChangeMe123!"}  (use Python: `python -c "import json; json.dump({'email':'gadmin@masterspace.co.ke','password':'ChangeMe123' + chr(33)}, open('d:/Projects/BengoBox/login.json','w'))"`)
- POST to /api/v1/auth/login with the JSON file
- Save the accessToken and user.stationId

Step 2: Create Scale Test
- POST /api/v1/scale-tests
- Body: {"stationId":"<stationId>","testType":"standard","status":"passed","targetWeight":10000,"measuredWeight":10005,"deviationPercent":0.05,"instrumentId":"WIM-001","testedByUserId":"<userId>","notes":"E2E test calibration"}
- Save scaleTestId from response

Step 3: Autoweigh (3-axle overloaded 550kg)
- POST /api/v1/weighing-transactions/autoweigh
- Body: {"stationId":"<stationId>","vehicleRegNumber":"KDG 789C","weighingMode":"static","source":"Middleware","axles":[{"axleNumber":1,"measuredWeightKg":8200},{"axleNumber":2,"measuredWeightKg":9200},{"axleNumber":3,"measuredWeightKg":9150}]}
- Verify: captureStatus, controlStatus, overloadKg
- Save weighingId

Step 4: Manual Capture (update status)
- POST /api/v1/weighing-transactions/<weighingId>/capture-weights
- Body: {"axles":[{"axleNumber":1,"measuredWeightKg":8200},{"axleNumber":2,"measuredWeightKg":9200},{"axleNumber":3,"measuredWeightKg":9150}]}
- Verify: captureStatus="captured", controlStatus="Overloaded"
- IMPORTANT: Check for "Auto-created case register" in response or logs

Step 5: Verify Auto-Created Case (this is the critical new auto-trigger test)
- GET /api/v1/case/cases/by-weighing/<weighingId>
- Verify: caseNo exists, caseStatus should be Open, actId should NOT be null (should be linked to TRAFFIC_ACT)
- Save caseId

Step 6: Verify Auto-Created Yard Entry
- GET /api/v1/yard-entries/by-weighing/<weighingId>
- Verify: status should be "pending", reason should be "overload"
- Save yardEntryId

Step 7: Get ActId for prosecution
- GET /api/v1/case/cases/<caseId> to get the actId from the case
- Or use: curl to query act_definitions table for TRAFFIC_ACT code
- Save trafficActId

Step 8: Create Prosecution
- POST /api/v1/cases/<caseId>/prosecution
- Body: {"actId":"<trafficActId>","caseNotes":"E2E test prosecution"}
- Verify: prosecution created, charges calculated
- Save prosecutionId

Step 9: Generate Invoice
- POST /api/v1/prosecutions/<prosecutionId>/invoices
- Verify: invoiceNo, status, amountDue
- Save invoiceId

Step 10: Record Payment
- POST /api/v1/invoices/<invoiceId>/payments
- Body: {"amountPaid":<amount from invoice>,"currency":"USD","paymentMethod":"cash","transactionReference":"E2E-CASH-001","idempotencyKey":"<random uuid>"}
- Verify: receiptNo created, payment recorded

Step 11: Initiate Reweigh (compliant)
- POST /api/v1/weighing-transactions/reweigh
- Body: {"originalWeighingId":"<weighingId>","reweighTicketNumber":"RWG-E2E-001"}
- Save reweighId

Step 12: Capture Reweigh Weights (compliant — under limits)
- POST /api/v1/weighing-transactions/<reweighId>/capture-weights
- Body: {"axles":[{"axleNumber":1,"measuredWeightKg":7500},{"axleNumber":2,"measuredWeightKg":8500},{"axleNumber":3,"measuredWeightKg":8500}]}
- Verify: isCompliant=true, controlStatus="Compliant"

Step 13: Verify Auto-Close Cascade
- GET /api/v1/case/cases/<caseId> — verify status is "Closed", dispositionType contains "Compliance"
- GET /api/v1/yard-entries/by-weighing/<original weighingId> — verify status="released"

Step 14: Summary
- Print full summary of all steps, which passed/failed, and key IDs

For EACH step:
1. Print the step number and description
2. Show the curl command
3. Show the key fields from the response (use python -c to parse JSON and extract relevant fields)
4. Print PASS/FAIL

Use the Auth header: -H "Authorization: Bearer <token>" for all authenticated requests.

Write all request bodies to files using Python to avoid bash escaping issues with special characters.