**CAPSTONE PROJECT REPORT**

**Report 5 – Software Test Documentation**

**Integrated UAV–AI Inspection and Maintenance Management System for Electrical Infrastructure**

**Project Code:** SEP490-UAV-AIInspec (UAV-PMS)  
**Group Name:** GFA26SE66  
**Supervisors:** Đặng Ngọc Minh Đức, Phạm Minh Trí  

– Ho Chi Minh City, November 2026 –

---

# **I. Record of Changes**

| Date | A\* M, D | In charge | Change Description |
| :---: | :---: | :---: | :---- |
| 10/10/2026 | A | SE193338 - Nguyễn Nhật An | Initial creation of Report 5 – Software Test Documentation defining test scope, strategy, environment, and plan for MF01 (Pre-Mission Feasibility & Readiness). |
| 18/10/2026 | M | SE193464 - Nguyễn Quốc Khánh, SE181995 - Trần Ánh Quốc | Added Unit Test Cases (`MF02-UT`, `MF03-UT`) and System Test Cases (`MF02`, `MF03`) covering Mission Dispatch & Field Execution. |
| 25/10/2026 | M | SE193418 - Phạm Hoàng Minh Châu, SE193338 - Nguyễn Nhật An | Added Unit & System Test Cases for `MF04` (`YOLO26m` AI Analysis & Maintenance Tickets), `CF01` (IAM/OTP) and `CF02` (GIS/Fleet/Notifications). |
| 04/11/2026 | M | SE193338 - Nguyễn Nhật An | Recorded Round 1–3 System Testing results, resolved `DEF-001` to `DEF-008`, and documented experimental `YOLO26m`/`RT-DETR` evaluation (Section III). |
| 20/11/2026 | M | GFA26SE66 Team | Synchronized Sections I–II and companion Excel workbooks (`90` UT, `100` ST) with codebase `main` (`db66153`): Ocelot Gateway, Angular 21 Portal, REST Vision API, and `.NET 9` xUnit results (`715/719`). |

\*A - Added | M - Modified | D - Deleted

---

# **II. Testing Documentation**

## **1. Scope of Testing**

### **1.1 Target-of-Test Features & Functional Modules (In-Scope)**

The **Integrated UAV–AI Inspection and Maintenance Management System for Electrical Infrastructure (`SEP490-UAV-AIInspec` / `UAV-PMS`)** is a microservices-oriented platform comprising an **Ocelot API Gateway (`UavPms.ApiGateway`)**, five **.NET 9 microservices** (`IdentityService`, `OperationsService`, `AIInspectionService`, `InspectionEvaluationService`, and `NotificationService`), a **Python FastAPI + YOLO26m Vision Inference Service** communicating over HTTP REST, **PostgreSQL 16 + PostGIS**, **RabbitMQ**, **MinIO** object storage, **Open-Meteo** weather API, and an **Angular 21 Web Portal (`UavPms_FE`)** with Leaflet GIS maps for all operational and field-execution roles.

