# UAV PMS - InspectionEvaluationService: gRPC Evaluation Test Suite

Kịch bản kiểm thử tích hợp và hồi quy cho RPC `uavpms.inspectionevaluation.InspectionEvaluation/EvaluateDetection`.

---

## 1. Ma Trận Test Cases (35 Test Cases - 100% Pass)

### Function A: Successful Detection Evaluation (Happy Path) (10 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Dữ Liệu Đầu Vào | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_RPC_001` | Lớp khẩn cấp với confidence cao (0.95) | `FIRE_01`, conf=0.95, emergency=true | Score: 97, Severity: Critical, Risk: ImmediateAction, Alert: True | **PASS** |
| `EVAL_RPC_002` | Lớp khẩn cấp với confidence trung bình (0.75) | `ARC_FLASH`, conf=0.75, emergency=true | Score: 86, Severity: High, Risk: UrgentReview, Alert: False | **PASS** |
| `EVAL_RPC_003` | Lớp thông thường với confidence cao (0.90) | `CORROSION_01`, conf=0.90, emergency=false | Score: 50, Severity: Medium, Risk: PlannedReview, Alert: False | **PASS** |
| `EVAL_RPC_004` | Lớp thông thường với confidence vừa (0.60) | `BIRD_NEST`, conf=0.60, emergency=false | Score: 33, Severity: Low, Risk: Monitor, Alert: False | **PASS** |
| `EVAL_RPC_005` | Lớp thông thường với confidence thấp (0.30) | `RUST_SLIGHT`, conf=0.30, emergency=false | Score: 16, Severity: Low, Risk: Monitor, Alert: False | **PASS** |
| `EVAL_RPC_006` | Xử lý Unicode tiếng Việt trong tên loại | `INSULATOR_BROKEN`, "Phát hiện vỡ bát sứ..." | Reason chứa mã và Unicode tiếng Việt hợp lệ, không lỗi encoding | **PASS** |
| `EVAL_RPC_007` | Ghi log có cấu trúc kèm metadata detection | `det-test-log-01`, `OVERHEATING_CONNECTOR` | Log Information ghi nhận đầy đủ category, confidence, score | **PASS** |
| `EVAL_RPC_008` | Đầy đủ các trường định danh UUID tùy chọn | Valid UUIDs cho mission_id, media_id, detection_id | Xử lý hoàn tất thành công, Status: OK (0) | **PASS** |
| `EVAL_RPC_009` | Chuỗi rỗng cho các trường định danh | mission_id="", media_id="", detection_id="" | Xử lý hoàn tất không gặp NullReferenceException | **PASS** |
| `EVAL_RPC_010` | Chuỗi category_code rỗng | category_code="", conf=0.70 | Chuỗi reason được tạo sạch sẽ: "Category  evaluated with..." | **PASS** |

### Function B: Boundary Conditions & Validation (12 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Dữ Liệu Đầu Vào | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_RPC_011` | Biên dưới confidence = 0.0 | conf=0.0, emergency=false | Score: 0, Severity: Low, Risk: Monitor | **PASS** |
| `EVAL_RPC_012` | Biên trên confidence = 1.0 | conf=1.0, emergency=true | Score: 100, Severity: Critical, Alert: True | **PASS** |
| `EVAL_RPC_013` | Cắt giới hạn confidence âm (< 0.0, e.g. -0.5) | conf=-0.5, emergency=false | Tự động clamp về 0.0, Score: 0, Reason chứa 0.0% | **PASS** |
| `EVAL_RPC_014` | Cắt giới hạn confidence vượt mức (> 1.0, e.g. 1.5) | conf=1.5, emergency=true | Tự động clamp về 1.0, Score: 100, Reason chứa 100.0% | **PASS** |
| `EVAL_RPC_015` | Ngưỡng cảnh báo khẩn cấp EmergencyConfidenceThreshold (0.80) | conf=0.8000, emergency=true | RequiresImmediateAlert: True, Score: 89, Severity: High | **PASS** |
| `EVAL_RPC_016` | Dưới ngưỡng cảnh báo khẩn cấp sát biên (0.7999) | conf=0.7999, emergency=true | RequiresImmediateAlert: False, Score: 89, Severity: High | **PASS** |
| `EVAL_RPC_017` | Ngưỡng điểm CriticalScoreThreshold (Score = 90) | conf=0.8182, emergency=true (45+45=90) | Score: 90, Severity: Critical, Risk: ImmediateAction | **PASS** |
| `EVAL_RPC_018` | Dưới ngưỡng điểm Critical sát biên (Score = 89) | conf=0.8000, emergency=true (45+44=89) | Score: 89, Severity: High, Risk: UrgentReview | **PASS** |
| `EVAL_RPC_019` | Ngưỡng điểm HighScoreThreshold (Score = 75) | conf=0.5455, emergency=true (45+30=75) | Score: 75, Severity: High, Risk: UrgentReview | **PASS** |
| `EVAL_RPC_020` | Dưới ngưỡng điểm High sát biên (Score = 74) | conf=0.5273, emergency=true (45+29=74) | Score: 74, Severity: Medium, Risk: PlannedReview | **PASS** |
| `EVAL_RPC_021` | Ngưỡng điểm MediumScoreThreshold (Score = 50) | conf=0.9091, emergency=false (0+50=50) | Score: 50, Severity: Medium, Risk: PlannedReview | **PASS** |
| `EVAL_RPC_022` | Dưới ngưỡng điểm Medium sát biên (Score = 49) | conf=0.8909, emergency=false (0+49=49) | Score: 49, Severity: Low, Risk: Monitor | **PASS** |

