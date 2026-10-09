#!/usr/bin/env python3
"""
UAV PMS - InspectionEvaluationService: DetectionEvaluationEngine & Mathematical Precision Test Suite
Engine Component Tested:
- UavPms.InspectionEvaluationService.Application.Common.Utilities.DetectionEvaluationEngine
- UavPms.InspectionEvaluationService.Application.Common.Options.EvaluationThresholdOptions
- UavPms.InspectionEvaluationService.Domain.Enums (EvaluationSeverity, EvaluationRiskLevel)

Covers 28 standardized test cases:
- Function A: Formula Calculation & Mathematical Precision [EVAL_MATH_001 - EVAL_MATH_009] (9 TCs)
- Function B: Severity & Risk Level State Machine Transitions [EVAL_MATH_010 - EVAL_MATH_015] (6 TCs)
- Function C: Custom Threshold Options Verification [EVAL_MATH_016 - EVAL_MATH_018] (3 TCs)
- Function D: Engine Options Configurations & Sub-Normal Floats [EVAL_MATH_019 - EVAL_MATH_028] (10 TCs)
"""

import json
import math
import os
import subprocess
import sys

# Ensure generated proto package is in sys.path
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROTOS_DIR = os.path.join(SCRIPT_DIR, "protos")
if PROTOS_DIR not in sys.path:
    sys.path.insert(0, PROTOS_DIR)

import grpc
import inspection_evaluation_pb2 as pb2
import inspection_evaluation_pb2_grpc as pb2_grpc


def resolve_grpc_target():
    env_target = os.environ.get("INSPECTION_EVALUATION_GRPC_URL") or os.environ.get("GRPC_TARGET")
    if env_target:
        return env_target.replace("http://", "").replace("https://", "")

    try:
        chan = grpc.insecure_channel("localhost:8080")
        grpc.channel_ready_future(chan).result(timeout=0.5)
        chan.close()
        return "localhost:8080"
    except Exception:
        pass

    try:
        cmd = ["docker", "inspect", "-f", "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}", "uav-inspection-evaluation-service"]
        ip = subprocess.run(cmd, capture_output=True, text=True).stdout.strip()
        if ip:
            return f"{ip}:8080"
    except Exception:
        pass

    return "localhost:8080"


GRPC_TARGET = resolve_grpc_target()

# Path to the compiled C# engine-runner dll
SERVICE_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
RUNNER_DLL = os.path.join(SERVICE_DIR, "engine-runner", "bin", "Release", "net9.0", "engine-runner.dll")
RUNNER_CSPROJ = os.path.join(SERVICE_DIR, "engine-runner", "engine-runner.csproj")


def get_csharp_engine_results():
    """Runs the compiled engine-runner or builds it on demand to get C#-specific unit tests results."""
    if not os.path.exists(RUNNER_DLL):
        subprocess.run(["dotnet", "build", RUNNER_CSPROJ, "-c", "Release"], check=True, capture_output=True)
    proc = subprocess.run(["dotnet", RUNNER_DLL, "test-all"], capture_output=True, text=True, check=True)
    return json.loads(proc.stdout.strip())


# Pre-run C# unit-level evaluations for custom options and reflection
csharp_results = get_csharp_engine_results()

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
print(f"   UAV PMS - ENGINE MATHEMATICAL PRECISION TEST SUITE (28 TEST CASES)")
print(f"   Live gRPC Endpoint: {GRPC_TARGET}")
print("=" * 105)

channel = grpc.insecure_channel(GRPC_TARGET)
stub = pb2_grpc.InspectionEvaluationStub(channel)


# ==============================================================================
# FUNCTION A: Formula Calculation & Mathematical Precision (9 TCs)
# ==============================================================================
print("\n>>> FUNCTION A: Formula Calculation & Mathematical Precision")

