# Live E2E runner

`run_live_suite.py` orchestrates the existing compliance and Pesaflow E2E
suites against the live test host, redacts secrets from their output, and
writes a single markdown evidence block suitable for pasting into
`truload-docs/docs/testing/live-e2e-results.md`.

## Usage

```bash
# Test environment (default, no extra flag)
python run_live_suite.py \
  --base-url https://kuraweighapitest.masterspace.co.ke \
  --output live_run_$(date -u +%Y%m%dT%H%M%SZ).md

# Production (requires explicit flag, release-manager sign-off)
python run_live_suite.py \
  --base-url https://truloadapi.codevertexitsolutions.com \
  --allow-production \
  --output live_run_prod_$(date -u +%Y%m%dT%H%M%SZ).md
```

## What it runs

- `compliancee2e/run_all_scenarios.py` — 14-step compliance flow
- `pesaflow_invoice_e2e.py` — unpaid-invoice push + status polling
- `pesaflow_callback_reconciliation_e2e.py` — callback + reconciliation probe
- `pesaflow_api_test.py` — direct Pesaflow OAuth/invoice/status

## What it redacts

- `Authorization: Bearer <token>`
- `X-API-Key: <key>`
- JSON fields: `access_token`, `secure_hash`, `api_key`, `password`
- Kenyan MSISDNs (254XXXXXXXXX, 07XXXXXXXX)

## Output

A markdown file with a header block, a per-scenario outcome table, and
collapsed `<details>` sections for stdout and stderr. Tail-truncated to keep
the evidence readable (last 8 KB stdout / 4 KB stderr per scenario).

## After the run

1. Review `live_run_<timestamp>.md` in a text editor and confirm no residual
   secrets slipped past the redactor.
2. Append the contents to
   `truload-docs/docs/testing/live-e2e-results.md` under a new dated section.
3. Rebuild the docs site and confirm the evidence renders correctly.
