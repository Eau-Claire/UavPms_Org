#!/usr/bin/env python3
"""
UAV PMS - NotificationService Test Suites Master Runner
Coordinates and executes all integration and regression test suites for NotificationService.
"""

import os
import subprocess
import sys
import time

SUITES = [
    ("Notification Creation & Lifecycle Suite", "tests/NotificationService/notification-suites/test_notification_creation_suite.py"),
    ("Notification History Retrieval Suite", "tests/NotificationService/notification-suites/test_notification_history_suite.py"),
    ("Notification Detail & Mark-As-Read Suite", "tests/NotificationService/notification-suites/test_notification_read_suite.py"),
    ("Notification Deletion Suite", "tests/NotificationService/notification-suites/test_notification_delete_suite.py"),
    ("Notification Email Enqueue Suite", "tests/NotificationService/notification-suites/test_notification_email_suite.py"),
    ("Notification Schedule Suite", "tests/NotificationService/notification-suites/test_notification_schedule_suite.py"),
    ("SignalR Notifications Hub Suite", "tests/NotificationService/notification-suites/test_notification_hub_suite.py"),
    ("RabbitMQ Event Consumers Suite", "tests/NotificationService/notification-suites/test_notification_consumer_suite.py"),
    ("Background Recurring Jobs Suite", "tests/NotificationService/notification-suites/test_notification_jobs_suite.py"),
    ("Email Delivery & EmailService Suite", "tests/NotificationService/notification-suites/test_notification_delivery_suite.py"),
]

def main():
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))
    print("=" * 80)
    print("   UAV PMS - NOTIFICATION SERVICE TEST SUITES RUNNER")
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
    print("   ALL NOTIFICATION SERVICE TEST SUITES COMPLETED")
    print("=" * 80)
    print(f"{'Suite Name':<45} | {'Status':<15} | {'Duration'}")
    print("-" * 80)
    for title, status, duration in results_summary:
        print(f"{title:<45} | {status:<15} | {duration}")
    print("-" * 80)
    print(f"Total Execution Time: {time.time() - total_start:.2f}s\n")

if __name__ == "__main__":
    main()