# EVAL_MATH_001: Exact score calculation with default options (Emergency=45, Max=55, conf=0.82)
try:
    req = pb2.EvaluateDetectionRequest(category_code="FIRE_01", confidence=0.82, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    # 45 + round(0.82 * 55) = 45 + round(45.1) = 45 + 45 = 90
    valid = (res.priority_score == 90 and
             res.severity == "Critical" and
             res.risk_level == "ImmediateAction" and
             res.requires_immediate_alert is True)
    record("EVAL_MATH_001", "Exact score calculation with default options (conf=0.82)", "PASS" if valid else "FAIL",
           f"Score={res.priority_score} (45+45=90), Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_MATH_001", "Exact score calculation with default options (conf=0.82)", "ERROR", str(e))

# EVAL_MATH_002: Midpoint rounding behavior (0.5 decimal rounding to nearest even integer)
try:
    # 0.00909090909090909 * 55 = 0.5000000000000000. Under MidpointRounding.ToEven, 0.5 rounds to 0.
    conf_midpoint = 1.0 / 110.0
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=conf_midpoint, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 0 and res.severity == "Low" and res.risk_level == "Monitor"
    record("EVAL_MATH_002", "Midpoint rounding behavior (Banker's ToEven 0.5 -> 0)", "PASS" if valid else "FAIL",
           f"Score={res.priority_score} (0.5 rounded to 0 via MidpointRounding.ToEven)")
except Exception as e:
    record("EVAL_MATH_002", "Midpoint rounding behavior (Banker's ToEven 0.5 -> 0)", "ERROR", str(e))

# EVAL_MATH_003: Near-boundary floating point precision below threshold (0.7999999)
try:
    req = pb2.EvaluateDetectionRequest(category_code="EMERG", confidence=0.7999999, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    # 0.7999999 < 0.80 -> requires_immediate_alert is False
    valid = res.requires_immediate_alert is False and res.priority_score == 89 and res.severity == "High"
    record("EVAL_MATH_003", "Near-boundary floating point precision below threshold (0.7999999)", "PASS" if valid else "FAIL",
           f"Alert={res.requires_immediate_alert} (strictly < 0.80), Score={res.priority_score}")
except Exception as e:
    record("EVAL_MATH_003", "Near-boundary floating point precision below threshold (0.7999999)", "ERROR", str(e))

# EVAL_MATH_004: Near-boundary floating point precision above threshold (0.8000001)
try:
    req = pb2.EvaluateDetectionRequest(category_code="EMERG", confidence=0.8000001, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    # 0.8000001 >= 0.80 -> requires_immediate_alert is True
    valid = res.requires_immediate_alert is True and res.priority_score == 89 and res.severity == "High"
    record("EVAL_MATH_004", "Near-boundary floating point precision above threshold (0.8000001)", "PASS" if valid else "FAIL",
           f"Alert={res.requires_immediate_alert} (strictly >= 0.80), Score={res.priority_score}")
except Exception as e:
    record("EVAL_MATH_004", "Near-boundary floating point precision above threshold (0.8000001)", "ERROR", str(e))

# EVAL_MATH_005: Priority score clamping at upper bound 100 with custom options (80 + 72 = 152 -> 100)
try:
    c_res = csharp_results["EVAL_MATH_005"]
    valid = c_res["priority_score"] == 100 and c_res["severity"] == "Critical" and c_res["risk_level"] == "ImmediateAction"
    record("EVAL_MATH_005", "Priority score clamping at upper bound 100 (raw 152 -> 100)", "PASS" if valid else "FAIL",
           f"Raw 152 clamped to: Score={c_res['priority_score']}, Severity={c_res['severity']}")
except Exception as e:
    record("EVAL_MATH_005", "Priority score clamping at upper bound 100 (raw 152 -> 100)", "ERROR", str(e))

# EVAL_MATH_006: Priority score clamping at lower bound 0 (confidence=0.0, non-emergency)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=0.0, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 0 and res.severity == "Low" and res.risk_level == "Monitor"
    record("EVAL_MATH_006", "Priority score clamping at lower bound 0 (conf=0.0)", "PASS" if valid else "FAIL",
           f"Score={res.priority_score}, Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_MATH_006", "Priority score clamping at lower bound 0 (conf=0.0)", "ERROR", str(e))

# EVAL_MATH_007: Safe evaluation behavior when confidence = double.NaN
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=float("nan"), is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 0 and res.severity == "Low" and "NaN" in res.reason and res.requires_immediate_alert is False
    record("EVAL_MATH_007", "Safe evaluation behavior when confidence = double.NaN", "PASS" if valid else "FAIL",
           f"Handled safely without crash: Score={res.priority_score}, Reason='{res.reason}'")
except Exception as e:
    record("EVAL_MATH_007", "Safe evaluation behavior when confidence = double.NaN", "ERROR", str(e))

# EVAL_MATH_008: Evaluation behavior when confidence = double.PositiveInfinity
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=float("inf"), is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 100 and res.severity == "Critical" and res.requires_immediate_alert is True
    record("EVAL_MATH_008", "Evaluation behavior when confidence = double.PositiveInfinity", "PASS" if valid else "FAIL",
           f"Clamped to 1.0: Score={res.priority_score}, Severity={res.severity}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_MATH_008", "Evaluation behavior when confidence = double.PositiveInfinity", "ERROR", str(e))

# EVAL_MATH_009: Evaluation behavior when confidence = double.NegativeInfinity
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=float("-inf"), is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 0 and res.severity == "Low" and res.requires_immediate_alert is False
    record("EVAL_MATH_009", "Evaluation behavior when confidence = double.NegativeInfinity", "PASS" if valid else "FAIL",
           f"Clamped to 0.0: Score={res.priority_score}, Severity={res.severity}")
except Exception as e:
    record("EVAL_MATH_009", "Evaluation behavior when confidence = double.NegativeInfinity", "ERROR", str(e))


# ==============================================================================
# FUNCTION B: Severity & Risk Level State Machine Transitions (6 TCs)
# ==============================================================================
print("\n>>> FUNCTION B: Severity & Risk Level State Machine Transitions")

# EVAL_MATH_010: Severity -> RiskLevel mapping: Critical -> ImmediateAction (Score >= 90)
try:
    req = pb2.EvaluateDetectionRequest(category_code="CRIT", confidence=0.95, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score >= 90 and res.severity == "Critical" and res.risk_level == "ImmediateAction"
    record("EVAL_MATH_010", "Severity -> RiskLevel mapping: Critical -> ImmediateAction", "PASS" if valid else "FAIL",
           f"Score={res.priority_score} >= 90, Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_MATH_010", "Severity -> RiskLevel mapping: Critical -> ImmediateAction", "ERROR", str(e))

# EVAL_MATH_011: Severity -> RiskLevel mapping: High -> UrgentReview (Score = 75 to 89)
try:
    req = pb2.EvaluateDetectionRequest(category_code="HIGH", confidence=0.75, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = 75 <= res.priority_score <= 89 and res.severity == "High" and res.risk_level == "UrgentReview"
    record("EVAL_MATH_011", "Severity -> RiskLevel mapping: High -> UrgentReview", "PASS" if valid else "FAIL",
           f"Score={res.priority_score} in [75, 89], Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_MATH_011", "Severity -> RiskLevel mapping: High -> UrgentReview", "ERROR", str(e))

# EVAL_MATH_012: Severity -> RiskLevel mapping: Medium -> PlannedReview (Score = 50 to 74)
try:
    req = pb2.EvaluateDetectionRequest(category_code="MED", confidence=0.90, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = 50 <= res.priority_score <= 74 and res.severity == "Medium" and res.risk_level == "PlannedReview"
    record("EVAL_MATH_012", "Severity -> RiskLevel mapping: Medium -> PlannedReview", "PASS" if valid else "FAIL",
           f"Score={res.priority_score} in [50, 74], Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_MATH_012", "Severity -> RiskLevel mapping: Medium -> PlannedReview", "ERROR", str(e))

# EVAL_MATH_013: Severity -> RiskLevel mapping: Low -> Monitor (Score = 0 to 49)
try:
    req = pb2.EvaluateDetectionRequest(category_code="LOW", confidence=0.30, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = 0 <= res.priority_score <= 49 and res.severity == "Low" and res.risk_level == "Monitor"
    record("EVAL_MATH_013", "Severity -> RiskLevel mapping: Low -> Monitor", "PASS" if valid else "FAIL",
           f"Score={res.priority_score} in [0, 49], Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_MATH_013", "Severity -> RiskLevel mapping: Low -> Monitor", "ERROR", str(e))

# EVAL_MATH_014: Reason string format when RequiresImmediateAlert = true
try:
    req = pb2.EvaluateDetectionRequest(category_code="FLASH_OVER", confidence=0.85, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = ("Emergency category FLASH_OVER exceeded confidence threshold" in res.reason and
             "85.0" in res.reason and
             res.requires_immediate_alert is True)
    record("EVAL_MATH_014", "Reason string format when RequiresImmediateAlert = true", "PASS" if valid else "FAIL",
           f"Reason='{res.reason}'")
except Exception as e:
    record("EVAL_MATH_014", "Reason string format when RequiresImmediateAlert = true", "ERROR", str(e))

# EVAL_MATH_015: Reason string format when RequiresImmediateAlert = false
try:
    req = pb2.EvaluateDetectionRequest(category_code="OIL_LEAK", confidence=0.65, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = ("Category OIL_LEAK evaluated with confidence" in res.reason and
             "65.0" in res.reason and
             res.requires_immediate_alert is False)
    record("EVAL_MATH_015", "Reason string format when RequiresImmediateAlert = false", "PASS" if valid else "FAIL",
           f"Reason='{res.reason}'")
except Exception as e:
    record("EVAL_MATH_015", "Reason string format when RequiresImmediateAlert = false", "ERROR", str(e))


# ==============================================================================
# FUNCTION C: Custom Threshold Options Verification (3 TCs)
# ==============================================================================
print("\n>>> FUNCTION C: Custom Threshold Options Verification")

# EVAL_MATH_016: Custom options with CriticalScoreThreshold = 95 (Score 92 yields High)
try:
    c_res = csharp_results["EVAL_MATH_016"]
    valid = c_res["priority_score"] == 92 and c_res["severity"] == "High" and c_res["risk_level"] == "UrgentReview"
    record("EVAL_MATH_016", "Custom options CriticalScoreThreshold = 95 (Score 92 -> High)", "PASS" if valid else "FAIL",
           f"Score={c_res['priority_score']} < 95 -> Severity={c_res['severity']}, Risk={c_res['risk_level']}")
except Exception as e:
    record("EVAL_MATH_016", "Custom options CriticalScoreThreshold = 95 (Score 92 -> High)", "ERROR", str(e))

# EVAL_MATH_017: Custom options EmergencyConfidenceThreshold = 0.50 (conf 0.55 triggers alert)
try:
    c_res = csharp_results["EVAL_MATH_017"]
    valid = c_res["requires_immediate_alert"] is True and c_res["confidence"] == 0.55 and c_res["threshold"] == 0.50
    record("EVAL_MATH_017", "Custom EmergencyConfidenceThreshold = 0.50 (conf 0.55 triggers alert)", "PASS" if valid else "FAIL",
           f"Alert={c_res['requires_immediate_alert']} (0.55 >= 0.50)")
except Exception as e:
    record("EVAL_MATH_017", "Custom EmergencyConfidenceThreshold = 0.50 (conf 0.55 triggers alert)", "ERROR", str(e))

# EVAL_MATH_018: EvaluationResult record immutability verification
try:
    c_res = csharp_results["EVAL_MATH_018"]
    valid = c_res["is_record"] is True and c_res["properties_init_only"] is True and c_res["property_count"] == 5
    record("EVAL_MATH_018", "EvaluationResult record immutability (init-only properties)", "PASS" if valid else "FAIL",
           f"IsRecord={c_res['is_record']}, AllInitOnly={c_res['properties_init_only']}, Props={c_res['property_count']}")
except Exception as e:
    record("EVAL_MATH_018", "EvaluationResult record immutability (init-only properties)", "ERROR", str(e))


# ==============================================================================
# FUNCTION D: Engine Options Configurations & Sub-Normal Floats (10 TCs)
# ==============================================================================
print("\n>>> FUNCTION D: Engine Options Configurations & Sub-Normal Floats")

# EVAL_MATH_019: Emergency-only scoring (EmergencyWeight = 100, MaxConfidenceWeight = 0)
try:
    c_res = csharp_results["EVAL_MATH_019"]
    valid = c_res["priority_score"] == 100 and c_res["severity"] == "Critical" and c_res["risk_level"] == "ImmediateAction"
    record("EVAL_MATH_019", "Emergency-only scoring (Weight 100, MaxConf 0)", "PASS" if valid else "FAIL",
           f"Score={c_res['priority_score']}, Severity={c_res['severity']}")
except Exception as e:
    record("EVAL_MATH_019", "Emergency-only scoring (Weight 100, MaxConf 0)", "ERROR", str(e))

# EVAL_MATH_020: Confidence-only scoring (EmergencyWeight = 0, MaxConfidenceWeight = 100)
try:
    c_res = csharp_results["EVAL_MATH_020"]
    valid = c_res["priority_score"] == 80 and c_res["severity"] == "High" and c_res["risk_level"] == "UrgentReview"
    record("EVAL_MATH_020", "Confidence-only scoring (Weight 0, MaxConf 100)", "PASS" if valid else "FAIL",
           f"Score={c_res['priority_score']}, Severity={c_res['severity']}")
except Exception as e:
    record("EVAL_MATH_020", "Confidence-only scoring (Weight 0, MaxConf 100)", "ERROR", str(e))

# EVAL_MATH_021: All thresholds equal (Critical = High = Medium = 50 -> Critical wins)
try:
    c_res = csharp_results["EVAL_MATH_021"]
    valid = c_res["priority_score"] == 50 and c_res["severity"] == "Critical"
    record("EVAL_MATH_021", "All thresholds equal (Critical=High=Medium=50 -> Critical)", "PASS" if valid else "FAIL",
           f"Score={c_res['priority_score']}, Severity={c_res['severity']} (first pattern arm matched)")
except Exception as e:
    record("EVAL_MATH_021", "All thresholds equal (Critical=High=Medium=50 -> Critical)", "ERROR", str(e))

# EVAL_MATH_022: Subnormal positive float confidence (1e-30 -> ~0.0)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=1e-30, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 0 and res.severity == "Low" and res.requires_immediate_alert is False
    record("EVAL_MATH_022", "Subnormal positive float confidence (1e-30 -> ~0.0)", "PASS" if valid else "FAIL",
           f"Score={res.priority_score}, Severity={res.severity}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_MATH_022", "Subnormal positive float confidence (1e-30 -> ~0.0)", "ERROR", str(e))

# EVAL_MATH_023: Negative zero confidence (-0.0 clamped to 0.0)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=-0.0, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 0 and res.severity == "Low" and res.requires_immediate_alert is False
    record("EVAL_MATH_023", "Negative zero confidence (-0.0 clamped to 0.0)", "PASS" if valid else "FAIL",
           f"Score={res.priority_score}, Severity={res.severity}, Reason='{res.reason}'")
except Exception as e:
    record("EVAL_MATH_023", "Negative zero confidence (-0.0 clamped to 0.0)", "ERROR", str(e))

# EVAL_MATH_024: EvaluationSeverity Enum PascalCase strings match Protobuf
try:
    c_res = csharp_results["EVAL_MATH_024"]
    expected_enums = {"Critical": "Critical", "High": "High", "Medium": "Medium", "Low": "Low"}
    valid = c_res == expected_enums
    record("EVAL_MATH_024", "EvaluationSeverity Enum PascalCase strings match Protobuf", "PASS" if valid else "FAIL",
           f"Enum values: {list(c_res.values())}")
except Exception as e:
    record("EVAL_MATH_024", "EvaluationSeverity Enum PascalCase strings match Protobuf", "ERROR", str(e))

# EVAL_MATH_025: EvaluationRiskLevel Enum PascalCase strings match Protobuf
try:
    c_res = csharp_results["EVAL_MATH_025"]
    expected_enums = {
        "ImmediateAction": "ImmediateAction",
        "UrgentReview": "UrgentReview",
        "PlannedReview": "PlannedReview",
        "Monitor": "Monitor"
    }
    valid = c_res == expected_enums
    record("EVAL_MATH_025", "EvaluationRiskLevel Enum PascalCase strings match Protobuf", "PASS" if valid else "FAIL",
           f"Enum values: {list(c_res.values())}")
except Exception as e:
    record("EVAL_MATH_025", "EvaluationRiskLevel Enum PascalCase strings match Protobuf", "ERROR", str(e))

# EVAL_MATH_026: Score calculation precision for confidence = 0.545454 (Score = 30)
try:
    # 0.545454 * 55 = 29.99997 -> Math.Round = 30
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=0.545454, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 30 and res.severity == "Low" and res.risk_level == "Monitor"
    record("EVAL_MATH_026", "Score calculation precision for confidence = 0.545454", "PASS" if valid else "FAIL",
           f"0.545454 * 55 = 29.99997 -> Math.Round = {res.priority_score}")
except Exception as e:
    record("EVAL_MATH_026", "Score calculation precision for confidence = 0.545454", "ERROR", str(e))

# EVAL_MATH_027: Score calculation precision for confidence = 0.999999 (Score = 55)
try:
    # 0.999999 * 55 = 54.999945 -> Math.Round = 55
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=0.999999, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 55 and res.severity == "Medium" and res.risk_level == "PlannedReview"
    record("EVAL_MATH_027", "Score calculation precision for confidence = 0.999999", "PASS" if valid else "FAIL",
           f"0.999999 * 55 = 54.999945 -> Math.Round = {res.priority_score}")
except Exception as e:
    record("EVAL_MATH_027", "Score calculation precision for confidence = 0.999999", "ERROR", str(e))

# EVAL_MATH_028: Reason string percentage formatting for confidence = 0.12345 (12.3%)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=0.12345, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = "12.3" in res.reason
    record("EVAL_MATH_028", "Reason string percentage formatting (conf=0.12345 -> 12.3%)", "PASS" if valid else "FAIL",
           f"Reason='{res.reason}'")
except Exception as e:
    record("EVAL_MATH_028", "Reason string percentage formatting (conf=0.12345 -> 12.3%)", "ERROR", str(e))


channel.close()

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
print("   ENGINE MATHEMATICAL PRECISION TEST SUITE EXECUTION SUMMARY")
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