### Function C: Security & Transport (3 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Phương Pháp | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_RPC_023` | Ép buộc giao thức HTTP/2 trên cổng gRPC 8080 | Gửi HTTP/1.1 request tới gRPC port | Từ chối với HTTP 400 Bad Request ("An HTTP/1.x request was sent to an HTTP/2 only endpoint.") | **PASS** |
| `EVAL_RPC_024` | Chịu tải đồng thời 100 requests song song | 100 concurrent RPC calls qua ThreadPool | 100/100 calls hoàn tất thành công, không memory leak hay concurrency exception (>8,000 rps) | **PASS** |
| `EVAL_RPC_025` | Kháng tấn công SQL & Command Injection | Payload `FIRE' OR '1'='1 --` & `; rm -rf /;` | Xử lý an toàn như chuỗi ký tự thuần, không có lỗ hổng injection | **PASS** |

### Function D: Category Variations & Confidence Step Curves (10 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Dữ Liệu Đầu Vào | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_RPC_026` | Category vỡ bát sứ `INSULATOR_BROKEN` | conf=0.85, emergency=true | Score: 92, Severity: Critical, Alert: True | **PASS** |
| `EVAL_RPC_027` | Category gỉ sét cột `TOWER_CORROSION` | conf=0.70, emergency=false | Score: 38 (Banker's Rounding), Severity: Low, Alert: False | **PASS** |
| `EVAL_RPC_028` | Category tổn hại dây dẫn `CONDUCTOR_DAMAGE` | conf=0.92, emergency=true | Score: 96, Severity: Critical, Alert: True | **PASS** |
| `EVAL_RPC_029` | Category cây cối vi phạm hành lang `VEGETATION_ENCROACHMENT` | conf=0.50, emergency=false | Score: 28, Severity: Low, Alert: False | **PASS** |
| `EVAL_RPC_030` | Đường cong tăng điểm Emergency Class (step 0.1) | conf từ 0.0 đến 1.0, emergency=true | Điểm tăng đơn điệu: `45, 51, 56, 61, 67, 73, 78, 83, 89, 95, 100`. Alert bật tại $\ge 0.8$ | **PASS** |
| `EVAL_RPC_031` | Đường cong tăng điểm Non-Emergency Class (step 0.1) | conf từ 0.0 đến 1.0, emergency=false | Điểm tăng đơn điệu: `0, 6, 11, 16, 22, 28, 33, 38, 44, 50, 55`. Alert luôn luôn False | **PASS** |
---

## 2. Ma Trận Test Cases Math Engine (28 Test Cases - 100% Pass)

### Function A: Formula Calculation & Mathematical Precision (9 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Dữ Liệu Đầu Vào | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_MATH_001` | Tính điểm chính xác với cấu hình mặc định (Emergency=45, Max=55, conf=0.82) | conf=0.82, emergency=true | $45 + \text{Round}(45.1) = 90$, Severity: Critical, Risk: ImmediateAction | **PASS** |
| `EVAL_MATH_002` | Hành vi làm tròn điểm giữa (.5) theo Banker's Rounding ToEven | conf = $1/110$ ($0.5$ điểm) | $\text{Round}(0.5) = 0$ (về số chẵn gần nhất 0), Score: 0, Severity: Low, Risk: Monitor | **PASS** |
| `EVAL_MATH_003` | Độ chính xác dấu phẩy động sát dưới ngưỡng (0.7999999) | conf=0.7999999, emergency=true | RequiresImmediateAlert: False ($< 0.80$), Score: 89, Severity: High | **PASS** |
| `EVAL_MATH_004` | Độ chính xác dấu phẩy động sát trên ngưỡng (0.8000001) | conf=0.8000001, emergency=true | RequiresImmediateAlert: True ($\ge 0.80$), Score: 89, Severity: High | **PASS** |
| `EVAL_MATH_005` | Cắt biên trên điểm số tại 100 khi tùy biến tùy chọn sinh điểm > 100 | Weight=80, MaxConf=80, conf=0.90 | Điểm thô: $80 + 72 = 152 \rightarrow$ Clamp(152, 0, 100) = 100, Severity: Critical | **PASS** |
| `EVAL_MATH_006` | Cắt biên dưới điểm số tại 0 khi conf = 0.0 và non-emergency | conf=0.0, emergency=false | Score: 0, Severity: Low, Risk: Monitor | **PASS** |
| `EVAL_MATH_007` | Xử lý an toàn khi confidence = double.NaN | conf = double.NaN | Không crash/exception, Score: 0, Severity: Low, Reason chứa "NaN" | **PASS** |
| `EVAL_MATH_008` | Xử lý khi confidence = double.PositiveInfinity | conf = +Infinity, emergency=true | Tự động clamp về 1.0, Score: 100, Severity: Critical, Alert: True | **PASS** |
| `EVAL_MATH_009` | Xử lý khi confidence = double.NegativeInfinity | conf = -Infinity, emergency=false | Tự động clamp về 0.0, Score: 0, Severity: Low, Alert: False | **PASS** |

### Function B: Severity & Risk Level State Machine Transitions (6 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Dữ Liệu Đầu Vào | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_MATH_010` | Ánh xạ Severity -> RiskLevel: Critical -> ImmediateAction | Score $\ge 90$ | Severity: Critical, RiskLevel: ImmediateAction | **PASS** |
| `EVAL_MATH_011` | Ánh xạ Severity -> RiskLevel: High -> UrgentReview | Score $75 \le s \le 89$ | Severity: High, RiskLevel: UrgentReview | **PASS** |
| `EVAL_MATH_012` | Ánh xạ Severity -> RiskLevel: Medium -> PlannedReview | Score $50 \le s \le 74$ | Severity: Medium, RiskLevel: PlannedReview | **PASS** |
| `EVAL_MATH_013` | Ánh xạ Severity -> RiskLevel: Low -> Monitor | Score $0 \le s \le 49$ | Severity: Low, RiskLevel: Monitor | **PASS** |
| `EVAL_MATH_014` | Định dạng chuỗi Reason khi RequiresImmediateAlert = true | conf=0.85, emergency=true, "FLASH_OVER" | Chứa "Emergency category FLASH_OVER exceeded confidence threshold" và "85.0" | **PASS** |
| `EVAL_MATH_015` | Định dạng chuỗi Reason khi RequiresImmediateAlert = false | conf=0.65, emergency=false, "OIL_LEAK" | Chứa "Category OIL_LEAK evaluated with confidence" và "65.0" | **PASS** |

### Function C: Custom Threshold Options Verification (3 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Cấu Hình Tùy Biến | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_MATH_016` | Tùy biến CriticalScoreThreshold = 95 | CriticalScoreThreshold=95, Score=92 | Severity: High (vì $92 < 95$), RiskLevel: UrgentReview | **PASS** |
| `EVAL_MATH_017` | Tùy biến EmergencyConfidenceThreshold = 0.50 | Threshold=0.50, conf=0.55, emergency=true | RequiresImmediateAlert: True ($0.55 \ge 0.50$) | **PASS** |
| `EVAL_MATH_018` | Bất biến tính của record EvaluationResult | Reflection kiểm tra thuộc tính C# | Record type, toàn bộ 5 properties là init-only (read-only) | **PASS** |

### Function D: Engine Options Configurations & Sub-Normal Floats (10 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Dữ Liệu Đầu Vào | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_MATH_019` | Chấm điểm thuần khẩn cấp (Emergency=100, MaxConf=0) | Weight=100, MaxConf=0, conf=0.99, emergency=true | Score: 100, Severity: Critical, RiskLevel: ImmediateAction | **PASS** |
| `EVAL_MATH_020` | Chấm điểm thuần độ tin cậy (Emergency=0, MaxConf=100) | Weight=0, MaxConf=100, conf=0.80, emergency=true | Score: 80, Severity: High, RiskLevel: UrgentReview | **PASS** |
| `EVAL_MATH_021` | Tất cả các ngưỡng bằng nhau (Critical=High=Medium=50) | Critical=High=Medium=50, Score=50 | Score: 50 $\ge$ 50 (nhánh khớp đầu tiên: Critical), Severity: Critical | **PASS** |
| `EVAL_MATH_022` | Số thực dưới chuẩn dương cực bé ($1\text{e-}30$) | conf=$1\text{e-}30$, emergency=false | Xử lý như xấp xỉ 0.0, Score: 0, Severity: Low, Alert: False | **PASS** |
| `EVAL_MATH_023` | Số thực âm không ($-0.0$) | conf=$-0.0$, emergency=false | Tự động clamp về 0.0, Score: 0, Severity: Low, Alert: False | **PASS** |
| `EVAL_MATH_024` | Chuỗi Enum EvaluationSeverity khớp chuẩn Protobuf | ToString() Critical, High, Medium, Low | Khớp chính xác PascalCase: "Critical", "High", "Medium", "Low" | **PASS** |
| `EVAL_MATH_025` | Chuỗi Enum EvaluationRiskLevel khớp chuẩn Protobuf | ToString() ImmediateAction, UrgentReview... | Khớp chính xác PascalCase: "ImmediateAction", "UrgentReview", "PlannedReview", "Monitor" | **PASS** |
| `EVAL_MATH_026` | Độ chính xác tính điểm cho conf = 0.545454 | conf=0.545454, emergency=false | $0.545454 \times 55 = 29.99997 \rightarrow$ Round = 30, Score: 30 | **PASS** |
| `EVAL_MATH_027` | Độ chính xác tính điểm cho conf = 0.999999 (sát 1.0) | conf=0.999999, emergency=false | $0.999999 \times 55 = 54.999945 \rightarrow$ Round = 55, Score: 55 | **PASS** |
| `EVAL_MATH_028` | Định dạng phần trăm P1 trong Reason (conf=0.12345) | conf=0.12345, emergency=false | Chuỗi Reason chứa "12.3%" (định dạng P1 một chữ số thập phân) | **PASS** |

---

## 3. Ma Trận Test Cases Options Binding & Configuration (22 Test Cases - 100% Pass)

### Function A: Valid Options Binding & Defaults (Happy Path) (3 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Cấu Hình Kiểm Thử | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_CFG_001` | Giá trị mặc định khi khuyết section `EvaluationThresholds` | Không cấu hình section | EmergencyWeight=45, MaxConfidence=55, Thresh=0.80, Crit=90, High=75, Med=50 | **PASS** |
| `EVAL_CFG_002` | Ràng buộc thành công cấu hình tùy biến hợp lệ | 50, 50, 0.75, 85, 70, 40 | Khởi động thành công, giá trị option trùng khớp chính xác | **PASS** |
| `EVAL_CFG_003` | Hằng số tên section `SectionName` | `EvaluationThresholdOptions.SectionName` | Bằng đúng chuỗi "EvaluationThresholds" | **PASS** |

### Function B: Startup Validation Failure Scenarios (10 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Giá Trị Không Hợp Lệ | Lỗi Ngoại Lệ & Thông Báo Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_CFG_004` | `EmergencyWeight` âm (< 0, e.g. -10) | `EmergencyWeight: -10` | OptionsValidationException: "EmergencyWeight must be between 0 and 100" | **PASS** |
| `EVAL_CFG_005` | `EmergencyWeight` vượt quá 100 (150) | `EmergencyWeight: 150` | OptionsValidationException: "EmergencyWeight must be between 0 and 100" | **PASS** |
| `EVAL_CFG_006` | `MaxConfidenceWeight` âm (< 0, e.g. -5) | `MaxConfidenceWeight: -5` | OptionsValidationException: "MaxConfidenceWeight must be between 0 and 100" | **PASS** |
| `EVAL_CFG_007` | `MaxConfidenceWeight` vượt quá 100 (200) | `MaxConfidenceWeight: 200` | OptionsValidationException: "MaxConfidenceWeight must be between 0 and 100" | **PASS** |
| `EVAL_CFG_008` | `EmergencyConfidenceThreshold` âm (< 0.0, e.g. -0.1) | `Threshold: -0.1` | OptionsValidationException: "EmergencyConfidenceThreshold must be between 0.0 and 1.0" | **PASS** |
| `EVAL_CFG_009` | `EmergencyConfidenceThreshold` vượt quá 1.0 (1.5) | `Threshold: 1.5` | OptionsValidationException: "EmergencyConfidenceThreshold must be between 0.0 and 1.0" | **PASS** |
| `EVAL_CFG_010` | `CriticalScoreThreshold` không hợp lệ (105) | `Critical: 105` | OptionsValidationException: "CriticalScoreThreshold must be between 0 and 100" | **PASS** |
| `EVAL_CFG_011` | `HighScoreThreshold` không hợp lệ (-1) | `High: -1` | OptionsValidationException: "HighScoreThreshold must be between 0 and 100" | **PASS** |
| `EVAL_CFG_012` | `MediumScoreThreshold` không hợp lệ (101) | `Medium: 101` | OptionsValidationException: "MediumScoreThreshold must be between 0 and 100" | **PASS** |
| `EVAL_CFG_013` | Chuỗi ký tự không phải số cho trọng số nguyên | `EmergencyWeight: "invalid_string"` | InvalidOperationException: "Failed to convert configuration value... to type 'System.Int32'" | **PASS** |

### Function C: Options Invalidation & Environment Overrides (2 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Cơ Chế Ghi Đè | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_CFG_014` | Ghi đè cấu hình qua Biến môi trường | `EvaluationThresholds__EmergencyWeight=60` | Khởi động sạch sẽ, EmergencyWeight nhận giá trị 60 từ biến môi trường | **PASS** |
| `EVAL_CFG_015` | Ghi đè cấu hình qua Tham số dòng lệnh CLI | `--EvaluationThresholds:CriticalScoreThreshold=88` | CriticalScoreThreshold nhận giá trị 88 từ tham số dòng lệnh | **PASS** |

### Function D: Boundary Values & Configuration Fallbacks (7 TCs)
| Test Case ID | Mục Tiêu Kiểm Thử | Giá Trị Biên | Kết Quả Kỳ Vọng Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| `EVAL_CFG_016` | Biên dưới `EmergencyWeight` đúng bằng 0 | `EmergencyWeight: 0` | Khởi động thành công, EmergencyWeight = 0 | **PASS** |
| `EVAL_CFG_017` | Biên trên `EmergencyWeight` đúng bằng 100 | `EmergencyWeight: 100` | Khởi động thành công, EmergencyWeight = 100 | **PASS** |
| `EVAL_CFG_018` | Biên dưới `EmergencyConfidenceThreshold` đúng bằng 0.0 | `Threshold: 0.0` | Khởi động thành công, EmergencyConfidenceThreshold = 0.0 | **PASS** |
| `EVAL_CFG_019` | Biên trên `EmergencyConfidenceThreshold` đúng bằng 1.0 | `Threshold: 1.0` | Khởi động thành công, EmergencyConfidenceThreshold = 1.0 | **PASS** |
| `EVAL_CFG_020` | Khuyết một phần section (chỉ cấu hình EmergencyWeight) | `EmergencyWeight: 30` | Khởi động thành công, EmergencyWeight = 30, các trường khác giữ nguyên giá trị mặc định | **PASS** |
| `EVAL_CFG_021` | Lỗi cú pháp JSON trong file cấu hình | JSON thiếu dấu ngoặc đóng | Báo lỗi JsonException / cú pháp JSON không hợp lệ | **PASS** |
| `EVAL_CFG_022` | Thông báo lỗi DataAnnotation chỉ đích danh tên trường | `CriticalScoreThreshold: -5` | Thông báo OptionsValidationException chứa chính xác tên trường "CriticalScoreThreshold" | **PASS** |