| Module Code | Module / Feature Group | Sub-Functions Covered | Target Components |
| :---: | :---- | :---- | :---- |
| **MF01** | **Pre-Mission Feasibility & Resource Readiness** | • **MF01.1** Pre-Mission Assessment Lifecycle (`Draft`, `Evaluating`, `Ready`, `NotReady`, `Expired`, `Completed`, `Cancelled`)<br>• **MF01.2** Site & Weather Assessment via Open-Meteo API & safety blockers<br>• **MF01.3** Personnel Readiness Verification (`Inspector` & `MaintenanceTechnician` scope)<br>• **MF01.4** UAV Fleet & Technical Inspection Check (`MaintenanceTechnician` role & single value-type rule)<br>• **MF01.5** Consolidated Readiness Decision & Idempotency / `RowVersion` concurrency | Angular 21 Portal, Ocelot Gateway, `OperationsService` (`PreMissionAssessmentService`, `DroneTechnicalInspectionService`), PostgreSQL |
| **MF02** | **Inspection Mission Creation & Dispatch** | • **MF02.1** Mission Creation from `Ready` Assessment & target tower scope resolution<br>• **MF02.2** Personnel Assignment & Regional Scope enforcement<br>• **MF02.3** UAV Resource Allocation & overlap check (excluding soft-deleted bookings)<br>• **MF02.4** Mission Dispatch & Atomic `ResourceBookings` lock (`Assigned` / `PendingAcceptance`)<br>• **MF02.5** Assignment Response (Accept/Postpone) & Mission Reschedule/Cancel | Angular 21 Portal, Ocelot Gateway, `OperationsService` (`MissionLifecycleService`, `DispatchMissionCommandHandler`), PostgreSQL |
| **MF03** | **Mission Execution, Media & Telemetry Log** | • **MF03.1** Field Check-In (`<= 200m` geodesic geofence) & UAV Handover (`deliver` / `return`)<br>• **MF03.2** Mission Execution Lifecycle (`Ready` -> `InProgress` -> `Suspended` -> `Completed` / `Postponed` / `Cancelled`)<br>• **MF03.3** Field RGB/Thermal Media Upload with EXIF GPS/tower link to MinIO<br>• **MF03.4** Critical Anomaly Alerting (`/api/v1/alerts`) & JSON Flight Log Upload via HTTP REST | Angular 21 Portal, Ocelot Gateway, `OperationsService` (`FieldCheckIn`, `UploadInspectionImage`, `EmergencyAlertService`), MinIO, SignalR |
| **MF04** | **AI Defect Analysis & Maintenance Tickets** | • **MF04.1** Media Submission, RabbitMQ Task Queuing & `DUPLICATE_MEDIA` idempotency<br>• **MF04.2** `YOLO26m` Vision REST Detection & `InspectionEvaluationService` gRPC severity scoring<br>• **MF04.3** Analyst Review (`Pending` -> `InReview` -> `Confirmed` / `Rejected`) & Tower Linking<br>• **MF04.4** Inspection Summary Report & PDF/CSV Defect Export (`/api/v1/reports`)<br>• **MF04.5** Maintenance Ticket Lifecycle (`Open` -> `InProgress` -> `PendingVerification` -> `Resolved` / `Closed`) | Angular 21 Portal, `AIInspectionService`, `InspectionEvaluationService` (gRPC), Python Vision Service (`YOLO26m` REST), `OperationsService`, RabbitMQ |
| **CF01** | **Identity, Auth, OTP & RBAC** | • **CF01.1** Email/BCrypt Login, JWT Issuance, 6-digit Redis OTP Verification & Token Rotation<br>• **CF01.2** Forgot-Password OTP, `ResetPassword` & Authenticated `ChangePassword`<br>• **CF01.3** SystemAdmin User CRUD, 5-Role RBAC (`SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `MaintenanceTechnician`) & `GeographicScopes` | Angular 21 Portal, Ocelot Gateway, `IdentityService` (`AuthController`, `UserController`, `RedisOtpService`), SMTP |
| **CF02** | **Infrastructure GIS, UAV Fleet & Platform** | • **CF02.1** Region & Transmission Tower CRUD, Bulk CSV Import & PostGIS Spatial Queries<br>• **CF02.2** UAV Fleet Registry, FlightController UID Pairing & `DRONE_IN_USE` protection<br>• **CF02.3** Notification Center (`/api/v1/notifications`), Unread Count, SignalR & Email Queue<br>• **CF02.4** Immutable `AuditLogs` (`ActionType` `varchar(100)`) & `SystemSettings` Reload | Angular 21 Portal, `OperationsService` (`Tower`, `Drones`, `FlightControllers`, `AuditLogs`, `SystemSettings`), `NotificationService`, PostGIS |

### **1.2 Non-Functional Requirements In-Scope**

1. **Security & Access Control:** JWT authentication, untrusted-device 6-digit Redis OTP verification, strict Role-Based Access Control (RBAC) across 5 system roles (`SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `MaintenanceTechnician`), and regional `GeographicScope` isolation.
2. **Data Integrity & Concurrency:** Transactional atomicity during mission dispatch and `ResourceBookings` allocation; exclusion of soft-deleted bookings from overlap constraints; `RowVersion` optimistic concurrency checks; idempotent RabbitMQ consumer handling.
3. **Compatibility & Usability:** Angular 21 Web Portal (`UavPms_FE`) verified on latest Google Chrome and Microsoft Edge across desktop (`1366x768` and `1920x1080`) and responsive tablet viewports.

### **1.3 Out-of-Scope Items**

