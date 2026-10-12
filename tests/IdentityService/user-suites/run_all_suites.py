import sys
import time
import test_user_create_suite
import test_user_list_suite
import test_user_update_suite
import test_user_assign_suite

def main():
    print("=" * 70)
    print("UAV PMS - COMPREHENSIVE USER MANAGEMENT TEST SUITE")
    print("=" * 70)
    start_time = time.time()

    suites = [
        ("USER_CREATE (25 Test Cases)", test_user_create_suite.run_suite),
        ("USER_LIST & DETAIL (20 Test Cases)", test_user_list_suite.run_suite),
        ("USER_UPDATE (20 Test Cases)", test_user_update_suite.run_suite),
        ("USER_ASSIGNABLE (15 Test Cases)", test_user_assign_suite.run_suite),
    ]

    suite_stats = []
    total_passed = 0
    total_cases = 0

    for name, runner in suites:
        print("\n" + "#" * 60)
        print(f"RUNNING: {name}")
        print("#" * 60)
        results = runner()
        passed = sum(1 for r in results if "PASS" in r.get("status", ""))
        count = len(results)
        total_passed += passed
        total_cases += count
        suite_stats.append((name, passed, count))

    elapsed = time.time() - start_time

    print("\n" + "=" * 70)
    print("FINAL TEST EXECUTION SUMMARY")
    print("=" * 70)
    for name, passed, count in suite_stats:
        status_flag = "PASS" if passed == count else "WARN/FAIL"
        print(f"  [{status_flag:9}] {name:<35}: {passed}/{count} passed ({passed/count*100:.1f}%)")

    print("-" * 70)
    print(f"TOTAL RESULT: {total_passed}/{total_cases} PASSED ({total_passed/total_cases*100:.1f}%) in {elapsed:.2f} seconds")
    print("=" * 70)

    if total_passed < total_cases:
        sys.exit(1)

if __name__ == "__main__":
    main()
