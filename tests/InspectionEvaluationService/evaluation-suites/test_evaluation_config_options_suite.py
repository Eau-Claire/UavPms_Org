#!/usr/bin/env python3
"""
UAV PMS - InspectionEvaluationService: Options Binding, Validation & Configuration Test Suite
Component Tested:
- UavPms.InspectionEvaluationService.Application.Common.Options.EvaluationThresholdOptions
- Microsoft.Extensions.Options (.ValidateDataAnnotations().ValidateOnStart())
- AppSettings, Environment Variables & CLI Overrides

Covers 22 standardized test cases:
- Function A: Valid Options Binding & Defaults (Happy Path) [EVAL_CFG_001 - EVAL_CFG_003] (3 TCs)
- Function B: Startup Validation Failure Scenarios [EVAL_CFG_004 - EVAL_CFG_013] (10 TCs)
- Function C: Options Invalidation & Environment Overrides [EVAL_CFG_014 - EVAL_CFG_015] (2 TCs)
- Function D: Boundary Values & Configuration Fallbacks [EVAL_CFG_016 - EVAL_CFG_022] (7 TCs)
"""

import json
import os
import subprocess
import sys

# Path to the compiled C# engine-runner dll
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SERVICE_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
RUNNER_DLL = os.path.join(SERVICE_DIR, "engine-runner", "bin", "Release", "net9.0", "engine-runner.dll")
RUNNER_CSPROJ = os.path.join(SERVICE_DIR, "engine-runner", "engine-runner.csproj")


def get_csharp_config_results():
    """Runs the compiled engine-runner or builds it on demand to get configuration validation results."""
    if not os.path.exists(RUNNER_DLL):
        subprocess.run(["dotnet", "build", RUNNER_CSPROJ, "-c", "Release"], check=True, capture_output=True)
    proc = subprocess.run(["dotnet", RUNNER_DLL, "test-config"], capture_output=True, text=True, check=True)
    return json.loads(proc.stdout.strip())


# Execute C# host/options evaluation tests
cfg_results = get_csharp_config_results()

# --- Test Framework & Results Tracking ---
results = []


def record(tc_id, name, status, details):
    results.append({
        "id": tc_id,
        "name": name,
        "status": status,
        "details": details
    })
    color = "\033[92m" if status == "PASS" else ("\033[93m" if status == "DEVIATION" else "\033[91m")
    reset = "\033[0m"
    print(f"[{color}{status:<9}{reset}] {tc_id:<16}: {name} -> {details[:85]}")


print("=" * 105)
print(f"   UAV PMS - EVALUATION THRESHOLD OPTIONS & CONFIGURATION TEST SUITE (22 TEST CASES)")
print("=" * 105)


# ==============================================================================
# FUNCTION A: Valid Options Binding & Defaults (Happy Path) (3 TCs)
# ==============================================================================
print("\n>>> FUNCTION A: Valid Options Binding & Defaults (Happy Path)")

# EVAL_CFG_001: Default option values when section is omitted
try:
    d = cfg_results["EVAL_CFG_001"]
    valid = (d["success"] is True and
             d["EmergencyWeight"] == 45 and
             d["MaxConfidenceWeight"] == 55 and
             d["EmergencyConfidenceThreshold"] == 0.80 and
             d["CriticalScoreThreshold"] == 90 and
             d["HighScoreThreshold"] == 75 and
             d["MediumScoreThreshold"] == 50)
    record("EVAL_CFG_001", "Default options when section is omitted", "PASS" if valid else "FAIL",
           f"Defaults: Emergency=45, Max=55, Thresh=0.80, Crit=90, High=75, Med=50")
except Exception as e:
    record("EVAL_CFG_001", "Default options when section is omitted", "ERROR", str(e))

# EVAL_CFG_002: Binding of custom valid configuration
try:
    d = cfg_results["EVAL_CFG_002"]
    valid = (d["success"] is True and
             d["EmergencyWeight"] == 50 and
             d["MaxConfidenceWeight"] == 50 and
             d["EmergencyConfidenceThreshold"] == 0.75 and
             d["CriticalScoreThreshold"] == 85 and
             d["HighScoreThreshold"] == 70 and
             d["MediumScoreThreshold"] == 40)
    record("EVAL_CFG_002", "Binding of custom valid configuration", "PASS" if valid else "FAIL",
           f"Resolved: Emergency=50, Max=50, Thresh=0.75, Crit=85, High=70, Med=40")