- Physical autonomous UAV flight autopilot firmware (DJI SDK low-level stabilization and obstacle avoidance remain on OEM onboard controller).
- Destructive stress testing or DDoS attacks against shared infrastructure.
- Third-party national grid SCADA hardware bus integration.

### **1.4 Testing Stages & Responsibilities**

| Test Stage / Level | Person In-Charge | Inputs & Schedule | Key Testing Focus | Acceptance Criteria |
| :---- | :---- | :---- | :---- | :---- |
| **1. Unit Testing (UT)** | All Developers (`Nguyễn Quốc Khánh`, `Nguyễn Nhật An`, `Trần Ánh Quốc`, `Phạm Hoàng Minh Châu`) | **Inputs:** SDD (Report 4), `.NET 9` CQRS handlers & services.<br>**Time:** 05/10/2026 – 20/11/2026 | Verify domain policies, validation rules, boundary conditions, exceptions, and state transitions via xUnit/Moq & UT matrices. | • 100% of 90 UT Cases (`MF01`–`MF04`, `CF01`–`CF02`) executed (100% pass).<br>• `.NET 9` xUnit suite: `715/719` passing across 6 test projects. |
| **2. Integration Testing (IT)** | `Nguyễn Nhật An` (QA Lead), `Nguyễn Quốc Khánh` | **Inputs:** OpenAPI v1 routes, gRPC evaluation contracts, RabbitMQ schemas, PostGIS migrations.<br>**Time:** 12/10/2026 – 20/11/2026 | Verify Ocelot API Gateway, microservices (`Identity`, `Operations`, `AIInspection`, `InspectionEvaluation`, `Notification`), PostGIS, MinIO, and RabbitMQ. | • Integration & unit test projects `OperationsService.IntegrationTests` (50/50), `NotificationService.Tests` (58/58), `AIInspectionService.Tests` (72/72), `IdentityService.Tests` (92/92), and `InspectionEvaluationService.Tests` (2/2) pass 100%. |
| **3. System Testing (ST)** | All Team Members (cross-testing modules) | **Inputs:** SRS, SDD, `Report5_Test Report.xlsm`.<br>**Time:** 20/10/2026 – 20/11/2026 (3 Rounds) | Validate end-to-end workflows (`MF01` -> `MF04` and `CF01`–`CF02`), RBAC rules, validation messages, and Angular 21 Portal flows. | • 100% of 100 ST Cases across 26 sub-functions executed across 3 rounds.<br>• Round 3 Pass Rate = **100%**; `DEF-001`..`DEF-008` resolved. |
| **4. User Acceptance Testing (UAT)** | Project Team & Supervisors (`Đặng Ngọc Minh Đức`, `Phạm Minh Trí`) | **Inputs:** Deployed Staging Stack & Regional Tower Dataset.<br>**Time:** 01/11/2026 – 20/11/2026 | Verify business alignment with grid inspection workflows and Leaflet GIS usability across roles. | • All 4 Main Flows demonstrated end-to-end cleanly. |

---

## **2. Test Strategy**

- **Function Testing:** Equivalence Partitioning (EP), Boundary Value Analysis (BVA), Decision Tables, and State Transition testing across all `90` UT and `100` ST cases.
- **UI & Usability Testing:** Exploratory UI & Leaflet GIS verification on Angular 21 (`UavPms_FE`) across Chrome and Edge.
- **Security & Access Control Testing:** Missing/invalid/revoked JWT tokens (`401`), cross-role privilege escalation (`403`), and regional `GeographicScope` isolation.
- **Regression Testing:** Re-execution of automated `.NET 9` xUnit suite (`dotnet test UavPms.sln`) + Round 1 -> Round 2 -> Round 3 manual system test cycles.

---

## **3. Test Plan**

| Worker/Doer | Role | Assigned Modules |
| :---- | :---- | :---- |
| **Nguyễn Nhật An** (`SE193338`) | QA / Test Lead & Developer | `MF01` (Pre-Mission Readiness), `CF01` (Identity/OTP), Regression CI coordination |
| **Nguyễn Quốc Khánh** (`SE193464`) | Project Leader & Developer | `MF02` (Mission Dispatch & Bookings), cross-testing `MF01` / `CF02` |
| **Trần Ánh Quốc** (`SE181995`) | Developer & Tester | `MF03` (Mission Execution & Flight Logs), `CF02` (GIS Towers, UAV Fleet & Notifications) |
| **Phạm Hoàng Minh Châu** | Developer & Tester | `MF04` (`YOLO26m` Vision REST, gRPC Evaluation & Maintenance Tickets), cross-testing `MF02` / `MF03` |

