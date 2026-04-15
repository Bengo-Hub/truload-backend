"""Live-environment orchestrator for the compliance + Pesaflow E2E suites.

This wrapper runs the four existing Python E2E suites against a live host
(the test environment, unless the caller explicitly opts into production),
captures stdout/stderr per scenario with secrets redacted, and writes a
single markdown evidence block that can be appended to the documentation at
truload-docs/docs/testing/live-e2e-results.md.

Usage:
    python run_live_suite.py \
        --base-url https://kuraweighapitest.masterspace.co.ke \
        --output live_run_$(date -u +%Y%m%dT%H%M%SZ).md

Do NOT set --base-url to a production host without the release manager's
explicit sign-off. The script refuses to run against codevertexitsolutions.com
unless --allow-production is passed.
"""
from __future__ import annotations

import argparse
import datetime as dt
import os
import pathlib
import re
import subprocess
import sys
from typing import Iterable

HERE = pathlib.Path(__file__).resolve().parent
E2E_ROOT = HERE.parent.parent  # .../Tests/e2e

SCENARIOS = [
    {
        "name": "Compliance E2E (14-step)",
        "cmd": ["python", str(E2E_ROOT / "compliancee2e" / "run_all_scenarios.py")],
        "covers": "login, autoweigh, case, yard, prosecution, invoice, payment, reweigh, auto-close",
    },
    {
        "name": "Pesaflow invoice E2E",
        "cmd": ["python", str(E2E_ROOT / "pesaflow_invoice_e2e.py")],
        "covers": "unpaid invoice selection + push to Pesaflow + status poll",
    },
    {
        "name": "Pesaflow callback & reconciliation probe",
        "cmd": ["python", str(E2E_ROOT / "pesaflow_callback_reconciliation_e2e.py")],
        "covers": "callback success/failure/timeout + webhook reconciliation idempotency",
    },
    {
        "name": "Pesaflow direct API validation",
        "cmd": ["python", str(E2E_ROOT / "pesaflow_api_test.py")],
        "covers": "OAuth, invoice iframe POST, payment status query",
    },
]

# Redactor: strips tokens, phone numbers, and long secrets from scenario output.
REDACTORS: Iterable[tuple[re.Pattern[str], str]] = [
    (re.compile(r"(Authorization:\s*Bearer\s+)[A-Za-z0-9._\-]+", re.I), r"\1<redacted>"),
    (re.compile(r"(X-API-Key:\s*)[A-Za-z0-9._\-]+", re.I), r"\1<redacted>"),
    (re.compile(r'("access[_-]?token"\s*:\s*")[^"]+', re.I), r'\1<redacted>'),
    (re.compile(r'("secure[_-]?hash"\s*:\s*")[^"]+', re.I), r'\1<redacted>'),
    (re.compile(r'("api[_-]?key"\s*:\s*")[^"]+', re.I), r'\1<redacted>'),
    (re.compile(r'("password"\s*:\s*")[^"]+', re.I), r'\1<redacted>'),
    (re.compile(r"\b254\d{9}\b"), "254<redacted>"),  # MSISDN
    (re.compile(r"\b07\d{8}\b"), "07<redacted>"),
]


def redact(text: str) -> str:
    for pattern, replacement in REDACTORS:
        text = pattern.sub(replacement, text)
    return text


def run_scenario(scenario: dict, base_url: str) -> dict:
    start = dt.datetime.now(dt.timezone.utc)
    env = {**os.environ, "TRULOAD_BASE_URL": base_url}
    cmd = list(scenario["cmd"]) + ["--base-url", base_url]
    try:
        proc = subprocess.run(
            cmd, capture_output=True, text=True, env=env, timeout=900, check=False
        )
        stdout = proc.stdout or ""
        stderr = proc.stderr or ""
        returncode = proc.returncode
    except subprocess.TimeoutExpired as e:
        stdout = e.stdout or ""
        stderr = f"TIMEOUT after 900s\n{e.stderr or ''}"
        returncode = -1
    except FileNotFoundError as e:
        stdout = ""
        stderr = f"SCRIPT NOT FOUND: {e}"
        returncode = -2
    end = dt.datetime.now(dt.timezone.utc)
    return {
        "name": scenario["name"],
        "covers": scenario["covers"],
        "cmd": " ".join(cmd),
        "start": start.isoformat(timespec="seconds"),
        "end": end.isoformat(timespec="seconds"),
        "returncode": returncode,
        "outcome": "PASS" if returncode == 0 else "FAIL",
        "stdout": redact(stdout),
        "stderr": redact(stderr),
    }


def render(results: list[dict], base_url: str) -> str:
    ts = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")
    lines = [
        f"## Controlled live run — {ts}",
        "",
        f"- Target base URL: `{base_url}`",
        f"- Operator: `{os.environ.get('USER') or os.environ.get('USERNAME') or 'unknown'}`",
        "",
        "| Scenario | Outcome | Duration | Covers |",
        "|---|---|---|---|",
    ]
    for r in results:
        start = dt.datetime.fromisoformat(r["start"])
        end = dt.datetime.fromisoformat(r["end"])
        dur = int((end - start).total_seconds())
        lines.append(
            f"| {r['name']} | {r['outcome']} | {dur}s | {r['covers']} |"
        )
    lines.append("")
    for r in results:
        lines.extend([
            f"### {r['name']}",
            "",
            f"- Command: `{r['cmd']}`",
            f"- Return code: `{r['returncode']}`",
            f"- Started: `{r['start']}`",
            f"- Ended:   `{r['end']}`",
            "",
            "<details><summary>stdout (redacted)</summary>",
            "",
            "```",
            r["stdout"][-8000:].strip() or "(empty)",
            "```",
            "",
            "</details>",
            "",
            "<details><summary>stderr (redacted)</summary>",
            "",
            "```",
            r["stderr"][-4000:].strip() or "(empty)",
            "```",
            "",
            "</details>",
            "",
        ])
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--output", default=str(HERE / "live_run.md"))
    parser.add_argument(
        "--allow-production",
        action="store_true",
        help="Required to run against *.codevertexitsolutions.com",
    )
    args = parser.parse_args()

    if "codevertexitsolutions.com" in args.base_url and not args.allow_production:
        print(
            "Refusing to run against production host without --allow-production.",
            file=sys.stderr,
        )
        return 2

    print(f"Running live suite against {args.base_url}")
    results = [run_scenario(s, args.base_url) for s in SCENARIOS]
    doc = render(results, args.base_url)
    pathlib.Path(args.output).write_text(doc, encoding="utf-8")
    print(f"Wrote evidence to {args.output}")
    return 0 if all(r["returncode"] == 0 for r in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
