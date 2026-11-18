![][image1]

**CAPSTONE PROJECT REPORT**

**Report 5 – Software Test Documentation**

**Integrated UAV–AI Inspection and Maintenance Management System for Electrical Infrastructure**

**Project Code:** SEP490-UAV-AIInspec (UAV-PMS)  
**Group Name:** GFA26SE66  
**Supervisors:** Đặng Ngọc Minh Đức, Phạm Minh Trí  

– Ho Chi Minh City, November 2026 –

---

**Table of Contents**  
[I. Record of Changes	3](#i.-record-of-changes)

[II. Testing Documentation	4](#ii.-testing-documentation)

[1\. Scope of Testing	4](#1.-scope-of-testing)

[2\. Test Strategy	6](#2.-test-strategy)

[2.1 Testing Types	6](#2.1-testing-types)

[2.2 Test Levels	8](#2.2-test-levels)

[2.3 Supporting Tools	9](#2.3-supporting-tools)

[3\. Test Plan	10](#3.-test-plan)

[3.1 Human Resources	10](#3.1-human-resources)

[3.2 Test Environment	11](#3.2-test-environment)

[3.3 Test Milestones	12](#3.3-test-milestones)

[4\. Test Cases	13](#4.-test-cases)

[5\. Test Reports	17](#5.-test-reports)

---

# **I. Record of Changes** {#i.-record-of-changes}

| Date | A\* M, D | In charge | Change Description |
| :---: | :---: | :---: | :---- |
| 10/10/2026 | A | Nguyễn Nhật An | Initial creation of Report 5 – Software Test Documentation template, defining test scope, test strategy, test environment, and test plan for MF01 (Pre-Mission Feasibility & Resource Readiness). |
| 18/10/2026 | M | Nguyễn Quốc Khánh, Trần Ánh Quốc | Expanded test scope and added Unit Test Cases (`MF02-UT`, `MF03-UT`) and System/Integration Test Cases (`MF02`, `MF03`) covering Create & Assign Inspection Mission and Mission Execution & Field Inspection. |
| 25/10/2026 | M | Phạm Hoàng Minh Châu, Nguyễn Nhật An | Added Unit Test Cases (`MF04-UT`, `CF01-UT`, `CF02-UT`) and System Test Cases (`MF04`, `CF01`, `CF02`) covering AI Inspection & Defect Detection, Authentication/User Management, and Infrastructure Assets/UAV Fleet/Platform Services. |
| 28/10/2026 | M | Nguyễn Nhật An | Updated Round 1 and Round 2 execution logs, defect tracking (`DEF-001` to `DEF-008`), and regression test plans across all 6 functional modules. |
| 04/11/2026 | M | GFA26SE66 Team | Completed Round 3 regression testing on staging/production environment; finalized 90 Unit Test Cases (100% Passed) and 100 System/Integration Test Cases (100% Passed); updated Section 4 and Section 5 test statistics. |

\*A \- Added | M \- Modified | D \- Deleted

---

# **II. Testing Documentation** {#ii.-testing-documentation}

## **1\. Scope of Testing** {#1.-scope-of-testing}

### **1.1 Target-of-Test Features & Functional Modules (In-Scope)**

The **Integrated UAV–AI Inspection and Maintenance Management System for Electrical Infrastructure (`SEP490-UAV-AIInspec` / `UAV-PMS`)** is a microservices-oriented platform comprising an **API Gateway (YARP)**, **Operations Service (.NET 9 / EF Core / PostgreSQL / PostGIS)**, **AI Service (Python / FastAPI / YOLOv12 / PyTorch / gRPC / RabbitMQ)**, **Web Portal (React / TypeScript)** for office roles, and **Mobile Application (Flutter)** for field inspectors and UAV operators.

Testing covers **all 4 core Main Flows (`MF01`–`MF04`)** and **all supporting Common/Platform Functions (`CF01`–`CF02`)** specified in Report 3 (SRS v0.4) and Report 4 (SDD):

| Module Code | Module / Feature Group | Sub-Functions Covered | Target Components |
| :---: | :---- | :---- | :---- |
| **MF01** | **Pre-Mission Feasibility & Resource Readiness** | • **MF01.1** Pre-Mission Assessment Management (Create `Evaluating`, View, Evaluate `Ready`/`NotReady`, Complete `Completed`, Cancel `Cancelled`, Auto-Expire `Expired`)<br>• **MF01.2** Site & Weather Assessment (Checklist evaluation, safety blocker detection)<br>• **MF01.3** Personnel Readiness & Certification Verification (Role availability, license expiry)<br>• **MF01.4** UAV Fleet Readiness & Technical Inspection Eligibility (Battery SOH, airframe status, valid inspection age)<br>• **MF01.5** Consolidated Readiness Decision (`Evaluating` -> `Ready` / `NotReady` -> `Completed` / `Expired` / `Cancelled`) | Web Portal, Operations Service API, `PreMissionAssessmentService`, Domain Readiness Policies, PostgreSQL |
| **MF02** | **Create & Assign Inspection Mission** | • **MF02.1** Mission Creation from Valid `Ready` Assessment (Handoff validation, target corridor/tower linking)<br>• **MF02.2** Personnel Assignment & Role Quota Validation (Inspector & UAV Operator assignment, region scope check)<br>• **MF02.3** UAV Resource Allocation & Conflict Checking (Overlapping mission schedule detection)<br>• **MF02.4** Mission Dispatch & Atomic Resource Booking (`ResourceBookings` creation, soft-deleted booking exclusion, status transition `Draft` -> `Pending` -> `Ready`)<br>• **MF02.5** Assignment Response & Post-Dispatch Mission Changes (Accept/Postpone assignment, reschedule, reassign, cancel mission `Cancelled`) | Web Portal, Mobile App, Operations Service API, `MissionCreationService`, `MissionChangeService`, `ResourceBookings` |
| **MF03** | **Mission Execution & Field Inspection** | • **MF03.1** Field Check-In & UAV Handover Prerequisite (GPS proximity check-in, UAV checkout/handover confirmation, return verification)<br>• **MF03.2** Mission Execution Lifecycle (`Ready` -> `InProgress` -> `Completed` / `Postponed` / `Cancelled`)<br>• **MF03.3** Field Inspection Media & Telemetry Upload (RGB/Thermal image upload, EXIF GPS/timestamp validation, MinIO/S3 object storage, tower linking)<br>• **MF03.4** Real-Time Critical Anomaly Alerting & Flight Log Upload (Immediate field hazard report, SignalR/FCM emergency broadcast, flight telemetry log ingestion) | Mobile App, Web Portal, Operations Service API, `MissionExecutionService`, `CriticalAlertService`, MinIO/S3, SignalR |
| **MF04** | **AI Inspection & Defect Detection** | • **MF04.1** Inspection Media Submission & AI Analysis Job Queuing (Batch/single image dispatch, RabbitMQ event publication, gRPC orchestration)<br>• **MF04.2** Automated YOLOv12 Defect Detection & Severity Classification (Insulator crack/flashover, damper slip, conductor corrosion, bird nest, vegetation encroachment, bounding box & confidence scoring)<br>• **MF04.3** AI Detection Review & Verification by Analyst (`Pending` -> `InReview` -> `Confirmed` / `Rejected`)<br>• **MF04.4** Inspection Summary & Defect Report Generation (Automated mission summary, PDF/Excel defect report export)<br>• **MF04.5** Maintenance Ticket Lifecycle Management (`Open` -> `Assigned` -> `InProgress` -> `PendingVerification` -> `Closed`) | Web Portal, Operations Service API, AI Service (FastAPI / YOLOv12), RabbitMQ, gRPC, `AIInspectionService`, `MaintenanceTicketService` |
| **CF01** | **Authentication, Authorization & User Management** | • **CF01.1** Authentication & Multi-Factor Device Verification (Email/password login, JWT issuance, untrusted device OTP verification, token refresh & logout)<br>• **CF01.2** Password Recovery & Security Policy Enforcement (Forgot password OTP, change password, complexity & lockout rules)<br>• **CF01.3** User Account, Role & Region Scope Management (Admin CRUD for users, RBAC enforcement for `Admin`, `Supervisor`, `Inspector`, `UAVOperator`, `Analyst`, regional data isolation) | Web Portal, Mobile App, API Gateway, Operations Service Identity Module (`AuthService`, `UserManagementService`), SMTP/OTP Service |
| **CF02** | **Infrastructure Assets, UAV Fleet & Platform Services** | • **CF02.1** Electrical Infrastructure Hierarchy Management (Regions, Power Corridors, Transmission Towers, Tower Components, PostGIS coordinates)<br>• **CF02.2** UAV Fleet Inventory, Flight Controller Pairing & Maintenance Logs (Drone registration, serial/FC binding, battery cycle tracking, status transitions)<br>• **CF02.3** Real-Time Notification Center & Email Dispatch (In-app notification feed, read/unread state, SignalR push, email alerts)<br>• **CF02.4** Immutable Audit Trail & System Configuration Settings (Audit log recording with widened `ActionType`, filtering/export, AI confidence threshold & SLA settings) | Web Portal, Mobile App, Operations Service (`AssetService`, `DroneFleetService`, `PlatformService`), PostgreSQL |

### **1.2 Non-Functional Requirements In-Scope**

1. **Security & Access Control:** JWT authentication (`HS256`/`RS256`), untrusted device OTP verification, strict Role-Based Access Control (RBAC) across 5 system roles, and regional data isolation (`RegionId` scoping).
2. **Data Integrity & Concurrency:** Transactional atomicity during mission dispatch and `ResourceBookings` allocation; exclusion of soft-deleted bookings from overlap constraints; idempotent RabbitMQ consumer handling for AI analysis results.
3. **Performance & Responsiveness:**
   - Standard REST API CRUD and query endpoints respond within `< 500 ms` (p95) under normal load.
   - AI defect inference via YOLOv12 processes standard inspection images (`1920x1080` to `4K`) within `< 3.0 seconds` per image on GPU/optimized runtime.
4. **Compatibility & Usability:** Web Portal verified on latest Google Chrome and Microsoft Edge (`1366x768` and `1920x1080`); Mobile App verified on Android 12–14 devices and emulators.

### **1.3 Out-of-Scope Items**

- Physical autonomous UAV flight autopilot firmware (DJI SDK low-level flight stabilization and hardware obstacle avoidance sensors are managed by the drone's onboard OEM controller).
- Destructive stress testing or Distributed Denial-of-Service (DDoS) attacks against the shared staging cloud infrastructure.
- Third-party national grid SCADA hardware bus integration.

### **1.4 Testing Stages, Responsibilities, Focus & Acceptance Criteria**

| Test Stage / Level | Person In-Charge | Inputs & Schedule | Key Testing Focus | Acceptance Criteria |
| :---- | :---- | :---- | :---- | :---- |
| **1. Unit Testing (UT)** | All Developers (`Nguyễn Quốc Khánh`, `Nguyễn Nhật An`, `Trần Ánh Quốc`, `Phạm Hoàng Minh Châu`) | **Inputs:** Detailed Design (Report 4), Domain/Application service classes.<br>**Time:** 05/10/2026 – 25/10/2026 | Verify domain policies, validation rules, boundary conditions, exception handling, and state transitions in isolation using mocked dependencies (`xUnit`, `Moq`, `PyTest`). | • 100% of planned Unit Test Cases (`90/90` cases across `MF01`–`MF04`, `CF01`–`CF02`) executed.<br>• Test successful coverage = **100%**.<br>• Zero unresolved critical/major unit defects. |
| **2. Integration Testing (IT)** | `Nguyễn Nhật An` (QA Lead), `Nguyễn Quốc Khánh` | **Inputs:** OpenAPI contracts, gRPC proto definitions, RabbitMQ schemas, ERD v2.2.<br>**Time:** 12/10/2026 – 30/10/2026 | Verify interaction between API Gateway, Operations Service, PostgreSQL/PostGIS, MinIO/S3, RabbitMQ, and Python AI Service via automated API/integration test suites (`PyTest` + `httpx`). | • All microservice contracts, DB migrations, and message queues communicate without schema mismatch.<br>• Automated regression suite (`58` API/workflow checks) passes 100% in CI/CD. |
| **3. System Testing (ST)** | All Team Members (cross-testing modules they did not author) | **Inputs:** SRS (Report 3), SDD (Report 4), System Test Case Specification (`Report5_Test Report.xlsm`).<br>**Time:** 18/10/2026 – 04/11/2026 (3 Rounds) | Validate end-to-end functional workflows (`MF01` -> `MF02` -> `MF03` -> `MF04` and `CF01`–`CF02`), RBAC permissions, validation messages, and non-functional targets across Web & Mobile. | • 100% of System Test Cases (`100/100` cases) executed across 3 testing rounds.<br>• Final Round 3 Pass Rate = **100%**.<br>• All High/Medium severity defects resolved and verified. |
| **4. User Acceptance Testing (UAT)** | Project Team & Academic Supervisors (`Đặng Ngọc Minh Đức`, `Phạm Minh Trí`) | **Inputs:** Deployed Staging/Production System, Demo Dataset (`ERD v2.2` seeded corridors & towers).<br>**Time:** 01/11/2026 – 04/11/2026 | Verify business alignment with electrical grid inspection workflows, usability for supervisors/inspectors/analysts, and readiness for capstone defense. | • All 4 Main Flows demonstrated end-to-end without blocking defects.<br>• Sign-off approval from project supervisors. |

### **1.5 Assumptions and Constraints**

- **Assumptions:**
  - Pre-seeded realistic electrical grid dataset (regions, 500kV/220kV corridors, transmission towers, UAV fleet, certified personnel, and sample aerial RGB/thermal inspection imagery) is available in the test database (`uavpms-db`).
  - External SMTP email delivery and FCM/SignalR notifications are accessible in the Dockerized staging environment.
- **Constraints:**
  - Live field flight testing near energized high-voltage transmission lines is restricted by safety regulations; therefore, field execution (`MF03`) utilizes GPS simulation and real UAV inspection imagery captured from approved test flights.

---

## **2\. Test Strategy** {#2.-test-strategy}

### **2.1 Testing Types** {#2.1-testing-types}

#### **2.1.1 Function Testing**
- **Test Objective:** Ensure every functional requirement and business rule across `MF01`–`MF04` and `CF01`–`CF02` behaves strictly according to the Software Requirements Specification (Report 3) and Software Design Document (Report 4), covering Normal (`N`), Abnormal (`A`), and Boundary (`B`) conditions.
- **Technique:**
  - *Equivalence Partitioning (EP)* and *Boundary Value Analysis (BVA)* for numeric/date inputs (e.g., UAV battery SOH thresholds, technical inspection age limits, GPS check-in radius, AI confidence score bounds `0.0`–`1.0`, pagination limits).
  - *Decision Table Testing* for multi-condition domain policies (e.g., `MF01` readiness aggregation combining site weather, personnel licenses, and UAV inspection validity; `MF02` mission dispatch eligibility).
  - *State Transition Testing* for entity lifecycles (`PreMissionAssessment`, `InspectionMission`, `AIAnalysisJob`, `MaintenanceTicket`).
- **Completion Criteria:** 100% of planned Unit Test Cases (`90` cases) and System Test Cases (`100` cases) are executed, with 100% pass rate in the final test round and zero open critical/high severity defects.

#### **2.1.2 User Interface (UI) & Usability Testing**
- **Test Objective:** Verify that the React Web Portal and Flutter Mobile Application present intuitive layouts, accurate status badges, responsive tables/forms, clear validation feedback, and consistent navigation across roles.
- **Technique:** Manual exploratory and checklist-based UI inspection against Figma screen specifications, verifying form field validations, modal confirmations, loading indicators, bounding-box canvas overlays on inspection images, and mobile touch targets.
- **Completion Criteria:** All Web and Mobile screens pass UI checklist verification without layout breakage, overlapping elements, or unhandled client-side exceptions.

#### **2.1.3 Security & Access Control Testing**
- **Test Objective:** Validate authentication flows, untrusted-device OTP verification, JWT expiration/revocation, Role-Based Access Control (RBAC), and regional data isolation so unauthorized users cannot access or mutate restricted resources.
- **Technique:**
  - Execute requests with missing, expired, malformed, or tampered JWT tokens.
  - Perform cross-role privilege escalation attempts (e.g., an `Inspector` or `Analyst` attempting to submit a UAV technical inspection or dispatch a mission; a `UAVOperator` attempting to confirm AI defect detections).
  - Perform cross-region access checks to verify that supervisors and inspectors only operate on corridors and missions within their assigned `RegionId`.
- **Completion Criteria:** 100% of unauthorized/unauthenticated requests return `401 Unauthorized` or `403 Forbidden`, and all security test cases in `CF01` and module RBAC suites pass.

#### **2.1.4 Performance & Reliability Testing**
- **Test Objective:** Ensure API response times, database spatial/relational queries, and asynchronous AI defect detection pipelines meet response-time SLA targets and handle concurrent operations reliably.
- **Technique:**
  - Measure API latency across core endpoints (`GET /api/v1/missions`, `POST /api/v1/pre-mission-assessments`, `POST /api/v1/ai-inspections/analyze`) using automated timing assertions and `benchmark_ai.py` / k6 scripts.
  - Test concurrent mission dispatch requests targeting the same UAV or inspector time window to verify transactional locking and conflict rejection (`409 Conflict`).
- **Completion Criteria:** REST API p95 latency `< 500 ms`, AI inference `< 3.0 s` per image, and zero duplicate resource bookings under concurrent requests.

#### **2.1.5 Regression Testing**
- **Test Objective:** Ensure that bug fixes, database schema migrations (e.g., `WidenAuditLogActionTypeLength`, `ExcludeDeletedResourceBookingsFromOverlap`), and new features do not break existing functionality.
- **Technique:** Re-execution of the automated `PyTest` regression suite (`58` automated test items) on every GitHub Actions CI/CD deployment, combined with multi-round manual execution (Round 1 -> Round 2 -> Round 3) of all impacted System Test Cases.
- **Completion Criteria:** 100% of automated CI regression tests and 100% of Round 3 manual regression test cases pass prior to release.

---

### **2.2 Test Levels** {#2.2-test-levels}

The project executes four sequential and complementary test levels:

1. **Unit Testing (`Unit`):** Executed by developers on individual .NET 9 domain entities, application services, validators, and Python AI inference helpers. External dependencies (repositories, message brokers, object storage) are mocked.
2. **Integration Testing (`Integration`):** Executed against containerized microservices (`uavpms-operations-service`, `uavpms-ai-service`, `uavpms-db`, `uavpms-rabbitmq`, `uavpms-minio`) to verify REST contracts, EF Core migrations, gRPC calls, and RabbitMQ event consumers.
3. **System Testing (`System`):** Executed end-to-end through the API Gateway, Web Portal, and Mobile Application against the integrated staging environment covering all 6 functional modules (`MF01`–`MF04`, `CF01`–`CF02`).
4. **Acceptance Testing (`Acceptance`):** Executed in realistic operational scenarios with project supervisors using the production-like dataset to confirm readiness for deployment.

| Type of Tests | Test Level |  |  |  |
| :---- | :---: | :---: | :---: | :---: |
|  | **Unit** | **Integration** | **System** | **Acceptance** |
| **Function Testing** | X | X | X | X |
| **UI & Usability Testing** |  |  | X | X |
| **Security & Access Control Testing** | X | X | X | X |
| **Performance & Reliability Testing** |  | X | X |  |
| **Regression Testing** | X | X | X |  |

---

### **2.3 Supporting Tools** {#2.3-supporting-tools}

| Purpose | Tool | Vendor/In-house | Version |
| :---- | :---- | :---- | :---- |
| .NET Unit Testing Framework | xUnit.net & Moq / FluentAssertions | Open Source (.NET Foundation) | 2.9.x / 4.20.x |
| Python Automated API & Regression Testing | PyTest & HTTPX (`anyio`) | Open Source | 8.4.1 / 0.28.x |
| API Manual & Contract Testing | Postman / Swagger OpenAPI UI | Postman Inc. / SmartBear | 11.x / OpenAPI 3.0 |
| CI/CD & Automated Test Runner | GitHub Actions (`deploy.yml`, `db-migrate.yml`) | GitHub (Microsoft) | v4 |
| Containerization & Test Environment Orchestration | Docker & Docker Compose | Docker Inc. | 27.x / v2.29 |
| Database Inspection & Verification | pgAdmin 4 / psql / Custom DB Workspace | PostgreSQL Global Dev Group | 16.x |
| Defect & Issue Tracking | GitHub Issues / Project Board & Excel Test Report | GitHub / Microsoft | 2026 |
| Web UI Debugging & Inspection | Chrome DevTools | Google | 130.x |
| Mobile App Testing & Debugging | Flutter DevTools & Android Emulator (ADB) | Google | Flutter 3.24.x |

---

## **3\. Test Plan** {#3.-test-plan}

### **3.1 Human Resources** {#3.1-human-resources}

| Worker/Doer | Role | Specific Responsibilities/Comments |
| :---- | :---- | :---- |
| **Nguyễn Nhật An** (`SE193338`) | **QA / Test Lead & Developer** | • Define overall Test Strategy, Test Plan, and Test Case templates (Report 5).<br>• Design and execute Unit & System Test Cases for **MF01** (Pre-Mission Readiness) and **CF01** (Auth & User Management).<br>• Maintain the automated `PyTest` regression test suite (`58` automated checks) and verify Round 1–3 defect fixes. |
| **Nguyễn Quốc Khánh** (`SE193464`) | **Project Leader, Developer & Tester** | • Review and approve test plans, test coverage metrics, and release readiness.<br>• Design and execute Unit & System Test Cases for **MF02** (Create & Assign Mission) and cross-test **MF01** / **CF02**.<br>• Coordinate CI/CD deployment, database migrations, and staging environment stability. |
| **Trần Ánh Quốc** (`SE181995`) | **Developer & Tester** | • Design and execute Unit & System Test Cases for **MF03** (Mission Execution & Field Inspection) and **CF02** (Infrastructure Assets, UAV Fleet & Platform Services).<br>• Perform cross-testing on Mobile App field inspection workflows and UAV handover/check-in rules. |
| **Phạm Hoàng Minh Châu** | **Developer & Tester** | • Design and execute Unit & System Test Cases for **MF04** (AI Inspection, YOLOv12 Defect Detection & Maintenance Tickets).<br>• Perform cross-testing on **MF02** and **MF03**, and verify AI inference accuracy and report generation. |

---

### **3.2 Test Environment** {#3.2-test-environment}

| Purpose | Tool / Component | Provider | Version |
| :---- | :---- | :---: | :---: |
| **Staging & Production Application Server** | Linux Cloud VPS (4 vCPU, 16 GB RAM, SSD, Docker Host `116.118.3.58`) | Cloud Infrastructure Provider | Ubuntu 22.04 LTS |
| **Backend Application Runtime** | .NET ASP.NET Core Runtime (`uavpms-operations-service`, `uavpms-gateway`) | Microsoft | .NET 9.0 |
| **AI Inference & Analysis Runtime** | Python FastAPI + PyTorch + Ultralytics YOLOv12 (`uavpms-ai-service`) | Python Software Foundation / Ultralytics | Python 3.12.16 |
| **Relational & Spatial Database** | PostgreSQL with PostGIS Extension (`uavpms-db`) | PostgreSQL Global Dev Group | PostgreSQL 16 |
| **Message Broker & Event Bus** | RabbitMQ Management (`uavpms-rabbitmq`) | VMware / Open Source | 3.13 |
| **Object Storage (Inspection Media & Logs)** | MinIO S3-Compatible Storage (`uavpms-minio`) | MinIO Inc. | RELEASE.2024 |
| **Web Client Test Environment** | Google Chrome & Microsoft Edge on Windows 11 (`1920x1080`) | Google / Microsoft | v130+ |
| **Mobile Client Test Environment** | Android Physical Device & Android Studio Emulator (Pixel 7 API 34) | Google | Android 14 (API 34) |

---

### **3.3 Test Milestones** {#3.3-test-milestones}

| Milestone Task | Start Date | End Date |
| :---- | :---: | :---: |
| **M1. Test Planning & Test Environment Setup** (Define test strategy, set up Docker staging stack, seed `ERD v2.2` test dataset) | 01/10/2026 | 08/10/2026 |
| **M2. Unit Test Case Design & Execution (`MF01`–`MF04`, `CF01`–`CF02`)** (Author and execute 90 unit test cases across 6 modules) | 06/10/2026 | 25/10/2026 |
| **M3. System & Integration Test Case Design** (Author 100 system test cases across 26 sub-functions in `Report5_Test Report.xlsm`) | 10/10/2026 | 19/10/2026 |
| **M4. System Testing – Round 1 Execution & Defect Logging** (Full execution of 100 test cases; log defects `DEF-001` to `DEF-008`) | 20/10/2026 | 23/10/2026 |
| **M5. Defect Resolution & System Testing – Round 2 Execution** (Verify fixes for Round 1 defects; identify and log remaining regression items) | 24/10/2026 | 29/10/2026 |
| **M6. Final Regression Testing – Round 3 & Automated CI Suite Verification** (Verify 100% pass across 100 ST cases, 90 UT cases, and 58 CI automated tests) | 30/10/2026 | 04/11/2026 |
| **M7. Finalize Report 5 – Software Test Documentation & Supervisor Sign-Off** | 03/11/2026 | 04/11/2026 |

---

## **4\. Test Cases** {#4.-test-cases}

All detailed test cases are prepared strictly following the FPT University Capstone Project templates and packaged in the accompanying spreadsheet workbooks and per-sheet CSV files inside `docs/reports/report5/`:

- **Unit Test Cases Workbook:** [`GFA26SE66_Report5_Unit Test` (Google Sheets)](https://docs.google.com/spreadsheets/d/1QU5WA6c8DIoFYHo4-o-ipqr_5vmklKOrZa9VgItn72s/edit) / `Report5_Unit Test.xlsx` / `Report5_Unit Test.xlsm` (10 sheets: `Guideline`, `Cover`, `Functions`, `Statistics`, `MF01`, `MF02`, `MF03`, `MF04`, `CF01`, `CF02`)
- **System / Integration / Acceptance Test Cases Workbook:** [`GFA26SE66_Report5_Test Report` (Google Sheets)](https://docs.google.com/spreadsheets/d/1gn-VBkfDAjh6pR-Oks3FTybf11g_tWiaOg-Lh3vmpjU/edit) / `Report5_Test Report.xlsx` / `Report5_Test Report.xlsm` (9 sheets: `Cover`, `Test Cases`, `Test Statistics`, `MF01`, `MF02`, `MF03`, `MF04`, `CF01`, `CF02`)

### **4.1 Summary of Unit Test Cases (`Report5_Unit Test.xlsm`)**

Each functional module is tested via decision-matrix Unit Test Cases covering **Normal (`N`)**, **Abnormal (`A`)**, and **Boundary (`B`)** input combinations, expected return payloads, expected domain exceptions, and validation messages:

| No | Module Code | Function / Service Under Test | Sheet Name | Unit Test ID Range | Normal (N) | Abnormal (A) | Boundary (B) | Total UT Cases |
| :---: | :---: | :---- | :---: | :---: | :---: | :---: | :---: | :---: |
| 1 | **MF01-UT** | `PreMissionAssessmentService` & Domain Readiness Policies | `MF01` | `MF01-UT-001` .. `MF01-UT-015` | 6 | 6 | 3 | **15** |
| 2 | **MF02-UT** | `MissionCreationService` & `MissionChangeService` | `MF02` | `MF02-UT-001` .. `MF02-UT-015` | 6 | 6 | 3 | **15** |
| 3 | **MF03-UT** | `MissionExecutionService` & `CriticalAlertService` | `MF03` | `MF03-UT-001` .. `MF03-UT-015` | 6 | 6 | 3 | **15** |
| 4 | **MF04-UT** | `AIInspectionService` & `MaintenanceTicketService` | `MF04` | `MF04-UT-001` .. `MF04-UT-015` | 6 | 6 | 3 | **15** |
| 5 | **CF01-UT** | `AuthService` & `UserManagementService` | `CF01` | `CF01-UT-001` .. `CF01-UT-015` | 6 | 6 | 3 | **15** |
| 6 | **CF02-UT** | `AssetService`, `DroneFleetService` & `PlatformService` | `CF02` | `CF02-UT-001` .. `CF02-UT-015` | 6 | 6 | 3 | **15** |
| **Total** | | **All 6 Functional Modules** | **6 Sheets** | **90 Unit Test Cases** | **36 (40%)** | **36 (40%)** | **18 (20%)** | **90** |

### **4.2 Summary of System & Integration Test Cases (`Report5_Test Report.xlsm`)**

The System Test specification organizes **100 end-to-end test cases** across **26 sub-functions** spanning all 4 Main Flows (`MF01`–`MF04`) and 2 Common Function groups (`CF01`–`CF02`):

| No | Sheet Name | Sub-Function Code & Name | Test Case ID Range | Total TCs | Description / Key Verification Points |
| :---: | :---: | :---- | :---: | :---: | :---- |
| 1 | **MF01** | **MF01.1** Pre-Mission Assessment Lifecycle | `MF01-TC-001` .. `MF01-TC-004` | 4 | Create assessment (`Evaluating`), view details, cancel assessment (`Cancelled`), auto-expire stale assessment (`Expired`). |
| 2 | **MF01** | **MF01.2** Site & Weather Assessment | `MF01-TC-005` .. `MF01-TC-008` | 4 | Record favorable site/weather checklist, flag severe wind/lightning blockers, boundary wind speed check. |
| 3 | **MF01** | **MF01.3** Personnel Readiness Verification | `MF01-TC-009` .. `MF01-TC-011` | 3 | Verify certified Inspector & UAV Operator availability, detect expired flight certification or schedule conflict. |
| 4 | **MF01** | **MF01.4** UAV Fleet & Technical Inspection Check | `MF01-TC-012` .. `MF01-TC-015` | 4 | Submit UAV technical inspection (Operator only), reject unauthorized roles (`Inspector`/`Analyst`), check battery SOH & inspection recency. |
| 5 | **MF01** | **MF01.5** Consolidated Readiness Decision | `MF01-TC-016` .. `MF01-TC-018` | 3 | Evaluate overall `Ready` and `NotReady` outcome; transition to `Completed` when mission is created; block mission creation when not ready. |
| 6 | **MF02** | **MF02.1** Create Mission from `Ready` Assessment | `MF02-TC-001` .. `MF02-TC-004` | 4 | Create mission linked to valid `Ready` assessment, reject expired/unapproved assessment, validate target towers in corridor. |
| 7 | **MF02** | **MF02.2** Personnel Assignment & Role Quotas | `MF02-TC-005` .. `MF02-TC-008` | 4 | Assign Inspector & UAV Operator within region, reject missing mandatory role or cross-region personnel. |
| 8 | **MF02** | **MF02.3** UAV Resource Allocation & Conflict Check | `MF02-TC-009` .. `MF02-TC-011` | 3 | Allocate available UAV, detect overlapping time window conflict (`409 Conflict`), verify boundary back-to-back window. |
| 9 | **MF02** | **MF02.4** Mission Dispatch & Atomic Resource Booking | `MF02-TC-012` .. `MF02-TC-015` | 4 | Dispatch mission (`Draft` -> `Pending` -> `Ready`), create atomic `ResourceBookings`, exclude soft-deleted bookings from overlap check, dispatch notifications. |
| 10 | **MF02** | **MF02.5** Assignment Response & Post-Dispatch Changes | `MF02-TC-016` .. `MF02-TC-018` | 3 | Accept/postpone assignment (`Pending` -> `Ready` / `Postponed`), reschedule/reassign mission, cancel mission (`Cancelled`) and release resource bookings. |
| 11 | **MF03** | **MF03.1** Field Check-In & UAV Handover Prerequisite | `MF03-TC-001` .. `MF03-TC-004` | 4 | Field GPS check-in within corridor radius, reject out-of-range check-in, confirm UAV handover before flight & UAV return after flight. |
| 12 | **MF03** | **MF03.2** Mission Execution Lifecycle | `MF03-TC-005` .. `MF03-TC-008` | 4 | Start mission execution (`Ready` -> `InProgress`), reject start without check-in/handover, complete mission (`Completed`), postpone/cancel mission due to weather (`Postponed` / `Cancelled`). |
| 13 | **MF03** | **MF03.3** Field Media & EXIF Telemetry Upload | `MF03-TC-009` .. `MF03-TC-012` | 4 | Upload RGB & thermal images with valid EXIF GPS/timestamp, associate with target tower component, reject corrupted/unsupported files. |
| 14 | **MF03** | **MF03.4** Critical Anomaly Alerting & Flight Log Upload | `MF03-TC-013` .. `MF03-TC-016` | 4 | Raise real-time critical field alert, supervisor acknowledge & resolve alert, upload UAV flight telemetry log (`.bin`/`.csv`). |
| 15 | **MF04** | **MF04.1** Media Submission & AI Analysis Queuing | `MF04-TC-001` .. `MF04-TC-004` | 4 | Trigger AI analysis job for mission media, publish RabbitMQ task, track job progress (`Pending` -> `Processing` -> `Completed`), prevent duplicate active job. |
| 16 | **MF04** | **MF04.2** YOLOv12 Defect Detection & Severity Scoring | `MF04-TC-005` .. `MF04-TC-008` | 4 | Detect insulator crack, damper slip, corrosion & foreign object; persist bounding boxes, confidence score, and severity (`LOW`/`MEDIUM`/`HIGH`/`CRITICAL`). |
| 17 | **MF04** | **MF04.3** Analyst Review, Verification & Reanalysis | `MF04-TC-009` .. `MF04-TC-012` | 4 | Analyst reviews detection (`Pending` -> `InReview`), confirms (`Confirmed`) or rejects false positive (`Rejected`) with reason, adjusts severity/bounding box. |
| 18 | **MF04** | **MF04.4** Inspection Summary & Defect Report Export | `MF04-TC-013` .. `MF04-TC-015` | 3 | Compute mission summary, generate consolidated inspection report, export PDF/CSV defect report. |
| 19 | **MF04** | **MF04.5** Maintenance Ticket Lifecycle | `MF04-TC-016` .. `MF04-TC-018` | 3 | Auto-generate maintenance ticket (`Open`) from `Confirmed` defect, assign technician (`Assigned`), start work (`InProgress`), submit proof (`PendingVerification`), and close (`Closed`). |
| 20 | **CF01** | **CF01.1** Authentication & Device OTP Verification | `CF01-TC-001` .. `CF01-TC-005` | 5 | Valid login, invalid credentials rejection, untrusted device OTP challenge & verification, reject expired/invalid OTP, token refresh & logout. |
| 21 | **CF01** | **CF01.2** Password Recovery & Security Policies | `CF01-TC-006` .. `CF01-TC-009` | 4 | Request password reset OTP, reset password with strong policy, reject weak password, change password while authenticated. |
| 22 | **CF01** | **CF01.3** User Management, RBAC & Region Scope | `CF01-TC-010` .. `CF01-TC-014` | 5 | Admin creates user & assigns role/region, deactivate suspended user, verify RBAC route protection, enforce regional data isolation. |
| 23 | **CF02** | **CF02.1** Electrical Infrastructure Asset Management | `CF02-TC-001` .. `CF02-TC-004` | 4 | Manage Regions, Power Corridors, and Transmission Towers; validate PostGIS coordinates & tower sequence ordering; prevent deleting corridor with active missions. |
| 24 | **CF02** | **CF02.2** UAV Fleet & Flight Controller Management | `CF02-TC-005` .. `CF02-TC-008` | 4 | Register UAV asset, pair flight controller serial, update UAV maintenance status (`AVAILABLE`/`MAINTENANCE`/`RETIRED`), block decommissioned UAV assignment. |
| 25 | **CF02** | **CF02.3** Notification Center & Real-Time Push | `CF02-TC-009` .. `CF02-TC-012` | 4 | Receive real-time mission/alert notifications, filter unread notifications, mark single/all notifications as read, verify email notification dispatch. |
| 26 | **CF02** | **CF02.4** Immutable Audit Logs & System Settings | `CF02-TC-013` .. `CF02-TC-016` | 4 | Record audit logs with long `ActionType` (`varchar(100)`), filter audit trail by actor/date/action, update AI confidence threshold & SLA system settings. |
| **Total** | **6 Sheets** | **26 Sub-Functions** | **100 System Test Cases** | **100** | **Full functional & non-functional coverage across Web, Mobile, API & AI Service** |

---

## **5\. Test Reports** {#5.-test-reports}

### **5.1 Unit Test Execution Statistics**

All **90 Unit Test Cases** across the 6 functional modules were executed against the .NET 9 and Python 3.12 codebases. The consolidated statistics from the `Statistics` sheet of `Report5_Unit Test.xlsm` are presented below:

| No | Module Code | Passed | Failed | Untested | Normal (N) | Abnormal (A) | Boundary (B) | Total Cases | Pass Rate (%) |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| 1 | **MF01-UT** | 15 | 0 | 0 | 6 | 6 | 3 | 15 | 100.0% |
| 2 | **MF02-UT** | 15 | 0 | 0 | 6 | 6 | 3 | 15 | 100.0% |
| 3 | **MF03-UT** | 15 | 0 | 0 | 6 | 6 | 3 | 15 | 100.0% |
| 4 | **MF04-UT** | 15 | 0 | 0 | 6 | 6 | 3 | 15 | 100.0% |
| 5 | **CF01-UT** | 15 | 0 | 0 | 6 | 6 | 3 | 15 | 100.0% |
| 6 | **CF02-UT** | 15 | 0 | 0 | 6 | 6 | 3 | 15 | 100.0% |
| **Sub total** | **All Modules** | **90** | **0** | **0** | **36 (40%)** | **36 (40%)** | **18 (20%)** | **90** | **100.0%** |

- **Unit Test Coverage:** `100.00%` (`90 / 90` executed)
- **Unit Test Successful Coverage:** `100.00%` (`90 / 90` passed)

---

### **5.2 System & Integration Test Execution Statistics (Across 3 Testing Rounds)**

System and Integration testing was conducted over **3 iterative rounds** (`Round 1` on `20/10/2026`, `Round 2` on `27/10/2026`, and `Round 3` on `04/11/2026`). The consolidated statistics from the `Test Statistics` sheet of `Report5_Test Report.xlsm` are summarized below:

#### **5.2.1 Progression Across Testing Rounds**

| Module / Sheet | Total TCs | Round 1 (20/10/2026) |  | Round 2 (27/10/2026) |  | Round 3 – Final (04/11/2026) |  |  |  |
| :---- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
|  |  | **Passed** | **Failed** | **Passed** | **Failed** | **Passed** | **Failed** | **Pending** | **N/A** |
| **MF01** – Pre-Mission Feasibility & Resource Readiness | **18** | 17 | 1 | 18 | 0 | **18** | 0 | 0 | 0 |
| **MF02** – Create & Assign Inspection Mission | **18** | 16 | 2 | 17 | 1 | **18** | 0 | 0 | 0 |
| **MF03** – Mission Execution & Field Inspection | **16** | 15 | 1 | 16 | 0 | **16** | 0 | 0 | 0 |
| **MF04** – AI Inspection & Defect Detection | **18** | 16 | 2 | 17 | 1 | **18** | 0 | 0 | 0 |
| **CF01** – Authentication, Authorization & User Management | **14** | 13 | 1 | 14 | 0 | **14** | 0 | 0 | 0 |
| **CF02** – Infrastructure Assets, UAV Fleet & Platform Services | **16** | 15 | 1 | 16 | 0 | **16** | 0 | 0 | 0 |
| **Sub total** | **100** | **92** | **8** | **98** | **2** | **100** | **0** | **0** | **0** |
| **Test Coverage (%)** | | **100.0%** | | **100.0%** | | **100.0%** | | | |
| **Test Successful Coverage (%)** | | **92.0%** | | **98.0%** | | **100.0%** | | | |

---

### **5.3 Automated CI/CD Regression Suite Results**

In addition to the manual and spreadsheet-tracked test cases, the project maintains an automated regression suite executed via `pytest` in Docker (`uavpms-ai-service` / API integration runner) during every deployment workflow:

- **Total Automated Regression Tests Collected:** `58 items`
- **Modules Covered by Automated Suite:**
  - `fixtures/mf01/test_apply_workflow.py` & `test_generator.py` (`8 passed`) – Database seeding safety, `ERD v2.2` isolation, and repeatable mock dataset verification.
  - `tests/test_auth.py` (`3 passed`) – Credential login, invalid login rejection, and OTP verification enforcement.
  - `tests/test_drone_technical_inspection.py` (`14 passed`) – RBAC enforcement (`UAVOperator` allowed; `Inspector` and `Analyst` rejected), payload validation, and latest technical inspection retrieval.
  - `tests/test_pre_mission_assessment.py` & `tests/test_missions.py` (`18 passed`) – Pre-mission assessment lifecycle, readiness evaluation, mission dispatch, and soft-deleted `ResourceBookings` overlap exclusion.
  - `tests/test_ai_inspection.py` & `tests/test_CriticalAlerts.py` (`15 passed`) – YOLOv12 defect analysis job processing, detection review, and critical field anomaly alerting.
- **Latest Execution Result (04/11/2026):** `58 passed in 14.82s` (`100% Passed`).

---

### **5.4 Defect Summary & Root Cause Analysis**

During Round 1 and Round 2 of System Testing, **8 defects (`DEF-001` to `DEF-008`)** were identified, tracked, fixed, and verified closed by Round 3:

| Defect ID | Module / TC ID | Severity | Description of Defect | Root Cause & Resolution | Status (Round 3) |
| :---: | :---: | :---: | :---- | :---- | :---: |
| **DEF-001** | `MF01` (`MF01-TC-014`) | Medium | Submitting a UAV technical inspection with multiple simultaneous value types (`BooleanValue` and `NumericValue` both populated) was accepted instead of returning `400 Bad Request`. | Added strict mutual-exclusion validation in `DroneTechnicalInspectionValidator`; verified via `test_drone_technical_inspection_rejects_multiple_value_types`. | **Closed (Verified)** |
| **DEF-002** | `MF02` (`MF02-TC-010`) | High | Re-dispatching a mission after cancelling a prior mission for the same UAV and time slot failed with a false resource overlap conflict (`409 Conflict`). | PostgreSQL exclusion/overlap index included soft-deleted `ResourceBookings`. Added migration `20261031165258_ExcludeDeletedResourceBookingsFromOverlap` filtering `WHERE "IsDeleted" = false`. | **Closed (Verified)** |
| **DEF-003** | `MF02` (`MF02-TC-017`) | Medium | Rescheduling a mission in `ASSIGNED` state updated `ScheduledStart`/`ScheduledEnd` on the mission record but did not atomically shift the corresponding `ResourceBookings` window. | Updated `MissionChangeService.RescheduleAsync` to update active `ResourceBookings` inside the same database transaction; verified in Round 3. | **Closed (Verified)** |
| **DEF-004** | `MF03` (`MF03-TC-011`) | Medium | Uploading field inspection images captured near corridor borders occasionally failed EXIF GPS coordinate parsing when altitude metadata used rational negative denominators. | Normalized EXIF rational GPS coordinate parser in `MediaMetadataExtractor` and added fallback to mission corridor centroid with warning flag. | **Closed (Verified)** |
| **DEF-005** | `MF04` (`MF04-TC-004`) | Medium | Clicking "Run AI Analysis" twice rapidly on the same mission queued duplicate RabbitMQ jobs and produced duplicate `AIAnalysisResult` bounding boxes. | Added idempotency check on active/pending `AnalysisJob` per `MissionId`/`MediaId` and unique constraint guard before publishing to RabbitMQ. | **Closed (Verified)** |
| **DEF-006** | `MF04` (`MF04-TC-012`) | Low | Requesting reanalysis with a custom confidence threshold (`0.35`) retained superseded detections in the active mission defect count summary. | Updated `AIInspectionService` summary query to exclude detections marked `IsSuperseded = true` when computing total confirmed defect counts. | **Closed (Verified)** |
| **DEF-007** | `CF01` (`CF01-TC-004`) | Medium | Entering an expired device verification OTP returned a generic `500 Internal Server Error` instead of a structured `400 Bad Request` validation message. | Handled `OtpExpiredException` in `AuthController` / exception middleware to return `400 Bad Request` with code `OTP_INVALID_OR_EXPIRED`. | **Closed (Verified)** |
| **DEF-008** | `CF02` (`CF02-TC-013`) | High | Recording detailed audit log entries for long domain action names (e.g., `PRE_MISSION_ASSESSMENT_READINESS_EVALUATED_CONDITIONALLY_READY`) failed with PostgreSQL string truncation (`varchar(50)`). | Added migration `20261031055707_WidenAuditLogActionTypeLength` widening `"AuditLogs"."ActionType"` to `character varying(100)`. | **Closed (Verified)** |

---

### **5.5 Overall Test Conclusion**

1. **Completeness:** All **4 Main Flows (`MF01`, `MF02`, `MF03`, `MF04`)** and **2 Common Function Modules (`CF01`, `CF02`)** have been thoroughly tested across Unit (`90` cases), Integration (`58` automated CI checks), and System (`100` cases across 26 sub-functions) levels.
2. **Quality Metrics:**
   - **Unit Test Pass Rate:** `100.0%` (`90 / 90` Passed; `40%` Normal, `40%` Abnormal, `20%` Boundary).
   - **System Test Pass Rate (Round 3):** `100.0%` (`100 / 100` Passed, `0` Failed, `0` Pending).
   - **Defect Resolution Rate:** `100.0%` (`8 / 8` defects fixed and verified via regression testing).
3. **Release Readiness:** The **UAV-PMS (`SEP490-UAV-AIInspec`)** system satisfies all functional and non-functional acceptance criteria defined in the Software Requirements Specification and is ready for production operation and Capstone Project defense.

[image1]: <data:image/png;base64,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>
