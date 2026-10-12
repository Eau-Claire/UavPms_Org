#!/usr/bin/env python3
"""
UAV PMS - InspectionEvaluationService: gRPC Detection Evaluation Test Suite
RPC Service Tested:
- uavpms.inspectionevaluation.InspectionEvaluation/EvaluateDetection

Covers 35 standardized test cases:
- Function A: Successful Detection Evaluation (Happy Path) [EVAL_RPC_001 - EVAL_RPC_010] (10 TCs)
- Function B: Boundary Conditions & Validation [EVAL_RPC_011 - EVAL_RPC_022] (12 TCs)
- Function C: Security & Transport [EVAL_RPC_023 - EVAL_RPC_025] (3 TCs)
- Function D: Category Code Variations & Confidence Step Curves [EVAL_RPC_026 - EVAL_RPC_035] (10 TCs)
"""

import concurrent.futures
import os
import subprocess
import sys
import time
import uuid

# Ensure generated proto package is in sys.path
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROTOS_DIR = os.path.join(SCRIPT_DIR, "protos")
if PROTOS_DIR not in sys.path:
    sys.path.insert(0, PROTOS_DIR)

import grpc
import requests

import inspection_evaluation_pb2 as pb2
import inspection_evaluation_pb2_grpc as pb2_grpc


def resolve_grpc_target():
    """
    Resolves the gRPC target address:
    1. Environment variable INSPECTION_EVALUATION_GRPC_URL or GRPC_TARGET
    2. Try connecting to localhost:8080
    3. If unreachable, discover Docker container IP for uav-inspection-evaluation-service
    """
    env_target = os.environ.get("INSPECTION_EVALUATION_GRPC_URL") or os.environ.get("GRPC_TARGET")
    if env_target:
        return env_target.replace("http://", "").replace("https://", "")

    # Check if localhost:8080 is directly reachable
    try:
        chan = grpc.insecure_channel("localhost:8080")
        grpc.channel_ready_future(chan).result(timeout=0.5)
        chan.close()
        return "localhost:8080"
    except Exception:
        pass

    # Inspect Docker container IP
    try:
        cmd = ["docker", "inspect", "-f", "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}", "uav-inspection-evaluation-service"]
        ip = subprocess.run(cmd, capture_output=True, text=True).stdout.strip()
        if ip:
            return f"{ip}:8080"
    except Exception:
        pass

    return "localhost:8080"


GRPC_TARGET = resolve_grpc_target()

# --- Test Framework & Results Tracking ---
results = []


def record(tc_id, name, status, rpc_code, details):
    results.append({
        "id": tc_id,
        "name": name,
        "status": status,
        "code": rpc_code,
        "details": details
    })
    color = "\033[92m" if status == "PASS" else ("\033[93m" if status == "DEVIATION" else "\033[91m")
    reset = "\033[0m"
    code_str = str(rpc_code)
    print(f"[{color}{status:<9}{reset}] {tc_id:<16} (Status {code_str:<3}): {name} -> {details[:85]}")


print("=" * 105)
print(f"   UAV PMS - INSPECTION EVALUATION gRPC TEST SUITE (35 TEST CASES)")
print(f"   Target Service: {GRPC_TARGET}")
print("=" * 105)

channel = grpc.insecure_channel(GRPC_TARGET)
stub = pb2_grpc.InspectionEvaluationStub(channel)


# ==============================================================================
# FUNCTION A: Successful Detection Evaluation (Happy Path) (10 TCs)
# ==============================================================================
print("\n>>> FUNCTION A: Successful Detection Evaluation (Happy Path)")