except Exception as e:
    record("EVAL_CFG_002", "Binding of custom valid configuration", "ERROR", str(e))

# EVAL_CFG_003: Section name constant equals 'EvaluationThresholds'
try:
    d = cfg_results["EVAL_CFG_003"]
    valid = d["is_valid"] is True and d["section_name"] == "EvaluationThresholds"
    record("EVAL_CFG_003", "Section name constant EvaluationThresholds", "PASS" if valid else "FAIL",
           f"Constant SectionName='{d['section_name']}'")
except Exception as e:
    record("EVAL_CFG_003", "Section name constant EvaluationThresholds", "ERROR", str(e))


# ==============================================================================
# FUNCTION B: Startup Validation Failure Scenarios (10 TCs)
# ==============================================================================
print("\n>>> FUNCTION B: Startup Validation Failure Scenarios (Invalid Configurations)")

# EVAL_CFG_004: EmergencyWeight negative (-10)
try:
    d = cfg_results["EVAL_CFG_004"]
    valid = d["failed"] is True and d["exception_type"] == "OptionsValidationException" and d["contains_expected"] is True
    record("EVAL_CFG_004", "EmergencyWeight negative (-10) fails startup", "PASS" if valid else "FAIL",
           f"Validation error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_004", "EmergencyWeight negative (-10) fails startup", "ERROR", str(e))

# EVAL_CFG_005: EmergencyWeight exceeds 100 (150)
try:
    d = cfg_results["EVAL_CFG_005"]
    valid = d["failed"] is True and d["exception_type"] == "OptionsValidationException" and d["contains_expected"] is True
    record("EVAL_CFG_005", "EmergencyWeight exceeds 100 (150) fails startup", "PASS" if valid else "FAIL",
           f"Validation error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_005", "EmergencyWeight exceeds 100 (150) fails startup", "ERROR", str(e))

# EVAL_CFG_006: MaxConfidenceWeight negative (-5)
try:
    d = cfg_results["EVAL_CFG_006"]
    valid = d["failed"] is True and d["exception_type"] == "OptionsValidationException" and d["contains_expected"] is True
    record("EVAL_CFG_006", "MaxConfidenceWeight negative (-5) fails startup", "PASS" if valid else "FAIL",
           f"Validation error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_006", "MaxConfidenceWeight negative (-5) fails startup", "ERROR", str(e))

# EVAL_CFG_007: MaxConfidenceWeight exceeds 100 (200)
try:
    d = cfg_results["EVAL_CFG_007"]
    valid = d["failed"] is True and d["exception_type"] == "OptionsValidationException" and d["contains_expected"] is True
    record("EVAL_CFG_007", "MaxConfidenceWeight exceeds 100 (200) fails startup", "PASS" if valid else "FAIL",
           f"Validation error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_007", "MaxConfidenceWeight exceeds 100 (200) fails startup", "ERROR", str(e))

# EVAL_CFG_008: EmergencyConfidenceThreshold negative (-0.1)
try:
    d = cfg_results["EVAL_CFG_008"]
    valid = d["failed"] is True and d["exception_type"] == "OptionsValidationException" and d["contains_expected"] is True
    record("EVAL_CFG_008", "EmergencyConfidenceThreshold negative (-0.1)", "PASS" if valid else "FAIL",
           f"Validation error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_008", "EmergencyConfidenceThreshold negative (-0.1)", "ERROR", str(e))

# EVAL_CFG_009: EmergencyConfidenceThreshold exceeds 1.0 (1.5)
try:
    d = cfg_results["EVAL_CFG_009"]
    valid = d["failed"] is True and d["exception_type"] == "OptionsValidationException" and d["contains_expected"] is True
    record("EVAL_CFG_009", "EmergencyConfidenceThreshold exceeds 1.0 (1.5)", "PASS" if valid else "FAIL",
           f"Validation error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_009", "EmergencyConfidenceThreshold exceeds 1.0 (1.5)", "ERROR", str(e))

