import sys
import time
import test_login_suite
import test_refresh_token_suite
import test_send_otp_suite
import test_verify_otp_suite
import test_reset_password_suite
import test_me_profile_suite

def main():
    print("=" * 70)
    print("UAV PMS - COMPREHENSIVE AUTHENTICATION & AUTHORIZATION TEST SUITE")
    print("=" * 70)
    start_time = time.time()

    suites = [
        ("AUTH_LOGIN (32 Test Cases)", test_login_suite.run_suite),
        ("AUTH_REFRESH (25 Test Cases)", test_refresh_token_suite.run_suite),
        ("AUTH_OTP_SEND (25 Test Cases)", test_send_otp_suite.run_suite),
        ("AUTH_OTP_VERIFY (25 Test Cases)", test_verify_otp_suite.run_suite),
        ("AUTH_RESET (20 Test Cases)", test_reset_password_suite.run_suite),
        ("AUTH_ME (20 Test Cases)", test_me_profile_suite.run_suite),
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