# EVAL_RPC_001: Emergency Class with High Confidence (0.95)
try:
    req = pb2.EvaluateDetectionRequest(
        category_code="FIRE_01",
        category_name="Phát hiện đám cháy",
        confidence=0.95,
        is_emergency_class=True,
        mission_id=str(uuid.uuid4()),
        media_id=str(uuid.uuid4()),
        detection_id="det-001"
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = (
        res.severity == "Critical" and
        res.risk_level == "ImmediateAction" and
        res.priority_score == 97 and
        res.requires_immediate_alert is True and
        "Emergency category FIRE_01 exceeded confidence threshold" in res.reason
    )
    record("EVAL_RPC_001", "Emergency Class with High Confidence (0.95)", "PASS" if valid else "FAIL", 0,
           f"Severity={res.severity}, Risk={res.risk_level}, Score={res.priority_score}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_001", "Emergency Class with High Confidence (0.95)", "ERROR", -1, str(e))

# EVAL_RPC_002: Emergency Class with Moderate Confidence (0.75)
try:
    req = pb2.EvaluateDetectionRequest(
        category_code="ARC_FLASH",
        confidence=0.75,
        is_emergency_class=True
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = (
        res.severity == "High" and
        res.risk_level == "UrgentReview" and
        res.priority_score == 86 and
        res.requires_immediate_alert is False and
        "Category ARC_FLASH evaluated with confidence" in res.reason
    )
    record("EVAL_RPC_002", "Emergency Class with Moderate Confidence (0.75)", "PASS" if valid else "FAIL", 0,
           f"Severity={res.severity}, Risk={res.risk_level}, Score={res.priority_score}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_002", "Emergency Class with Moderate Confidence (0.75)", "ERROR", -1, str(e))

# EVAL_RPC_003: Non-Emergency Class with High Confidence (0.90)
try:
    req = pb2.EvaluateDetectionRequest(
        category_code="CORROSION_01",
        category_name="Gỉ sét thanh giằng",
        confidence=0.90,
        is_emergency_class=False
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = (
        res.severity == "Medium" and
        res.risk_level == "PlannedReview" and
        res.priority_score == 50 and
        res.requires_immediate_alert is False and
        "Category CORROSION_01 evaluated with confidence" in res.reason
    )
    record("EVAL_RPC_003", "Non-Emergency Class with High Confidence (0.90)", "PASS" if valid else "FAIL", 0,
           f"Severity={res.severity}, Risk={res.risk_level}, Score={res.priority_score}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_003", "Non-Emergency Class with High Confidence (0.90)", "ERROR", -1, str(e))

# EVAL_RPC_004: Non-Emergency Class with Medium Confidence (0.60)
try:
    req = pb2.EvaluateDetectionRequest(
        category_code="BIRD_NEST",
        confidence=0.60,
        is_emergency_class=False
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = (
        res.severity == "Low" and
        res.risk_level == "Monitor" and
        res.priority_score == 33 and
        res.requires_immediate_alert is False
    )
    record("EVAL_RPC_004", "Non-Emergency Class with Medium Confidence (0.60)", "PASS" if valid else "FAIL", 0,
           f"Severity={res.severity}, Risk={res.risk_level}, Score={res.priority_score}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_004", "Non-Emergency Class with Medium Confidence (0.60)", "ERROR", -1, str(e))

# EVAL_RPC_005: Non-Emergency Class with Low Confidence (0.30)
try:
    req = pb2.EvaluateDetectionRequest(
        category_code="RUST_SLIGHT",
        confidence=0.30,
        is_emergency_class=False
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = (
        res.severity == "Low" and
        res.risk_level == "Monitor" and
        res.priority_score == 16 and
        res.requires_immediate_alert is False
    )
    record("EVAL_RPC_005", "Non-Emergency Class with Low Confidence (0.30)", "PASS" if valid else "FAIL", 0,
           f"Severity={res.severity}, Risk={res.risk_level}, Score={res.priority_score}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_005", "Non-Emergency Class with Low Confidence (0.30)", "ERROR", -1, str(e))

# EVAL_RPC_006: Unicode Vietnamese Category Name & Code
try:
    req = pb2.EvaluateDetectionRequest(
        category_code="INSULATOR_BROKEN",
        category_name="Phát hiện vỡ bát sứ cách điện cột #127",
        confidence=0.85,
        is_emergency_class=True
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = (
        res.severity == "Critical" and
        "INSULATOR_BROKEN" in res.reason and
        res.requires_immediate_alert is True
    )
    record("EVAL_RPC_006", "Unicode Vietnamese Category Name & Code", "PASS" if valid else "FAIL", 0,
           f"Handled safely: Severity={res.severity}, Reason='{res.reason}'")
except Exception as e:
    record("EVAL_RPC_006", "Unicode Vietnamese Category Name & Code", "ERROR", -1, str(e))

# EVAL_RPC_007: Structured Logging with Detection Metadata
try:
    req = pb2.EvaluateDetectionRequest(
        detection_id="det-test-log-01",
        category_code="OVERHEATING_CONNECTOR",
        confidence=0.88,
        is_emergency_class=True
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 93 and res.requires_immediate_alert is True
    record("EVAL_RPC_007", "Structured Logging with Detection Metadata", "PASS" if valid else "FAIL", 0,
           f"DetectionId logged & evaluated: Score={res.priority_score}")
except Exception as e:
    record("EVAL_RPC_007", "Structured Logging with Detection Metadata", "ERROR", -1, str(e))

# EVAL_RPC_008: All Optional Identifier Fields Populated
try:
    req = pb2.EvaluateDetectionRequest(
        mission_id=str(uuid.uuid4()),
        media_id=str(uuid.uuid4()),
        detection_id=str(uuid.uuid4()),
        category_code="FIRE_02",
        confidence=0.92,
        is_emergency_class=True
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 96 and res.severity == "Critical"
    record("EVAL_RPC_008", "All Optional Identifier Fields Populated", "PASS" if valid else "FAIL", 0,
           f"Evaluation successful with all UUIDs: Score={res.priority_score}")
except Exception as e:
    record("EVAL_RPC_008", "All Optional Identifier Fields Populated", "ERROR", -1, str(e))

# EVAL_RPC_009: Empty String Identifiers Handling
try:
    req = pb2.EvaluateDetectionRequest(
        mission_id="",
        media_id="",
        detection_id="",
        category_code="FIRE_02",
        confidence=0.92,
        is_emergency_class=True
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 96 and res.severity == "Critical"
    record("EVAL_RPC_009", "Empty String Identifiers Handling", "PASS" if valid else "FAIL", 0,
           f"Evaluation completes without null reference: Score={res.priority_score}")
except Exception as e:
    record("EVAL_RPC_009", "Empty String Identifiers Handling", "ERROR", -1, str(e))

# EVAL_RPC_010: Empty Category Code String Handling
try:
    req = pb2.EvaluateDetectionRequest(
        category_code="",
        confidence=0.70,
        is_emergency_class=False
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = "Category  evaluated with confidence" in res.reason and res.priority_score == 38
    record("EVAL_RPC_010", "Empty Category Code String Handling", "PASS" if valid else "FAIL", 0,
           f"Reason generated cleanly: '{res.reason}'")
except Exception as e:
    record("EVAL_RPC_010", "Empty Category Code String Handling", "ERROR", -1, str(e))


# ==============================================================================
# FUNCTION B: Boundary Conditions & Validation (12 TCs)
# ==============================================================================
print("\n>>> FUNCTION B: Boundary Conditions & Validation")

# EVAL_RPC_011: Confidence Exactly 0.0 (Min Boundary)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=0.0, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 0 and res.severity == "Low" and res.risk_level == "Monitor"
    record("EVAL_RPC_011", "Confidence Exactly 0.0 (Min Boundary)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_RPC_011", "Confidence Exactly 0.0 (Min Boundary)", "ERROR", -1, str(e))

# EVAL_RPC_012: Confidence Exactly 1.0 (Max Boundary)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=1.0, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = (
        res.priority_score == 100 and
        res.severity == "Critical" and
        res.risk_level == "ImmediateAction" and
        res.requires_immediate_alert is True
    )
    record("EVAL_RPC_012", "Confidence Exactly 1.0 (Max Boundary)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_012", "Confidence Exactly 1.0 (Max Boundary)", "ERROR", -1, str(e))

# EVAL_RPC_013: Negative Confidence Clamping (< 0.0, e.g. -0.5)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=-0.5, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 0 and ("0.0" in res.reason or "0%" in res.reason)
    record("EVAL_RPC_013", "Negative Confidence Clamping (< 0.0)", "PASS" if valid else "FAIL", 0,
           f"Clamped to 0.0: Score={res.priority_score}, Reason='{res.reason}'")
except Exception as e:
    record("EVAL_RPC_013", "Negative Confidence Clamping (< 0.0)", "ERROR", -1, str(e))

# EVAL_RPC_014: Excessive Confidence Clamping (> 1.0, e.g. 1.5)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=1.5, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 100 and res.requires_immediate_alert is True
    record("EVAL_RPC_014", "Excessive Confidence Clamping (> 1.0)", "PASS" if valid else "FAIL", 0,
           f"Clamped to 1.0: Score={res.priority_score}, Reason='{res.reason}'")
except Exception as e:
    record("EVAL_RPC_014", "Excessive Confidence Clamping (> 1.0)", "ERROR", -1, str(e))

# EVAL_RPC_015: Emergency Confidence Threshold Boundary (Exactly 0.80)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=0.8000, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = (
        res.requires_immediate_alert is True and
        res.priority_score == 89 and
        res.severity == "High" and
        "exceeded confidence threshold" in res.reason
    )
    record("EVAL_RPC_015", "Emergency Confidence Threshold Boundary (0.80)", "PASS" if valid else "FAIL", 0,
           f"Alert={res.requires_immediate_alert}, Score={res.priority_score}, Reason='{res.reason}'")
except Exception as e:
    record("EVAL_RPC_015", "Emergency Confidence Threshold Boundary (0.80)", "ERROR", -1, str(e))

# EVAL_RPC_016: Just Below Emergency Confidence Threshold (0.7999)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=0.7999, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = (
        res.requires_immediate_alert is False and
        res.priority_score == 89 and
        res.severity == "High" and
        "evaluated with confidence" in res.reason
    )
    record("EVAL_RPC_016", "Just Below Emergency Confidence Threshold (0.7999)", "PASS" if valid else "FAIL", 0,
           f"Alert={res.requires_immediate_alert}, Score={res.priority_score}, Reason='{res.reason}'")
except Exception as e:
    record("EVAL_RPC_016", "Just Below Emergency Confidence Threshold (0.7999)", "ERROR", -1, str(e))

# EVAL_RPC_017: Critical Score Threshold Boundary (Score = 90)
try:
    req = pb2.EvaluateDetectionRequest(category_code="T", confidence=0.8182, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 90 and res.severity == "Critical" and res.risk_level == "ImmediateAction"
    record("EVAL_RPC_017", "Critical Score Threshold Boundary (Score = 90)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_RPC_017", "Critical Score Threshold Boundary (Score = 90)", "ERROR", -1, str(e))

# EVAL_RPC_018: Just Below Critical Score Threshold (Score = 89)
try:
    req = pb2.EvaluateDetectionRequest(category_code="T", confidence=0.8000, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 89 and res.severity == "High" and res.risk_level == "UrgentReview"
    record("EVAL_RPC_018", "Just Below Critical Score Threshold (Score = 89)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_RPC_018", "Just Below Critical Score Threshold (Score = 89)", "ERROR", -1, str(e))

# EVAL_RPC_019: High Score Threshold Boundary (Score = 75)
try:
    req = pb2.EvaluateDetectionRequest(category_code="T", confidence=0.5455, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 75 and res.severity == "High" and res.risk_level == "UrgentReview"
    record("EVAL_RPC_019", "High Score Threshold Boundary (Score = 75)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_RPC_019", "High Score Threshold Boundary (Score = 75)", "ERROR", -1, str(e))

# EVAL_RPC_020: Just Below High Score Threshold (Score = 74)
try:
    req = pb2.EvaluateDetectionRequest(category_code="T", confidence=0.5273, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 74 and res.severity == "Medium" and res.risk_level == "PlannedReview"
    record("EVAL_RPC_020", "Just Below High Score Threshold (Score = 74)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_RPC_020", "Just Below High Score Threshold (Score = 74)", "ERROR", -1, str(e))

# EVAL_RPC_021: Medium Score Threshold Boundary (Score = 50)
try:
    req = pb2.EvaluateDetectionRequest(category_code="T", confidence=0.9091, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 50 and res.severity == "Medium" and res.risk_level == "PlannedReview"
    record("EVAL_RPC_021", "Medium Score Threshold Boundary (Score = 50)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_RPC_021", "Medium Score Threshold Boundary (Score = 50)", "ERROR", -1, str(e))

# EVAL_RPC_022: Just Below Medium Score Threshold (Score = 49)
try:
    req = pb2.EvaluateDetectionRequest(category_code="T", confidence=0.8909, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 49 and res.severity == "Low" and res.risk_level == "Monitor"
    record("EVAL_RPC_022", "Just Below Medium Score Threshold (Score = 49)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Risk={res.risk_level}")
except Exception as e:
    record("EVAL_RPC_022", "Just Below Medium Score Threshold (Score = 49)", "ERROR", -1, str(e))


# ==============================================================================
# FUNCTION C: Security & Transport (3 TCs)
# ==============================================================================
print("\n>>> FUNCTION C: Security & Transport")

# EVAL_RPC_023: HTTP/2 Protocol Enforcement on gRPC Port
try:
    # Send HTTP/1.1 request via requests to gRPC endpoint
    target_http_url = f"http://{GRPC_TARGET}/"
    rejected = False
    status_code = 0
    err_msg = ""
    try:
        r = requests.get(target_http_url, timeout=3)
        status_code = r.status_code
        if r.status_code == 400 or "HTTP/2 only endpoint" in r.text or r.status_code == 426:
            rejected = True
            err_msg = f"HTTP {r.status_code}: {r.text.strip()}"
    except Exception as ex:
        # Client connection reset/closed due to HTTP/2 only is also valid rejection
        rejected = True
        err_msg = f"Connection rejected cleanly: {type(ex).__name__}"
    record("EVAL_RPC_023", "HTTP/2 Protocol Enforcement on gRPC Port", "PASS" if rejected else "FAIL", status_code, err_msg)
except Exception as e:
    record("EVAL_RPC_023", "HTTP/2 Protocol Enforcement on gRPC Port", "ERROR", -1, str(e))

# EVAL_RPC_024: Concurrency Load (100 parallel RPC calls)
try:
    def send_call(i):
        r_req = pb2.EvaluateDetectionRequest(
            detection_id=f"c-{i}",
            category_code="FIRE_01",
            confidence=0.85,
            is_emergency_class=True
        )
        return stub.EvaluateDetection(r_req, timeout=10)

    start_t = time.time()
    with concurrent.futures.ThreadPoolExecutor(max_workers=10) as executor:
        futures = [executor.submit(send_call, i) for i in range(100)]
        results_call = [f.result() for f in futures]
    elapsed = time.time() - start_t
    valid = len(results_call) == 100 and all(r.severity == "Critical" for r in results_call)
    record("EVAL_RPC_024", "Concurrency Load (100 parallel RPC calls)", "PASS" if valid else "FAIL", 0,
           f"100/100 calls succeeded in {elapsed:.2f}s ({100/elapsed:.1f} rps)")
except Exception as e:
    record("EVAL_RPC_024", "Concurrency Load (100 parallel RPC calls)", "ERROR", -1, str(e))

# EVAL_RPC_025: SQL & Command Injection Resistance in String Fields
try:
    req = pb2.EvaluateDetectionRequest(
        category_code="FIRE' OR '1'='1 --",
        detection_id="; rm -rf /;",
        confidence=0.95,
        is_emergency_class=True
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.severity == "Critical" and "FIRE' OR '1'='1 --" in res.reason
    record("EVAL_RPC_025", "SQL & Command Injection Resistance in Strings", "PASS" if valid else "FAIL", 0,
           f"Treated as literal string: Severity={res.severity}, Reason='{res.reason}'")
except Exception as e:
    record("EVAL_RPC_025", "SQL & Command Injection Resistance in Strings", "ERROR", -1, str(e))


# ==============================================================================
# FUNCTION D: Category Variations & Confidence Curves (10 TCs)
# ==============================================================================
print("\n>>> FUNCTION D: Category Code Variations & Confidence Step Curves")

# EVAL_RPC_026: INSULATOR_BROKEN Category (Emergency)
try:
    req = pb2.EvaluateDetectionRequest(category_code="INSULATOR_BROKEN", confidence=0.85, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 92 and res.severity == "Critical" and res.risk_level == "ImmediateAction" and res.requires_immediate_alert is True
    record("EVAL_RPC_026", "INSULATOR_BROKEN Category (Emergency)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_026", "INSULATOR_BROKEN Category (Emergency)", "ERROR", -1, str(e))

# EVAL_RPC_027: TOWER_CORROSION Category (Non-Emergency)
try:
    req = pb2.EvaluateDetectionRequest(category_code="TOWER_CORROSION", confidence=0.70, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 38 and res.severity == "Low" and res.risk_level == "Monitor" and res.requires_immediate_alert is False
    record("EVAL_RPC_027", "TOWER_CORROSION Category (Non-Emergency)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_027", "TOWER_CORROSION Category (Non-Emergency)", "ERROR", -1, str(e))

# EVAL_RPC_028: CONDUCTOR_DAMAGE Category (Emergency)
try:
    req = pb2.EvaluateDetectionRequest(category_code="CONDUCTOR_DAMAGE", confidence=0.92, is_emergency_class=True)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 96 and res.severity == "Critical" and res.risk_level == "ImmediateAction" and res.requires_immediate_alert is True
    record("EVAL_RPC_028", "CONDUCTOR_DAMAGE Category (Emergency)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_028", "CONDUCTOR_DAMAGE Category (Emergency)", "ERROR", -1, str(e))

# EVAL_RPC_029: VEGETATION_ENCROACHMENT Category (Non-Emergency)
try:
    req = pb2.EvaluateDetectionRequest(category_code="VEGETATION_ENCROACHMENT", confidence=0.50, is_emergency_class=False)
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 28 and res.severity == "Low" and res.risk_level == "Monitor" and res.requires_immediate_alert is False
    record("EVAL_RPC_029", "VEGETATION_ENCROACHMENT (Non-Emergency)", "PASS" if valid else "FAIL", 0,
           f"Score={res.priority_score}, Severity={res.severity}, Alert={res.requires_immediate_alert}")
except Exception as e:
    record("EVAL_RPC_029", "VEGETATION_ENCROACHMENT (Non-Emergency)", "ERROR", -1, str(e))

# EVAL_RPC_030: Escalation Curve for Emergency Class (Steps 0.0 - 1.0)
try:
    steps = [0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0]
    expected_scores = [45, 51, 56, 61, 67, 73, 78, 83, 89, 95, 100]
    actual_scores = []
    alert_flips_at_08 = True
    for s in steps:
        r = stub.EvaluateDetection(pb2.EvaluateDetectionRequest(category_code="E", confidence=s, is_emergency_class=True), timeout=5)
        actual_scores.append(r.priority_score)
        if s >= 0.8 and not r.requires_immediate_alert:
            alert_flips_at_08 = False
        if s < 0.8 and r.requires_immediate_alert:
            alert_flips_at_08 = False

    valid = actual_scores == expected_scores and alert_flips_at_08
    record("EVAL_RPC_030", "Escalation Curve for Emergency Class", "PASS" if valid else "FAIL", 0,
           f"Scores: {actual_scores}, Alert flips >= 0.8: {alert_flips_at_08}")
except Exception as e:
    record("EVAL_RPC_030", "Escalation Curve for Emergency Class", "ERROR", -1, str(e))

# EVAL_RPC_031: Escalation Curve for Non-Emergency Class (Steps 0.0 - 1.0)
try:
    steps = [0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0]
    expected_scores = [0, 6, 11, 16, 22, 28, 33, 38, 44, 50, 55]
    actual_scores = []
    no_alert_at_all = True
    for s in steps:
        r = stub.EvaluateDetection(pb2.EvaluateDetectionRequest(category_code="NE", confidence=s, is_emergency_class=False), timeout=5)
        actual_scores.append(r.priority_score)
        if r.requires_immediate_alert:
            no_alert_at_all = False

    valid = actual_scores == expected_scores and no_alert_at_all
    record("EVAL_RPC_031", "Escalation Curve for Non-Emergency Class", "PASS" if valid else "FAIL", 0,
           f"Scores: {actual_scores}, RequiresImmediateAlert always False: {no_alert_at_all}")
except Exception as e:
    record("EVAL_RPC_031", "Escalation Curve for Non-Emergency Class", "ERROR", -1, str(e))

# EVAL_RPC_032: Custom gRPC Metadata Header Handling (x-request-id)
try:
    metadata = (("x-request-id", "req-abc-123"),)
    req = pb2.EvaluateDetectionRequest(category_code="TEST", confidence=0.50, is_emergency_class=False)
    res = stub.EvaluateDetection(req, metadata=metadata, timeout=5)
    valid = res.priority_score == 28 and res.severity == "Low"
    record("EVAL_RPC_032", "Custom gRPC Metadata Header Handling", "PASS" if valid else "FAIL", 0,
           f"Metadata processed without error, Score={res.priority_score}")
except Exception as e:
    record("EVAL_RPC_032", "Custom gRPC Metadata Header Handling", "ERROR", -1, str(e))

# EVAL_RPC_033: Special Symbols in Category Name
try:
    req = pb2.EvaluateDetectionRequest(
        category_name="Vỡ sứ #127 (khẩn cấp!)",
        category_code="SPECIAL_01",
        confidence=0.88,
        is_emergency_class=True
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.severity == "Critical" and "SPECIAL_01" in res.reason
    record("EVAL_RPC_033", "Special Symbols in Category Name", "PASS" if valid else "FAIL", 0,
           f"Handled safely: Severity={res.severity}, Reason='{res.reason}'")
except Exception as e:
    record("EVAL_RPC_033", "Special Symbols in Category Name", "ERROR", -1, str(e))

# EVAL_RPC_034: 36-character UUID Detection Identifier
try:
    uuid_str = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    req = pb2.EvaluateDetectionRequest(
        detection_id=uuid_str,
        category_code="UUID_TEST",
        confidence=0.85,
        is_emergency_class=False
    )
    res = stub.EvaluateDetection(req, timeout=5)
    valid = res.priority_score == 47 and res.severity == "Low"
    record("EVAL_RPC_034", "36-character UUID Detection Identifier", "PASS" if valid else "FAIL", 0,
           f"UUID processed without error, Score={res.priority_score}")
except Exception as e:
    record("EVAL_RPC_034", "36-character UUID Detection Identifier", "ERROR", -1, str(e))

# EVAL_RPC_035: Deterministic Repeated Invocations
try:
    req = pb2.EvaluateDetectionRequest(
        detection_id="det-det",
        category_code="DETERMINISTIC",
        confidence=0.84,
        is_emergency_class=True
    )
    responses = [stub.EvaluateDetection(req, timeout=5) for _ in range(5)]
    all_identical = all(
        r.severity == responses[0].severity and
        r.priority_score == responses[0].priority_score and
        r.risk_level == responses[0].risk_level and
        r.requires_immediate_alert == responses[0].requires_immediate_alert and
        r.reason == responses[0].reason
        for r in responses
    )
    record("EVAL_RPC_035", "Deterministic Repeated Invocations", "PASS" if all_identical else "FAIL", 0,
           f"5/5 repeated invocations 100% identical")
except Exception as e:
    record("EVAL_RPC_035", "Deterministic Repeated Invocations", "ERROR", -1, str(e))


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
print("   INSPECTION EVALUATION gRPC TEST SUITE EXECUTION SUMMARY")
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
