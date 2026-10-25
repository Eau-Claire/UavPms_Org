 ![][image1]

 

 

 

 

 

 

 

**CAPSTONE PROJECT REPORT** 

**Report 2  – Project Management Plan**

 

 

 

 

 

 

 

 

 

Ho Chi Minh City, October 2026

**Table of Contents**

**[I. Record of Changes	4](#i.-record-of-changes)**

[**II. Project Management Plan	5**](#ii.-project-management-plan)

[1\. Overview	5](#1.-overview)

[1.1 Scope & Estimation	5](#1.1-scope-&-estimation)

[1.2 Project Objectives	7](#1.2-project-objectives)

[1.3 Project Risks	9](#1.3-project-risks)

[2\. Management Approach	9](#2.-management-approach)

[2.1 Project Process	10](#2.1-project-process)

[2.2 Quality Management	10](#2.2-quality-management)

[2.3 Training Plan	11](#2.3-training-plan)

[3\. Project Deliverables	11](#3.-project-deliverables)

[4\. Responsibility Assignments	12](#4.-responsibility-assignments)

[5\. Project Communications	13](#5.-project-communications)

[6\. Configuration Management	14](#6.-configuration-management)

[6.1 Document Management	14](#6.1-document-management)

[6.2 Source Code Management	14](#6.2-source-code-management)

[6.3 Tools & Infrastructures	14](#6.3-tools-&-infrastructures)

[**III. Research Direction and Conceptual Framework	15**](#iii.-research-direction-and-conceptual-framework)

[1\. Research Direction and Conceptual Framework	15](#1.-research-direction-and-conceptual-framework)

[1.1 Gap Positioning	15](#1.1-gap-positioning)

[1.2 Proposed Research Concept & Solution Vision	15](#1.2-proposed-research-concept-&-solution-vision)

[1.3 Strategic Research Approach Selection	16](#1.3-strategic-research-approach-selection)

[2\. Research Feasibility & Resource Assessment	16](#2.-research-feasibility-&-resource-assessment)

[3\. Expected Research Outcomes	17](#3.-expected-research-outcomes)

[4\. Responsibility Assignments	17](#4.-responsibility-assignments-1)

[5\. Research Roadmap & Milestone Planning	17](#5.-research-roadmap-&-milestone-planning)

[6\. Configuration Management	18](#6.-configuration-management-1)

[6.1 Dataset Management	18](#6.1-dataset-management)

[6.2 Source Code Management	18](#6.2-source-code-management-1)

**ABBREVIATIONS**

A list of abbreviations and acronyms used in the thesis, with their full forms.

| Acronym | Meaning |
| ----- | ----- |
| UAV | Unmanned Aerial Vehicle |
| UAV-PMS | Project abbreviation for the Integrated UAV-AI Inspection and Maintenance Management System for Electrical Infrastructure |
| SRS | Software Requirements Specification |
| WBS | Work Breakdown Structure |
| CF | Common Function |
| MF | Main Flow |
| GIS | Geographic Information System |
| AI | Artificial Intelligence |
| YOLO | You Only Look Once |
| DETR | DEtection TRansformer |
| mAP | mean Average Precision |
| API | Application Programming Interface |
| CI | Continuous Integration |
| P0/P1 | Priority 0 / Priority 1 test cases |

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

The team develops a prototype integrating UAV inspection coordination, edge-assisted alerts, server-side defect analysis, GIS-based visualization, human validation and maintenance follow-up. Publicly documented electricity-sector use cases provide domain context; they do not constitute confirmed requirements or formal sponsorship from EVN or any affiliated organization.

The working internal target remains 30 November 2026\. Development is planned to reach feature freeze by 15 November, followed by integration, regression, safety checks and defect fixing through 30 November. The December 2026–March 2027 period remains available for supervisor feedback, academic reporting and defense preparation. These are planning targets, not claims of completed work.

### **1.1 Scope & Estimation** {#1.1-scope-&-estimation}

The scope is organized around common functions (CF) and four main flows (MF01–MF04). Table 1 provides a function-level Work Breakdown Structure estimate of 99 person-days for the defined product functions. Table 3 provides an overall activity-based project budget of 145 person-days, covering requirements analysis, design, implementation, AI research, UAV engineering, testing, and project management. These estimates serve different planning purposes and must not be added together. The overall project capacity and schedule are tracked against the 145 person-day activity budget.

| \# | WBS Item | Complexity | Est. Effort (person-days) |
| :---- | :---- | :---- | :---- |
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
| MF03-03 | Stream, display and store UAV telemetry | Complex | 5 |
| MF03-04 | Capture camera evidence | Complex | 5 |
| MF03-05 | Run the deployed YOLO11s edge model during inspection to detect suspected critical anomalies and send an alert with the captured image, timestamp and confidence score | Complex | 4 |
| MF03-06 | Submit field incident report | Medium | 2 |
| MF03-07 | Store flight logs and mission-completion evidence | Simple | 1 |
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

***Table 1\.** UAV-PMS Function-level Work Breakdown Structure Estimate.*

### **1.2 Project Objectives** {#1.2-project-objectives}

The objective is to complete a working UAV-assisted inspection system from mission planning to AI result review. The team plans 145 person-days (1,160 hours) from 5 September to 30 November 2026 (87 calendar days). This equals about 3.3 hours per member per calendar day for four students. The 145 person-day activity budget is used for overall capacity and schedule tracking. The 99 person-day estimate in Table 1 is a separate function-level estimate for the defined product scope; it is not added to the overall activity budget. 

| Dimension | Objective | Metric / Acceptance Target | Due Date |
| :---- | :---- | :---- | :---- |
| Scope | MF01–MF04 demonstrate core role/state-controlled workflows; evidence links retained | 15–30 Nov 2026 | Scope |
| Quality | P0/P1 integration and regression cases executed; critical defects resolved or explicitly documented | 30 Nov 2026 | Quality |
| AI | Versioned raw/curated data; reproducible model comparisons; per-class error analysis | Research milestone / revised as needed | AI |
| UAV | Bench verification and controlled flight evidence subject to site permission and safety readiness | By 30 Nov if feasible | UAV |
| GIS | Demonstrate asset/route and defect workflow using clearly labeled sample spatial data; import interface if available | System testing | GIS |
| Emergency | Device-originated alerts can reach a configured duty manager without requiring an approved mission | Integration testing | Emergency |
| Cost | Track actual effort and expenses weekly | Weekly | Cost |

***Table 2\.** UAV-PMS objectives.*

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

***Table 3\.** Overall Activity-based Project Effort Budget.*

### 

### **1.3 Project Risks** {#1.3-project-risks}

| \# | Risk Description | Impact | Probability | Response Plan |
| :---- | :---- | :---- | :---- | :---- |
| 1 | AI data has too few samples or very small defects | High | High | Check labels and class counts; add suitable augmentation and report weak classes clearly. |
| 2 | Dataset curation, six-model benchmarking or structural reasoning experiments exceed the planned effort | High | High | Prioritize a reproducible YOLO11/YOLO26/RT-DETR baseline and the raw-versus-curated comparison. Treat structural reasoning as an optional extension, subject to time and evaluation results. |
| 3 | Raspberry Pi, camera or telemetry is unstable | High | Medium | Test each part on the bench before connecting the full system; lower resolution or rate if needed. |
| 4 | Soldering, power or vibration causes UAV failure | Very High | Medium | Check wiring and balance, test without propellers first and prepare spare parts. |
| 5 | Manual flight, failsafe or site conditions cause an unsafe test | Very High | Medium | Use a pre-flight checklist, geofence, observer and abort plan; stop the test when site or weather conditions are unsafe. |
| 6 | Frontend, backend, AI and UAV use different data formats | High | Medium | Agree on request/result examples and test interfaces early. |
| 7 | The team misses the 30 November deadline | Very High | High | Review progress weekly, finish all development by 15 November, use 16-26 November only for testing and defect fixing, and freeze the release on 27 November. |
| 8 | A member is unavailable for a specialist task | Medium | Medium | Share setup notes and let one other member support each important task. |

***Table 4\.** UAV-PMS risk register.*

## **2\. Management Approach** {#2.-management-approach}

The team uses Iterative and Incremental Development. The scope is mostly fixed, but AI and UAV work needs repeated testing. Therefore, the project is divided into short stages, and each stage ends with a working result that the team can review.

• The leader updates the plan, assigns work and records important changes.

• Members report progress and problems in the weekly meeting.

• Changes to scope, interface, safety or deadline must be discussed before implementation.

• The team freezes features on 15 November, uses 16-26 November for system testing, regression and defect fixing, freezes the release on 27 November, checks it on 28-29 November and completes it on 30 November 2026\. 

### **2.1 Project Process** {#2.1-project-process}

| Iteration 0 \- Plan and Baseline  \|  05 Sep-10 Oct Focus: Feature estimates, SRS draft, architecture, interfaces, dataset and hardware plans Exit gate: Requirements and interface baseline approved for implementation |
| :---: |

↓  Review, evidence and controlled feedback  ↓

| Iteration 1 \- Core Foundations \| 11-25 Oct Focus: MF01-MF02, web/backend foundation, YOLO11s baseline and UAV bench assembly  Exit gate: MF01-MF02 integration candidate; reproducible AI baseline; powered UAV bench test  |
| :---: |

↓  Review, evidence and controlled feedback  ↓

| Iteration 2 \- Field and Research  \|  26 Oct-08 Nov  Focus: MF03, camera/telemetry, controlled manual flight, SDI/SCI experiments and per-class diagnosis  Exit gate: Field-data path demonstrated; controlled flight and SDI/SCI evidence reviewed  |
| :---: |

↓  Review, evidence and controlled feedback  ↓

| Iteration 3 \- Advanced Integration \| 09-15 Nov  Focus: MF04, StructDETR candidate, analyst review, waypoint route and cross-system integration  Exit gate: Feature freeze on 15 November; all four flows integrated; waypoint-flight and AI candidate evidence available  |
| :---: |

↓  Review, evidence and controlled feedback  ↓

| Iteration 4 \- Test, Fix and Accept \| 16-30 Nov  Focus: MF01-MF04 system and regression testing, defect fixing, field re-validation, final AI evaluation, SRS finalization and release Exit gate: Release frozen on 27 November; 30 November completion criteria accepted; no open Severity 1/2 defect  |
| :---: |

↓  Review, evidence and controlled feedback  ↓

| Contingency and Defense  \|  01 Dec-Mar 2027 Focus: Supervisor-requested changes, extended validation, reports, demo and defense rehearsal Exit gate: Academic submission and defense package approved |
| :---: |

***Figure 1\. UAV-PMS iterative and incremental development process***

### **2.2 Quality Management** {#2.2-quality-management}

| Quality Approach | Application to UAV-PMS | Exit Evidence / Criterion |
| :---- | :---- | :---- |
| Defect Prevention | Agree on acceptance criteria, API formats, coding rules and UAV safety checks before work starts. | Checklist or requirement is available before implementation. |
| Reviewing | Another member reviews important SRS, code, test cases, AI results and flight evidence. | Review comments are fixed or explained. |
| Unit Testing | Test backend rules, validators and important frontend services/components. | Required unit tests pass before merge. |
| Integration Testing | Test frontend-backend, service-database, AI callback and UAV telemetry connections. | Data is sent and received in the expected format. |
| System Testing | Run the main MF01-MF04 scenarios using the correct roles and mission states. | P0/P1 cases pass and no Severity 1/2 defect remains. |
| AI Evaluation | Compare YOLO11/YOLO26/RT-DETR, data curation and structural reasoning on the same split and check errors by class. | Metrics, error analysis and selected-model reason are recorded. |
| UAV Verification | Check wiring, calibration, geofence and failsafe before controlled and waypoint flights. | Checklist, telemetry, camera and flight evidence are saved. |

***Table 5\.** Quality-management approach and evidence.*

### 

### **2.3 Training Plan** {#2.3-training-plan}

| Training Area | Participants | When / Duration | Waiver Criteria |
| :---- | :---- | :---- | :---- |
| SRS, traceability and Git | All members | 05 Sep-10 Oct; two 90-minute sessions | Waived after completing one reviewed requirement and pull request. |
| Angular, GIS and API integration | Nguyễn Quốc Khánh; Nguyễn Nhật An | By 15 Oct; two 2-hour sessions  | Waived after completing one reviewed integrated screen. |
| ASP.NET Core and messaging | Nguyễn Nhật An; Phạm Hoàng Minh Châu | By 15 Oct; two 2-hour sessions  | Waived after completing one reviewed service change. |
| UAV assembly and flight safety | Trần Ánh Quốc | Before flight; one 3-hour bench session | No waiver for a member joining a live flight. |
| Raspberry Pi, camera and waypoint flight | Trần Ánh Quốc; Nguyễn Nhật An | By 31 Oct; two 2-hour sessions  | Waived after a successful bench or simulated mission. |
| YOLO11s, SDI/SCI, StructDETR and error analysis | Phạm Hoàng Minh Châu | 11 Oct-08 Nov; four 2-hour sessions  | Waived after reproducing one experiment and explaining its result. |

***Table 6\.** UAV-PMS training plan.*

## **3\. Project Deliverables** {#3.-project-deliverables}

| \# | Main Deliverable | Deliverable Type | Planned Period / Due Date |
| :---- | :---- | :---- | :---- |
| 1 | Report 1 \- Project Introduction | External | Completed project baseline |
| 2 | Report 2 \- Project Management Plan | External | 24/09/2026 |
| 3 | SRS Working Baseline and Interface Contracts | Internal / Baseline | 05/09-10/10/2026 |
| 4 | MF01-MF02 Integration and Test Report | Internal | 11/10-25/10/2026 |
| 5 | Dataset Audit and YOLO11/YOLO26/RT-DETR Baseline Report | Internal / Research | 11/10-25/10/2026 |
| 6 | UAV Bench Assembly and Safety Checklist | Internal / Engineering | 11/10-25/10/2026 |
| 7 | MF03 and Camera/Telemetry Integration | Internal | 26/10-08/11/2026 |
| 8 | Raw-versus-Curated Benchmark and Per-Class Error Report | Internal / Research | 26/10-08/11/2026 |
| 9 | MF04 Integration and Structural Reasoning Feasibility Assessment | Internal / Research | 01/11-15/11/2026 |
| 10 | Waypoint Flight Evidence | Internal / Validation | 09/11-15/11/2026 |
| 11 | System Test, Regression and Defect-Fix Report | Internal / Validation | 16/11-30/11/2026 |
| 12 | Final SRS, Integrated System and User Guide | External / Baseline | Due 30/11/2026 |
| 13 | Final Report, Demo and Defense Package | External | By the official schedule |

***Table 7\.** UAV-PMS master deliverable schedule.*

## **4\. Responsibility Assignments** {#4.-responsibility-assignments}

D \- Do; R \- Review; S \- Support; I \- Informed; blank \- omitted

| Responsibility | Nguyễn Quốc Khánh (SE193464) | Nguyễn Nhật An (SE193338) | Trần Ánh Quốc (SE181995) | Phạm Hoàng Minh Châu (SE193418) |
| :---- | :---- | :---- | :---- | :---- |
| Project leadership, schedule, risk and stakeholder coordination | D | S | S | S |
| Business analysis, SRS and requirement traceability | D | S | S | R |
| Frontend architecture, UX and MF01-MF04 screens | D | D | I | S |
| Backend services, database and API contracts | R | D | S | S |
| MF01-MF02 full-stack implementation | R | D | I | S |
| MF03 field workflow and system integration | R | D | S | S |
| UAV hardware assembly, soldering and power integration | R | S | D | I |
| Raspberry Pi, camera, telemetry and autonomous route | R | S | D | I |
| AI dataset curation, six-model baseline and experiment protocol | R | I | S | D |
| Raw-versus-curated benchmarking and per-class diagnosis | R | I | S | D |
| Structural reasoning extension and ablation planning | R | I | S | D |
| MF04 AI-service, analyst review and result integration | R | D | S | D |
| Unit, integration and system testing | R | D | S | D |
| UAV safety and flight validation | R | I | D | S |
| Release, documentation and demonstration package | D | S | S | S |

***Table 8\.** UAV-PMS responsibility assignment matrix.*

The Project Leader confirms task-level ownership and resolves conflicts. D identifies the member directly producing the output; R independently reviews or accepts it; S provides technical support; I receives status or decisions. Two D entries are used only where a deliverable genuinely requires joint ownership across disciplines.

## **5\. Project Communications** {#5.-project-communications}

| Communication Item | Who / Target | Purpose | When / Frequency | Type, Tool and Method |
| :---- | :---- | :---- | :---- | :---- |
| Internal Planning and Review | All members | Review progress, evidence, blockers, individual ownership and the next iteration. | Weekly; twice weekly from 26 October to 15 November; daily short check-in from 16 to 30 November 2026  | Meeting; task board; minutes; GitHub Issues |
| Supervisor Meeting | Team; Đặng Ngọc Minh Đức; Phạm Minh Trí | Review scope, requirements, technical direction, risk and milestone readiness. | Weekly and before formal gates | Meeting; Word/Google Docs; slides; evidence links |
| Requirement and Change Review | Leader, affected owners and reviewers | Approve requirement clarification and assess schedule/interface/test impact. | Before an affected baseline changes | Google sheet |
| Code and Interface Review | Software owners and one peer reviewer | Verify correctness, contract compatibility, security and test evidence. | For every pull request | GitHub pull request; CI/build results |
| AI Experiment Review | Phạm Hoàng Minh Châu; Trần Ánh Quốc; leader | Review data version, configuration, per-class results, failure modes and next experiment. | Weekly during AI work; after each experiment block | Google docs |
| UAV Engineering and Flight Review | Trần Ánh Quốc; leader; flight participants | Review assembly, safety, configuration, weather/site, route and abort criteria. | Before every powered or flight test | Google sheet \+ Google Drive |
| Defect and Test Triage | Relevant owner, tester and leader | Classify defects, assign action, decide release impact and verify closure. | After each integration/system-test session | Issue tracker; test report; retest evidence |
| Milestone Demonstration | Team, supervisors and reviewers | Demonstrate accepted scope and record decisions or corrective actions. | At each integration gate and formal review | Live demo; report; slides; signed minutes |

***Table 9\.** UAV-PMS project communication plan.*

## **6\. Configuration Management** {#6.-configuration-management}

### **6.1 Document Management** {#6.1-document-management}

Project documents are stored in a shared Drive folder with separate folders for reports, SRS, design, test evidence, AI, UAV and meeting notes. The editable file and submitted PDF are stored together.  
• File naming: UAV-PMS\_\<Artifact\>\_v\<major.minor\>\_\<YYYYMMDD\>.\<extension\>.  
• Small updates increase the minor version; an approved submission increases the major version.  
• The leader or document owner uploads the approved version. Older versions are moved to Archive instead of being deleted.

### **6.2 Source Code Management** {#6.2-source-code-management}

Source code is stored in GitHub repositories for backend, frontend, AI and UAV/Raspberry Pi work. The main branch contains the latest stable integrated version.  
• Each task uses a short feature or fix branch and is merged by pull request.  
• At least one other member reviews the pull request and relevant tests must pass.  
• API, telemetry and AI-result format changes are updated in both producer and consumer code.  
• Large datasets and model files are stored outside normal Git history; secrets are never committed.  
• A working release is tagged before a milestone demo or submission.

### **6.3 Tools & Infrastructures** {#6.3-tools-&-infrastructures}

| Category | Tools / Infrastructure |
| :---- | :---- |
| Frontend | Angular 21, TypeScript, Tailwind CSS, Leaflet/MapLibre and SignalR client |
| Backend and Services | ASP.NET Core 9, Microservices \+ Clean architecture /CQRS, Ocelot API Gateway, gRPC, SignalR and background services |
| Database and Geospatial | PostgreSQL/PostGIS and EF Core migrations |
| Messaging and Cache | RabbitMQ, MQTT/Mosquitto and Redis |
| AI Research | Python, PyTorch/Ultralytics YOLO11/YOLO26/RT-DETR, structural reasoning, OpenCV and versioned experiment configurations |
| UAV and Edge | UAV airframe/electronics, soldering and measurement tools, flight controller, Raspberry Pi, camera, GPS/telemetry and waypoint-planning software |
| Testing | xUnit/.NET test, API regression tests, Angular/Vitest, Playwright, manual system tests, AI evaluation scripts and UAV bench/flight checklists |
| Deployment and CI | Docker/Docker Compose, GitHub Actions and environment-based configuration |
| Development | JetBrains Rider, Visual Studio and Visual Studio Code, Antigravity, Codex |
| Diagramming and Documentation | Draw.io, Microsoft Word, Google Docs/Sheets and shared Drive storage, ProjectLibre |
| Version and Work Tracking | GitHub repositories, Google Drive |

***Table 10\.** UAV-PMS tools and infrastructure.*

# 

# **III. Research Direction and Conceptual Framework** {#iii.-research-direction-and-conceptual-framework}

## **1\. Research Direction and Conceptual Framework** {#1.-research-direction-and-conceptual-framework}

The research investigates data quality and detector performance for multi-class power-line defect detection in UAV RGB images. Its primary direction is a data-centric benchmark integrating multi-source annotations, systematic quality curation, and controlled comparison of YOLO11, YOLO26 and RT-DETR models. Structural reasoning is considered a subsequent experimental extension, subject to feasibility and ablation evidence.  
The baseline study compares raw and curated training data across six detectors (YOLO11n/m, YOLO26n/m and RT-DETR-R50/R101), using a consistent evaluation protocol and held-out test set. Performance is assessed using precision, recall, F1, mAP, per-class errors, inference latency and resource requirements. Proposed structural constraints will be tested separately rather than assumed to improve results.  
The overall research flow is:

* Multi-source dataset integration and taxonomy mapping  
* Annotation audit, deduplication and quality curation  
* Raw versus curated detector benchmarking  
* Per-class, localization and noise error analysis  
* Optional structural reasoning and ablation  
* Deployment-oriented evaluation and reporting

The study aims to distinguish improvements attributable to data quality from those attributable to model choice, and to document practical trade-offs for UAV edge and server-side analysis.

### **1.1 Gap Positioning** {#1.1-gap-positioning}

Public UAV power-line datasets vary in class definitions, annotation coverage and box quality. Combining them without controlled mapping and verification can introduce label inconsistencies, duplicate imagery and misleading benchmark results.

Small objects, damaged conductor strands, nests and foreign objects are especially sensitive to scale, clutter, localization noise and missing labels. These issues motivate a dataset-quality study alongside architecture comparison.

The initial six-class taxonomy covers insulator, insulator\_broken, insulator\_flashover, bird\_nest, foreign\_object and broken\_strand. Additional sources are candidates for expansion, not yet part of the verified baseline count.

The research gap addressed here is the limited controlled evidence separating the effect of annotation curation from model scaling on a harmonized power-line inspection benchmark. Structural relationships among assets and defects form an exploratory follow-up question.

### **1.2 Proposed Research Concept & Solution Vision** {#1.2-proposed-research-concept-&-solution-vision}

The proposed framework uses multi-source ingestion, class harmonization, annotation auditing, duplicate control, human review and selective box refinement before reproducible model training and evaluation.

The proposal baseline comprises 21,767 images across six classes, with 17,483 training, 2,272 validation and 2,012 held-out test images. These counts describe the existing proposal baseline and will change only after a documented dataset revision.

Candidate expansion sources include Roboflow Transmission Detection (approximately 1,393 images as currently reported by the team), the Power Equipment Image Dataset (sxiong), MPCD and Open Images V7 with synthetic-mask-based foreign-object examples. Inclusion depends on licensing, annotation validation, class mapping and cross-source deduplication.

After the controlled benchmark, the team may investigate structural reasoning based on relationships among conductors, insulators, towers and defects. Such experiments require explicit hypotheses, baselines and ablations and are not presented as completed contributions.

The expected solution can be summarized as:

* UAV RGB images and source annotations  
* Schema harmonization and dataset quality audit  
* Raw/curated YOLO11, YOLO26 and RT-DETR training  
* Held-out evaluation and error diagnosis  
* Optional structural reasoning and deployment analysis

The intended result is a traceable dataset-and-model evaluation, with deployment suitability assessed separately for resource-limited edge hardware and server processing.

### **1.3 Strategic Research Approach Selection** 	 {#1.3-strategic-research-approach-selection}

The core approach is a controlled data-centric comparative experiment, followed by an optional model-structure extension.

Six baseline detectors are evaluated across two training-data conditions, raw and curated, with consistent splits and documented training settings. The frozen test set is reserved for final comparison.

Dataset integration and audit precede model training. Benchmark results are analyzed by class, error type, latency and compute cost. Structural reasoning experiments, if undertaken, will use controlled ablations and will not replace the primary benchmark.

This design prioritizes reproducibility and avoids attributing gains to architectural changes when the training annotations also changed.

## **2\. Research Feasibility & Resource Assessment** {#2.-research-feasibility-&-resource-assessment}

The study is feasible as a staged experiment using publicly available image datasets, established detector implementations and reproducible scripts; additional data and structural reasoning remain conditional work packages.

The existing proposal integrates six public sources. New candidate datasets require verification of redistribution terms, annotation quality, class correspondence and near-duplicate overlap before inclusion.

GPU platforms such as Kaggle or other available computing resources can be used for model training. Model development, evaluation, and result analysis can also be performed using Python and common computer vision libraries.

GPU availability and training time may limit the full experiment matrix. The team will prioritize the fixed raw/curated baseline and document any deferred structural reasoning experiments.

Model-assisted label auditing can introduce false corrections; proposed additions, removals and SAM-assisted box refinements therefore require explicit rules, sampling checks and versioned human review.

## **3\. Expected Research Outcomes**	 {#3.-expected-research-outcomes}

Expected outcomes include a harmonized and versioned six-class benchmark, a documented annotation-curation protocol, and a controlled raw-versus-curated comparison across YOLO11n/m, YOLO26n/m and RT-DETR-R50/R101.

Evaluation will report precision, recall, F1, mAP50, mAP50-95, per-class failure modes, localization errors, latency and memory or compute trade-offs, with limitations clearly stated.

Potential extensions include vetted new data sources and structural reasoning modules. Their value will be assessed through separate controlled experiments rather than presumed in advance.

Deliverables comprise dataset manifests and version records, curation scripts, experiment configurations, checkpoints where feasible, comparative results, error analysis and research documentation.

## **4\. Responsibility Assignments** {#4.-responsibility-assignments-1}

| Task | Main Responsibility | Owner |
| ----- | ----- | ----- |
| Dataset preparation | Collect, organize, clean, and verify image datasets and annotations | Phạm Hoàng Minh Châu |
| Model research | Review YOLO, DETR, and related object detection methods | Phạm Hoàng Minh Châu |
| Model training | Prepare training environments and train baseline models | Phạm Hoàng Minh Châu |
| Model improvement | Test data curation and structural reasoning modifications | Phạm Hoàng Minh Châu |
| Evaluation | Calculate Precision, Recall, F1-score, mAP50, mAP50-95, per-class results and inference time | Phạm Hoàng Minh Châu |
| Result analysis | Compare models and analyze detection errors | Phạm Hoàng Minh Châu |
| Documentation | Prepare research report, figures, tables, and presentation | Phạm Hoàng Minh Châu |
| System integration | Prepare the trained model for later UAV or application integration | Phạm Hoàng Minh Châu |

***Table 11\.** Responsibility Assignments table.*

## **5\. Research Roadmap & Milestone Planning** {#5.-research-roadmap-&-milestone-planning}

| Stage | Main Activities | Expected Result | Period |
| ----- | ----- | ----- | ----- |
| Stage 1 | Literature review and dataset study | Research background and selected datasets | 05/09-30/09/2026 |
| Stage 2 | Dataset preparation and preprocessing | Clean, versioned dataset with a fixed train/validation/test split | 01/10-10/10/2026 |
| Stage 3 | Baseline model training | Reproducible baseline results for selected YOLO11, YOLO26 and RT-DETR models | 11/10-25/10/2026 |
| Stage 4 | Raw-versus-curated controlled benchmarking and error analysis | Preliminary 2×6 comparison and per-class error report | 26/10-08/11/2026 |
| Stage 5 | Structural reasoning feasibility and optional extension | Proposed structural constraints and evaluation plan, subject to feasibility | 01/11-15/11/2026 |
| Stage 6 | Final evaluation | Final comparison and selected model | 16/11-22/11/2026 |
| Stage 7 | Documentation and presentation | Final report and research presentation | 23/11-30/11/2026 |

***Table 12\.** Research Roadmap.*

Research milestones are planning targets. Dataset expansion and structural reasoning may continue into the academic reporting period if the baseline comparison is complete and additional experiments remain feasible.

## **6\. Configuration Management** {#6.-configuration-management-1}

* Configuration management is used to keep the research experiments organized and reproducible.  
* Because different datasets, model versions, training parameters, and checkpoints may be used during the project, each experiment should be recorded clearly.  
* Important information such as model name, dataset version, image size, batch size, number of epochs, learning rate, evaluation results, and checkpoint location will be stored for each main experiment.  
* This helps the team compare experiments and avoid losing important configurations during the research process.

### **6.1 Dataset Management** {#6.1-dataset-management}

* Datasets used in the research will be organized into separate versions.  
* Each dataset version records source, usage rights, taxonomy mapping, annotation decisions, deduplication results and split membership; proposed new sources are tracked separately until accepted.  
* When changes are made to the dataset, such as removing invalid annotations or adding new images, a new dataset version will be created instead of replacing the previous version directly.  
* Near-duplicate checks must cover sources and partitions to reduce leakage; the held-out test set is not used to tune curation decisions or model hyperparameters.  
* Dataset statistics such as the number of images, number of annotations, and number of samples per class will also be recorded.  
* This allows experiments using different dataset versions to be compared more clearly.

### **6.2 Source Code Management** {#6.2-source-code-management-1}

* The project source code will be managed using Git and GitHub.  
* Important changes in training code, model configurations, preprocessing scripts, and evaluation scripts will be recorded through commits.  
* Separate branches may be used when testing major model modifications. After the experiment is checked, useful changes can be merged into the main development branch.  
* Large files such as trained model weights and datasets will not be stored directly in the main Git repository. They will be stored separately, while their version and location are recorded in the project documentation.  
* This approach helps the team maintain the source code, track changes, and reproduce previous experiments when needed.

[image1]: <data:image/png;base64,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>