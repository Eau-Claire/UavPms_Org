# UAV PMS - InspectionEvaluationService Integration & Regression Test Suites

Tài liệu và bộ mã nguồn kiểm thử tích hợp (integration & regression test suite) cho dịch vụ tính toán độ ưu tiên và đánh giá phát hiện khiếm khuyết qua gRPC trong hệ thống UAV PMS (`uavpms.inspectionevaluation.InspectionEvaluation/EvaluateDetection`).

---

## 1. Danh Sách Các Test Suites (Tổng Cộng 121 Test Cases)

| Suite Thư Mục | Module Kiểm Thử | Số Lượng TC | Trạng Thái Kiểm Thử Trực Tiếp |
| :--- | :--- | :---: | :---: |
| [`evaluation-suites/test_evaluation_rpc_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/InspectionEvaluationService/evaluation-suites/test_evaluation_rpc_suite.py) | Đánh giá gRPC `EvaluateDetection`, kiểm tra trọng số khẩn cấp, ngưỡng điểm, clamp confidence, HTTP/2 enforcement, concurrency load, SQL/Command injection | 35 TCs | **100% PASS (35/35)** |
| [`evaluation-suites/test_evaluation_math_engine_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/InspectionEvaluationService/evaluation-suites/test_evaluation_math_engine_suite.py) | Độ chính xác toán học `DetectionEvaluationEngine`, Banker's Rounding, cận biên số thực, xử lý NaN/Infinity/Subnormal, cấu hình tùy biến ngưỡng, record immutability, enum mapping | 28 TCs | **100% PASS (28/28)** |
| [`evaluation-suites/test_evaluation_config_options_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/InspectionEvaluationService/evaluation-suites/test_evaluation_config_options_suite.py) | Ràng buộc cấu hình `EvaluationThresholdOptions`, xác thực DataAnnotations (`ValidateDataAnnotations()`, `ValidateOnStart()`), ghi đè biến môi trường và tham số dòng lệnh | 22 TCs | **100% PASS (22/22)** |
| [`evaluation-suites/test_evaluation_http_health_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/InspectionEvaluationService/evaluation-suites/test_evaluation_http_health_suite.py) | Điểm cuối HTTP Health Check (`GET /health`) & Metadata JSON (`GET /`), chuyển hướng cổng Kestrel (PORT / Health:Port), chống CR-LF injection, tải 500 requests đồng thời | 18 TCs | **100% PASS (18/18)** |
| [`evaluation-suites/test_evaluation_integration_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/InspectionEvaluationService/evaluation-suites/test_evaluation_integration_suite.py) | gRPC Deadlines & Cancellation, Protobuf field numbering/deserialization, Singleton channel multiplexing, 500 concurrent RPC calls, độ trễ < 5ms, channel shutdown & retry policy | 18 TCs | **100% PASS (18/18)** |
| **Tổng cộng** | **Toàn bộ Phân Hệ InspectionEvaluationService** | **121 TCs** | **100% PASS (121/121)** |

---

## 2. Cấu Trúc Thư Mục

```
tests/InspectionEvaluationService/
├── README.md                                  # Tài liệu tổng quan phân hệ kiểm thử
├── requirements.txt                           # Thư viện phụ thuộc (grpcio, protobuf, requests)
├── run_all_evaluation_suites.py               # Master Runner điều phối toàn bộ test suites
└── evaluation-suites/
    ├── README.md                              # Chi tiết ma trận 35 test cases & logic thuật toán
    ├── protos/                                # Generated Python gRPC Stubs từ Protos/inspection_evaluation.proto
    │   ├── __init__.py
    │   ├── inspection_evaluation_pb2.py
    │   └── inspection_evaluation_pb2_grpc.py
    └── test_evaluation_rpc_suite.py           # Kịch bản kiểm thử 35 test cases chi tiết
```

---

## 3. Kiến Trúc & Công Thức Đánh Giá (DetectionEvaluationEngine)

Dịch vụ chạy giao thức **HTTP/2 gRPC** trên port 8080 (hoặc qua IP nội bộ Docker `uav-inspection-evaluation-service`).

### Công thức tính điểm ưu tiên (Priority Score):
$$\text{clampedConfidence} = \text{Clamp}(\text{confidence}, 0.0, 1.0)$$
$$\text{emergencyWeight} = \begin{cases} 45 & \text{nếu } is\_emergency\_class = \text{true} \\ 0 & \text{nếu } is\_emergency\_class = \text{false} \end{cases}$$
$$\text{confidenceWeight} = \text{Round}(\text{clampedConfidence} \times 55)$$
$$\text{priorityScore} = \text{Clamp}(\text{emergencyWeight} + \text{confidenceWeight}, 0, 100)$$

> [!NOTE]
> Thuật toán làm tròn trong .NET C# sử dụng **Banker's Rounding (`MidpointRounding.ToEven`)**:
> - $0.30 \times 55 = 16.5 \rightarrow \mathbf{16}$ (làm tròn về số chẵn gần nhất).
> - $0.70 \times 55 = 38.5 \rightarrow \mathbf{38}$ (làm tròn về số chẵn gần nhất).

### Ngưỡng phân loại mức độ nghiêm trọng (Severity) & rủi ro (Risk Level):
- **Critical** ($\ge 90$ điểm): Risk Level = `ImmediateAction`
- **High** ($\ge 75$ điểm): Risk Level = `UrgentReview`
- **Medium** ($\ge 50$ điểm): Risk Level = `PlannedReview`
- **Low** ($< 50$ điểm): Risk Level = `Monitor`

### Cảnh báo khẩn cấp tức thời (RequiresImmediateAlert):
$$\text{requiresImmediateAlert} = (is\_emergency\_class == \text{true}) \land (\text{clampedConfidence} \ge 0.80)$$

---

## 4. Yêu Cầu Môi Trường & Cài Đặt

### Cài đặt thư viện Python:
```bash
python3 -m pip install -r tests/InspectionEvaluationService/requirements.txt
```

### Môi trường thực thi:
- Container Docker `uav-inspection-evaluation-service` đang hoạt động (gRPC port 8080).
- Test runner tự động phát hiện IP của container hoặc kết nối qua `localhost:8080` / biến môi trường `INSPECTION_EVALUATION_GRPC_URL`.

---

## 5. Hướng Dẫn Chạy Kiểm Thử

### Chạy toàn bộ suite qua Master Runner:
```bash
python3 tests/InspectionEvaluationService/run_all_evaluation_suites.py
```

### Chạy trực tiếp test suite:
```bash
python3 tests/InspectionEvaluationService/evaluation-suites/test_evaluation_rpc_suite.py
```

### Kết quả mẫu:
```text
=========================================================================================================
   INSPECTION EVALUATION gRPC TEST SUITE EXECUTION SUMMARY
=========================================================================================================
Total Test Cases: 35
Passed          : 35
Deviations      : 0
Failed          : 0
Errors          : 0
Compliance Rate : 100.0%
=========================================================================================================
```
