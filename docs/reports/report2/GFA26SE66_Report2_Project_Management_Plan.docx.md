 ![][image1]

 

 

 

 

 

 

 

**CAPSTONE PROJECT REPORT** 

**Report 2  – Project Management Plan**

 

 

 

 

 

 

 

 

 

**Table of Contents**

**[I. Record of Changes	5](#i.-record-of-changes)**

[**II. Project Management Plan	6**](#ii.-project-management-plan)

[1\. Overview	6](#1.-overview)

[1.1 Scope & Estimation	6](#1.1-scope-&-estimation)

[1.2 Project Objectives	8](#1.2-project-objectives)

[1.3 Project Risks	10](#1.3-project-risks)

[2\. Management Approach	10](#2.-management-approach)

[2.1 Project Process	11](#2.1-project-process)

[2.2 Quality Management	11](#2.2-quality-management)

[2.3 Training Plan	12](#2.3-training-plan)

[3\. Project Deliverables	12](#3.-project-deliverables)

[4\. Responsibility Assignments	13](#4.-responsibility-assignments)

[5\. Project Communications	14](#5.-project-communications)

[6\. Configuration Management	15](#6.-configuration-management)

[6.1 Document Management	15](#6.1-document-management)

[6.2 Source Code Management	15](#6.2-source-code-management)

[6.3 Tools & Infrastructures	15](#6.3-tools-&-infrastructures)

**Table of Figures**

[Figure 1\. UAV-PMS iterative and incremental development process.	11](#heading=h.figure1)

**Table of Tables**

[Table 1\. UAV-PMS Function-level Work Breakdown Structure Estimate.	6](#heading=h.table1)

[Table 2\. UAV-PMS objectives.	8](#heading=h.table2)

[Table 3\. Overall Activity-based Project Effort Budget.	9](#heading=h.table3)

[Table 4\. UAV-PMS risk register.	10](#heading=h.table4)

[Table 5\. Quality-management approach and evidence.	11](#heading=h.table5)

[Table 6\. UAV-PMS training plan.	12](#heading=h.table6)

[Table 7\. UAV-PMS master deliverable schedule.	12](#heading=h.table7)

[Table 8\. UAV-PMS responsibility assignment matrix.	13](#heading=h.table8)

[Table 9\. UAV-PMS project communication plan.	14](#heading=h.table9)

[Table 10\. UAV-PMS tools and infrastructure.	15](#heading=h.table10)

**ABBREVIATIONS**

A list of abbreviations and acronyms used in the thesis, with their full forms.

| Acronym | Meaning |
| ----- | ----- |
| AI | Artificial Intelligence |
| API | Application Programming Interface |
| ASP.NET | Active Server Pages .NET |
| CF | Common Function |
| CI | Continuous Integration |
| CNN | Convolutional Neural Network |
| CQRS | Command Query Responsibility Segregation |
| CSS | Cascading Style Sheets |
| DETR | Detection Transformer |
| D | Do |
| EF | Entity Framework |
| F1 | F1 Score |
| GIS | Geographic Information System |
| GPS | Global Positioning System |
| gRPC | gRPC Remote Procedure Calls |
| I | Informed |
| MF | Main Flow |
| mAP | Mean Average Precision |
| MQTT | Message Queuing Telemetry Transport |
| OpenCV | Open Source Computer Vision Library |
| P0/P1 | Priority 0 / Priority 1 Test Cases |
| PDF | Portable Document Format |
| R | Review |
| RGB | Red, Green, Blue |
| RT-DETR | Real-Time Detection Transformer |
| S | Support |
| SAM | Segment Anything Model |
| SCI | Structural Consistency Index |
| SDI | Structural Dependency Index |
| SRS | Software Requirements Specification |
| UI | User Interface |
| UAV | Unmanned Aerial Vehicle |
| UAV-PMS | Integrated UAV-AI Inspection and Maintenance Management System for Electrical Infrastructure |
| UX | User Experience |
| ViT | Vision Transformer |
| WBS | Work Breakdown Structure |
| YOLO | You Only Look Once |

# **I. Record of Changes** {#i.-record-of-changes}

| Date | A\* / M / D | In charge | Change Description |
| :---- | :---- | :---- | :---- |
| 24/09/2026 | A | SE193464 \- Nguyễn Quốc Khánh | Established the project-management baseline for the web system, MF01-MF04, AI research pipeline and UAV engineering workstreams, with an internal completion target of 30 November 2026\. |
| 26/09/2026 | M | SE193464 \- Nguyễn Quốc Khánh | Updated scope, milestones, manual-flight plan, AI work items, effort tracking and the process-diagram handoff. |
| 07/10/2026  | M | SE193338 \- Nguyễn Nhật An | Aligned the completion target to 30/11/2026; corrected the effort baseline (117 software \+ 28 non-software \= 145 person-days), objective due dates and training dates; removed the duplicated quality row.  |
| 07/10/2026  | M | SE193338 \- Nguyễn Nhật An  | Rescheduled all development to finish by 15/11/2026 and reserved 16/11-30/11/2026 for system testing, regression and defect fixing; updated iterations, objectives, risks, training, deliverables, communications and the research roadmap. |
| 18/10/2026 | M | SE193338 \- Nguyễn Nhật An  | Adjust scope and estimation |
| 24/10/2026 | M | SE193464 \- Nguyễn Quốc Khánh | Reconciled the function-level WBS estimate and the activity-based project effort budget; clarified that the 99 person-day WBS estimate and the 145 person-day activity budget are not additive.  |

 

\*A \- Added; M \- Modified; D \- Deleted

# **II. Project Management Plan** {#ii.-project-management-plan}

## **1\. Overview** {#1.-overview}

The team develops a prototype integrating UAV inspection coordination, server-side AI defect analysis, GIS-based visualization, human validation and maintenance follow-up. Publicly documented electricity-sector use cases provide domain context; they do not constitute confirmed requirements or formal sponsorship from EVN or any affiliated organization.

The working internal target remains 30 November 2026\. Development is planned to reach feature freeze by 15 November, followed by integration, regression, safety checks and defect fixing through 30 November. The December 2026–March 2027 period remains available for supervisor feedback, academic reporting and defense preparation. These are planning targets, not claims of completed work.

### **1.1 Scope & Estimation** {#1.1-scope-&-estimation}

The defined scope operationalizes the 7 Major Features (FE-01 to FE-07) established in Report 1 into foundational Common Functions (CF) and four end-to-end operational main flows (MF01–MF04). Table 1 provides a function-level Work Breakdown Structure estimate of 99 person-days for the defined product functions. Table 3 provides an overall activity-based project budget of 145 person-days, covering requirements analysis, design, implementation, AI research, UAV engineering, testing, and project management. These estimates serve different planning purposes and must not be added together. The overall project capacity and schedule are tracked against the 145 person-day activity budget.

***Table 1\.** UAV-PMS Function-level Work Breakdown Structure Estimate.*

| \# | WBS Item | Complexity | Est. Effort (person-days) |
| :---- | ----- | ----- | ----- |
| CF-01 | Authenticate users and enforce role-based access | Complex | 4 |
| CF-02 | Manage users, roles | Simple | 1 |
| CF-03 | Manage assets, towers, sites and GIS coordinates | Medium | 3 |
| CF-04 | Manage UAV inventory, operational status | Medium | 3 |
| CF-05 | Send operational notifications | Medium | 2 |
| MF01-01 | Define the proposed inspection scope | Simple | 1 |
| MF01-02 | Check personnel availability and eligibility | Medium | 2 |
| MF01-03 | Check UAV availability and technical readiness | Medium | 3 |
| MF01-04 | Record and view UAV pre-flight technical inspection results | Medium | 2 |
| MF01-05 | Review site and flight conditions | Simple | 1 |
| MF01-06 | Determine and record overall pre-mission readiness | Medium | 2 |
| MF02-01 | Review the approved readiness assessment and preconditions | Simple | 1 |
| MF02-02 | Configure flight-mission parameters | Medium | 2 |
| MF02-03 | Define schedule, deadline, priority and manager instructions | Medium | 2 |
| MF02-04 | Assign eligible personnel and UAVs with conflict check | Simple | 1 |
| MF02-05 | Dispatch the mission and notify assigned participants | Complex | 4 |
| MF02-06 | Preview tower coordinates and GIS route | Medium | 3 |
| MF02-07 | Accept or postpone an assignment with a recorded reason | Simple | 1 |
| MF02-08 | Notify manager of assignment response and track deadlines | Simple | 1 |
| MF02-09 | Cancel, suspend, resume, reassign or reschedule a mission | Simple | 1 |
| MF03-01 | Perform field check-in for the assigned mission | Medium | 2 |
| MF03-02 | Start and finish mission execution with state validation | Complex | 4 |
| MF03-03 | Submit field incident report | Medium | 2 |
| MF03-04 | Store flight logs and mission-completion evidence | Simple | 1 |
| MF04-01 | Upload inspection media and telemetry | Simple | 1 |
| MF04-02 | Validate inspection files and required metadata | Medium | 2 |
| MF04-03 | Create, monitor and retry AI analysis jobs | Medium | 2 |
| MF04-04 | Train and evaluate YOLO and RT-DETR as the baseline model using the versioned dataset and fixed data split | Complex | 10 |
| MF04-05 | Display detected classes, bounding boxes and confidence scores | Complex | 5 |
| MF04-06 | Analyst to confirm, reject or correct detections | Simple | 1 |
| MF04-07 | Evaluate severity and persist the final defect result | Complex | 5 |
| MF04-08 | Create and dispatch maintenance tickets for confirmed defects | Medium | 3 |
| MF04-09 | Log maintenance proof-of-work | Medium | 3 |
| MF04-10 | Provide dashboards, reports and result export | Medium | 4 |
|   | Total software-function effort |   | 99 |

### **1.2 Project Objectives** {#1.2-project-objectives}

The objective is to complete a working UAV-assisted inspection system from mission planning to AI result review. The team plans 145 person-days (1,160 hours) from 5 September to 30 November 2026 (87 calendar days). This equals about 3.3 hours per member per calendar day for four students. The 145 person-day activity budget is used for overall capacity and schedule tracking. The 99 person-day estimate in Table 1 is a separate function-level estimate for the defined product scope; it is not added to the overall activity budget. 

***Table 2\.** UAV-PMS objectives.*

| Dimension | Objective | Due Date | Metric / Acceptance Target |
| ----- | ----- | ----- | ----- |
| Scope | Deliver core role-based workflows across MF01–MF04 and common administrative functions without dependency on legacy asset-health modules. | 15–30 Nov 2026 | 100% of prioritized P0/P1 functional use cases accepted and operational. |
| Quality | Execute comprehensive unit, integration, and end-to-end regression test suites; verify zero open critical/high defects before sign-off. | 30 Nov 2026 | 100% test case pass rate (90/90 UT, 100/100 ST); zero Severity-1/Severity-2 defects remaining. |
| AI Detection | Establish version-controlled datasets; benchmark YOLO and RT-DETR architectures; achieve operational mAP on verified defect taxonomy. | Milestone Schedule | mAP@0.5 \>= 0.82 across target 6 defect classes; automated inference latency \< 2.5s per frame. |
| UAV & Hardware | Conduct bench diagnostics, flight log verification, and controlled flight validation subject to site safety clearance. | By 30 Nov 2026 | Periodic UAV flight telemetry ingestion with acceptable field network transmission latency (15–30s); flight logs and BIST sensor records verified. |
| GIS Infrastructure | Provide interactive spatial visualization of electrical towers, flight routes, and defect locations using Leaflet and GeoJSON. | System Testing | Responsive map rendering (\< 350 ms query latency); accurate spatial coordinate binding (WGS84). |
| Schedule & Cost | Maintain project progress within the 145 person-day budget (1,160 hours) across the 87-calendar-day timeline. | Weekly Tracking | Milestone variance \<= 5%; actual effort tracked within planned activity budget distributions. |

***Table 3\.** Overall Activity-based Project Effort Budget.*

| Activity | Planned Distribution | Person-days |
| :---- | :---- | :---- |
| Requirements and Analysis | 12% | 17 |
| System, Interface and UI Design | 12% | 17 |
| Web and Backend Implementation | 26% | 38 |
| AI Research and Experimentation | 21% | 31 |
| UAV and Embedded Engineering | 15% | 22 |
| Integration and Testing | 10% | 14 |
| Project Management and Documentation | 4% | 6 |
| Total | 100% | 145 |

### **1.3 Project Risks** {#1.3-project-risks}

***Table 4\.** UAV-PMS risk register.*

| \# | Risk Description | Impact | Probability | Response Plan |
| :---- | ----- | ----- | ----- | ----- |
| 1 | AI data has too few samples or very small defects | High | High | Check labels and class counts; add suitable augmentation and report weak classes clearly. |
| 2 | Dataset curation, six-model benchmarking or structural reasoning experiments exceed the planned effort | High | High | Prioritize a reproducible YOLO11/YOLO26/RT-DETR baseline and the raw-versus-curated comparison. Treat structural reasoning as an optional extension, subject to time and evaluation results. |
| 3 | Soldering, power or vibration causes UAV failure | Very High | Medium | Check wiring and balance, test without propellers first and prepare spare parts. |
| 4 | Manual flight, failsafe or site conditions cause an unsafe test | Very High | Medium | Use a pre-flight checklist, geofence, observer and abort plan; stop the test when site or weather conditions are unsafe. |
| 5 | Frontend, backend, AI and UAV use different data formats | High | Medium | Agree on request/result examples and test interfaces early. |
| 6 | The team misses the 30 November deadline | Very High | High | Review progress weekly, finish all development by 15 November, use 16-26 November only for testing and defect fixing, and freeze the release on 27 November. |
| 7 | A member is unavailable for a specialist task | Medium | Medium | Share setup notes and let one other member support each important task. |

## **2\. Management Approach** {#2.-management-approach}

The UAV-PMS project adopts the **Iterative and Incremental Development** model. Because the system involves complex multidisciplinary components—including web enterprise management, deep learning AI defect detection, and custom physical UAV hardware—a traditional linear waterfall model introduces high integration and safety risks. 

By applying an iterative and incremental approach, the project is structured into manageable cycles. Each cycle delivers a working, testable increment of the system, allowing the team to progressively mitigate technical risks, incorporate continuous feedback, and refine both software and hardware iteratively.

### **2.1 Project Process** {#2.1-project-process}

The software development life cycle (SDLC) is structured around the iterative and incremental workflow illustrated in **Figure 1**. Following an initial project-wide setup, the team repeatedly executes a core engineering cycle to build and refine system capabilities, deploying verified increments until project completion.

***Figure 1\.** UAV-PMS iterative and incremental development process.*

The process consists of the following key stages:

1. **Initial Planning:** Establishes the overarching project vision, feasibility assessment, high-level scope, resource allocation, and general roadmap across the software, AI, and UAV domains.
2. **Planning:** At the start of each iteration, the team defines specific iteration goals, selects and prioritizes backlog items (covering MF01–MF04), and schedules task allocations.
3. **Requirements:** Gathers, refines, and formalizes detailed functional and non-functional requirements, user stories, safety constraints, and interface specifications for the planned increment.
4. **Analysis and Design:** Formulates domain models, database schemas (PostgreSQL/PostGIS), REST/gRPC API contracts, UI/UX wireframes, AI inference data flows, and hardware avionics schematics.
5. **Implementation:** Develops backend services (ASP.NET Core), frontend views (Angular), trains and tunes defect detection models (YOLO), and integrates UAV airframe components.
6. **Testing:** Conducts rigorous verification at multiple levels—including unit tests, integration tests, end-to-end system test scenarios, AI performance benchmarks, and UAV pre-flight checks.
7. **Evaluation:** Assesses the iteration outcomes against defined acceptance criteria, conducts code and artifact reviews, and gathers stakeholder/supervisor feedback. Lessons learned and identified defects feed directly back into the next **Planning** stage.
8. **Deployment:** Packages, integrates, and releases stable, verified product increments to the staging/production environment, leading to the final operational release and capstone defense demonstration.

### **2.2 Quality Management** {#2.2-quality-management}

***Table 5\.** Quality-management approach and evidence.*

| Quality Approach | Application to UAV-PMS | Exit Evidence / Criterion |
| ----- | ----- | ----- |
| Defect Prevention | Agree on acceptance criteria, API formats, coding rules and UAV safety checks before work starts. | Checklist or requirement is available before implementation. |
| Reviewing | Another member reviews important SRS, code, test cases, AI results and flight evidence. | Review comments are fixed or explained. |
| Unit Testing | Test backend rules, validators and important frontend services/components. | Required unit tests pass before merge. |
| Integration Testing | Test frontend-backend, service-database, AI callback. | Data is sent and received in the expected format. |
| System Testing | Run the main MF01-MF04 scenarios using the correct roles and mission states. | P0/P1 cases pass and no Severity 1/2 defect remains. |
| AI Evaluation | Compare YOLO11/YOLO26/RT-DETR, data curation and structural reasoning on the same split and check errors by class. | Metrics, error analysis and selected-model reason are recorded. |
| UAV Verification | Check wiring, calibration, geofence and failsafe before controlled and waypoint flights. | Checklist, telemetry, camera and flight evidence are saved. |

### **2.3 Training Plan** {#2.3-training-plan}

***Table 6\.** UAV-PMS training plan.*

| Training Area | Participants | When / Duration | Waiver Criteria |
| ----- | ----- | ----- | ----- |
| SRS, traceability and Git | All members | 05 Sep-10 Oct; two 90-minute sessions | Waived after completing one reviewed requirement and pull request. |
| Angular, GIS and API integration | Nguyễn Quốc Khánh; Nguyễn Nhật An | By 15 Oct; two 2-hour sessions  | Waived after completing one reviewed integrated screen. |
| ASP.NET Core and messaging | Nguyễn Nhật An; Phạm Hoàng Minh Châu | By 15 Oct; two 2-hour sessions  | Waived after completing one reviewed service change. |
| UAV assembly and flight safety | Trần Ánh Quốc | Before flight; one 3-hour bench session | No waiver for a member joining a live flight. |
| YOLO11, YOLO26, RT-DETR and error analysis | Phạm Hoàng Minh Châu | 11 Oct-08 Nov; four 2-hour sessions  | Waived after reproducing one experiment and explaining its result. |

## **3\. Project Deliverables** {#3.-project-deliverables}

***Table 7\.** UAV-PMS master deliverable schedule.*

| \# | Main Deliverable | Deliverable Type | Planned Period / Due Date |
| :---- | ----- | ----- | ----- |
| 1 | Report 1 \- Project Introduction | External | Completed project baseline |
| 2 | Report 2 \- Project Management Plan | External | 24/09/2026 |
| 3 | SRS Working Baseline and Interface Contracts | Internal / Baseline | 05/09-10/10/2026 |
| 4 | MF01-MF02 Integration and Test Report | Internal | 11/10-25/10/2026 |
| 5 | Dataset Audit and YOLO11/YOLO26/RT-DETR Baseline Report | Internal / Research | 11/10-25/10/2026 |
| 6 | UAV Bench Assembly and Safety Checklist | Internal / Engineering | 11/10-25/10/2026 |
| 7 | Raw-versus-Curated Benchmark and Per-Class Error Report | Internal / Research | 26/10-08/11/2026 |
| 8 | MF04 Integration and Structural Reasoning Feasibility Assessment | Internal / Research | 01/11-15/11/2026 |
| 9 | Waypoint Flight Evidence | Internal / Validation | 09/11-15/11/2026 |
| 10 | System Test, Regression and Defect-Fix Report | Internal / Validation | 16/11-30/11/2026 |
| 11 | Final SRS, Integrated System and User Guide | External / Baseline | Due 30/11/2026 |
| 12 | Final Report, Demo and Defense Package | External | By the official schedule |

## **4\. Responsibility Assignments** {#4.-responsibility-assignments}

D \- Do; R \- Review; S \- Support; I \- Informed; blank \- omitted

***Table 8\.** UAV-PMS responsibility assignment matrix.*

| Responsibility | Nguyễn Quốc Khánh (SE193464) | Nguyễn Nhật An (SE193338) | Trần Ánh Quốc (SE181995) | Phạm Hoàng Minh Châu (SE193418) |
| ----- | :---- | ----- | :---- | :---- |
| Project leadership, schedule, risk and stakeholder coordination | D | S | S | S |
| Business analysis, SRS and requirement traceability | D | S | S | R |
| Frontend architecture, UX and MF01-MF04 screens | I | D | I | S |
| Backend services, database and API contracts | I | D | S | D |
| MF01-MF02 full-stack implementation | I | D | I | S |
| MF03 field workflow and system integration | I | D | S | S |
| UAV hardware assembly, soldering and power integration | R | S | D | I |
| AI dataset curation, six-model baseline and experiment protocol | I | I | I | D |
| Raw-versus-curated benchmarking and per-class diagnosis | I | I | I | D |
| MF04 AI-service, analyst review and result integration | I | D | I | D |
| Unit, integration and system testing | R | D | S | D |
| UAV safety and flight validation | R | I | D | S |
| Release, documentation and demonstration package | I | D | I | S |

The Project Leader confirms task-level ownership and resolves conflicts. D identifies the member directly producing the output; R independently reviews or accepts it; S provides technical support; I receives status or decisions. Two D entries are used only where a deliverable genuinely requires joint ownership across disciplines.

## **5\. Project Communications** {#5.-project-communications}

***Table 9\.** UAV-PMS project communication plan.*

| Communication Item | Who / Target | Purpose | When / Frequency | Type, Tool and Method |
| ----- | ----- | ----- | ----- | ----- |
| Internal Planning and Review | All members | Review progress, evidence, blockers, individual ownership and the next iteration. | Weekly; twice weekly from 26 October to 15 November; daily short check-in from 16 to 30 November 2026  | Meeting; task board; minutes; GitHub Issues |
| Supervisor Meeting | Team; Đặng Ngọc Minh Đức; Phạm Minh Trí | Review scope, requirements, technical direction, risk and milestone readiness. | Weekly and before formal gates | Meeting; Word/Google Docs; slides; evidence links |
| Requirement and Change Review | Leader, affected owners and reviewers | Approve requirement clarification and assess schedule/interface/test impact. | Before an affected baseline changes | Google sheet |
| Code and Interface Review | Software owners and one peer reviewer | Verify correctness, contract compatibility, security and test evidence. | For every pull request | GitHub pull request; CI/build results |
| AI Experiment Review | Phạm Hoàng Minh Châu; Trần Ánh Quốc; leader | Review data version, configuration, per-class results, failure modes and next experiment. | Weekly during AI work; after each experiment block | Google docs |
| UAV Engineering and Flight Review | Trần Ánh Quốc; leader; flight participants | Review assembly, safety, configuration, weather/site, route and abort criteria. | Before every powered or flight test | Google sheet \+ Google Drive |
| Defect and Test Triage | Relevant owner, tester and leader | Classify defects, assign action, decide release impact and verify closure. | After each integration/system-test session | Issue tracker; test report; retest evidence |
| Milestone Demonstration | Team, supervisors and reviewers | Demonstrate accepted scope and record decisions or corrective actions. | At each integration gate and formal review | Live demo; report; slides; signed minutes |

## **6\. Configuration Management** {#6.-configuration-management}

### **6.1 Document Management** {#6.1-document-management}

Project documents are stored in a shared Drive folder with separate folders for reports, SRS, design, test evidence, AI, UAV and meeting notes. The editable file and submitted PDF are stored together.  
• File naming: UAV-PMS\_\<Artifact\>\_v\<major.minor\>\_\<YYYYMMDD\>.\<extension\>.  
• Small updates increase the minor version; an approved submission increases the major version.  
• The leader or document owner uploads the approved version. Older versions are moved to Archive instead of being deleted.

### **6.2 Source Code Management** {#6.2-source-code-management}

Source code is stored in GitHub repositories for backend, frontend and AI work. The main branch contains the latest stable integrated version.  
• Each task uses a short feature or fix branch and is merged by pull request.  
• At least one other member reviews the pull request and relevant tests must pass.  
• API and AI-result format changes are updated in both producer and consumer code.  
• Large datasets and model files are stored outside normal Git history; secrets are never committed.  
• A working release is tagged before a milestone demo or submission.

### **6.3 Tools & Infrastructures** {#6.3-tools-&-infrastructures}

***Table 10\.** UAV-PMS tools and infrastructure.*

| Category | Tools / Infrastructure |
| ----- | ----- |
| Frontend | Angular 19, TypeScript, Tailwind CSS, Leaflet/MapLibre and SignalR client |
| Backend and Services | ASP.NET Core 9, Microservices \+ Clean architecture /CQRS, Ocelot API Gateway, gRPC, SignalR and background services |
| Database and Geospatial | PostgreSQL/PostGIS and EF Core migrations, MinIO |
| Messaging and Cache | RabbitMQ and Redis |
| AI Research | Python, PyTorch/Ultralytics YOLO11/YOLO26/RT-DETR, structural reasoning, OpenCV and versioned experiment configurations |
| UAV Hardware | UAV airframe/electronics, soldering and measurement tools, flight controller, camera, GPS and waypoint-planning software |
| Testing | xUnit/.NET test, API regression tests, Angular/Vitest, Playwright, manual system tests, AI evaluation scripts and UAV bench/flight checklists |
| Deployment and CI | Docker/Docker Compose, GitHub Actions and environment-based configuration |
| Development | JetBrains Rider, Visual Studio and Visual Studio Code, Antigravity, Codex |
| Diagramming and Documentation | Draw.io, Microsoft Word, Google Docs/Sheets and shared Drive storage, ProjectLibre |
| Version and Work Tracking | GitHub repositories, Google Drive |

[image1]: <data:image/png;base64,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>