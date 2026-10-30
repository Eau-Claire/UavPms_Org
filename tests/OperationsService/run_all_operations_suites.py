import os
import subprocess
import sys
import time

SUITES = [
    ("Region Operations Suite", "tests/OperationsService/region-suites/test_region_suite.py"),
    ("Tower & PostGIS Spatial Suite", "tests/OperationsService/tower-suites/test_tower_suite.py"),
    ("Flight Mission Full Lifecycle Suite", "tests/OperationsService/mission-suites/test_mission_suite.py"),
    ("Maintenance Ticket Operations Suite", "tests/OperationsService/maintenance-suites/test_maintenance_suite.py"),
    ("Monitor & Audit Log Operations Suite", "tests/OperationsService/monitor-suites/test_monitor_suite.py"),
]

def main():
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))
    print("=" * 80)
    print("   UAV PMS - OPERATIONS SERVICE TEST SUITES RUNNER")
    print("=" * 80)
    
    total_start = time.time()
    results_summary = []
    
    for title, script_rel_path in SUITES:
        script_full_path = os.path.join(root_dir, script_rel_path)
        print(f"\n>>> Running: {title}")
        print(f"    Script: {script_rel_path}")
        print("-" * 80)
        
        t0 = time.time()
        proc = subprocess.run([sys.executable, script_full_path], cwd=root_dir)
        elapsed = time.time() - t0
        
        status = "COMPLETED" if proc.returncode == 0 else f"FAILED (Exit {proc.returncode})"
        results_summary.append((title, status, f"{elapsed:.2f}s"))
        time.sleep(1)
        
    print("\n" + "=" * 80)
    print("   ALL OPERATIONS SERVICE TEST SUITES COMPLETED")
    print("=" * 80)
    print(f"{'Suite Name':<42} | {'Status':<15} | {'Duration'}")
    print("-" * 80)
    for title, status, duration in results_summary:
        print(f"{title:<42} | {status:<15} | {duration}")
    print("-" * 80)
    print(f"Total Execution Time: {time.time() - total_start:.2f}s\n")

if __name__ == "__main__":
    main()