---

## **4. Test Cases**

- **Unit Test Cases Workbook:** [`Report5_Unit Test.xlsm` (Google Drive)](https://docs.google.com/spreadsheets/d/1Ty65bBgrUu1_a6YrkLmNZiGC-DCX3F81/edit?gid=1581387368#gid=1581387368) (`90` UT cases across 6 module sheets `MF01`–`MF04`, `CF01`–`CF02`: `15` each — `6 N / 3 B / 6 A`).
- **System / Integration Test Cases Workbook:** [`Report5_Test Report.xlsm` (Google Drive)](https://docs.google.com/spreadsheets/d/1LxoOwNaAVg4qoVOU2liKxtwZ15l0T3Jv/edit?gid=1265708093#gid=1265708093) (`100` ST cases across `26` sub-functions: `MF01=18`, `MF02=18`, `MF03=16`, `MF04=18`, `CF01=14`, `CF02=16`).
- **Software Test Documentation:** [`Report5_Test Documentation.docx` (Google Drive)](https://docs.google.com/document/d/1zOyLugQ2mxB3LKgqYAftvHK9jD5VL54T/edit) (Sections I–II synchronized; Section III *Experimental and Result Review* preserved).

---

## **5. Test Reports**

### **5.1 Overall Test Execution Summary**

| Metric | Planned | Executed | Passed | Failed / Blocked | Pass Rate (%) |
| :--- | :---: | :---: | :---: | :---: | :--- |
| Unit Test Cases (`Report5_Unit Test.xlsm`) | 90 | 90 | 90 | 0 | 100.0% |
| System & Integration Test Cases (`Report5_Test Report.xlsm`) | 100 | 100 | 100 (Round 3) | 0 Open (8 Fixed R1–R2) | 92.0% (R1) -> 98.0% (R2) -> 100.0% (R3) |
| Automated `.NET 9` xUnit Suite (`dotnet test UavPms.sln`) | 719 | 719 | 715 | 4 (`OperationsService.Tests`) | 99.4% (5/6 Test Projects 100%) |
| **Combined Structured Workbook Total (UT + ST)** | **190** | **190** | **190** | **0 Open** | **100.0% (Final)** |

### **5.2 System Testing Defect Summary (`DEF-001` .. `DEF-008`)**

| Defect ID | Module / TC ID | Severity | Description & Root Cause | Status (Round 3) |
| :---: | :---: | :---: | :--- | :---: |
| **DEF-001** | `MF01` (`MF01-TC-014`) | Medium | Drone technical inspection accepted both `boolValue` and `numericValue`; fixed validator. | **Closed (Verified)** |
| **DEF-002** | `MF02` (`MF02-TC-010`) | High | PostgreSQL overlap index included soft-deleted `ResourceBookings`; fixed via migration `20261031165258_ExcludeDeletedResourceBookingsFromOverlap`. | **Closed (Verified)** |
| **DEF-003** | `MF02` (`MF02-TC-017`) | Medium | Postpone/reschedule left stale booking windows; fixed atomically in transaction. | **Closed (Verified)** |
| **DEF-004** | `MF03` (`MF03-TC-011`) | Medium | EXIF GPS rational coordinate parser failed on edge images; normalized rational parsing + fallback. | **Closed (Verified)** |
| **DEF-005** | `MF04` (`MF04-TC-004`) | Medium | Concurrent duplicate media analysis lacked `DUPLICATE_MEDIA` guard; added uniqueness/idempotency guard. | **Closed (Verified)** |
| **DEF-006** | `MF04` (`MF04-TC-012`) | Low | Rejected false positives were counted in `/api/v1/reports/statistics`; added filter exclusion. | **Closed (Verified)** |
| **DEF-007** | `CF01` (`CF01-TC-004`) | Medium | Expired OTP returned `500` instead of structured `400 Bad Request`; fixed error mapping. | **Closed (Verified)** |
| **DEF-008** | `CF02` (`CF02-TC-013`) | High | `AuditLogs.ActionType` `varchar(50)` truncated long action names; fixed via migration `20261031055707_WidenAuditLogActionTypeLength`. | **Closed (Verified)** |