# EVAL_CFG_010: CriticalScoreThreshold invalid (105)
try:
    d = cfg_results["EVAL_CFG_010"]
    valid = d["failed"] is True and d["exception_type"] == "OptionsValidationException" and d["contains_expected"] is True
    record("EVAL_CFG_010", "CriticalScoreThreshold invalid (105) fails startup", "PASS" if valid else "FAIL",
           f"Validation error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_010", "CriticalScoreThreshold invalid (105) fails startup", "ERROR", str(e))

# EVAL_CFG_011: HighScoreThreshold invalid (-1)
try:
    d = cfg_results["EVAL_CFG_011"]
    valid = d["failed"] is True and d["exception_type"] == "OptionsValidationException" and d["contains_expected"] is True
    record("EVAL_CFG_011", "HighScoreThreshold invalid (-1) fails startup", "PASS" if valid else "FAIL",
           f"Validation error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_011", "HighScoreThreshold invalid (-1) fails startup", "ERROR", str(e))

# EVAL_CFG_012: MediumScoreThreshold invalid (101)
try:
    d = cfg_results["EVAL_CFG_012"]
    valid = d["failed"] is True and d["exception_type"] == "OptionsValidationException" and d["contains_expected"] is True
    record("EVAL_CFG_012", "MediumScoreThreshold invalid (101) fails startup", "PASS" if valid else "FAIL",
           f"Validation error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_012", "MediumScoreThreshold invalid (101) fails startup", "ERROR", str(e))

# EVAL_CFG_013: Non-numeric string provided for integer weight ('invalid_string')
try:
    d = cfg_results["EVAL_CFG_013"]
    valid = d["failed"] is True and (d["is_invalid_operation"] is True or "Failed to convert" in d["error_message"])
    record("EVAL_CFG_013", "Non-numeric string for integer weight fails binding", "PASS" if valid else "FAIL",
           f"Binding error: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_013", "Non-numeric string for integer weight fails binding", "ERROR", str(e))


# ==============================================================================
# FUNCTION C: Options Invalidation & Environment Overrides (2 TCs)
# ==============================================================================
print("\n>>> FUNCTION C: Options Invalidation & Environment Overrides")

# EVAL_CFG_014: Environment variable override (EvaluationThresholds__EmergencyWeight=60)
try:
    d = cfg_results["EVAL_CFG_014"]
    valid = d["is_overridden"] is True and d["resolved_weight"] == 60
    record("EVAL_CFG_014", "Environment variable override (__EmergencyWeight=60)", "PASS" if valid else "FAIL",
           f"Overridden value: EmergencyWeight={d['resolved_weight']}")
except Exception as e:
    record("EVAL_CFG_014", "Environment variable override (__EmergencyWeight=60)", "ERROR", str(e))

# EVAL_CFG_015: Command Line argument override (--EvaluationThresholds:CriticalScoreThreshold=88)
try:
    d = cfg_results["EVAL_CFG_015"]
    valid = d["is_overridden"] is True and d["resolved_critical"] == 88
    record("EVAL_CFG_015", "Command Line argument override (CriticalScoreThreshold=88)", "PASS" if valid else "FAIL",
           f"Overridden value: CriticalScoreThreshold={d['resolved_critical']}")
except Exception as e:
    record("EVAL_CFG_015", "Command Line argument override (CriticalScoreThreshold=88)", "ERROR", str(e))


# ==============================================================================
# FUNCTION D: Boundary Values & Configuration Fallbacks (7 TCs)
# ==============================================================================
print("\n>>> FUNCTION D: Boundary Values & Configuration Fallbacks")

# EVAL_CFG_016: EmergencyWeight exactly 0 (Min boundary)
try:
    d = cfg_results["EVAL_CFG_016"]
    valid = d["success"] is True and d["EmergencyWeight"] == 0
    record("EVAL_CFG_016", "EmergencyWeight exactly 0 (Min boundary)", "PASS" if valid else "FAIL",
           f"Startup succeeds: EmergencyWeight={d['EmergencyWeight']}")
except Exception as e:
    record("EVAL_CFG_016", "EmergencyWeight exactly 0 (Min boundary)", "ERROR", str(e))

