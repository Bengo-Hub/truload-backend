"""Run all 7 e2e compliance scenarios sequentially and save results to TEST_RESULTS.md"""
import subprocess
import sys
import datetime

RESULTS_FILE = "TEST_RESULTS.md"
SCENARIOS = [
    ("compliance_e2e_scenario_1.py", "Scenario 1: Standard Overload Workflow"),
    ("compliance_e2e_scenario_2.py", "Scenario 2: Compliant Vehicle (No Overload)"),
    ("compliance_e2e_scenario_3.py", "Scenario 3: Tag-Hold and Yard Release"),
    ("compliance_e2e_scenario_4.py", "Scenario 4: Permit-Based Exemption"),
    ("compliance_e2e_scenario_5.py", "Scenario 5: Court Escalation Path"),
    ("compliance_e2e_scenario_6.py", "Scenario 6: Full Court Case Lifecycle"),
    ("compliance_e2e_scenario_7.py", "Scenario 7: Repeat Offender Multiple Overloads"),
]


def main():
    with open(RESULTS_FILE, "w", encoding="utf-8") as f:
        f.write("# TruLoad E2E Compliance Test Results\n\n")
        f.write(f"**Date**: {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write("**Environment**: localhost:4000 (fresh database)\n\n")
        f.write("---\n\n")

        overall_pass = 0
        overall_fail = 0
        overall_total = 0
        scenario_summaries = []

        for script, title in SCENARIOS:
            print(f"\n=== Running {title} ===", flush=True)
            f.write(f"## {title}\n\n")
            f.write("```\n")

            try:
                result = subprocess.run(
                    [sys.executable, script],
                    capture_output=True,
                    text=True,
                    timeout=300,
                )
                output = result.stdout
                f.write(output)

                # Filter non-deprecation stderr
                if result.stderr:
                    stderr_lines = [
                        l
                        for l in result.stderr.split("\n")
                        if l.strip() and "DeprecationWarning" not in l
                    ]
                    if stderr_lines:
                        f.write("\nSTDERR:\n")
                        f.write("\n".join(stderr_lines))

                # Parse totals from output
                sc_pass = 0
                sc_fail = 0
                sc_total = 0
                for line in output.split("\n"):
                    if "TOTAL:" in line and "PASS:" in line and "FAIL:" in line:
                        parts = line.strip().split("|")
                        for part in parts:
                            part = part.strip()
                            if part.startswith("TOTAL:"):
                                sc_total = int(part.split(":")[1].strip())
                            elif part.startswith("PASS:"):
                                sc_pass = int(part.split(":")[1].strip())
                            elif part.startswith("FAIL:"):
                                sc_fail = int(part.split(":")[1].strip())

                overall_total += sc_total
                overall_pass += sc_pass
                overall_fail += sc_fail

                status = "PASS" if sc_fail == 0 and sc_total > 0 else "FAIL"
                scenario_summaries.append((title, sc_total, sc_pass, sc_fail, status))
                print(f"  Result: {sc_pass}/{sc_total} passed ({status})", flush=True)

            except subprocess.TimeoutExpired:
                f.write("\n*** TIMEOUT (300s) ***\n")
                scenario_summaries.append((title, 0, 0, 0, "TIMEOUT"))
                print("  TIMEOUT after 300s", flush=True)
            except Exception as e:
                f.write(f"\n*** ERROR: {e} ***\n")
                scenario_summaries.append((title, 0, 0, 0, "ERROR"))
                print(f"  ERROR: {e}", flush=True)

            f.write("```\n\n---\n\n")

        # Write overall summary
        f.write("## Overall Summary\n\n")
        f.write("| Scenario | Total | Pass | Fail | Status |\n")
        f.write("|----------|-------|------|------|--------|\n")
        for title, total, passed, failed, status in scenario_summaries:
            f.write(f"| {title} | {total} | {passed} | {failed} | {status} |\n")
        f.write(f"| **TOTAL** | **{overall_total}** | **{overall_pass}** | **{overall_fail}** | ")
        if overall_fail == 0 and overall_total > 0:
            f.write("**ALL PASS** |\n")
        else:
            f.write(f"**{overall_fail} FAILURES** |\n")
        f.write("\n")

        if overall_fail == 0 and overall_total > 0:
            f.write("### ALL SCENARIOS PASSED\n")
        else:
            f.write(f"### {overall_fail} FAILURES DETECTED\n")

        print(f"\n{'='*60}", flush=True)
        print(f"OVERALL: {overall_total} total, {overall_pass} pass, {overall_fail} fail", flush=True)
        print(f"Results saved to {RESULTS_FILE}", flush=True)


if __name__ == "__main__":
    main()
