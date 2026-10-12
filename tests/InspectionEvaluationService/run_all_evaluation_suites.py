#!/usr/bin/env python3
"""
UAV PMS - InspectionEvaluationService Test Suites Master Runner
Coordinates and executes all integration and regression test suites for InspectionEvaluationService.
"""

import os
import subprocess
import sys
import time

SUITES = [
    ("gRPC Detection Evaluation Test Suite (35 TCs)", "tests/InspectionEvaluationService/evaluation-suites/test_evaluation_rpc_suite.py"),
    ("Engine Mathematical Precision Test Suite (28 TCs)", "tests/InspectionEvaluationService/evaluation-suites/test_evaluation_math_engine_suite.py"),
    ("Options Binding & Configuration Test Suite (22 TCs)", "tests/InspectionEvaluationService/evaluation-suites/test_evaluation_config_options_suite.py"),
    ("HTTP Health & Service Metadata Test Suite (18 TCs)", "tests/InspectionEvaluationService/evaluation-suites/test_evaluation_http_health_suite.py"),
    ("gRPC Integration & Performance Test Suite (18 TCs)", "tests/InspectionEvaluationService/evaluation-suites/test_evaluation_integration_suite.py"),
]


def main():
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))
    print("=" * 80)
    print("   UAV PMS - INSPECTION EVALUATION SERVICE TEST SUITES RUNNER")
    print("=" * 80)

    total_start = time.time()
    results_summary = []
    has_failure = False

    for title, script_rel_path in SUITES:
        script_full_path = os.path.join(root_dir, script_rel_path)
        print(f"\n>>> Running: {title}")
        print(f"    Script: {script_rel_path}")
        print("-" * 80)

        t0 = time.time()
        proc = subprocess.run([sys.executable, script_full_path], cwd=root_dir)
        elapsed = time.time() - t0

        if proc.returncode == 0:
            status = "COMPLETED"
        else:
            status = f"FAILED (Exit {proc.returncode})"
            has_failure = True

        results_summary.append((title, status, f"{elapsed:.2f}s"))
        time.sleep(1)

    print("\n" + "=" * 80)
    print("   ALL INSPECTION EVALUATION SERVICE TEST SUITES COMPLETED")
    print("=" * 80)
    print(f"{'Suite Name':<50} | {'Status':<15} | {'Duration'}")
    print("-" * 80)
    for title, status, duration in results_summary:
        print(f"{title:<50} | {status:<15} | {duration}")
    print("-" * 80)
    print(f"Total Execution Time: {time.time() - total_start:.2f}s\n")

    if has_failure:
        sys.exit(1)
    sys.exit(0)


if __name__ == "__main__":
    main()