# EVAL_CFG_017: EmergencyWeight exactly 100 (Max boundary)
try:
    d = cfg_results["EVAL_CFG_017"]
    valid = d["success"] is True and d["EmergencyWeight"] == 100
    record("EVAL_CFG_017", "EmergencyWeight exactly 100 (Max boundary)", "PASS" if valid else "FAIL",
           f"Startup succeeds: EmergencyWeight={d['EmergencyWeight']}")
except Exception as e:
    record("EVAL_CFG_017", "EmergencyWeight exactly 100 (Max boundary)", "ERROR", str(e))

# EVAL_CFG_018: EmergencyConfidenceThreshold exactly 0.0 (Min boundary)
try:
    d = cfg_results["EVAL_CFG_018"]
    valid = d["success"] is True and d["EmergencyConfidenceThreshold"] == 0.0
    record("EVAL_CFG_018", "EmergencyConfidenceThreshold exactly 0.0 (Min boundary)", "PASS" if valid else "FAIL",
           f"Startup succeeds: Threshold={d['EmergencyConfidenceThreshold']}")
except Exception as e:
    record("EVAL_CFG_018", "EmergencyConfidenceThreshold exactly 0.0 (Min boundary)", "ERROR", str(e))

# EVAL_CFG_019: EmergencyConfidenceThreshold exactly 1.0 (Max boundary)
try:
    d = cfg_results["EVAL_CFG_019"]
    valid = d["success"] is True and d["EmergencyConfidenceThreshold"] == 1.0
    record("EVAL_CFG_019", "EmergencyConfidenceThreshold exactly 1.0 (Max boundary)", "PASS" if valid else "FAIL",
           f"Startup succeeds: Threshold={d['EmergencyConfidenceThreshold']}")
except Exception as e:
    record("EVAL_CFG_019", "EmergencyConfidenceThreshold exactly 1.0 (Max boundary)", "ERROR", str(e))

# EVAL_CFG_020: Partial section in configuration (EmergencyWeight = 30, others default)
try:
    d = cfg_results["EVAL_CFG_020"]
    valid = d["success"] is True and d["retains_defaults"] is True
    record("EVAL_CFG_020", "Partial section retains unspecified defaults", "PASS" if valid else "FAIL",
           f"EmergencyWeight={d['EmergencyWeight']}, MaxConfidenceWeight={d['MaxConfidenceWeight']} (default)")
except Exception as e:
    record("EVAL_CFG_020", "Partial section retains unspecified defaults", "ERROR", str(e))

# EVAL_CFG_021: Malformed JSON syntax error in configuration
try:
    d = cfg_results["EVAL_CFG_021"]
    valid = d["failed"] is True and d["is_json_exception"] is True
    record("EVAL_CFG_021", "Malformed JSON syntax error in configuration", "PASS" if valid else "FAIL",
           f"JSON Exception caught: {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_021", "Malformed JSON syntax error in configuration", "ERROR", str(e))

# EVAL_CFG_022: OptionsValidationException message contains exact field name
try:
    d = cfg_results["EVAL_CFG_022"]
    valid = d["failed"] is True and d["contains_field_name"] is True
    record("EVAL_CFG_022", "Validation message lists exact field name", "PASS" if valid else "FAIL",
           f"Message contains 'CriticalScoreThreshold': {d['error_message']}")
except Exception as e:
    record("EVAL_CFG_022", "Validation message lists exact field name", "ERROR", str(e))


# ==============================================================================
# SUMMARY REPORT
# ==============================================================================
total = len(results)
passed = sum(1 for r in results if r["status"] == "PASS")
deviations = sum(1 for r in results if r["status"] == "DEVIATION")
failed = sum(1 for r in results if r["status"] == "FAIL")
errors = sum(1 for r in results if r["status"] == "ERROR")
compliance_rate = (passed / total * 100.0) if total > 0 else 0.0

print("\n" + "=" * 105)
print("   EVALUATION THRESHOLD OPTIONS & CONFIGURATION TEST SUITE EXECUTION SUMMARY")
print("=" * 105)
print(f"Total Test Cases: {total}")
print(f"Passed          : {passed}")
print(f"Deviations      : {deviations}")
print(f"Failed          : {failed}")
print(f"Errors          : {errors}")
print(f"Compliance Rate : {compliance_rate:.1f}%")
print("=" * 105 + "\n")

if failed > 0 or errors > 0:
    sys.exit(1)
sys.exit(0)
