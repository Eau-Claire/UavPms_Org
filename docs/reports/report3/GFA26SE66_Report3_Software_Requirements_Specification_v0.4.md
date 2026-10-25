![][image1]

**CAPSTONE PROJECT REPORT**

**Report 3 – Software Requirements Specification**

Project: Integrated UAV-AI Inspection and Maintenance Management System for Electrical Infrastructure (UAV-PMS)

Group: GFA26SE66

– Ho Chi Minh City, October 2026 –

<div style="page-break-after: always;"></div>

**Table of Contents**

- [I. Record of Changes](#record-of-changes)
- [II. Software Requirements Specification](#srs)
  - [1. Product Overview](#product-overview)
  - [2. User Requirements](#user-requirements)
    - [2.1 Actors](#actors)
    - [2.2 Use Cases](#use-cases)
  - [3. Functional Requirements](#functional-requirements)
    - [3.1 System Functional Overview](#system-functional-overview)
    - [3.2 MF01 – Pre-Mission Feasibility and Resource Readiness](#mf01)
    - [3.3 MF02 – Create and Assign Inspection Mission](#mf02)
    - [3.4 MF03 – Mission Execution and Field Inspection](#mf03)
    - [3.5 MF04 – AI Inspection and Defect Detection](#mf04)
    - [3.6 Common Functions](#cf)
  - [4. Non-Functional Requirements](#non-functional-requirements)
    - [4.1 External Interfaces](#external-interfaces)
    - [4.2 Quality Attributes](#quality-attributes)
  - [5. Requirement Appendix](#requirement-appendix)
    - [5.1 Business Rules](#business-rules)
    - [5.2 Common Requirements](#common-requirements)
    - [5.3 Application Messages List](#application-messages)
    - [5.4 Other Requirements](#other-requirements)
- [III. Literature Review](#iii.-literature-review)
- [IV. References](#iv.-references)

**ABBREVIATIONS**

| Acronym | Meaning |
| :------- | :------------------------------------ |
| AI | Artificial Intelligence |
| AMQP | Advanced Message Queuing Protocol (used by RabbitMQ) |
| API | Application Programming Interface |
| BR | Business Rule identifier (Section 5.1) |
| CF | Common Function identifier (Section 3.6) |
| CR | Common Requirement identifier (Section 5.2) |
| DETR | DEtection TRansformer |
| DLQ | Dead-Letter Queue |
| ERD | Entity Relationship Diagram |
| EVN | Vietnam Electricity |
| EXIF | Exchangeable Image File Format (image metadata) |
| FC | Flight Controller |
| GIS | Geographic Information System |
| gRPC | Google Remote Procedure Call (internal service call over HTTP/2) |
| JWT | JSON Web Token |
| mAP | mean Average Precision |
| MF | Main Flow identifier; MF01–MF04 name the four project main flows |
| MQTT | Message Queuing Telemetry Transport |
| MSG | Application message identifier (Section 5.3) |
| NFR | Non-Functional Requirement |
| NS | Non-Screen function identifier (Section 3.1.4) |
| OTP | One-Time Password |
| RPO / RTO | Recovery Point Objective / Recovery Time Objective |
| SCR | Screen identifier (Section 3.1.2) |
| SDI / SCI | The two YOLO11s-based improved detection models evaluated in the AI research |
| SRS | Software Requirements Specification |
| TLS | Transport Layer Security |
| UAV | Unmanned Aerial Vehicle |
| UAV-PMS | Integrated UAV-AI Inspection and Maintenance Management System for Electrical Infrastructure |
| UC | Use Case identifier |
| UID | Unique Identifier (flight-controller hardware ID) |
| UTC | Coordinated Universal Time |
| WBS | Work Breakdown Structure (Report 2, Table 1) |
| WKT | Well-Known Text (geometry format) |
| YOLO | You Only Look Once |

<div style="page-break-after: always;"></div>

# I. Record of Changes {#record-of-changes}

| Date | A\*, M, D | In charge | Change Description |
| :---- | :---- | :---- | :---- |
| 24/09/2026 | A | Nguyễn Quốc Khánh | Created SRS draft v0.1 from Report 1, MF01–MF04, the supplied use-case diagrams and the verified backend behaviour. |
| 07/10/2026 | M | SE193338 – Nguyễn Nhật An | Unified actor names; redrew the context, use-case, screen-flow and ER diagrams; renumbered use cases and aligned them with the Report 2 WBS; added Common Functions. |
| 07/10/2026 | A | SE193338 – Nguyễn Nhật An | Added the screen list, screen authorization, non-screen functions, entity descriptions and detailed use-case specifications. |
| 07/10/2026 | M | SE193338 – Nguyễn Nhật An | Revised NFRs to measurable targets, aligned AI rules with the seven-class dataset and the YOLO11s/SDI/SCI/StructDETR models, reorganized business rules and messages, and added traceability and state tables. |
| 14/10/2026 | M | SE193338 – Nguyễn Nhật An | v0.3 – Aligned the SRS with the implemented backend (commit 124d764): microservices, six system roles, e-mail/OTP login, Region–Substation–Line–Tower–Asset hierarchy, UAV status model and flight-controller pairing, automatic assessment evaluation with GIS suitability, combined create-and-dispatch of missions, mission/assignment/booking states, MQTT topics, media ingestion with EXIF, Vision Bridge, RabbitMQ AI pipeline with gRPC severity evaluation, analyst review with automatic maintenance tickets, reports and system settings. |
| 14/10/2026 | D | SE193338 – Nguyễn Nhật An | v0.3 – Removed username login and account lockout, the Needs Review assessment result and override, automatic weather thresholds, flight altitude/speed parameters, the Field Completed state, the assignment Expired state, confidence-based auto-acceptance/discarding, Edge-Device metric import and the technical-inspection expiry job. |
| 24/10/2026 | D | SE193338 – Nguyễn Nhật An | v0.4 – Removed the Asset Health feature from the scope: asset health index, risk level, health history (CONDITION_HISTORY), health penalty on confirmed defects and health recovery on ticket closure. SCR-07 is now the Grid Asset Catalogue. |
| 24/10/2026 | A | SE193338 – Nguyễn Nhật An | v0.4 – Added the use-case specifications of MF02 (Section 3.3), MF03 (Section 3.4), MF04 (Section 3.5) and the Common Functions (Section 3.6). |
| 24/10/2026 | M | SE193338 – Nguyễn Nhật An | v0.4 – Reformatted the whole report (headings, tables, captions, numbering); re-aligned MF01, business rules, messages, configuration parameters and state tables with the backend (commit 7a90ab8); marked UC-CF-03 and UC-MF03-04 as deferred. |
| 24/10/2026 | M | SE193338 – Nguyễn Nhật An | v0.5 – Redrew the screens flow (Figure 8) and rewrote the screen descriptions and authorization matrix from the current web-client routes and role guards; replaced formula-level wording in BR-17, BR-21, BR-23, BR-53 and UC-MF01-02/05 with plain descriptions; aligned role, state and notification statements with the backend. |

\*A – Added, M – Modified, D – Deleted

<div style="page-break-after: always;"></div>

# II. Software Requirements Specification {#srs}

## 1. Product Overview {#product-overview}

UAV-PMS is a web-based system that supports UAV-assisted inspection of electrical infrastructure for the EVN southern business unit described in Report 1. The system manages an inspection from the pre-mission readiness assessment, through mission assignment and field execution, to AI-based defect detection, analyst review and maintenance follow-up.

The product covers four main flows and a set of common functions:

- **MF01 – Pre-Mission Feasibility and Resource Readiness.** Defines the inspection scope (region, target assets and an optional boundary) and the planned window, then automatically evaluates site feasibility, personnel eligibility and UAV readiness (operational availability, technical health and GIS scope suitability). The result is Ready or Not Ready.
- **MF02 – Create and Assign Inspection Mission.** Creates a mission from a Ready assessment with priority and objective, assigns eligible personnel and a UAV, reserves them with resource bookings, dispatches the mission and tracks each assignee’s acceptance or postponement. Supports later changes: cancel, suspend, resume, postpone, reassign personnel, change the UAV and change the priority.
- **MF03 – Mission Execution and Field Inspection.** Supports field check-in, UAV handover and return, mission start and completion, live UAV status through MQTT, inspection media upload with EXIF metadata, flight logs, field incident reports and edge AI emergency alerts.
- **MF04 – AI Inspection and Defect Detection.** Sends every uploaded media file to the AI service through RabbitMQ, stores detections with category, bounding box and confidence, evaluates severity and priority through the internal Inspection Evaluation service, supports analyst review and reanalysis, creates maintenance tickets for confirmed defects and produces defect reports (PDF/Excel).
- **Common Functions (CF).** Login with OTP, user accounts and geographic scope, the infrastructure asset catalogue, the UAV inventory and flight-controller pairing, notifications, the audit log and system settings.

UAV-PMS uses two AI processing modes, consistent with Report 1 (FE-03) and Report 2 (MF03-06, MF04-04 to MF04-07):

- **Edge/Fast mode.** A YOLO-based model on the UAV’s onboard computer (Raspberry Pi) detects suspected anomalies during flight and sends them to the Vision Bridge API (UC-MF03-07). Analysis requests can also be routed to the edge model queue (preferred model `EDGE` or `YOLO11`).
- **Server/Deep mode.** After upload, the AI service analyses the full-resolution media with the model selected by the AI research (StructDETR candidate; if StructDETR is not stable by 15 November 2026, the best YOLO11s, SDI or SCI model is used) (UC-MF04-02). `SERVER` is the default preferred model.

The backend consists of ASP.NET Core 9 microservices behind an Ocelot API gateway:

| Service | Responsibility |
| :------- | :------------------------------------ |
| Identity Service | Authentication, OTP, trusted devices, refresh tokens, user accounts and roles. |
| Operations Service | Regions, substations, lines, towers, assets, UAVs, assessments, missions, field execution, media, alerts, maintenance, reports, audit log and system settings. |
| AI Inspection Service | AI analysis orchestration (RabbitMQ), result consumption and the Vision Bridge for edge detections. |
| Notification Service | In-app and e-mail notifications, the SignalR hub and the Hangfire background jobs. |
| Inspection Evaluation Service | Internal gRPC service that calculates severity, risk level, priority score and the immediate-alert decision of a detection. |

The services share one PostgreSQL/PostGIS database, use Redis for UAV live state and OTP data, and communicate asynchronously through RabbitMQ.

The system does **not** control the UAV in flight, generate flight waypoints, perform physical repair, certify UAV hardware, calculate long-term asset health or replace aviation licensing and safety procedures. Flight control and flight-condition decisions remain with the flight controller and the pilot.

Figure 1 shows the system boundary, the external entities and the data exchanged with them. Each external entity is described as an actor in Table 1.

![][image2]

**Figure 1.** *UAV-PMS system context.*

## 2. User Requirements {#user-requirements}

### 2.1 Actors {#actors}

UAV-PMS has five human actors and four system actors. All human actors specialize the generic User actor (Figure 2), so they share the functions Log In (UC-CF-01) and View Notifications (UC-CF-06). A user account may hold more than one role; the actor is determined by the role used for the action.

![][image3]

**Figure 2.** *Generalization of human actors.*

**Table 1.** *UAV-PMS actors.*

| # | Actor | System role | Type | Description |
| :---- | :---- | :---- | :---- | :---- |
| 1 | System Administrator | `SystemAdmin` | Human | Manages user accounts, roles, geographic scopes and system settings; has access to every function and region; reviews the audit log. |
| 2 | Mission Manager | `Manager` | Human | Maintains infrastructure assets and the UAV inventory, creates readiness assessments, creates and dispatches missions, changes missions, monitors field execution, requests reanalysis, manages maintenance tickets and approves reports. Limited to the regions in their geographic scope. |
| 3 | Inspector | `Inspector` | Human | Field operator who also acts as the UAV pilot. Responds to assignments, checks in, receives and returns the UAV, starts and completes the mission, uploads media and flight logs and reports field incidents. |
| 4 | Analyst | `Analyst` | Human | Reviews AI detections (confirm or reject), reviews and escalates emergency alerts, requests reanalysis and prepares reports. |
| 5 | UAV Technician | `Technician` (alias `MaintenanceTechnician`) | Human | Records UAV technical inspections, pairs flight controllers with UAVs, maintains UAV records and executes maintenance tickets (status, proof, materials). |
| 6 | Edge Device | – | System | The UAV’s onboard computer (Raspberry Pi with camera, GPS and flight-controller link). Publishes UAV status and telemetry through MQTT and sends edge AI detections with images to the Vision Bridge API. |
| 7 | Cloud AI Service | – | System | Server-side AI inference service (FastAPI). Consumes analysis requests from RabbitMQ and returns processing status and detections through the result queue. |
| 8 | Map Service | – | System | Leaflet with OpenStreetMap tiles; provides the base map for towers, assets, assessment boundaries, mission targets and live UAV positions. |
| 9 | Notification Provider | – | System | E-mail services: SendGrid for notification e-mails and Brevo for OTP and password-reset e-mails. In-app notifications are produced by UAV-PMS itself. |

### 2.2 Use Cases {#use-cases}

#### 2.2.1 Diagrams

The use cases are grouped into five diagrams: the common functions (Figure 3) and one diagram for each main flow (Figure 4 – Figure 7). Each use case is labelled with the identifier used in Table 2 and in Sections 3.2–3.6. A use case reused by another flow is shown in both diagrams with its original identifier; for example, UC-MF02-02 includes UC-MF01-03 in Figure 5.

![][image4]

**Figure 3.** *Use case diagram – Common Functions.*

![][image5]

**Figure 4.** *Use case diagram – MF01 Pre-Mission Feasibility and Resource Readiness.*

![][image6]

**Figure 5.** *Use case diagram – MF02 Create and Assign Inspection Mission.*

![][image7]

**Figure 6.** *Use case diagram – MF03 Mission Execution and Field Inspection.*

![][image8]

**Figure 7.** *Use case diagram – MF04 AI Inspection and Defect Detection.*

#### 2.2.2 Descriptions

Table 2 describes every use case in Figure 3 – Figure 7. The WBS column links each use case to the work item in Report 2, Table 1. The AI model training and evaluation items (MF04-04 to MF04-07) are research work and have no use case. Use cases marked *Deferred* appear in the diagrams but are not part of the current release.

**Table 2.** *Use case descriptions.*

| ID | Use Case | Primary Actor(s) | Secondary Actor(s) | Description | WBS |
| :---- | :---- | :---- | :---- | :---- | :---- |
| UC-CF-01 | Log In | User | Notification Provider | Authenticate with e-mail and password; on an untrusted device verify an e-mailed OTP; reset or change the password. | CF-01 |
| UC-CF-02 | Manage User Accounts | System Administrator | – | Create, update, suspend and delete accounts and assign one or more roles. | CF-02 |
| UC-CF-03 | Manage Personnel Qualifications | System Administrator | – | *Deferred.* Record each person’s qualifications with validity dates. | CF-02 |
| UC-CF-04 | Manage Infrastructure Assets | System Administrator, Mission Manager | Map Service | Maintain regions, substations, lines, towers and asset components with coordinates; import towers from Excel; browse the grid asset catalogue. | CF-03 |
| UC-CF-05 | Manage UAV Inventory | Mission Manager, UAV Technician, System Administrator | – | Maintain UAV records and operational status; pair a flight controller (Betaflight UID) with a UAV. | CF-04 |
| UC-CF-06 | View Notifications | User | – | Receive in-app notifications in real time, mark them as read and open the linked record. | CF-05 |
| UC-CF-07 | View Audit Log | System Administrator, Mission Manager | – | Search audit records of data changes and business actions. | CF-05 |
| UC-CF-08 | Manage System Settings | System Administrator | – | View and change the configurable thresholds (Table 57). | CF-05 |
| UC-MF01-01 | Create Readiness Assessment | Mission Manager | Map Service | Define the region, planned window, target assets and optional boundary; the assessment is evaluated immediately (includes UC-MF01-02). | MF01-01 |
| UC-MF01-02 | Evaluate Pre-Mission Readiness | Mission Manager | – | Run the site, personnel and UAV checks and obtain Ready or Not Ready; re-evaluate an assessment. | MF01-05, MF01-06 |
| UC-MF01-03 | Check Personnel Availability and Eligibility | – (included) | – | Included by UC-MF01-02 and UC-MF02-02. List field personnel who are active, in scope, hold a field role and are not booked in the window. | MF01-02 |
| UC-MF01-04 | Check UAV Availability and Technical Readiness | – (included) | – | Included by UC-MF01-02 and UC-MF02-03. List UAVs that are available, not booked, technically healthy and suitable for the scope. | MF01-03 |
| UC-MF01-05 | Record UAV Technical Inspection | UAV Technician | Edge Device | Record the checklist or metrics, verify the paired flight controller and obtain Passed or Failed with a technical health. | MF01-04 |
| UC-MF01-06 | View Readiness Assessment Result | Mission Manager | – | View the overall result and the reason of each component; cancel the assessment or hand it over to MF02. | MF01-06 |
| UC-MF02-01 | Create Inspection Mission | Mission Manager | – | Create a mission from a Ready assessment with title, priority, objective, personnel and UAV (includes UC-MF02-02, UC-MF02-03, UC-MF02-04). | MF02-01 – MF02-03 |
| UC-MF02-02 | Assign Personnel to Mission | Mission Manager | – | Assign eligible personnel with a mission role and required flag; add, remove or reassign later (includes UC-MF01-03). | MF02-04 |
| UC-MF02-03 | Assign UAV to Mission | Mission Manager | – | Assign an available, healthy and unbooked UAV; change it before the start (includes UC-MF01-04). | MF02-05 |
| UC-MF02-04 | Dispatch Mission | – (included) | Notification Provider | Reserve resources with bookings, set the mission to Pending Acceptance and notify the assignees. | MF02-06 |
| UC-MF02-05 | Preview Mission Targets on Map | Mission Manager, Inspector | Map Service | Display the mission targets in sequence, their towers and the scope boundary on the map. | MF02-07 |
| UC-MF02-06 | View Assigned Mission | Inspector | – | View the scope, schedule, instructions and assigned UAV of own missions. | MF02-08 |
| UC-MF02-07 | Respond to Mission Assignment | Inspector | – | Accept the assignment, or postpone it with a reason. | MF02-08 |
| UC-MF02-08 | Monitor Assignment Status | Mission Manager | Notification Provider | Track responses and the confirmation deadline, send reminders and messages. | MF02-06 |
| UC-MF02-09 | Change Dispatched Mission | Mission Manager | Notification Provider | Cancel, suspend, resume, postpone, reassign personnel, change the UAV or change the priority with re-validation. | MF02-09 |
| UC-MF03-01 | Check In at Mission Site | Inspector | – | Record arrival at the site after accepting the assignment. | MF03-01 |
| UC-MF03-02 | Confirm UAV Handover | Inspector | – | Receive custody of the assigned UAV with its condition, and return it after the flight. | MF03-02\* |
| UC-MF03-03 | Start Mission Execution | Inspector | – | Start a ready mission; the UAV becomes Flying and the mission In Progress. | MF03-02 |
| UC-MF03-04 | Record Asset Inspection Progress | Inspector | – | *Deferred.* Mark each mission target as Inspected or Skipped. | MF03-05 |
| UC-MF03-05 | Transmit Telemetry and Field Media | Edge Device, Inspector | – | Publish UAV status and telemetry through MQTT; upload inspection images and videos with EXIF/GPS metadata. | MF03-03, MF03-04 |
| UC-MF03-06 | Monitor Field Execution | Mission Manager | Map Service | View live UAV state, mission lifecycle events, communications and alerts. | MF03-03 |
| UC-MF03-07 | Report Critical Anomaly | Edge Device, Inspector | Notification Provider | Send an edge AI detection, trigger an emergency alert or file a field incident report. | MF03-06 |
| UC-MF03-08 | Handle Critical Alert | Analyst | Mission Manager | Review an emergency alert (confirm or dismiss) or escalate it to a responsible user. | MF03-06 |
| UC-MF03-09 | Finish Field Inspection | Inspector | – | Submit the flight log and complete the mission; the UAV becomes Idle and the bookings are released. | MF03-07 |
| UC-MF04-01 | Submit Inspection Data | Inspector | – | Upload media for a mission target; each valid file is queued for analysis automatically (includes UC-MF04-02). | MF04-01, MF04-02 |
| UC-MF04-02 | Process AI Analysis | – (included) | Cloud AI Service | Create an analysis request, publish it to the AI queue, receive the result, evaluate each detection and store it. | MF04-03 |
| UC-MF04-03 | Monitor AI Processing Status | Inspector, Analyst, Mission Manager | – | Track analysis requests in Pending, Processing, Completed or Failed state. | MF04-03 |
| UC-MF04-04 | View AI Analysis Results | Mission Manager, Inspector, Analyst | – | View detections with category, bounding box, confidence, severity and review status on the image. | MF04-08 |
| UC-MF04-05 | Review AI Detections | Analyst | – | Confirm or reject detections; a confirmed detection creates a maintenance ticket. | MF04-09, MF04-11 |
| UC-MF04-06 | Request Media Reanalysis | Analyst, Mission Manager | Cloud AI Service | Request a new analysis of existing media with a model and analysis type (includes UC-MF04-02). | MF04-10 |
| UC-MF04-07 | View Defect Report and Export | Mission Manager, Analyst, Inspector | – | Create, submit and approve defect reports, view statistics and export PDF or Excel. | MF04-12 |
| UC-MF04-08 | Manage Maintenance Ticket | Mission Manager, UAV Technician | – | Create, execute (status, proof, materials) and close maintenance tickets for confirmed defects. | MF04-11 |

\*UAV handover has no separate item in Report 2, Table 1; it is estimated inside MF03-02.

## 3. Functional Requirements {#functional-requirements}

### 3.1 System Functional Overview {#system-functional-overview}

#### 3.1.1 Screens Flow

Figure 8 shows the screens of the Angular web client and how users move between them. All users sign in on SCR-01; on an untrusted device they confirm an e-mailed OTP on SCR-28. Every user then lands on SCR-02 Dashboard. The sidebar of SCR-02 shows only the modules allowed for the user’s role (Table 4), so each role follows its own path:

- **System Administrator:** user accounts (SCR-04), audit log (SCR-25), and every operational screen of the Mission Manager.
- **Mission Manager:** readiness assessments (SCR-09, SCR-10) → mission creation and dispatch (SCR-12) → mission follow-up (SCR-13, SCR-14) → emergency alerts (SCR-19), AI review (SCR-21, SCR-22) and reports (SCR-24).
- **Inspector, Analyst and UAV Technician (field roles):** assigned missions (SCR-11, *Assigned to me* tab) → Inspector Console (SCR-15) to accept or postpone the assignment. The Inspector then executes the mission and uploads data on SCR-13. The Analyst reviews detections (SCR-22, SCR-21) and alerts (SCR-19). The Technician maintains the UAV fleet (SCR-08) and works on maintenance tickets (SCR-26).

Notation used in Figure 8:

- Rectangles are pages with their own route; a rectangle divided into rows is a page with tabs.
- Dashed ellipses are dialogs and pop-overs.
- Dashed frames group the screens by Common Functions and MF01–MF04. A dashed arrow from SCR-02 is a sidebar entry; the roles that see each page are written under its name.

```mermaid
flowchart TD
    classDef auth fill:#f5f5f5,stroke:#616161;
    classDef cf fill:#e3f2fd,stroke:#1565c0;
    classDef mf01 fill:#e8f5e9,stroke:#2e7d32;
    classDef mf02 fill:#fffde7,stroke:#f9a825;
    classDef mf03 fill:#fbe9e7,stroke:#d84315;
    classDef mf04 fill:#f3e5f5,stroke:#6a1b9a;
    classDef dlg fill:#ffffff,stroke:#424242,stroke-dasharray: 4 4;

    subgraph AUTH ["Authentication – all users"]
        SCR01["SCR-01 Login"]:::auth
        SCR28["SCR-28 OTP Verification"]:::auth
        SCR27["SCR-27 Password Management"]:::auth
    end
    SCR01 -->|"Untrusted device"| SCR28
    SCR01 -->|"Forgot password"| SCR27
    SCR27 -->|"Password reset"| SCR01
    SCR28 -->|"OTP verified"| SCR02
    SCR01 -->|"Trusted device"| SCR02

    SCR02["SCR-02 Dashboard"]:::cf
    SCR03(["SCR-03 Notification Center"]):::dlg
    SCR02 -->|"Bell"| SCR03
    SCR02 -->|"Change password"| SCR27

    subgraph CF ["Common Functions"]
        SCR04["SCR-04 User Management<br/><i>Admin</i>"]:::cf
        SCR25["SCR-25 Audit Log<br/><i>Admin, Manager</i>"]:::cf
        SCR06["SCR-06 GIS Map<br/><i>Admin, Manager, Analyst, Inspector</i>"]:::cf
        SCR07["SCR-07 Grid Asset Catalogue<br/><i>all roles</i>"]:::cf
        SCR08["SCR-08 UAV Fleet Management<br/><i>Admin, Manager, Technician</i>"]:::cf
        DLG08(["SCR-08-M Add/Edit UAV · Technical check · GIS capacity estimate"]):::dlg
    end
    SCR02 -.->|"Sidebar"| CF
    SCR06 -->|"View asset"| SCR07
    SCR08 --> DLG08

    subgraph MF01 ["MF01 – Pre-Mission Feasibility · Manager, Admin"]
        SCR09["SCR-09 Assessment List"]:::mf01
        SCR10A["SCR-10A Create Assessment"]:::mf01
        SCR10B["SCR-10B Assessment Workspace<br/>5 tabs"]:::mf01
        DLG10(["SCR-10-M Cancel Assessment"]):::dlg
    end
    SCR02 -.->|"Sidebar"| MF01
    SCR09 -->|"Create"| SCR10A
    SCR10A -->|"Create & evaluate"| SCR10B
    SCR09 -->|"Open"| SCR10B
    SCR10B -->|"Cancel"| DLG10

    subgraph MF02 ["MF02 – Create & Assign Mission"]
        SCR11["SCR-11 Mission List<br/>Assigned to me / All"]:::mf02
        SCR12["SCR-12 Create & Dispatch Mission<br/><i>Manager, Admin</i>"]:::mf02
        SCR13["SCR-13 Mission Detail<br/>7 tabs"]:::mf02
        DLG14(["SCR-14 Change Mission"]):::dlg
        SCR15["SCR-15 Inspector Console<br/><i>Inspector, Analyst, Technician</i>"]:::mf02
        DLG16(["SCR-16 Postpone Assignment"]):::dlg
    end
    SCR02 -.->|"Sidebar"| MF02
    SCR10B -->|"Ready → Create mission"| SCR12
    SCR06 -->|"Create mission from selection"| SCR12
    SCR11 -->|"New mission"| SCR12
    SCR12 -->|"Dispatch"| SCR13
    SCR11 -->|"Open (Manager, Admin)"| SCR13
    SCR11 -->|"Open (field roles)"| SCR15
    SCR03 -->|"Mission notification"| SCR13
    SCR03 -->|"Mission notification (field roles)"| SCR15
    SCR13 -->|"Change"| DLG14
    SCR15 -->|"Postpone"| DLG16

    subgraph MF03 ["MF03 – Field Execution & Alerts"]
        SCR17["SCR-17 Inspection History & Flight Logs<br/><i>all roles</i>"]:::mf03
        SCR19["SCR-19 Emergency Alerts<br/><i>Admin, Manager, Analyst</i>"]:::mf03
        DLG19(["SCR-19-M Confirm · Dismiss · Escalate"]):::dlg
    end
    SCR02 -.->|"Sidebar"| MF03
    SCR13 -->|"Emergency banner"| SCR19
    SCR17 -->|"View results"| SCR13
    SCR19 --> DLG19

    subgraph MF04 ["MF04 – AI Analysis, Review & Reports"]
        SCR22["SCR-22 AI Review Queue<br/><i>Admin, Manager, Analyst</i>"]:::mf04
        SCR21["SCR-21 Detection Review"]:::mf04
        DLG23(["SCR-23 Reject Detection"]):::dlg
        SCR20["SCR-20 AI Trial Upload<br/><i>Admin, Manager, Analyst</i>"]:::mf04
        SCR24["SCR-24 Reports<br/><i>all roles</i>"]:::mf04
        DLG24(["SCR-24-M Create Report"]):::dlg
    end
    SCR02 -.->|"Sidebar"| MF04
    SCR06 -->|"Open detection"| SCR21
    SCR22 -->|"Select detection"| SCR21
    SCR21 -->|"Reject"| DLG23
    SCR24 -->|"Create"| DLG24
```

**Figure 8.** *UAV-PMS screens flow. The draw.io version for the Word report is `report3_diagrams/Screen_Flow_Diagram_v0.5.drawio`.*

#### 3.1.2 Screen Descriptions

Table 3 describes each screen in Figure 8 and the use cases it serves. Live UAV monitoring (SCR-18) and maintenance tickets (SCR-26) are not separate pages; they are parts of SCR-13 Mission Detail.

**Table 3.** *Screen descriptions.*

| ID | Feature | Screen | Route | Description | Use Case(s) |
| :---- | :---- | :---- | :---- | :---- | :---- |
| SCR-01 | CF | Login | `/login` | E-mail and password entry. Opens SCR-28 when the device is not trusted, or the change-password page when the password must be changed. | UC-CF-01 |
| SCR-02 | CF | Dashboard | `/dashboard` | Home page of every role: active emergency alerts, mission status overview, defect statistics and recent defects. The sidebar shows only the modules of the user’s role. | UC-CF-01 |
| SCR-03 | CF | Notification Center | Header pop-over | Real-time notification list with unread count, filters, mark as read and a link to the related mission. | UC-CF-06 |
| SCR-04 | CF | User Management | `/admin/users` | User list with search and filters; dialogs to create and edit accounts, change the role, unlock and reset the password. | UC-CF-02 |
| SCR-06 | CF | GIS Map | `/gis` | Map of lines, towers, defects and alerts with layer toggles and filters. Selecting assets on the map can start a new mission (SCR-12); a detail drawer opens the asset (SCR-07) or the detection (SCR-21). | UC-CF-04, UC-MF02-05 |
| SCR-07 | CF | Grid Asset Catalogue | `/assets`, `/assets/:id` | List of grid assets with code, type, tower, line and operational status, and a detail drawer with technical data and linked defects. | UC-CF-04 |
| SCR-08 | CF / MF01 | UAV Fleet Management | `/drones` | UAV list with operational status and technical health. Dialogs to add, edit and delete a UAV, run the technical check (optionally reading the flight controller) and estimate the GIS coverage of a UAV. | UC-CF-05, UC-MF01-05 |
| SCR-09 | MF01 | Assessment List | `/pre-mission` | Assessments with code, region, planned window and status; filters; open, re-evaluate and cancel actions. | UC-MF01-01, UC-MF01-06 |
| SCR-10 | MF01 | Create Assessment / Assessment Workspace | `/pre-mission/new`, `/pre-mission/:id` | **Create:** one form with scope (region, line, towers on the map), time window, GIS boundary, required personnel and a summary. **Workspace:** 5 tabs – Overview & scope map, Line layout & weather, Personnel candidates, UAV candidates, Technical check – with readiness cards, a cancel dialog and the *Create mission* button when the result is Ready. | UC-MF01-01 – UC-MF01-06 |
| SCR-11 | MF02 | Mission List | `/missions` | Two tabs: *Assigned to me* and *All missions*; filters by status and priority. Managers open SCR-13; field roles open SCR-15. | UC-MF02-06, UC-MF02-08 |
| SCR-12 | MF02 | Create & Dispatch Mission | `/missions/new` | Mission form filled from a Ready assessment (or from a GIS selection): scope and readiness, resource assignment (Inspector, Analyst, Technician, UAV) with a conflict check and map preview, deadline and instructions; *Dispatch*. | UC-MF02-01 – UC-MF02-04 |
| SCR-13 | MF02 / MF03 / MF04 | Mission Detail | `/missions/:id` | 7 tabs: Overview (mission data, team, target map and live UAV telemetry), Upload data, AI processing, AI results, Target assets, Maintenance and Activity. Actions: start and complete the flight, send a reminder, and the change dialogs of SCR-14. | UC-MF02-05, UC-MF02-08, UC-MF03-03, UC-MF03-06, UC-MF03-09, UC-MF04-01, UC-MF04-03, UC-MF04-04 |
| SCR-14 | MF02 | Change Mission Dialogs | Dialogs in SCR-13 | Postpone, suspend, resume, cancel, reassign a role and change the priority, each with a reason. | UC-MF02-09 |
| SCR-15 | MF02 | Inspector Console | `/missions/:id/inspector` | Field view of an assigned mission: accept the assignment, UAV specification, pre-flight checklist, route map and tower list. | UC-MF02-06, UC-MF02-07 |
| SCR-16 | MF02 | Postpone Assignment Dialog | Dialog in SCR-15 | Reason for postponing or rescheduling an assignment. | UC-MF02-07 |
| SCR-17 | MF03 | Inspection History & Flight Logs | `/inspections` | Inspection records with result and filters; each row opens the AI results of the mission. | UC-MF03-05, UC-MF03-09 |
| SCR-18 | MF03 | Live Monitoring | Overview tab of SCR-13 | Telemetry panel and map with the latest UAV position, altitude, battery, speed and heading. | UC-MF03-06 |
| SCR-19 | MF03 | Emergency Alerts | `/emergency-alerts` | Two tabs: *Active alerts* and *Alert & review history*, with evidence image; dialogs to confirm, dismiss or escalate. | UC-MF03-07, UC-MF03-08 |
| SCR-20 | MF04 | AI Trial Upload | `/ai-analysis/upload` | Two tabs: image analysis and video analysis. Trial upload outside a mission; results are shown but not stored. | UC-MF04-03 |
| SCR-21 | MF04 | Detection Review | `/ai-review/:id` | Image with bounding box, category, confidence and severity; approve, or reject with a reason (SCR-23). | UC-MF04-04, UC-MF04-05 |
| SCR-22 | MF04 | AI Review Queue | `/ai-review` | Detections by review status (Pending, Confirmed, Rejected) in grid or table view. | UC-MF04-05 |
| SCR-23 | MF04 | Reject Detection Dialog | Dialog in SCR-21 | Reason for rejecting a detection. | UC-MF04-05 |
| SCR-24 | MF04 | Reports | `/reports` | Report list filtered by type and status; create report dialog; submit, approve and download. | UC-MF04-07 |
| SCR-25 | CF | Audit Log | `/system/audit-logs` | Audit records with filters, search and CSV export; detail dialog with the change comparison and raw data. | UC-CF-07 |
| SCR-26 | MF04 | Maintenance Tickets | Maintenance tab of SCR-13 | Tickets created from confirmed defects with status, technician, due date and proof. | UC-MF04-08 |
| SCR-27 | CF | Password Management | `/forgot-password`, `/reset-password`, `/change-password` | Request an OTP, reset a forgotten password, or change the password. | UC-CF-01 |
| SCR-28 | CF | OTP Verification | `/otp` | Six-digit OTP entry with resend for login and password reset. | UC-CF-01 |

*SCR-05 Personnel Qualifications is deferred together with UC-CF-03. UC-CF-08 Manage System Settings has no screen in the current release; it is performed through the System Settings API (Section 3.6.8).*

#### 3.1.3 Screen Authorization

Table 4 states which actor may open each screen. *X* = full access, *V* = view only, *own* = only missions the user is assigned to, blank = no access. Each user has one role. Access is checked by the web client (route guards) and again by the backend role and geographic-scope checks (BR-01, BR-02).

**Table 4.** *Screen authorization matrix.*

| Screen | System Administrator | Mission Manager | Inspector | Analyst | UAV Technician |
| :---- | :----: | :----: | :----: | :----: | :----: |
| SCR-01 – SCR-03, SCR-27, SCR-28 | X | X | X | X | X |
| SCR-04 User Management | X | | | | |
| SCR-06 GIS Map | X | X | V | V | |
| SCR-07 Grid Asset Catalogue | V | V | V | V | V |
| SCR-08 UAV Fleet Management | X | X | | | X |
| SCR-09, SCR-10 Assessment List / Workspace | X | X | | | |
| SCR-11 Mission List | X | X | V (own) | V (own) | V (own) |
| SCR-12 Create Mission, SCR-14 Change Mission | X | X | | | |
| SCR-13 Mission Detail (incl. SCR-18) | X | X | X (own: start, complete, upload) | V (own) | V (own) |
| SCR-15 Inspector Console, SCR-16 Postpone | | | X (own) | X (own) | X (own) |
| SCR-17 Inspection History | V | V | V | V | V |
| SCR-19 Emergency Alerts | X | X | | X | |
| SCR-20 AI Trial Upload | X | X | | X | |
| SCR-21 – SCR-23 AI Review | X | V | | X | |
| SCR-24 Reports | X (approve) | X (approve) | X (create, submit) | X (create, submit) | X (create, submit) |
| SCR-25 Audit Log | X | V | | | |
| SCR-26 Maintenance Tickets | X | X | V (own) | V (own) | X (own tickets) |

#### 3.1.4 Non-Screen Functions

Table 5 lists the functions that run without a user screen: background jobs, message consumers and integrations.

**Table 5.** *Non-screen functions.*

| ID | Feature | System Function | Description | Use Case |
| :---- | :---- | :---- | :---- | :---- |
| NS-01 | MF01 | Assessment Expiry Job | Operations Service background job, every 2 minutes: sets Ready assessments whose validity has passed to Expired. The same expiry rule is applied whenever an assessment is read, evaluated or used (BR-17). | UC-MF01-02 |
| NS-02 | MF02 | Mission Confirmation Overdue Job | Hangfire job, every minute: for a Pending Acceptance mission whose confirmation deadline has passed, notifies the manager (`MISSION_CONFIRMATION_OVERDUE`), writes an OVERDUE communication log and sends a real-time event, once per mission (BR-31). | UC-MF02-08 |
| NS-03 | CF | Notification Dispatcher | RabbitMQ consumers that turn integration events (mission created, mission dispatched, defect detected, maintenance ticket assigned, notification push, AI status changed) into in-app notifications, and the Hangfire job `push-notifications-sync` (every minute) that e-mails unsent notifications through SendGrid (BR-46). | UC-CF-06 |
| NS-04 | MF03 | MQTT Drone Consumer | Subscribes to `uav/+/status` and `uav/+/telemetry`, validates the UAV code and stores the latest UAV live state in Redis for 30 seconds (BR-39). | UC-MF03-05 |
| NS-05 | CF | Outbox Dispatcher | Publishes integration events stored in the outbox table (for example media uploaded, mission created from assessment, defect confirmed) to the RabbitMQ topic exchange. | UC-MF02-04, UC-MF04-01 |
| NS-06 | MF03 | Vision Bridge Ingestion | Receives edge AI detections (UAV, class, confidence, bounding box, GPS, image) for a UAV on an In Progress mission, stores the media and detection and notifies the mission manager and inspector (BR-42). | UC-MF03-07 |
| NS-07 | MF04 | AI Request Publisher | Consumes the media-uploaded event, creates an analysis request and publishes it to the server or edge, image or video request queue (BR-49). | UC-MF04-02 |
| NS-08 | MF04 | AI Result Consumer | Consumes AI results, validates them, evaluates each detection through NS-10, stores the detections, retries transient errors and moves failed messages to the dead-letter queue (BR-50 – BR-52). | UC-MF04-02 |
| NS-09 | CF | Audit Logger | Records inserts, updates and soft deletes of audited entities with user, table, record, old and new values, IP address and user agent, plus explicit business actions (BR-06). | UC-CF-07 |
| NS-10 | MF04 | Inspection Evaluation (gRPC) | Calculates severity, risk level, priority score and the immediate-alert decision of a detection (BR-53). | UC-MF04-02 |
| NS-11 | CF | Real-time Relay | Pushes assessment, mission-lifecycle and AI-status events to SignalR groups (user, mission, assessment). | UC-MF01-02, UC-MF03-06, UC-MF04-03 |
| NS-12 | CF | Housekeeping Jobs | `daily-summary-job` (daily summary to administrators and managers) and `auto-cleanup-job` (weekly removal of old temporary files and expired refresh tokens). | UC-CF-06 |

#### 3.1.5 Entity Relationship Diagram

Figure 9 is the conceptual ERD of UAV-PMS in crow’s-foot notation. It shows the business entities and their relationships and omits keys and technical tables (outbox, refresh tokens, trusted devices). Table 6 describes each entity.

```mermaid
erDiagram
    REGION ||--o{ SUBSTATION : contains
    SUBSTATION ||--o{ TRANSMISSION_LINE : feeds
    TRANSMISSION_LINE ||--o{ TOWER : carries
    TOWER ||--o{ ASSET : holds

    USER }o--o{ ROLE : holds
    USER }o--o{ REGION : "is scoped to"

    REGION ||--o{ PRE_MISSION_ASSESSMENT : covers
    PRE_MISSION_ASSESSMENT }o--o{ ASSET : targets
    PRE_MISSION_ASSESSMENT ||--o| MISSION : "creates"

    MISSION }o--o{ ASSET : targets
    MISSION ||--o{ MISSION_ASSIGNMENT : has
    USER ||--o{ MISSION_ASSIGNMENT : receives
    MISSION ||--o{ RESOURCE_BOOKING : reserves
    UAV ||--o{ RESOURCE_BOOKING : "is booked by"
    USER ||--o{ RESOURCE_BOOKING : "is booked by"
    UAV ||--o{ MISSION : "is assigned to"

    UAV ||--o{ TECHNICAL_INSPECTION : undergoes
    TECHNICAL_INSPECTION ||--o{ TECHNICAL_METRIC : records
    UAV ||--o{ FLIGHT_CONTROLLER_BINDING : "is paired by"

    MISSION ||--o{ FIELD_CHECK_IN : records
    MISSION ||--o{ DRONE_HANDOVER : records
    MISSION ||--o{ FLIGHT_LOG : generates
    MISSION ||--o{ INCIDENT_REPORT : records
    MISSION ||--o{ COMMUNICATION_LOG : records

    MISSION ||--o{ INSPECTION_MEDIA : produces
    ASSET ||--o{ INSPECTION_MEDIA : "appears in"
    INSPECTION_MEDIA ||--o{ AI_ANALYSIS_REQUEST : "is analysed by"
    INSPECTION_MEDIA ||--o{ DETECTED_ANOMALY : contains
    DEFECT_CATEGORY ||--o{ DETECTED_ANOMALY : classifies

    DETECTED_ANOMALY ||--o{ EMERGENCY_ALERT : triggers
    EMERGENCY_ALERT ||--o{ ALERT_ESCALATION : "is escalated by"
    DETECTED_ANOMALY ||--o| MAINTENANCE_TICKET : requires
    MAINTENANCE_TICKET ||--o{ MAINTENANCE_PROOF : "is documented by"
    MAINTENANCE_TICKET ||--o{ MATERIAL_LOG : consumes

    REPORT }o--o{ MISSION : summarises
    USER ||--o{ NOTIFICATION : receives
    USER ||--o{ AUDIT_LOG : generates
```

**Figure 9.** *UAV-PMS conceptual entity relationship diagram.*

**Table 6.** *Conceptual entity descriptions.*

| # | Entity | Description |
| :---- | :---- | :---- |
| 1 | REGION | Management region that groups substations and defines the geographic scope of users, assessments and missions. |
| 2 | SUBSTATION | Substation in a region; the origin of transmission lines. |
| 3 | TRANSMISSION_LINE | Power line identified by code and voltage level. |
| 4 | TOWER | Tower on a line, identified by code and WGS 84 coordinates. |
| 5 | ASSET | Inspectable component on a tower (insulator string, conductor, surge arrester, cross-arm…) with type, code and operational status. |
| 6 | USER | Account with e-mail, password hash, status and profile. |
| 7 | ROLE | System role: SystemAdmin, Manager, Inspector, Analyst, Technician (MaintenanceTechnician). |
| 8 | PRE_MISSION_ASSESSMENT | Readiness assessment with region, planned window, target assets, boundary, component results, candidates, status and validity. |
| 9 | MISSION | Inspection mission with code, title, priority, objective, planned window, targets, UAV, status and the link to its assessment. |
| 10 | MISSION_ASSIGNMENT | Assignment of a user to a mission with mission role, required flag and response (Pending, Accepted, Postponed). |
| 11 | RESOURCE_BOOKING | Reservation of a user or UAV for a time window (Active, Released, Cancelled), used for conflict detection. |
| 12 | UAV | Drone with code, model, serial number, operational status, flight status, technical health, battery and endurance. |
| 13 | TECHNICAL_INSPECTION | Technical inspection of a UAV with result (Passed/Failed), technical health and validity. |
| 14 | TECHNICAL_METRIC | Checklist item or measured metric of an inspection with subsystem, value, critical flag and result. |
| 15 | FLIGHT_CONTROLLER_BINDING | Pairing between a flight-controller hardware UID and a UAV, with active flag and last-seen time. |
| 16 | FIELD_CHECK_IN | Check-in of an assigned user at the mission site. |
| 17 | DRONE_HANDOVER | Custody of the UAV given to a mission member (Accepted/Rejected) and its return with condition. |
| 18 | FLIGHT_LOG | GPS track, flight duration, maximum altitude, minimum battery and connection status of a flight. |
| 19 | INCIDENT_REPORT | Field incident with type, severity, description and optional evidence file. |
| 20 | COMMUNICATION_LOG | Messages and system events (dispatch, reminder, overdue, emergency) exchanged on a mission. |
| 21 | INSPECTION_MEDIA | Uploaded image or video with capture time, GPS position, target asset and review status. |
| 22 | AI_ANALYSIS_REQUEST | Analysis job for one media file with model, analysis type, status and result summary. |
| 23 | DETECTED_ANOMALY | Detection with category, bounding box, confidence, source, validation status and analyst notes. |
| 24 | DEFECT_CATEGORY | Canonical defect class with code, name and emergency-class flag. |
| 25 | EMERGENCY_ALERT | Emergency alert raised from a critical detection with priority, status, reviewer and notes. |
| 26 | ALERT_ESCALATION | Escalation of an alert to a responsible user with reason. |
| 27 | MAINTENANCE_TICKET | Work order for a confirmed defect with code, priority, technician, due date and status. |
| 28 | MAINTENANCE_PROOF | Repair evidence photo with technician notes. |
| 29 | MATERIAL_LOG | Material or component consumed by a ticket with quantity and unit. |
| 30 | REPORT | Defect or inspection report with type, scope, period, status and generated files. |
| 31 | NOTIFICATION | In-app notification with type, title, body, reference and read/pushed state. |
| 32 | AUDIT_LOG | Record of a data change or business action with user, time, old and new values and network origin. |

### Use case specification template

Sections 3.2–3.6 specify each use case in Table 2 with the same structure:

- **Specification table** – actors, trigger, preconditions, postconditions, priority, screens, business rules (Section 5.1), messages (Section 5.3) and backend API.
- **Normal flow**, **alternative flows** (A*n*) and **exceptions** (E*n*).
- **Screen layout** – the screenshot of the related screen.

Priority **High** marks the use cases required for the MF01–MF04 end-to-end demonstration. All API paths are relative to the gateway and start with `/api/v1` unless `/api/v2` is shown.

### 3.2 MF01 – Pre-Mission Feasibility and Resource Readiness {#mf01}

MF01 is the prerequisite of every inspection mission. It checks that the planned scope is valid, that eligible and unbooked field personnel exist, and that at least one technically ready UAV can cover the scope in the planned window. A mission can only be created from an assessment whose result is Ready (MF02).

#### 3.2.1 Create Readiness Assessment (UC-MF01-01)

**Table 7.** *UC-MF01-01 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager (also System Administrator) |
| Secondary actor | Map Service |
| Trigger | The manager selects **“+ Tạo thẩm định mới”** on SCR-09, or opens `/pre-mission/new`. |
| Description | Create an assessment with region, planned window, target assets and an optional boundary; the system evaluates it immediately (includes UC-MF01-02). |
| Preconditions | The user is logged in as Mission Manager or System Administrator, is Active and has the region in their geographic scope. |
| Postconditions | An assessment is stored with policy version v2.0, evaluated, and its result is Ready or Not Ready. The audit log records `ASSESSMENT_CREATED`. |
| Priority | High |
| Screens | SCR-09, SCR-10 |
| Business rules | BR-01, BR-02, BR-14, BR-15, BR-16 |
| Messages | MSG-002, MSG-006, MSG-013, MSG-103, MSG-104, MSG-105 |
| API | `POST /api/v2/pre-mission-assessments` |

**Normal flow**

1. The manager opens SCR-10 (create form).
2. The manager selects the region; the map shows the towers and assets of that region.
3. The manager enters the planned start and end date-time.
4. The manager selects the target assets from the list or by clicking map markers.
5. Optionally, the manager draws the boundary polygon of the flight area.
6. The manager selects **“Khởi tạo & Đánh giá”**.
7. The system validates the input (BR-14, BR-15), stores the assessment with status Evaluating and runs UC-MF01-02 in the same request.
8. The system opens the assessment workspace (`/pre-mission/:id`) with the result.

**Alternative flows**

- **A1 – Repeated submission.** If the request carries an idempotency key that already exists, the system returns the existing assessment instead of creating a new one.

**Exceptions**

- **E1 – Invalid window.** The end is not after the start or is in the past: the system rejects the request (`INVALID_PLANNED_TIME`, MSG-002).
- **E2 – Empty or duplicate scope.** No asset or a repeated asset is selected (`INVALID_PROPOSED_SCOPE`, MSG-105).
- **E3 – Out of scope.** The region is not in the user’s scope (`REGION_MANAGEMENT_SCOPE_REQUIRED`, MSG-006) or an asset is not in the region (`ASSET_OUTSIDE_MANAGEMENT_SCOPE`, MSG-103).
- **E4 – Invalid asset.** An asset is inactive, has no coordinates or is outside the boundary (`ASSETS_INACTIVE`, `ASSET_MISSING_GEOLOCATION`, `ASSET_OUTSIDE_BOUNDARY`, MSG-104).
- **E5 – Invalid boundary.** The boundary is not a valid polygon or exceeds the size limits (`INVALID_GEOMETRY`, MSG-013).

**Screen layout**

> *[Screenshot: SCR-10 Create Assessment – `/pre-mission/new` – region selector, planned start/end, map with tower markers and boundary drawing, target asset checklist, button “Khởi tạo & Đánh giá”.]*

**Figure 10.** *Screen layout – Create Readiness Assessment.*

#### 3.2.2 Evaluate Pre-Mission Readiness (UC-MF01-02)

**Table 8.** *UC-MF01-02 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager (also System Administrator) |
| Trigger | Automatically after UC-MF01-01; manually with **“Đánh giá lại”** on SCR-10. |
| Description | Evaluate the site, personnel and UAV components and set the overall result Ready or Not Ready with the reason of each failed component. |
| Preconditions | The assessment exists and is not Completed, Cancelled or Expired. |
| Postconditions | Personnel and UAV candidates are rebuilt; component results, overall status and validity (`ValidUntil`) are stored; the version is incremented; the audit log records `ASSESSMENT_EVALUATED`; a real-time `AssessmentStatusChanged` event is sent. |
| Priority | High |
| Screens | SCR-10 |
| Business rules | BR-15 – BR-21 |
| Messages | MSG-101, MSG-102, MSG-106, MSG-107 |
| API | `POST /api/v2/pre-mission-assessments/{id}/evaluate` (alias `/re-evaluate`) |

**Evaluation components**

| Component | Passes when |
| :------- | :------------------------------------ |
| Site | Every target asset is active, belongs to the region, has coordinates and lies inside the boundary (if any) (BR-15). |
| Personnel | At least one eligible Inspector, one eligible Analyst and one eligible Technician exist (BR-18, UC-MF01-03). |
| UAV | At least one UAV is eligible: available, unbooked, technically healthy and suitable for the scope (BR-20, BR-21, UC-MF01-04). |

Weather is not evaluated automatically; flight-condition decisions remain with the pilot.

**Normal flow**

1. The system checks that the assessment can be evaluated; if its validity or planned end has passed, it is set to Expired.
2. The system evaluates the site component.
3. The system rebuilds the personnel candidates (UC-MF01-03).
4. The system rebuilds the UAV candidates (UC-MF01-04).
5. The system sets the overall status: Ready if all components pass, otherwise Not Ready (BR-16).
6. The system sets how long the result stays valid (BR-17).
7. The system saves the result, writes the audit record and pushes the real-time event.
8. SCR-10 shows the overall badge, the component cards and enables **“Tạo nhiệm vụ bay”** when the result is Ready (MSG-101); otherwise it lists the reasons (MSG-102).

**Alternative flows**

- **A1 – Re-evaluation after a change.** After a technician records an inspection or personnel availability changes, the manager selects **“Đánh giá lại”** and the flow restarts at step 1.

**Exceptions**

- **E1 – Already used.** The assessment is Completed (`ASSESSMENT_ALREADY_COMPLETED`, MSG-107).
- **E2 – Cancelled.** The assessment is Cancelled (`ASSESSMENT_CANCELLED`, MSG-107).
- **E3 – Expired.** The validity or planned end has passed; the status becomes Expired (`ASSESSMENT_EXPIRED`, MSG-106).

**Screen layout**

> *[Screenshot: SCR-10 Assessment Workspace, Overview tab – overall status badge (Ready / Not Ready), validity countdown, component cards (Site, Personnel, UAV), button “Đánh giá lại”.]*

**Figure 11.** *Screen layout – Readiness evaluation (Overview tab).*

#### 3.2.3 Check Personnel Availability and Eligibility (UC-MF01-03)

**Table 9.** *UC-MF01-03 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | – (included by UC-MF01-02 and UC-MF02-02); viewed by the Mission Manager |
| Trigger | Readiness evaluation; the manager opens the **“Ứng viên Nhân sự”** tab of SCR-10. |
| Description | List the field personnel and decide for each person whether they are eligible for the planned window and region. |
| Preconditions | The assessment has been evaluated. |
| Postconditions | Each candidate is stored with role, availability, eligibility and reason. |
| Priority | High |
| Screens | SCR-10 (Personnel Candidates tab) |
| Business rules | BR-02, BR-18, BR-19 |
| Messages | MSG-102 |
| API | Part of `GET /api/v2/pre-mission-assessments/{id}` |

**Eligibility criteria (BR-18)**

| Criterion | Rule | Reason code when failed |
| :---- | :---- | :---- |
| Account | Active and e-mail verified | `USER_INACTIVE` |
| Role | Inspector (Pilot), Analyst or Technician (MaintenanceTechnician); accounts with SystemAdmin or Manager role are excluded | `INVALID_ROLE` |
| Scope | Has a scope for the region or a global scope | `OUTSIDE_MANAGEMENT_SCOPE` |
| Schedule | No active booking overlapping the planned window (BR-19) | `SCHEDULE_CONFLICT` |

**Normal flow**

1. The manager opens the Personnel Candidates tab.
2. The system shows the role summary (eligible Inspectors, Analysts, Technicians) and the candidate table with name, role, scope, booking status and eligibility badge.
3. The manager checks that each required role has at least one eligible person.

**Alternative flows**

- **A1 – Filter by role.** The manager selects a role tab to show only that role.

**Exceptions**

- **E1 – Missing role.** No eligible person exists for a required role; the personnel component fails and the reason is shown (MSG-102).

**Screen layout**

> *[Screenshot: SCR-10, Personnel Candidates tab – role summary bar, role filter, candidate table with eligibility badge “Đủ điều kiện / Không đủ điều kiện”.]*

**Figure 12.** *Screen layout – Personnel candidates.*

#### 3.2.4 Check UAV Availability and Technical Readiness (UC-MF01-04)

**Table 10.** *UC-MF01-04 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | – (included by UC-MF01-02 and UC-MF02-03); viewed by the Mission Manager and UAV Technician |
| Trigger | Readiness evaluation; the user opens the **“Ứng viên Thiết bị UAV”** tab of SCR-10. |
| Description | List the UAVs and decide for each one whether it is available, technically ready and able to cover the scope. |
| Preconditions | The assessment has been evaluated. |
| Postconditions | Each UAV candidate is stored with availability, technical health, suitability, estimated flights and reason. |
| Priority | High |
| Screens | SCR-10 (UAV Candidates tab), SCR-08 |
| Business rules | BR-12, BR-19, BR-20, BR-21, BR-22 |
| Messages | MSG-102 |
| API | Part of `GET /api/v2/pre-mission-assessments/{id}`; `GET /drones/eligible?assessmentId=`; `POST /drones/estimate-gis-scope` |

**Evaluation criteria**

| Criterion | Rule | Reason code when failed |
| :---- | :---- | :---- |
| Operational availability | Operational status Available and no overlapping active booking | `DRONE_NOT_AVAILABLE`, `SCHEDULE_CONFLICT` |
| Technical health | Health of the latest Passed inspection valid at the planned start (or the UAV’s stored health) is Healthy or Warning | `TECHNICAL_HEALTH_CRITICAL`, `TECHNICAL_HEALTH_DEGRADED`, `NO_VALID_TECHNICAL_INSPECTION` |
| Scope suitability | Suitable (one flight within range) or SuitableWithSplit (2–4 flights); otherwise Unsuitable (BR-21) | `SCOPE_EXCEEDS_CAPABILITY` |

**Normal flow**

1. The user opens the UAV Candidates tab.
2. The system shows the fleet summary and the candidate table: UAV code, model, operational status, technical health badge (Healthy, Warning, Critical), battery, suitability badge (“Phù hợp”, “Phù hợp (chia chuyến)”, “Không phù hợp”) and the estimated number of flights.
3. The manager identifies the UAVs that can be used for the mission.
4. If a UAV needs a new inspection, the technician selects **“Kiểm định”** to open UC-MF01-05.

**Alternative flows**

- **A1 – Split recommendation.** When the scope needs 2–4 flights, the system shows SuitableWithSplit with the estimated number of flights.

**Exceptions**

- **E1 – No eligible UAV.** The UAV component fails and the reasons are shown (MSG-102).

**Screen layout**

> *[Screenshot: SCR-10, UAV Candidates tab – fleet summary cards, candidate table with health and suitability badges, button “Kiểm định”.]*

**Figure 13.** *Screen layout – UAV candidates.*

#### 3.2.5 Record UAV Technical Inspection and Flight-Controller Binding (UC-MF01-05)

**Table 11.** *UC-MF01-05 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | UAV Technician (also Mission Manager, System Administrator) |
| Secondary actor | Edge Device (flight controller read over USB / Web Serial) |
| Trigger | The technician opens the **“Kiểm định Kỹ thuật”** tab of SCR-10, or the technical-check dialog of a UAV on SCR-08. |
| Description | Read the flight-controller UID, verify that it is bound to the UAV, record the checklist or metrics and obtain the inspection result and technical health. |
| Preconditions | The UAV exists and the user has access to it (BR-02). |
| Postconditions | An inspection with result Passed or Failed, technical health and validity (7 days) is stored; the UAV’s technical health and last inspection are updated; the audit log records `INSPECTION_SUBMITTED`; a real-time `DroneInspectionCompleted` event is sent to the assessment. |
| Priority | High |
| Screens | SCR-10 (Technical Inspection tab), SCR-08 |
| Business rules | BR-13, BR-22, BR-23, BR-24 |
| Messages | MSG-108 – MSG-112 |
| API | `POST /drone-technical-inspections`; `POST /drones/{id}/basic-technical-check`; `GET /flight-controllers/{uid}/drone`; `POST`/`DELETE /drones/{id}/flight-controller` |

**Checklist used by the basic technical check**

| Item | Subsystem | Critical |
| :---- | :---- | :----: |
| Frame and body | Airframe | Yes |
| Propeller integrity | Propulsion | Yes |
| Motors | Propulsion | Yes |
| Battery level (low battery gives a warning, very low battery fails – BR-23) | Power | Yes |
| GPS / RTK fix | Avionics | Yes |
| RF link and telemetry (signal quality above the minimum – BR-23) | Communication | Yes |
| Flight controller | Avionics | Yes |
| Camera payload | Payload | No |

**Normal flow**

1. The technician connects the flight controller to the workstation by USB.
2. The technician selects the UAV; the browser reads the hardware UID through Web Serial (MSP).
3. The system resolves the UID; if it is bound to the selected UAV, the inspection controls are enabled.
4. The technician fills in the checklist or reads the metrics and selects **“Chạy kiểm định kỹ thuật”**.
5. The system evaluates each item (BR-22, BR-23) and shows pass/fail per item.
6. The technician submits; the system stores the inspection, updates the UAV health and shows the result (MSG-108).

**Alternative flows**

- **A1 – New flight controller.** The UID is not registered; the technician pairs it with the UAV (`POST /drones/{id}/flight-controller`). Any earlier active binding of the UAV or of the UID is deactivated (BR-13); the audit log records `FLIGHT_CONTROLLER_PAIRED` (MSG-112).
- **A2 – Unpair.** The technician removes the binding; the audit log records `FLIGHT_CONTROLLER_UNPAIRED`.

**Exceptions**

- **E1 – Controller belongs to another UAV.** Submission is blocked (`FC_DRONE_MISMATCH`, MSG-110).
- **E2 – Controller not registered.** Submission is blocked until pairing (`FC_NOT_REGISTERED`, MSG-109).
- **E3 – Critical failure.** A critical item fails; the result is Failed with health Critical and the UAV cannot be assigned (BR-20).
- **E4 – Invalid metrics.** No metric or a duplicate metric is sent (`METRICS_REQUIRED`, `DUPLICATE_METRIC`, MSG-111).
- **E5 – UAV not in assessment.** The assessment given does not contain the UAV as a candidate (`ASSESSMENT_DRONE_MISMATCH`).

**Screen layout**

> *[Screenshot: SCR-10, Technical Inspection tab – flight-controller UID card with connection state, validation banner, subsystem grid, metric table, button “Chạy kiểm định kỹ thuật”.]*

**Figure 14.** *Screen layout – Technical inspection and flight-controller binding.*

#### 3.2.6 View Readiness Assessment Result (UC-MF01-06)

**Table 12.** *UC-MF01-06 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager (also System Administrator); Inspector view only |
| Trigger | The user opens an assessment from SCR-09 or the **“Kết quả Thẩm định”** tab of SCR-10. |
| Description | Show the overall result and the reason of each component; cancel the assessment or hand a Ready assessment over to mission creation (MF02). |
| Preconditions | The assessment exists and the user may access it (BR-02). |
| Postconditions | No change (view); or the assessment is Cancelled; or SCR-12 is opened with the assessment. |
| Priority | High |
| Screens | SCR-09, SCR-10 (Result tab), SCR-12 |
| Business rules | BR-16, BR-17, BR-25 |
| Messages | MSG-011, MSG-101, MSG-102, MSG-106, MSG-107 |
| API | `GET /api/v2/pre-mission-assessments`, `GET /api/v2/pre-mission-assessments/{id}`, `POST /api/v2/pre-mission-assessments/{id}/cancel` |

**Normal flow**

1. The manager opens the Result tab.
2. The system shows the overall badge (“READY – SẴN SÀNG BAY” or “NOT READY – CHƯA ĐỦ ĐIỀU KIỆN”), the validity countdown, the component results and the list of reasons.
3. If the result is Ready, the manager selects **“Tạo nhiệm vụ bay”**.
4. The system opens SCR-12 (`/missions/new?assessmentId={id}`) with the region, planned window, target assets and the eligible personnel and UAV candidates (UC-MF02-01).

**Alternative flows**

- **A1 – Cancel.** The manager selects **“Hủy đánh giá”**, enters an optional reason and confirms (MSG-011). The status becomes Cancelled, the audit log records `ASSESSMENT_CANCELLED` and a real-time `AssessmentCancelled` event is sent.

**Exceptions**

- **E1 – Not Ready.** **“Tạo nhiệm vụ bay”** is disabled and the reasons are highlighted (MSG-102).
- **E2 – Expired or used.** The assessment is Expired or Completed; the hand-over is blocked (MSG-106, MSG-107).

**Screen layout**

> *[Screenshot: SCR-10, Result tab – result banner, validity countdown, component outcome blocks, reason list, buttons “Hủy đánh giá” and “Tạo nhiệm vụ bay”.]*

**Figure 15.** *Screen layout – Assessment result and hand-over.*

#### 3.2.7 Assessment Expiry Job (NS-01)

**Table 13.** *NS-01 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Actor | Operations Service background worker |
| Trigger | Every 2 minutes; the same rule is applied when an assessment is read, evaluated or used. |
| Description | Prevent an outdated assessment from being used to create a mission. |
| Processing | The job selects assessments that are not deleted, have status Ready and whose `ValidUntil` has passed, and sets them to Expired. When an assessment in Draft, Evaluating, Ready or Not Ready is read or used after its `ValidUntil` or planned end, it is set to Expired at that moment. |
| Output | Updated status shown on SCR-09 and SCR-10. |
| Business rules | BR-05, BR-17 |
| Implementation | `PreMissionAssessmentExpiryJob`, `AssessmentExpiryPolicy` (Operations Service) |

### 3.3 MF02 – Create and Assign Inspection Mission {#mf02}

MF02 turns a Ready assessment into a dispatched mission. Creation, assignment, resource booking and dispatch are performed in one transaction, so a mission is never left half-booked. After dispatch, the assignees respond and the manager can change the mission until it starts.

#### 3.3.1 Create Inspection Mission (UC-MF02-01)

**Table 14.** *UC-MF02-01 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager (also System Administrator) |
| Trigger | The manager selects **“Tạo nhiệm vụ bay”** on a Ready assessment (UC-MF01-06). |
| Description | Create a mission from a Ready assessment with title, description, priority, objective, priority defect categories, personnel and UAV, then dispatch it (includes UC-MF02-02, UC-MF02-03, UC-MF02-04). |
| Preconditions | The assessment is Ready, not expired and not used (BR-25). The manager may access it (BR-02). |
| Postconditions | A mission with code `MS-yyyyMMddHHmmssfff-nnn` and status Pending Acceptance exists; targets are copied from the assessment in order; assignments and bookings are created; the assessment becomes Completed and is linked to the mission; the audit log records `MISSION_CREATED_FROM_ASSESSMENT`. |
| Priority | High |
| Screens | SCR-12, SCR-13 |
| Business rules | BR-25 – BR-29 |
| Messages | MSG-201 – MSG-207 |
| API | `POST /api/v2/pre-mission-assessments/{id}/create-mission`; `POST /missions/check-resource-conflicts` |

**Input data**

| Field | Rule |
| :------- | :------------------------------------ |
| Title, description | Title required. |
| Priority | Normal, High or Emergency. Emergency requires an emergency reason of at least 10 characters (BR-27). |
| Objective | Periodic Inspection, Targeted Re-inspection, Incident Follow-up or Emergency Inspection. |
| Priority defects | Optional focus categories: Insulator, Conductor, Surge Arrester, Foreign Object, Tower Corrosion, Other. |
| Personnel | At least one person; each with mission role and required flag; all must be eligible candidates of the assessment (BR-26). |
| UAV | At least one UAV from the eligible candidates; the first one is the mission UAV and must be Available and not Critical (BR-26). |
| Idempotency key | Optional; a repeated key returns the mission already created. |

**Normal flow**

1. The system opens SCR-12 with the scope and window of the assessment and the eligible candidates.
2. The manager enters title, priority, objective and optional priority defects.
3. The manager selects personnel with mission role and required flag (UC-MF02-02) and the UAV (UC-MF02-03).
4. Optionally, the manager runs the conflict check.
5. The manager selects **Dispatch**.
6. The system validates the request (BR-25 – BR-27), creates the mission, targets and assignments, creates the bookings (BR-28) and dispatches the mission (UC-MF02-04).
7. The system marks the assessment Completed and opens SCR-13 (MSG-203).

**Alternative flows**

- **A1 – Repeated submission.** The same idempotency key returns the existing mission.

**Exceptions**

- **E1 – Assessment not usable.** It is not Ready, has expired or was already used (`ASSESSMENT_EXPIRED_OR_NOT_READY`, `ASSESSMENT_EXPIRED`, `ASSESSMENT_ALREADY_COMPLETED`, MSG-201).
- **E2 – Missing resources.** No personnel or no UAV (`PERSONNEL_ASSIGNMENT_REQUIRED`, `DRONE_ASSIGNMENT_REQUIRED`, MSG-204).
- **E3 – Not a candidate.** A person or UAV is not an eligible candidate (`RESOURCE_NOT_IN_ASSESSMENT`, MSG-205).
- **E4 – Emergency without reason** (`EMERGENCY_REASON_REQUIRED`, MSG-206).
- **E5 – Conflict or UAV not usable.** A resource is already booked, or the UAV is not Available or is Critical (`RESOURCE_BOOKING_CONFLICT`, `DRONE_NOT_AVAILABLE`, `DRONE_CRITICAL_HEALTH`, MSG-202, MSG-207).

**Screen layout**

> *[Screenshot: SCR-12 Create & Dispatch Mission – assessment summary, title, priority, objective, priority defects, personnel table with role and required flag, UAV selector, conflict check result, Dispatch button.]*

**Figure 16.** *Screen layout – Create and dispatch mission.*

#### 3.3.2 Assign Personnel to Mission (UC-MF02-02)

**Table 15.** *UC-MF02-02 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager (also System Administrator) |
| Trigger | The manager selects personnel on SCR-12, or adds, removes or reassigns a member on SCR-13/SCR-14. |
| Description | Assign eligible personnel with a mission role (for example INSPECTOR, PILOT, ANALYST, TECHNICIAN) and a required flag; change the team before the mission starts (includes UC-MF01-03). |
| Preconditions | The mission has not started (BR-32). The new person is Active and not booked in the window. |
| Postconditions | Assignment and booking created, revoked or moved; the affected users are notified (`MISSION_ASSIGNED`, `MISSION_ASSIGNMENT_REMOVED`); a reassignment is audited (`ASSIGNMENT_REASSIGNED`). |
| Priority | High |
| Screens | SCR-12, SCR-13, SCR-14 |
| Business rules | BR-18, BR-19, BR-32 |
| Messages | MSG-202, MSG-211, MSG-212 |
| API | `POST /missions/{id}/assignments`; `DELETE /missions/{id}/assignments/{assignmentId}`; `POST /missions/{id}/assignments/{assignmentId}/reassign`; `GET /missions/{id}/assignments`; `GET /users/assignable` |

**Normal flow (reassign)**

1. On SCR-13 the manager selects **Reassign** for a member.
2. The system lists the assignable users.
3. The manager selects the new user and enters an optional reason.
4. The system checks the new user (Active, no conflict), releases the old booking, creates a new booking and resets the assignment to Pending.
5. If the role is INSPECTOR or PILOT, the new user becomes the mission inspector.
6. If the mission was ready, it returns to Pending Acceptance (BR-32); a real-time `REASSIGNED` event is sent.

**Alternative flows**

- **A1 – Add member.** The manager adds a person; the system checks the person, creates the assignment and booking and notifies the person.
- **A2 – Remove member.** The manager removes a person; the assignment is revoked and the booking cancelled.

**Exceptions**

- **E1 – Inactive user** (`ASSIGNEE_INACTIVE`, MSG-212).
- **E2 – Booking conflict** (`RESOURCE_BOOKING_CONFLICT`, MSG-202).
- **E3 – Mission started** (`MISSION_IMMUTABLE_AFTER_START`, MSG-211).

**Screen layout**

> *[Screenshot: SCR-13 Overview tab – team panel with role, required flag, response status and Reassign / Remove actions; reassign dialog.]*

**Figure 17.** *Screen layout – Mission team management.*

#### 3.3.3 Assign UAV to Mission (UC-MF02-03)

**Table 16.** *UC-MF02-03 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager (also System Administrator) |
| Trigger | The manager selects the UAV on SCR-12 or **Change UAV** on SCR-14. |
| Description | Assign an available, healthy and unbooked UAV to the mission, or replace it before the start (includes UC-MF01-04). |
| Preconditions | The mission has not started (BR-32). |
| Postconditions | The mission UAV is set; the old UAV booking is cancelled and a new booking created; the audit log records `DRONE_ASSIGNED` or `DRONE_REPLACED`. |
| Priority | High |
| Screens | SCR-12, SCR-14 |
| Business rules | BR-12, BR-19, BR-20, BR-32 |
| Messages | MSG-202, MSG-207, MSG-211 |
| API | `PUT /missions/{id}/drone` |

**Normal flow**

1. The manager opens **Change UAV**.
2. The system lists the UAVs with status, health and conflicts.
3. The manager selects a UAV and confirms.
4. The system checks the UAV (flight status Idle, operational status Available, health not Critical, no booking conflict), replaces the booking and updates the mission.

**Exceptions**

- **E1 – UAV not usable** (`DRONE_UNAVAILABLE`, `DRONE_NOT_AVAILABLE`, `DRONE_CRITICAL_HEALTH`, `DRONE_ALREADY_RESERVED`, MSG-207).
- **E2 – Booking conflict** (`RESOURCE_BOOKING_CONFLICT`, MSG-202).
- **E3 – Mission started** (`MISSION_IMMUTABLE_AFTER_START`, MSG-211).

**Screen layout**

> *[Screenshot: SCR-14 Change UAV dialog – UAV list with operational status, technical health and conflict indicator.]*

**Figure 18.** *Screen layout – Change UAV.*

#### 3.3.4 Dispatch Mission (UC-MF02-04)

**Table 17.** *UC-MF02-04 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | – (included by UC-MF02-01) |
| Secondary actor | Notification Provider |
| Trigger | Mission creation (UC-MF02-01). |
| Description | Reserve the resources, set the mission to Pending Acceptance and notify every assignee. |
| Preconditions | The mission, targets and assignments are valid. |
| Postconditions | One Active booking per person and UAV over the planned window; mission status Pending Acceptance; each assignee receives a `MISSION_DISPATCH` notification (in-app, real-time and e-mail); an outbox event `MissionCreatedFromAssessment` and a real-time `AssessmentCompleted` event are published. |
| Priority | High |
| Business rules | BR-28, BR-29, BR-46 |
| Messages | MSG-203 |
| API | Part of `POST /api/v2/pre-mission-assessments/{id}/create-mission` |

**Normal flow**

1. The system creates the bookings in the same transaction as the mission (BR-28).
2. The system sets the mission to Pending Acceptance.
3. The system stores a `MISSION_DISPATCH` notification for each assignee and publishes the outbox event.
4. The Notification Service pushes the notification in real time and e-mails it (NS-03).

**Exceptions**

- **E1 – Conflict at commit.** A booking conflict is detected; the whole transaction is rolled back (MSG-202).

#### 3.3.5 Preview Mission Targets on Map (UC-MF02-05)

**Table 18.** *UC-MF02-05 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager, Inspector |
| Secondary actor | Map Service |
| Trigger | The user opens the Overview or Assets tab of SCR-13, or the map of SCR-15. |
| Description | Show the mission targets in sequence, their towers and the scope boundary on the map. |
| Preconditions | The user may access the mission (BR-02). |
| Postconditions | None (view only). |
| Priority | Medium |
| Screens | SCR-13, SCR-15, SCR-06 |
| Business rules | BR-02, BR-34 |
| Messages | MSG-003 |
| API | `GET /missions/{id}`; `GET /gis/infrastructure`; `PUT /missions/{id}/assets` (manager edits targets) |

**Normal flow**

1. The user opens the map of the mission.
2. The system draws the boundary and the target towers with their sequence numbers.
3. The user selects a marker to see the tower and asset details.

**Alternative flows**

- **A1 – Edit targets (manager).** Before the start, the manager changes the target assets. If no boundary is given, the system generates one around the assets. Assets outside the assessment scope require a new assessment (BR-34).

**Exceptions**

- **E1 – Invalid targets** (`MISSION_TARGET_REQUIRED`, `DUPLICATE_ASSET`, `ASSET_OUTSIDE_REGION_OR_SCOPE`, `SCOPE_REASSESSMENT_REQUIRED`, MSG-213).

**Screen layout**

> *[Screenshot: SCR-13 Overview tab – map with numbered target towers and boundary.]*

**Figure 19.** *Screen layout – Mission targets on the map.*

#### 3.3.6 View Assigned Mission (UC-MF02-06)

**Table 19.** *UC-MF02-06 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Inspector (also Analyst and UAV Technician for missions they are assigned to) |
| Trigger | The user opens a mission from the notification, SCR-11 or SCR-02. |
| Description | View the scope, planned window, priority, objective, instructions, team, UAV and own assignment status. |
| Preconditions | The user has an active assignment in the mission. |
| Postconditions | None (view only). |
| Priority | High |
| Screens | SCR-11, SCR-15 |
| Business rules | BR-01, BR-02 |
| Messages | MSG-003, MSG-006 |
| API | `GET /missions/my`; `GET /missions/{id}`; `GET /missions/{id}/assignments` |

**Normal flow**

1. The user opens the list of own missions.
2. The user selects a mission; the system opens SCR-15.
3. The system shows the acceptance banner, mission data, targets, UAV and team.

**Exceptions**

- **E1 – No access.** The user is not a member of the mission (`MISSION_ACCESS_DENIED`, MSG-006).

**Screen layout**

> *[Screenshot: SCR-15 Inspector Console – acceptance banner (Accept / Postpone), mission instructions, targets, UAV and team.]*

**Figure 20.** *Screen layout – Inspector Console.*

#### 3.3.7 Respond to Mission Assignment (UC-MF02-07)

**Table 20.** *UC-MF02-07 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Inspector (any assigned member) |
| Trigger | The member selects **Accept** or **Postpone** on SCR-15. |
| Description | Accept the assignment, or postpone it with a reason. |
| Preconditions | The member has an active assignment in the mission. |
| Postconditions | Accept: the response is Accepted; when every required member has accepted, the mission becomes Assigned (`MISSION_CONFIRMED`, real-time `CONFIRMED`). Postpone: the response is Postponed with reason and time; the manager is notified (`ASSIGNMENT_POSTPONED`); if the member is required, the mission becomes Postponed. |
| Priority | High |
| Screens | SCR-15, SCR-16 |
| Business rules | BR-30 |
| Messages | MSG-208, MSG-209 |
| API | `POST /missions/{id}/assignments/accept`; `POST /missions/{id}/assignments/postpone` |

**Normal flow**

1. The member reviews the mission (UC-MF02-06) and selects **Accept**.
2. The system records the response and time.
3. The system checks whether all required members have accepted (BR-30); if so, the mission becomes Assigned.
4. The system shows MSG-208.

**Alternative flows**

- **A1 – Postpone.** The member selects **Postpone**, enters the reason on SCR-16 and confirms. The system records the postponement and notifies the manager.
- **A2 – Accept again.** A second accept returns the existing result without change.

**Exceptions**

- **E1 – Reason missing** (`POSTPONE_REASON_REQUIRED`, MSG-209).
- **E2 – No assignment.** The user has no active assignment in the mission (MSG-006).

**Screen layout**

> *[Screenshot: SCR-16 Postpone dialog – reason field, Confirm / Cancel.]*

**Figure 21.** *Screen layout – Postpone assignment.*

#### 3.3.8 Monitor Assignment Status (UC-MF02-08)

**Table 21.** *UC-MF02-08 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager |
| Secondary actor | Notification Provider |
| Trigger | The manager opens SCR-11 or SCR-13; the overdue job runs (NS-02). |
| Description | Track the responses of the team, send reminders and messages, and be warned when the confirmation deadline passes. |
| Preconditions | The mission is dispatched. |
| Postconditions | Reminder: a `MISSION_REMINDER` notification, a REMINDER communication log and a real-time event. Message: a communication log entry. |
| Priority | Medium |
| Screens | SCR-11, SCR-13 (Overview, Activity) |
| Business rules | BR-30, BR-31 |
| Messages | MSG-214 |
| API | `GET /missions`; `GET /missions/{id}/assignments`; `POST /missions/{id}/remind`; `GET`/`POST /missions/{id}/communications`; `GET /missions/{id}/activities` |

**Normal flow**

1. The manager opens SCR-13.
2. The system shows the assignment overview: required count, accepted count, all-confirmed flag, the confirmation deadline (if set) and each member’s response.
3. The manager selects **Remind**; the system notifies the inspector (or the first active member) and records the reminder (MSG-214).

**Alternative flows**

- **A1 – Send message.** The manager writes a message in the Activity tab; it is stored as a communication log entry.
- **A2 – Deadline passed.** NS-02 notifies the manager once with `MISSION_CONFIRMATION_OVERDUE` and records an OVERDUE log entry; the mission status does not change.

**Screen layout**

> *[Screenshot: SCR-13 Overview and Activity tabs – assignment overview, Remind button, communication timeline.]*

**Figure 22.** *Screen layout – Assignment monitoring.*

#### 3.3.9 Change Dispatched Mission (UC-MF02-09)

**Table 22.** *UC-MF02-09 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager (also System Administrator) |
| Secondary actor | Notification Provider |
| Trigger | The manager selects a change action on SCR-13; the dialog SCR-14 opens. |
| Description | Cancel, suspend, resume or postpone the mission, reassign personnel (UC-MF02-02), change the UAV (UC-MF02-03) or change the priority. |
| Preconditions | The manager may manage the mission’s region (BR-02); the action is allowed in the current status (Table 59). |
| Postconditions | The mission status, bookings and related records are updated; affected users are notified; a communication log entry and a real-time event are recorded. |
| Priority | High |
| Screens | SCR-13, SCR-14 |
| Business rules | BR-05, BR-27, BR-32, BR-33 |
| Messages | MSG-011, MSG-012, MSG-206, MSG-209, MSG-210, MSG-211 |
| API | `POST /missions/{id}/cancel`, `/suspend`, `/resume`, `/postpone`; `PATCH /missions/{id}/priority` |

**Change actions**

| Action | Allowed from | Effect |
| :---- | :---- | :---- |
| Cancel | Draft, Pending Acceptance, Assigned, Preparing, Ready, Postponed, Suspended | Status Cancelled; active bookings cancelled; the assessment link is removed and the assessment returns to Ready if still valid, otherwise Expired; members notified (`MISSION_CANCELLED`). |
| Suspend | Assigned, Preparing, Ready, In Progress | Status Suspended; members notified (`MISSION_SUSPENDED`). |
| Resume | Suspended | In Progress if the mission had started, otherwise Ready or Assigned by readiness; members notified (`MISSION_RESUMED`). |
| Postpone | Pending Acceptance, Assigned, Preparing, Ready | Status Postponed with reason; manager notified (`MISSION_POSTPONED`). |
| Change priority | Any status before the start | Priority, objective and priority defects updated; Emergency requires a reason (BR-27); audited (`MISSION_PRIORITY_UPDATED`). |

**Normal flow**

1. The manager selects the action on SCR-13.
2. The system opens SCR-14 with the reason field and asks for confirmation (MSG-011).
3. The manager enters the reason and confirms.
4. The system checks the status transition (BR-05) and applies the effect.
5. The system notifies the affected users and shows MSG-210.

**Exceptions**

- **E1 – Transition not allowed** (`INVALID_MISSION_STATE`, MSG-012).
- **E2 – Change after start.** Personnel, UAV, targets or priority are changed after the start (`MISSION_IMMUTABLE_AFTER_START`, MSG-211).
- **E3 – Reason missing.** Postpone without reason (`POSTPONE_REASON_REQUIRED`, MSG-209); Emergency priority without reason (`EMERGENCY_REASON_REQUIRED`, MSG-206).
- **E4 – Out of scope** (`REGION_MANAGEMENT_SCOPE_REQUIRED`, MSG-006).

**Screen layout**

> *[Screenshot: SCR-14 Change Mission dialog – action, reason, confirmation.]*

**Figure 23.** *Screen layout – Change mission.*

### 3.4 MF03 – Mission Execution and Field Inspection {#mf03}

MF03 covers the work at the site: check-in, UAV custody, mission start, live telemetry, media upload, field incidents, edge AI alerts and completion. The system tracks the readiness of the mission from the members’ actions and moves it from Assigned to Preparing and Ready (BR-37).

#### 3.4.1 Check In at Mission Site (UC-MF03-01)

**Table 23.** *UC-MF03-01 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Inspector (any assigned member) |
| Trigger | The member selects **Check in** on SCR-15. |
| Description | Record the member’s arrival at the mission site. |
| Preconditions | The mission is Assigned or Preparing; the member has an active and Accepted assignment and has not checked in yet (BR-35). |
| Postconditions | A check-in record with server time is stored; the mission readiness is recalculated (BR-37); when it becomes Ready the audit log records `MISSION_READY`. |
| Priority | High |
| Screens | SCR-15 |
| Business rules | BR-35, BR-37 |
| Messages | MSG-301, MSG-302 |
| API | `POST /missions/{id}/check-in` |

**Normal flow**

1. The member arrives at the site and selects **Check in**.
2. The browser sends the current GPS position when available; the system validates its range.
3. The system records the check-in and recalculates the mission readiness.
4. SCR-15 shows the check-in time and the new mission status.

**Exceptions**

- **E1 – Not allowed.** The mission is not Assigned or Preparing, the assignment is not Accepted, or the user is not a member (`CHECK_IN_INVALID_STATE`, `ASSIGNMENT_NOT_ACCEPTED`, `USER_NOT_ASSIGNED`, MSG-301).
- **E2 – Duplicate** (`DUPLICATE_CHECK_IN`, MSG-302).

**Screen layout**

> *[Screenshot: SCR-15 – field checklist with Check in, UAV handover and Start steps.]*

**Figure 24.** *Screen layout – Field checklist (check-in).*

#### 3.4.2 Confirm UAV Handover (UC-MF03-02)

**Table 24.** *UC-MF03-02 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Inspector |
| Trigger | The inspector selects **Receive UAV** before the flight, or **Return UAV** after the flight, on SCR-15. |
| Description | Record the custody of the mission UAV with its condition, and its return. |
| Preconditions | Handover: the mission is Assigned or Preparing; the UAV is the mission UAV; the receiver is an active member; the UAV has no open handover. Return: the mission is In Progress, Completed or Cancelled and an accepted handover is open (BR-36). |
| Postconditions | Handover: a record Accepted or Rejected with condition; readiness recalculated. Return: return time and condition recorded; the UAV flight status becomes Idle. |
| Priority | High |
| Screens | SCR-15 |
| Business rules | BR-36, BR-37 |
| Messages | MSG-303, MSG-304, MSG-313 |
| API | `POST /missions/{id}/drone-handover`; `POST /missions/{id}/drone-handover/return` |

**Normal flow**

1. The inspector checks the UAV physically and selects **Receive UAV**.
2. The inspector enters the condition and accepts (or rejects) the UAV.
3. The system validates the handover (BR-36), stores it and recalculates readiness.

**Alternative flows**

- **A1 – Reject.** The inspector rejects the UAV because of its condition; the handover is stored as Rejected and the mission does not become Ready. The manager can change the UAV (UC-MF02-03).
- **A2 – Return.** After the flight, the inspector selects **Return UAV** and enters the return condition; the UAV becomes Idle.

**Exceptions**

- **E1 – Handover not allowed** (`HANDOVER_INVALID_STATE`, `DRONE_NOT_ASSIGNED`, `RECEIVER_NOT_ASSIGNED`, `DUPLICATE_HANDOVER`, MSG-303).
- **E2 – Condition missing** (`DRONE_CONDITION_REQUIRED`, MSG-304).
- **E3 – Return not allowed** (`RETURN_HANDOVER_INVALID_STATE`, MSG-313).

**Screen layout**

> *[Screenshot: SCR-15 – UAV handover dialog with UAV code, condition text and Accept / Reject.]*

**Figure 25.** *Screen layout – UAV handover.*

#### 3.4.3 Start Mission Execution (UC-MF03-03)

**Table 25.** *UC-MF03-03 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Inspector (also Mission Manager, System Administrator) |
| Trigger | The inspector selects **Start mission** on SCR-15. |
| Description | Start the field execution of the mission. |
| Preconditions | The mission is Ready, Assigned or Preparing; the user is the mission inspector, a member, or a manager of the region (BR-38). |
| Postconditions | The mission is In Progress; the UAV flight status is Flying; a real-time `IN_PROGRESS` event is sent. |
| Priority | High |
| Screens | SCR-15 |
| Business rules | BR-37, BR-38 |
| Messages | MSG-012, MSG-305 |
| API | `POST /missions/{id}/start` |

**Normal flow**

1. The inspector selects **Start mission**.
2. The system checks the status and the user (BR-38).
3. The system sets the mission In Progress and the UAV Flying and shows MSG-305.

**Alternative flows**

- **A1 – Start from Assigned or Preparing.** If the inspector has not checked in or received the UAV, the system records the check-in and an accepted handover for the inspector before starting.

**Exceptions**

- **E1 – Invalid status** (`INVALID_MISSION_STATE`, MSG-012).
- **E2 – Not a member** (`USER_NOT_ASSIGNED_TO_MISSION`, MSG-006).

#### 3.4.4 Record Asset Inspection Progress (UC-MF03-04) – Deferred

Marking each target as Inspected or Skipped is deferred to a later release. The mission target keeps the status Pending; progress is followed through the uploaded media of each target (UC-MF03-05).

#### 3.4.5 Transmit Telemetry and Field Media (UC-MF03-05)

**Table 26.** *UC-MF03-05 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | Edge Device (telemetry), Inspector (media) |
| Trigger | The edge device publishes MQTT messages during the flight; the inspector uploads files on SCR-15 or SCR-13 (Upload tab). |
| Description | Publish UAV status and telemetry through MQTT; upload inspection images and videos with capture time and GPS position. |
| Preconditions | Telemetry: the UAV code is registered. Media: the mission is not Draft, Pending Acceptance or Cancelled; the uploader is a member (or a manager/admin); the asset is a mission target (BR-40). |
| Postconditions | Telemetry: the latest live state (position, altitude, battery, speed, heading, status) is stored in Redis for 30 seconds. Media: the file is stored, an inspection media record is created and a media-uploaded event starts AI analysis (UC-MF04-01). |
| Priority | High |
| Screens | SCR-15, SCR-13 (Upload tab), SCR-17, SCR-18 |
| Business rules | BR-39, BR-40 |
| Messages | MSG-306, MSG-307, MSG-308 |
| API | MQTT topics `uav/{code}/status`, `uav/{code}/telemetry`; `POST /missions/{id}/media` (multipart); `POST /inspections/upload`; `GET /drones/{id}/status` |

**Normal flow (media)**

1. The inspector selects the target asset and the files.
2. The system validates each file: type JPEG, PNG, WebP, TIFF or MP4, matching extension, at most 50 MB (BR-40).
3. If capture time or GPS position is missing, the system reads them from EXIF.
4. The system stores the file and the media record and publishes the media-uploaded event.
5. SCR-15 shows MSG-306 and the file appears in the Processing tab with status Pending (UC-MF04-03).

**Alternative flows**

- **A1 – Telemetry.** The edge device publishes status and telemetry; NS-04 validates the UAV code and updates the live state shown on SCR-18.
- **A2 – No asset selected.** On the mission upload route, the first target asset is used and the current time is used when no capture time is available.

**Exceptions**

- **E1 – Invalid file.** Unsupported type or extension, or larger than 50 MB (MSG-307).
- **E2 – Upload not allowed.** The mission has not been accepted or is cancelled, the user is not assigned, or the asset is not a target (`MISSION_NOT_EXECUTING_OR_COMPLETED`, `MISSION_CANCELLED`, MSG-308).
- **E3 – Storage failure** (`STORAGE_FAILURE`, MSG-007).
- **E4 – Unknown UAV code.** An MQTT message from an unregistered UAV is ignored and logged.

**Screen layout**

> *[Screenshot: SCR-13 Upload tab – target asset selector, drop zone, upload progress and file list.]*

**Figure 26.** *Screen layout – Media upload.*

#### 3.4.6 Monitor Field Execution (UC-MF03-06)

**Table 27.** *UC-MF03-06 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Mission Manager |
| Secondary actor | Map Service |
| Trigger | The manager opens SCR-02, SCR-13 or the live layer of SCR-06. |
| Description | Follow the live UAV state, the mission lifecycle events, communications, incidents and alerts. |
| Preconditions | The manager may access the mission. |
| Postconditions | None (view only). |
| Priority | Medium |
| Screens | SCR-02, SCR-13, SCR-18 |
| Business rules | BR-39 |
| Messages | MSG-003 |
| API | `GET /drones/{id}?live=true`; `GET /monitor/summary`, `/mission-status`, `/alerts`; `GET /missions/{id}/activities`, `/communications`, `/incidents`, `/flight-logs`; SignalR hub `/hubs/notifications` |

**Normal flow**

1. The manager opens the mission.
2. The system shows the live UAV position, altitude, battery and speed, refreshed from the live state.
3. Mission lifecycle events (accepted, ready, started, suspended, completed) arrive in real time and are added to the timeline.

**Alternative flows**

- **A1 – Link lost.** If no status is received for 30 seconds, the live state expires and the UAV is shown without live data.

**Screen layout**

> *[Screenshot: SCR-18 – map with live UAV marker and telemetry panel; mission timeline.]*

**Figure 27.** *Screen layout – Live monitoring.*

#### 3.4.7 Report Critical Anomaly (UC-MF03-07)

**Table 28.** *UC-MF03-07 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | Edge Device, Inspector |
| Secondary actor | Notification Provider |
| Trigger | The edge model detects an anomaly in flight; the inspector reports a field incident on SCR-15; a client triggers an emergency alert. |
| Description | Bring critical field findings to the manager and analysts immediately. |
| Preconditions | Edge detection: the UAV is registered and assigned to an In Progress mission (BR-42). Incident: the user may execute the mission. |
| Postconditions | Edge detection: an image media and a Pending detection are stored and the mission manager, assignee and inspector are notified (`CriticalAlert` or `InfoAlert`). Emergency alert: an alert with status Active and priority Critical is stored, an `EMERGENCY_ALERT` communication log is written and Analysts, Managers and Administrators are notified. Incident: a report with status Reported is stored; a Critical incident notifies the manager (`CRITICAL_INCIDENT`). |
| Priority | High |
| Screens | SCR-15, SCR-19, SCR-03 |
| Business rules | BR-42, BR-43, BR-44, BR-46 |
| Messages | MSG-310, MSG-311 |
| API | `POST /vision/detections` (multipart), `POST /vision/detections/json`; `POST /alerts/trigger`; `POST /missions/{id}/incidents` |

**Normal flow (edge detection)**

1. The edge device sends the detection: UAV code, class, confidence, bounding box, track ID, time, GPS and image.
2. NS-06 finds the UAV and its In Progress mission, stores the image and the detection.
3. If the class is an emergency class (or its name contains Anomaly, Critical or Emergency), the system sends a critical notification; otherwise an informational one.
4. The manager sees the alert on SCR-03 and SCR-19 (MSG-311).

**Alternative flows**

- **A1 – Field incident.** The inspector enters type, severity (Low, Medium, High, Critical), description and an optional file; the system stores the incident and, if Critical, notifies the manager.
- **A2 – Trigger emergency alert.** An alert is created for the detection and the mission; its reviewers are notified.

**Exceptions**

- **E1 – UAV not registered or no active mission.** The edge detection is refused with a failure result.
- **E2 – Invalid incident** (`INCIDENT_TYPE_REQUIRED`, `SEVERITY_REQUIRED`, `INVALID_SEVERITY`, `DESCRIPTION_TOO_SHORT`, `ASSET_REQUIRED`, MSG-310).

**Screen layout**

> *[Screenshot: SCR-15 – incident report form (type, severity, description, evidence file).]*

**Figure 28.** *Screen layout – Field incident report.*

#### 3.4.8 Handle Critical Alert (UC-MF03-08)

**Table 29.** *UC-MF03-08 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Analyst (also Mission Manager, System Administrator) |
| Secondary actor | Mission Manager (escalation target) |
| Trigger | The analyst opens an alert on SCR-19. |
| Description | Review an emergency alert (confirm or dismiss) or escalate it to a responsible user. |
| Preconditions | The alert exists. |
| Postconditions | Review: status Confirmed or Dismissed, reviewer, notes and resolution time recorded. Escalation: status Escalated, an escalation record with target user and reason, and a notification to the target user. |
| Priority | High |
| Screens | SCR-19 |
| Business rules | BR-44 |
| Messages | MSG-011, MSG-004 |
| API | `GET /alerts/active`, `GET /alerts`, `GET /alerts/{id}`; `PUT /alerts/{id}/review`; `POST /alerts/{id}/escalate` |

**Normal flow**

1. The analyst opens the Active tab of SCR-19.
2. The system lists the active alerts with evidence image, category, confidence, mission and time.
3. The analyst selects an alert and chooses **Confirm** or **Dismiss** with notes.
4. The system records the decision and moves the alert to the History tab.

**Alternative flows**

- **A1 – Escalate.** The analyst selects **Escalate**, chooses the responsible user and enters the reason; the system records the escalation and notifies the user.

**Screen layout**

> *[Screenshot: SCR-19 – Active / History tabs, alert card with evidence image, Confirm / Dismiss / Escalate actions.]*

**Figure 29.** *Screen layout – Emergency alerts.*

#### 3.4.9 Finish Field Inspection (UC-MF03-09)

**Table 30.** *UC-MF03-09 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Inspector (also Mission Manager, System Administrator) |
| Trigger | The inspector submits the flight log and selects **Complete mission** on SCR-15. |
| Description | Submit the flight log, complete the mission and release the resources. |
| Preconditions | Flight log: the mission has been accepted (not Draft or Pending Acceptance). Complete: the mission is In Progress (BR-45). |
| Postconditions | A flight log is stored; the mission is Completed; the UAV flight status becomes Idle; all active bookings are Released; a real-time `COMPLETED` event is sent. |
| Priority | High |
| Screens | SCR-15, SCR-17 |
| Business rules | BR-41, BR-45 |
| Messages | MSG-309, MSG-312 |
| API | `POST /missions/{id}/flight-log`; `POST /missions/{id}/complete`; `GET /missions/{id}/flight-logs` |

**Normal flow**

1. The inspector enters the flight log: GPS track (JSON), flight duration, maximum altitude, minimum battery and connection status.
2. The system validates and stores the flight log (BR-41).
3. The inspector selects **Complete mission**.
4. The system completes the mission, sets the UAV Idle, releases the bookings and shows MSG-312.
5. The inspector returns the UAV (UC-MF03-02, A2).

**Exceptions**

- **E1 – Invalid flight log** (`FLIGHT_LOG_INVALID_STATE`, `GPS_TRACK_REQUIRED`, `INVALID_GPS_TRACK_JSON`, `INVALID_BATTERY_LEVEL`, `INVALID_ALTITUDE`, `INVALID_FLIGHT_DURATION`, MSG-309).
- **E2 – Not In Progress** (`INVALID_MISSION_STATE`, MSG-012).

**Screen layout**

> *[Screenshot: SCR-15 – flight log form and Complete mission button; SCR-17 inspection history.]*

**Figure 30.** *Screen layout – Flight log and completion.*

### 3.5 MF04 – AI Inspection and Defect Detection {#mf04}

MF04 analyses every uploaded media file asynchronously. The AI Inspection Service publishes an analysis request to RabbitMQ, the Cloud AI Service returns the detections, the Inspection Evaluation Service scores each detection, and an analyst confirms or rejects it. A confirmed detection becomes a maintenance ticket and can be included in a defect report.

#### 3.5.1 Submit Inspection Data (UC-MF04-01)

**Table 31.** *UC-MF04-01 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Inspector (also Mission Manager, System Administrator) |
| Trigger | The user uploads media for a mission target (UC-MF03-05). |
| Description | Store the media and queue it for AI analysis automatically (includes UC-MF04-02). |
| Preconditions | The media passed the upload checks (BR-40). |
| Postconditions | A media-uploaded event is in the outbox; NS-05 publishes it and NS-07 creates one analysis request with status Pending; a real-time `AiAnalysisStatusChanged` event is sent to the uploader. |
| Priority | High |
| Screens | SCR-13 (Upload, Processing tabs), SCR-15 |
| Business rules | BR-40, BR-47, BR-49 |
| Messages | MSG-306, MSG-401 |
| API | `POST /missions/{id}/media` |

**Normal flow**

1. The user uploads a file (UC-MF03-05).
2. The system stores the media and writes the media-uploaded event in the same transaction.
3. NS-05 publishes the event; NS-07 creates the analysis request and publishes it to the queue chosen by the preferred model and media type (BR-49).
4. The Processing tab shows the request as Pending (MSG-401).

**Exceptions**

- **E1 – Invalid event.** An event without media, mission, asset or uploader identifiers is moved to the dead-letter queue.

#### 3.5.2 Process AI Analysis (UC-MF04-02)

**Table 32.** *UC-MF04-02 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | – (included by UC-MF04-01 and UC-MF04-06) |
| Secondary actor | Cloud AI Service |
| Trigger | An analysis request is published to an AI request queue. |
| Description | Analyse the media, receive the status and detections, evaluate each detection and store it. |
| Preconditions | The analysis request exists with status Pending. |
| Postconditions | The request is Completed (detections stored with validation status Pending, media set to Pending Review) or Failed (error code and message stored); a real-time `AiAnalysisStatusChanged` event is sent. |
| Priority | High |
| Business rules | BR-49 – BR-53 |
| Messages | MSG-402 – MSG-404 |
| Interfaces | Request queues `ai.analysis.{server\|edge}.{image\|video}.requested`; result queue `ai.analysis.result` with retry queue and dead-letter queue; gRPC `InspectionEvaluation/EvaluateDetection` |

**Normal flow**

1. The Cloud AI Service consumes the request and reports status Processing; the system sets the request to Processing (MSG-402).
2. The Cloud AI Service reports Completed with the model name and version and the list of detections (category code, confidence, bounding box, frame index and time for video, GPS, crop image).
3. NS-08 checks that the identifiers match the stored request and media (BR-50).
4. For each detection, the system maps the category code to a defect category (BR-51), stores the detection and calls NS-10, which returns severity, risk level, priority score and the immediate-alert decision (BR-53).
5. The system sets the request to Completed and the media to Pending Review and notifies the uploader (MSG-403).

**Alternative flows**

- **A1 – Video.** Detections carry frame index and time stamp; the video duration, frame rate and resolution are stored.
- **A2 – Duplicate result.** A result for a request already Completed or Failed is ignored.

**Exceptions**

- **E1 – AI failure.** The Cloud AI Service reports Failed; the request is Failed with the error code and message (MSG-404).
- **E2 – Transient error.** A time-out or database error is retried up to 3 times with a 15-second delay; then the message goes to the dead-letter queue (BR-52).
- **E3 – Unknown category or mismatched identifiers.** The result is rejected and moved to the dead-letter queue.

#### 3.5.3 Monitor AI Processing Status (UC-MF04-03)

**Table 33.** *UC-MF04-03 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | Inspector, Analyst, Mission Manager |
| Trigger | The user opens the Processing tab of SCR-13. |
| Description | Track each analysis request in Pending, Processing, Completed or Failed status. |
| Preconditions | The user may access the mission. |
| Postconditions | None (view only). |
| Priority | Medium |
| Screens | SCR-13 (Processing tab), SCR-20 |
| Business rules | BR-50 |
| Messages | MSG-401 – MSG-404 |
| API | `GET /ai-analysis/{id}`; SignalR event `AiAnalysisStatusChanged` |

**Normal flow**

1. The user opens the Processing tab.
2. The system lists the requests with media, model, status, number of detections and time.
3. When a status changes, the list is refreshed from the real-time event; on Completed the Results tab is reloaded; on Failed the error message is shown.

**Alternative flows**

- **A1 – Trial upload.** On SCR-20, an Analyst or Manager uploads files outside a mission to try the AI. The results are shown but not stored.

**Screen layout**

> *[Screenshot: SCR-13 Processing tab – request list with status badges and detection counts.]*

**Figure 31.** *Screen layout – AI processing status.*

#### 3.5.4 View AI Analysis Results (UC-MF04-04)

**Table 34.** *UC-MF04-04 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | Mission Manager, Inspector, Analyst |
| Trigger | The user opens the Results tab of SCR-13 or a detection on SCR-21. |
| Description | View the detections on the image (or video frame) with category, bounding box, confidence, severity, source and review status. |
| Preconditions | The user may access the mission. |
| Postconditions | None (view only). |
| Priority | High |
| Screens | SCR-13 (Results tab), SCR-21 |
| Business rules | BR-02, BR-53 |
| Messages | MSG-003 |
| API | `GET /missions/{id}/detections?status=&mediaType=&isEmergency=`; `GET /missions/{missionId}/ai-analysis/detections` |

**Normal flow**

1. The user opens the Results tab.
2. The system lists the detections with thumbnail, category, confidence and status, filtered by status, media type and emergency flag.
3. The user selects a detection; the system shows the image with the bounding box and the evaluation (severity, risk level, priority score).

**Screen layout**

> *[Screenshot: SCR-13 Results tab – detection list and image viewer with bounding boxes.]*

**Figure 32.** *Screen layout – AI analysis results.*

#### 3.5.5 Review AI Detections (UC-MF04-05)

**Table 35.** *UC-MF04-05 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | Analyst (also Mission Manager, System Administrator) |
| Trigger | The analyst selects a detection on SCR-22 and opens SCR-21. |
| Description | Confirm or reject the detection. A confirmed detection creates a maintenance ticket. |
| Preconditions | The detection belongs to the mission; the analyst may access the mission. |
| Postconditions | The detection is Confirmed or Rejected with analyst, notes and time; the audit log records `DETECTION_APPROVED` or `DETECTION_REJECTED`. On confirmation: one maintenance ticket is created for the detection (BR-54, BR-55), a defect-confirmed notification is sent and real-time `DETECTION_REVIEWED` and `MAINTENANCE_TASK_CREATED` events are published. |
| Priority | High |
| Screens | SCR-22, SCR-21, SCR-23 |
| Business rules | BR-54, BR-55 |
| Messages | MSG-405, MSG-406 |
| API | `PUT /missions/{missionId}/detections/{detectionId}/review` (Operations Service) |

**Normal flow**

1. The analyst opens SCR-22 and filters the detections waiting for review.
2. The analyst opens a detection on SCR-21 and checks the image, category and confidence.
3. The analyst selects **Confirm**, optionally overrides the severity and enters notes.
4. The system records the decision and creates the maintenance ticket with priority and due date (BR-55), assigned to the mission technician.
5. The system shows MSG-405 with the ticket code.

**Alternative flows**

- **A1 – Reject.** The analyst selects **Reject**, enters the notes on SCR-23 and confirms; the detection is Rejected and no ticket is created.
- **A2 – Re-review.** The analyst changes an earlier decision; the new decision and notes replace the old ones. A ticket already created is kept.

**Exceptions**

- **E1 – Invalid decision or detection** (`INVALID_REVIEW_STATUS`, `ANOMALY_MISSION_MISMATCH`, MSG-406).

**Screen layout**

> *[Screenshot: SCR-21 Detection Review Workspace – image with bounding box, category, confidence, severity, notes, Confirm / Reject / Reanalyse.]*

**Figure 33.** *Screen layout – Detection review.*

#### 3.5.6 Request Media Reanalysis (UC-MF04-06)

**Table 36.** *UC-MF04-06 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | Analyst, Mission Manager |
| Secondary actor | Cloud AI Service |
| Trigger | The user selects **Reanalyse** on SCR-21 (dialog SCR-23). |
| Description | Request a new analysis of existing media with an analysis type and a preferred model (includes UC-MF04-02). |
| Preconditions | The media belongs to the mission; a manager must manage the mission (BR-48). |
| Postconditions | A new analysis request with status Pending is created and queued; earlier requests and detections are kept. |
| Priority | Medium |
| Screens | SCR-21, SCR-23 |
| Business rules | BR-48, BR-49 |
| Messages | MSG-407 |
| API | `POST /missions/{missionId}/ai-analysis/from-media/{mediaId}?analysisType=&preferredModel=&notes=` |

**Normal flow**

1. The user selects **Reanalyse**.
2. The user chooses the analysis type (default General) and the preferred model (default SERVER; EDGE or YOLO11 for the edge model) and enters notes.
3. The system creates and queues the request (MSG-407); the flow continues in UC-MF04-02.

**Exceptions**

- **E1 – Media not in mission** (MSG-406).
- **E2 – No access** (MSG-006).

#### 3.5.7 View Defect Report and Export (UC-MF04-07)

**Table 37.** *UC-MF04-07 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | Mission Manager, Analyst, Inspector (UAV Technician may create and submit) |
| Trigger | The user opens SCR-24. |
| Description | Create a report for a line, substation, missions and period; submit it for approval; approve or reject it; generate and download PDF or Excel; view defect statistics. |
| Preconditions | The user has a role allowed for the action (BR-59). |
| Postconditions | The report is stored with status Draft, Pending or Approved; generated files are stored and can be downloaded. |
| Priority | Medium |
| Screens | SCR-24 |
| Business rules | BR-59, BR-60 |
| Messages | MSG-411, MSG-412 |
| API | `GET`/`POST /reports`; `GET`/`PUT`/`DELETE /reports/{id}`; `PUT /reports/{id}/submit`, `/approve`, `/reject`; `POST /reports/{id}/generate?format=pdf\|excel`; `GET /reports/{id}/download`; `GET /reports/statistics` |

**Report content**

The generated file contains: general information (code, title, type, line, substation, creator, approver, period, number of defects), the missions table (code, title, status, date) and the defects table (category, mission, confidence, validation status, date; the Excel file also contains the analyst notes).

**Normal flow**

1. The user selects **Create report**, enters title, type (Defect, Periodic, Thermal, Corridor), scope, missions and period, and saves; the report is Draft.
2. The user submits the report; it becomes Pending.
3. The manager approves it; it becomes Approved (MSG-411).
4. The user selects **Export PDF** or **Export Excel**; the system generates the file and offers the download (MSG-412).

**Alternative flows**

- **A1 – Reject.** The manager rejects a Pending report with a reason; it returns to Draft.
- **A2 – Edit or delete.** A Draft report can be edited or deleted.

**Exceptions**

- **E1 – Not allowed.** Editing a non-Draft report or approving without the Manager role is refused (MSG-006, MSG-012).

**Screen layout**

> *[Screenshot: SCR-24 – report list with status, create dialog, statistics and export buttons.]*

**Figure 34.** *Screen layout – Reports.*

#### 3.5.8 Manage Maintenance Ticket (UC-MF04-08)

**Table 38.** *UC-MF04-08 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | Mission Manager, UAV Technician |
| Trigger | A ticket is created by UC-MF04-05 or manually; the technician opens the Maintenance tab of SCR-13. |
| Description | Execute the repair of a confirmed defect: start the work, add proof photos and materials, request verification and close the ticket. |
| Preconditions | The ticket exists; the technician is assigned to it, or the user is a Manager or System Administrator. |
| Postconditions | The ticket status is updated; on closure the ticket is Closed, the resolution time is recorded and the detection becomes Resolved; a real-time `MAINTENANCE_TASK_STATUS_CHANGED` event is sent; actions are audited. |
| Priority | High |
| Screens | SCR-26 (Maintenance tab of SCR-13) |
| Business rules | BR-56, BR-57, BR-58 |
| Messages | MSG-408, MSG-409, MSG-410 |
| API | `GET /missions/{id}/maintenance-tasks`; `GET /maintenance/tickets`, `/maintenance/tickets/my`, `/maintenance/tickets/{id}`; `POST /maintenance/tickets`; `PUT /maintenance/tickets/{id}/status`; `POST /maintenance/tickets/{id}/proof`; `POST /maintenance/tickets/{id}/materials`; `PUT /maintenance/tickets/{id}/close` |

**Normal flow**

1. The technician opens an Open ticket and selects **Start**; the ticket becomes In Progress.
2. The technician uploads at least one proof photo with notes and records the materials used.
3. The technician selects **Request verification**; the ticket becomes Pending Verification.
4. The manager checks the proof and closes the ticket; the detection becomes Resolved (MSG-408).

**Alternative flows**

- **A1 – Manual ticket.** The manager creates a ticket for a detection with description, priority, due date and technician (BR-58).
- **A2 – Direct close.** The manager closes a ticket that has a proof photo with **Close** (`PUT /close`).

**Exceptions**

- **E1 – No proof.** Verification or closure without a proof photo is refused (MSG-409).
- **E2 – Ticket closed.** A Closed ticket cannot be changed (MSG-410).
- **E3 – Not assigned.** A technician who is not assigned to the ticket cannot change it (MSG-006).
- **E4 – Invalid status order.** For example Open → Pending Verification (MSG-012).

**Screen layout**

> *[Screenshot: SCR-26 – ticket list and detail with status steps, proof photos, material log, Start / Request verification / Close.]*

**Figure 35.** *Screen layout – Maintenance ticket.*

### 3.6 Common Functions {#cf}

#### 3.6.1 Log In (UC-CF-01)

**Table 39.** *UC-CF-01 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | User (all human actors) |
| Secondary actor | Notification Provider (Brevo) |
| Trigger | The user opens `/login`, or selects **Forgot password** or **Change password**. |
| Description | Authenticate with e-mail and password; verify an e-mailed OTP on an untrusted device; reset a forgotten password or change the password. |
| Preconditions | The account exists, is Active and its e-mail is verified (BR-01). |
| Postconditions | The user receives an access token (60 minutes) and a refresh token (7 days) and lands on SCR-02; if “trust this device” was chosen, a device-trust token valid for 30 days is issued (BR-04). |
| Priority | High |
| Screens | SCR-01, SCR-28, SCR-27 |
| Business rules | BR-01, BR-04 |
| Messages | MSG-001, MSG-008, MSG-009, MSG-010 |
| API | `POST /auth/login`; `POST /auth/otp/send`; `POST /auth/otp/verify`; `POST /auth/refresh-token`; `POST /auth/reset-password`; `POST /users/change-password` |

**Normal flow**

1. The user enters e-mail and password and selects **Đăng nhập**.
2. The system checks the credentials and the account status.
3. If the device is trusted, the system issues the tokens and opens SCR-02.
4. Otherwise the system e-mails a six-digit OTP and opens SCR-28.
5. The user enters the OTP (and optionally ticks “trust this device”).
6. The system verifies the OTP (valid 15 minutes, at most 5 attempts), issues the tokens and opens SCR-02.

**Alternative flows**

- **A1 – Resend OTP.** The user can request a new OTP after the 30-second cooldown.
- **A2 – Forgot password.** The user enters the e-mail, verifies the OTP and receives a verification token (10 minutes) used to set a new password.
- **A3 – Change password.** A logged-in user verifies an OTP to obtain a step-up token (5 minutes) and sets a new password.
- **A4 – Token refresh.** When the access token expires, the client obtains a new one with the refresh token.

**Exceptions**

- **E1 – Wrong credentials** (MSG-008).
- **E2 – Inactive account or unverified e-mail** (MSG-009).
- **E3 – Wrong or expired OTP, or too many attempts** (MSG-010).

**Screen layout**

> *[Screenshot: SCR-01 Login and SCR-28 OTP Verification.]*

**Figure 36.** *Screen layout – Login and OTP.*

#### 3.6.2 Manage User Accounts (UC-CF-02)

**Table 40.** *UC-CF-02 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | System Administrator |
| Trigger | The administrator opens SCR-04. |
| Description | Create, view, update, suspend, reactivate, reset the password of, and delete user accounts; assign one or more roles. |
| Preconditions | The user is a System Administrator. |
| Postconditions | The account is created or updated; changes are audited. |
| Priority | High |
| Screens | SCR-04 |
| Business rules | BR-01, BR-03, BR-06, BR-10 |
| Messages | MSG-001, MSG-004, MSG-011 |
| API | `GET`/`POST /users`; `GET`/`PUT`/`PATCH`/`DELETE /users/{id}`; `POST /users/{id}/suspend`; `POST /users/{id}/reset-password`; `GET /users/assignable` (Manager) |

**Normal flow**

1. The administrator opens SCR-04; the system lists the users with search and paging.
2. The administrator selects **Create user** and enters e-mail, full name, phone, initial password and roles.
3. The system validates and stores the account (MSG-004).

**Alternative flows**

- **A1 – Edit.** The administrator changes name, phone, status or the role list.
- **A2 – Suspend or reactivate.** The administrator suspends an account after confirmation (MSG-011); a suspended user cannot log in or be assigned (BR-18).
- **A3 – Reset password.** The administrator sets a new password for the user.

**Exceptions**

- **E1 – Duplicate e-mail or missing field** (MSG-001).

**Screen layout**

> *[Screenshot: SCR-04 User Management – user table, create/edit dialog with role selection.]*

**Figure 37.** *Screen layout – User management.*

#### 3.6.3 Manage Personnel Qualifications (UC-CF-03) – Deferred

Recording personnel qualifications (licences and certificates with validity dates) is deferred to a later release. In the current release, personnel eligibility is decided by role, account status, geographic scope and booking conflicts (BR-18).

#### 3.6.4 Manage Infrastructure Assets (UC-CF-04)

**Table 41.** *UC-CF-04 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | System Administrator, Mission Manager (other roles view only) |
| Secondary actor | Map Service |
| Trigger | The user opens SCR-07 or SCR-06. |
| Description | Maintain the infrastructure hierarchy (region, substation, line, tower, asset) with coordinates; import towers from Excel; browse the grid asset catalogue and its linked defects and tickets. |
| Preconditions | Changes require the System Administrator or Mission Manager role. |
| Postconditions | Records are created, updated or soft-deleted; changes are audited. |
| Priority | High |
| Screens | SCR-07, SCR-06 |
| Business rules | BR-06, BR-10, BR-11 |
| Messages | MSG-001, MSG-003, MSG-004, MSG-013 |
| API | `/regions`, `/substations`, `/lines`, `/towers` (incl. `POST /towers/import`), `/assets` (incl. `POST /assets/spatial-query`); `GET /gis/infrastructure` |

**Normal flow**

1. The user opens SCR-07.
2. The system lists the assets with code, type, tower, line, region, operational status, last inspection date and number of open defects; filters by region, line, tower, type and status.
3. The user opens an asset; the system shows its technical data, location on the map and the linked detections and maintenance tickets.

**Alternative flows**

- **A1 – Create or edit.** The manager adds or edits a region, substation, line, tower or asset with its coordinates.
- **A2 – Import towers.** The manager uploads an Excel file of towers for a line; the system validates and imports the rows.
- **A3 – Spatial search.** The user draws a polygon on SCR-06; the system returns the assets inside it.

**Exceptions**

- **E1 – Invalid coordinates or geometry** (MSG-013).
- **E2 – Not authorised** (MSG-006).

**Screen layout**

> *[Screenshot: SCR-07 Grid Asset Catalogue – asset table with filters; asset detail with map and linked defects.]*

**Figure 38.** *Screen layout – Grid asset catalogue.*

#### 3.6.5 Manage UAV Inventory (UC-CF-05)

**Table 42.** *UC-CF-05 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | Mission Manager, UAV Technician, System Administrator |
| Trigger | The user opens SCR-08. |
| Description | Maintain UAV records (code, model, serial number, region, operational status, battery, endurance, range) and the flight-controller pairing. |
| Preconditions | Create and update: Manager, Technician or Administrator. Delete: Manager or Administrator. |
| Postconditions | The UAV record is created, updated or soft-deleted; deleting a UAV deactivates its flight-controller bindings; changes are audited. |
| Priority | High |
| Screens | SCR-08 |
| Business rules | BR-10, BR-12, BR-13 |
| Messages | MSG-001, MSG-004, MSG-011, MSG-112 |
| API | `GET`/`POST /drones`; `GET`/`PUT`/`DELETE /drones/{id}`; `GET /drones/available`; `GET /drones/{id}/technical-inspections`; `GET /drones/{id}/flight-controller`, `/flight-controller/history` |

**Normal flow**

1. The user opens SCR-08; the system lists the UAVs with operational status, flight status, technical health, battery and last inspection.
2. The user selects **Add UAV** and enters the data.
3. The system validates and stores the UAV (MSG-004).

**Alternative flows**

- **A1 – Edit or change status.** The user changes the data or the operational status (for example Maintenance).
- **A2 – Pair flight controller.** See UC-MF01-05, A1.
- **A3 – Technical check.** See UC-MF01-05.

**Screen layout**

> *[Screenshot: SCR-08 UAV Fleet Management – UAV table, add/edit dialog, technical check and pairing dialog.]*

**Figure 39.** *Screen layout – UAV fleet management.*

#### 3.6.6 View Notifications (UC-CF-06)

**Table 43.** *UC-CF-06 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | User |
| Trigger | A notification arrives in real time, or the user opens SCR-03. |
| Description | Receive in-app notifications, mark them as read, delete them and open the linked record. |
| Preconditions | The user is logged in and connected to the SignalR hub. |
| Postconditions | The notification is marked as read or deleted. |
| Priority | Medium |
| Screens | SCR-03 |
| Business rules | BR-46 |
| Messages | MSG-003 |
| API | `GET /notifications/history`; `GET /notifications/{id}`; `PUT /notifications/{id}/read`; `DELETE /notifications/{id}`; SignalR hub `/hubs/notifications` (event `NotificationReceived`) |

**Normal flow**

1. The system pushes a `NotificationReceived` event to all connections of the user; the bell shows the unread count.
2. The user opens SCR-03; the system lists the notifications by time.
3. The user selects a notification; the system marks it as read and opens the linked record (mission, assessment, detection, alert or ticket).

**Alternative flows**

- **A1 – Offline user.** Notifications are stored first (BR-46); the user sees them in the history after logging in, and NS-03 sends them by e-mail.

#### 3.6.7 View Audit Log (UC-CF-07)

**Table 44.** *UC-CF-07 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actors | System Administrator, Mission Manager |
| Trigger | The user opens SCR-25. |
| Description | Search the audit records of data changes and business actions. |
| Preconditions | The user is a System Administrator or Mission Manager. |
| Postconditions | None (view only). |
| Priority | Medium |
| Screens | SCR-25 |
| Business rules | BR-06 |
| Messages | MSG-003 |
| API | `GET /audit-logs?tableName=&actionType=&search=&page=&pageSize=` |

**Normal flow**

1. The user opens SCR-25.
2. The system lists the audit records with time, user, table, record, action (Added, Modified, Deleted or a business action), old and new values and IP address.
3. The user filters by table, action and keyword and opens a record to compare old and new values.

**Screen layout**

> *[Screenshot: SCR-25 Audit Log Viewer – filters, record table and old/new value comparison.]*

**Figure 40.** *Screen layout – Audit log.*

#### 3.6.8 Manage System Settings (UC-CF-08)

**Table 45.** *UC-CF-08 specification.*

| Item | Description |
| :------- | :------------------------------------ |
| Primary actor | System Administrator (Mission Manager may view) |
| Trigger | The administrator calls the System Settings API (no web screen in the current release). |
| Description | View and change the configurable thresholds in Table 57 without changing the code. |
| Preconditions | Change: System Administrator. View and reload: System Administrator or Mission Manager. |
| Postconditions | The new value is stored and used by all services within 60 seconds (cache), or immediately after a reload. |
| Priority | Low |
| Business rules | BR-09 |
| Messages | MSG-001, MSG-004 |
| API | `GET /system-settings`; `GET /system-settings/{key}`; `PUT /system-settings/{key}`; `POST /system-settings/reload` |

**Normal flow**

1. The administrator lists the settings.
2. The administrator updates the value of a key.
3. The system validates that the value is not empty, stores it and clears the cache.

**Exceptions**

- **E1 – Empty value** (`INVALID_VALUE`, MSG-001).

## 4. Non-Functional Requirements {#non-functional-requirements}

### 4.1 External Interfaces {#external-interfaces}

Table 46 lists the software interfaces between UAV-PMS and the actors in Figure 1, and the internal infrastructure the system depends on. Table 47 lists the hardware of the team-built UAV that UAV-PMS communicates with.

**Table 46.** *Software interfaces.*

| ID | Interface | Protocol | Requirement |
| :---- | :---- | :---- | :---- |
| UI-01 | Web client (Angular) | HTTPS, JSON REST through the gateway; SignalR (WebSocket) for real-time events | Used by all human actors for every screen in Table 3. |
| EXT-01 | Edge Device | MQTT (Mosquitto) topics `uav/{code}/status` and `uav/{code}/telemetry`; HTTPS for the Vision Bridge (`/vision/detections`) | The UAV code in the payload must match a registered UAV; Vision Bridge calls carry a valid access token. |
| EXT-02 | Cloud AI Service | RabbitMQ (AMQP): request queues `ai.analysis.{server\|edge}.{image\|video}.requested`, result queue `ai.analysis.result` | Asynchronous; every request and result carries the analysis request ID and event ID. |
| EXT-03 | Map Service | Leaflet with OpenStreetMap tiles (HTTPS) | Base map only; UAV-PMS is the source of truth for tower and asset coordinates (WGS 84). |
| EXT-04 | Notification Provider | HTTPS APIs of SendGrid (notification e-mails) and Brevo (OTP and password e-mails) | The in-app notification is stored even if e-mail delivery fails. |
| INT-01 | API Gateway | Ocelot, HTTPS | Single public entry point; routes `/api/v{n}/…` to the owning service; exposes `/health` per service. |
| INT-02 | Message broker | RabbitMQ topic exchange with dead-letter exchanges | Durable queues for integration events and AI jobs; consumers are idempotent. |
| INT-03 | Cache | Redis | Stores UAV live state (30 s TTL) and OTP data; a cache failure must not change stored data. |
| INT-04 | Database | PostgreSQL with PostGIS | Stores all business and geospatial data; schema managed by EF Core migrations. |
| INT-05 | Inspection Evaluation | gRPC over HTTP/2 (internal only) | Called by the AI Inspection Service for every stored detection. |

**Table 47.** *Hardware interfaces of the team-built UAV.*

| ID | Component | Connection to UAV-PMS | Requirement |
| :---- | :---- | :---- | :---- |
| HW-01 | Onboard computer (Raspberry Pi) | Wi-Fi or 4G; MQTT and HTTPS | Runs the edge client and the YOLO edge model; buffers telemetry and detections when the link is lost. |
| HW-02 | Flight controller (Betaflight) | Serial link to the Raspberry Pi; USB/Web Serial to the technician’s browser | Provides position, attitude and battery data; its hardware UID identifies the UAV (UC-MF01-05). Flight control stays with the flight controller and the pilot’s radio. |
| HW-03 | Camera | CSI or USB to the Raspberry Pi | Captures images with time and GPS position (UC-MF03-05). |
| HW-04 | GPS module | Connected to the flight controller | Supplies the position used in telemetry, media metadata and alerts. |

*Note for the UAV team: add the exact model and main specifications of HW-01 to HW-04 (and the airframe, motors and battery) as agreed in Report 2.*

### 4.2 Quality Attributes {#quality-attributes}

#### 4.2.1 Usability

**Table 48.** *Usability requirements.*

| ID | Requirement |
| :------- | :------------------------------------ |
| NFR-USA-01 | A trained user reaches any MF01–MF04 primary screen from SCR-02 in at most three clicks. |
| NFR-USA-02 | Validation errors name the field and the corrective action (Section 5.3); stack traces are never shown to users. |
| NFR-USA-03 | Uploads and AI jobs show their status (Pending, Processing, Completed, Failed); reloading the page does not lose the status. |
| NFR-USA-04 | Cancel, suspend, reject and delete actions ask for confirmation (MSG-011) and offer a reason field. |
| NFR-USA-05 | The web client works on the current stable Chrome and Edge; SCR-15 is usable on a 360-pixel-wide mobile screen for field use. Web Serial pairing requires a Chromium-based desktop browser. |
| NFR-USA-06 | The user interface is in Vietnamese; codes and identifiers follow this specification. |

#### 4.2.2 Reliability

**Table 49.** *Reliability requirements.*

| ID | Requirement |
| :------- | :------------------------------------ |
| NFR-REL-01 | Mission creation, assignment and booking changes are transactional; a failed operation leaves no partial booking (BR-28). |
| NFR-REL-02 | Integration events are written to an outbox in the same transaction as the business data and published afterwards. |
| NFR-REL-03 | Message consumers are idempotent: processing the same AI result or event twice gives the same result (BR-50). |
| NFR-REL-04 | Transient AI-result failures are retried at most 3 times with a 15-second delay; failed messages are moved to a dead-letter queue (BR-52). |
| NFR-REL-05 | Concurrent updates of assessments and missions are detected by a version number and rejected (BR-07). |
| NFR-REL-06 | The Edge Device buffers telemetry and detections for at least 30 minutes without link and resends them in order. |
| NFR-REL-07 | The system is available 99% of the time during scheduled test and demonstration windows. |
| NFR-REL-08 | The database is backed up daily; restore is tested once before the release freeze (RPO 24 hours, RTO 4 hours). |

#### 4.2.3 Performance

**Table 50.** *Performance requirements.*

| ID | Requirement |
| :------- | :------------------------------------ |
| NFR-PER-01 | With 50 concurrent users, 95% of read requests complete within 2 seconds and 95% of write requests within 3 seconds (excluding file transfer). |
| NFR-PER-02 | An upload is acknowledged within 5 seconds after the file transfer ends. |
| NFR-PER-03 | A new analysis request is visible as Pending within 3 seconds; a received AI result is visible on SCR-13 within 5 seconds. |
| NFR-PER-04 | Telemetry is shown on SCR-18 within 2 seconds after the server receives it. |
| NFR-PER-05 | An edge critical detection is stored and notified to the manager within 5 seconds after the server receives it; e-mail delivery time is measured separately. |
| NFR-PER-06 | Lists are paginated (default 10–20 rows, at most 100 rows per page). |

#### 4.2.4 Security and Privacy

**Table 51.** *Security and privacy requirements.*

| ID | Requirement |
| :------- | :------------------------------------ |
| NFR-SEC-01 | All external traffic uses TLS. Credentials, API keys and signing keys are not stored in source control. |
| NFR-SEC-02 | Passwords are stored with a one-way password-hashing algorithm; access tokens expire after 60 minutes and refresh tokens after 7 days. |
| NFR-SEC-03 | Every protected API checks the user’s role and geographic scope on the server (BR-01, BR-02); path IDs are GUIDs. |
| NFR-SEC-04 | Uploaded files are checked for type, extension and size before storage; user-supplied file names are never used as storage paths. |
| NFR-SEC-05 | OTPs expire after 15 minutes, allow at most 5 attempts and can be resent after 30 seconds. The OTP bypass code (`Otp:BypassCode`) is used only in development and test and is disabled in production. |
| NFR-SEC-06 | Passwords, tokens and keys are never written to logs; administrative and security actions are audited (BR-06). |
| NFR-SEC-07 | The SignalR hub accepts only authenticated connections; a user receives only the events of their own user group and of missions they may access. |

#### 4.2.5 Maintainability, Observability and Compatibility

**Table 52.** *Maintainability, observability and compatibility requirements.*

| ID | Requirement |
| :------- | :------------------------------------ |
| NFR-MNT-01 | Each service writes structured logs with timestamp, level, service name, correlation ID and message. |
| NFR-MNT-02 | Each service exposes a `/health` endpoint that returns HTTP 200 when it is running; the gateway exposes `/health/{service}`. |
| NFR-MNT-03 | API changes stay backward compatible or are released as a new API version (`/api/v1`, `/api/v2`); deprecated endpoints return a `Warning: 299` header. |
| NFR-MNT-04 | Database schema changes are made only through version-controlled EF Core migrations. |
| NFR-MNT-05 | Each service follows the CQRS folder and naming conventions of the codebase and returns the common `ApiResponse` envelope. |
| NFR-MNT-06 | A release is accepted only if the solution builds without errors and all P0/P1 test cases pass. |
| NFR-MNT-07 | Business thresholds in Table 57 are configuration values and can be changed without code changes (BR-09). |

#### 4.2.6 AI Model Quality

**Table 53.** *AI model quality requirements.*

| ID | Requirement |
| :------- | :------------------------------------ |
| NFR-AI-01 | Every deployed model is evaluated on the fixed, versioned test split of the seven-class dataset and reported with precision, recall, mAP50, mAP50-95 and per-class results (Report 2, Table 2). |
| NFR-AI-02 | Each analysis request stores the model name and version that produced its detections, so results can be traced to an evaluation report. |
| NFR-AI-03 | The Server/Deep analysis of one image completes within 60 seconds under normal load (`Ai:TimeoutSeconds`), excluding queue waiting time. |
| NFR-AI-04 | The final accuracy targets of the Edge/Fast and Server/Deep models are set from the AI evaluation results and recorded in Part III before the release freeze. |

## 5. Requirement Appendix {#requirement-appendix}

### 5.1 Business Rules {#business-rules}

Business rules are grouped by function area. Default values of configurable rules are in Table 57.

**Table 54.** *Business rules.*

| ID | Rule Definition |
| :------- | :------------------------------------ |
| | **General** |
| BR-01 | Only an authenticated user with an Active account and a verified e-mail may use protected functions. Each API checks the user’s role (Table 4). |
| BR-02 | A Mission Manager acts only on regions in their geographic scope (a scope row for the region, or a global scope). A System Administrator has every region. A mission member may access the missions they are assigned to. |
| BR-03 | Each account has one system role. `Technician` and `MaintenanceTechnician` are equivalent. `Pilot` is used only as a mission-assignment role equivalent to Inspector; it is not a system role. |
| BR-04 | Login from an untrusted device requires an e-mailed OTP. A trusted device stays trusted for 30 days after its last use. |
| BR-05 | Status changes follow the state tables in Section 5.4.2; other transitions are rejected. |
| BR-06 | Data changes and business actions are audited with user, time, table, record, old and new values, IP address and user agent. |
| BR-07 | Assessments and missions carry a version number; an update based on an older version is rejected. |
| BR-08 | Times are stored in UTC and displayed in the Asia/Ho_Chi_Minh time zone. |
| BR-09 | Business thresholds are system settings (Table 57), cached for 60 seconds, not hard-coded. |
| BR-10 | Regions, substations, lines, towers, assets, UAVs, users and missions are soft-deleted; their history is kept. |
| | **Assets and UAVs** |
| BR-11 | Coordinates use WGS 84 (EPSG:4326). The hierarchy is Region → Substation → Line → Tower → Asset; each asset belongs to one tower. |
| BR-12 | A UAV has an operational status (Available, Reserved, In Use, Maintenance, Unavailable, Lost) and a flight status (Idle, Online, Flying, Maintenance, Offline). Only an Available UAV can be assigned. |
| BR-13 | A flight-controller UID is bound to at most one UAV and a UAV to at most one active UID; pairing deactivates the previous bindings of both. |
| | **MF01 – Readiness** |
| BR-14 | Only a Mission Manager or System Administrator may create, evaluate or cancel an assessment. The region must be active and in scope; at least one asset is selected, without duplicates; the planned end is after the planned start and in the future. |
| BR-15 | Every target asset is Active/Operational, belongs to the region and has coordinates. A boundary, if given, is a valid Polygon or MultiPolygon of at most 1,000 vertices and 65,536 characters and contains every target asset. |
| BR-16 | The result is Ready only when the site component passes and at least one eligible Inspector, one eligible Analyst, one eligible Technician and one eligible UAV exist; otherwise Not Ready with the reason of each failed component. |
| BR-17 | An evaluated assessment stays valid for a limited time (default 4 hours) and never beyond its planned end. After that it becomes Expired. Expired, Completed and Cancelled assessments cannot be evaluated. |
| BR-18 | A person is eligible when the account is Active and verified, holds a field role (Inspector, Analyst or Technician) and no SystemAdmin or Manager role, is in scope for the region and has no booking conflict. |
| BR-19 | A resource has a booking conflict when an Active booking overlaps the requested interval [start, end). |
| BR-20 | A UAV is eligible when it is Available, has no booking conflict, its technical health is Healthy or Warning, and its scope suitability is not Unsuitable. |
| BR-21 | Scope suitability: the system estimates the flight distance and time needed to inspect the target assets and compares them with the UAV’s endurance and operational range. The result is *Suitable* when one flight is enough, *Suitable with split* when a few flights (2–4) are needed, and *Unsuitable* otherwise. |
| BR-22 | A technical inspection is Failed if any critical item fails. Health is Critical if a critical item fails, Warning if a non-critical item fails, otherwise Healthy. The inspection is valid for 7 days. |
| BR-23 | A battery level below the critical threshold fails the check; a level below the warning threshold passes with a warning. An RF link quality below the minimum fails the check. The thresholds are system settings (Table 57). |
| BR-24 | If a flight-controller UID is supplied with an inspection, it must be registered and bound to the inspected UAV. |
| | **MF02 – Mission and assignment** |
| BR-25 | Only a Ready, unexpired and unused assessment can create a mission. One assessment creates at most one mission; the assessment then becomes Completed and keeps the link. |
| BR-26 | A mission has at least one person and one UAV, all chosen from the eligible candidates of the assessment. The mission UAV must be Available and not Critical. |
| BR-27 | Emergency priority requires an emergency reason of at least 10 characters. |
| BR-28 | The mission, its targets, assignments and one Active booking per person and UAV over the planned window are created in one transaction. |
| BR-29 | On dispatch, the mission becomes Pending Acceptance and every assignee receives a `MISSION_DISPATCH` notification. |
| BR-30 | Only the assignee responds to their assignment. The mission becomes Assigned when every active required assignment is Accepted. A postponement requires a reason; a postponement by a required member sets the mission to Postponed. |
| BR-31 | When the confirmation deadline of a Pending Acceptance mission has passed, the manager is notified once (`MISSION_CONFIRMATION_OVERDUE`). |
| BR-32 | Personnel, UAV, targets and priority cannot be changed after the mission has started. A reassignment checks that the new user is Active and unbooked, releases the old booking, resets the assignment to Pending and returns a ready mission to Pending Acceptance. |
| BR-33 | Cancelling a mission cancels its bookings and releases its assessment (Ready if still valid, otherwise Expired). In Progress and Completed missions cannot be cancelled; an In Progress mission can only be suspended. |
| BR-34 | Mission targets must be inside the assessment scope; targets outside it require a new assessment. |
| | **MF03 – Field execution** |
| BR-35 | Only an active member with an Accepted assignment can check in, once, while the mission is Assigned or Preparing. |
| BR-36 | A UAV handover is allowed while the mission is Assigned or Preparing, only for the mission UAV, to an active member, with a condition, and only one open handover per UAV. The return is allowed when the mission is In Progress, Completed or Cancelled and sets the UAV to Idle. |
| BR-37 | A mission is Ready when all required members have accepted, all active members have checked in, the UAV is set, at least one target exists and the UAV has an accepted, unreturned handover. With assignments and a UAV and at least one check-in or handover it is Preparing; otherwise Assigned. |
| BR-38 | A mission can be started from Ready, Assigned or Preparing by the mission inspector, a member or a manager of the region; the UAV becomes Flying. |
| BR-39 | Telemetry is accepted only from a registered UAV code that matches the topic; the live state expires after 30 seconds without a message. |
| BR-40 | Accepted media: JPEG, PNG, WebP, TIFF and MP4 with a matching extension, at most 50 MB per file. Upload is refused for Draft, Pending Acceptance and Cancelled missions, for non-members and for assets that are not mission targets. Missing capture time and GPS position are taken from EXIF. |
| BR-41 | A flight log is accepted only after the mission has been accepted; the GPS track is valid JSON; battery is 0–100%; altitude and duration are not negative. |
| BR-42 | An edge detection is accepted only for a registered UAV assigned to an In Progress mission. It is critical when its category is an emergency class or its class name contains “Anomaly”, “Critical” or “Emergency”. |
| BR-43 | A field incident has a type, a severity (Low, Medium, High, Critical) and a description of at least 10 characters; a Critical incident notifies the manager. |
| BR-44 | An emergency alert starts Active; a review sets it to Confirmed or Dismissed; an escalation requires a target user and a reason. Analysts, Managers and Administrators are notified when an alert is created. |
| BR-45 | A mission can be completed only from In Progress; completion sets the UAV to Idle and releases all Active bookings. |
| BR-46 | A notification is stored before it is pushed in real time or e-mailed; a delivery failure does not lose it. |
| | **MF04 – AI analysis, review and maintenance** |
| BR-47 | Each uploaded media file creates one analysis request; repeated media-uploaded events do not create duplicates. |
| BR-48 | Reanalysis may be requested by an Analyst or by a Manager of the mission; it creates a new request and keeps earlier requests and detections. |
| BR-49 | Preferred model `EDGE` or `YOLO11` routes the request to the edge queue, any other value (default `SERVER`) to the server queue; images and videos use separate queues. |
| BR-50 | An analysis request moves Pending → Processing → Completed or Failed. A result must match the stored request and media; results for a finished request are ignored. |
| BR-51 | Every detection must map to an existing defect category code; it is stored with validation status Pending. |
| BR-52 | A transient error while processing a result is retried at most 3 times with a 15-second delay; other errors move the message to the dead-letter queue. |
| BR-53 | Each detection receives a priority score from 0 to 100 that grows with its confidence and is higher for emergency defect classes. The score sets the severity: Critical, High, Medium or Low. An emergency-class detection with high confidence also raises an immediate alert. The thresholds are system settings (Table 57). |
| BR-54 | A review decision is Confirmed or Rejected with optional notes; the reviewer and time are recorded. A confirmed detection creates exactly one maintenance ticket, and the ticket is pushed to the mission page in real time. |
| BR-55 | An automatic ticket has priority Emergency if the category is an emergency class or the analyst overrides the severity to Critical/Urgent, Low if overridden to Low, otherwise High. Due date: 1 day for Emergency, otherwise 7 days. It is assigned to the mission’s technician. |
| BR-56 | Ticket status moves Open → In Progress → Pending Verification → Closed. Only the assigned technician, a Manager or an Administrator changes it. Pending Verification and Closed require at least one proof photo. A Closed ticket cannot be changed; closing sets the detection to Resolved. |
| BR-57 | A proof is a JPEG, PNG, WebP or TIFF image of at most 50 MB. A material record has a name, a unit and a quantity greater than 0. |
| BR-58 | A manual ticket has a description of at most 1,000 characters, a priority (Low, Medium, High, Emergency), a due date not in the past, a technician with the Technician role and an existing detection. |
| BR-59 | A report is edited or deleted only in Draft. Submit: Draft → Pending. Approve: Pending → Approved, by a Manager or Administrator. Reject (with reason): Pending → Draft. |
| BR-60 | Reports and exports apply the same role and scope permissions as the screens. |

### 5.2 Common Requirements {#common-requirements}

**Table 55.** *Common requirements.*

| ID | Requirement |
| :------- | :------------------------------------ |
| CR-01 | Mandatory fields are marked with an asterisk and validated on both client and server. |
| CR-02 | List screens provide search, sorting, filters and pagination (NFR-PER-06). |
| CR-03 | An empty result shows an empty-state message (MSG-003), not an error. |
| CR-04 | Dates are shown as dd/MM/yyyy HH:mm in the Asia/Ho_Chi_Minh time zone (BR-08). |
| CR-05 | Every API response uses the envelope `{ success, message, data, errors }`; paginated data contains `items` and pagination information. |
| CR-06 | A version conflict shows MSG-005 and reloads the record. |
| CR-07 | User-visible errors show a message and the backend error code; technical details are written only to logs. |
| CR-08 | Notifications link only to records the recipient may open and contain no password or token. |
| CR-09 | An external-service failure (storage, database, e-mail, AI) is shown as MSG-007, not as a user input error. |
| CR-10 | Status is never shown by colour alone; badges also carry text. Controls are keyboard-operable and labelled. |

### 5.3 Application Messages List {#application-messages}

Messages are numbered by area: MSG-0xx common, MSG-1xx MF01, MSG-2xx MF02, MSG-3xx MF03 and MSG-4xx MF04. Text in braces is replaced at run time. The last column gives the backend error code(s) that produce the message.

**Table 56.** *Application messages.*

| Code | Type | Context | Content | Backend code |
| :---- | :---- | :---- | :---- | :---- |
| MSG-001 | Inline | Required or invalid input | {Field} is required. | Validation error, `INVALID_VALUE` |
| MSG-002 | Inline | Invalid time range | The end time must be later than the start time and in the future. | `INVALID_PLANNED_TIME`, `INVALID_TIME_RANGE` |
| MSG-003 | Empty state | No result | No records match the selected filters. | – |
| MSG-004 | Toast | Save succeeded | Changes saved successfully. | – |
| MSG-005 | Error banner | Version conflict | This record was changed by another user. Refresh and try again. | `ASSESSMENT_CONCURRENCY_CONFLICT`, `MISSION_CONCURRENCY_CONFLICT` |
| MSG-006 | Error banner | Not authorised | You do not have permission to perform this action. | HTTP 403, `REGION_MANAGEMENT_SCOPE_REQUIRED`, `MISSION_ACCESS_DENIED`, `ASSESSMENT_ACCESS_DENIED`, `USER_NOT_ASSIGNED_TO_MISSION` |
| MSG-007 | Error banner | External service failure | {Service} is temporarily unavailable. Try again later. | `STORAGE_FAILURE`, `DATABASE_FAILURE` |
| MSG-008 | Error banner | Wrong credentials | E-mail or password is incorrect. | HTTP 401 |
| MSG-009 | Error banner | Account not usable | Your account is inactive or its e-mail is not verified. Contact the administrator. | HTTP 401, `ACTIVE_USER_REQUIRED` |
| MSG-010 | Inline | Invalid OTP | The OTP is incorrect or has expired. Request a new code. | OTP verification failure |
| MSG-011 | Confirm dialog | Confirm critical action | Are you sure you want to {action} {record}? | – |
| MSG-012 | Error banner | Invalid status | This action is not allowed while the {record} is {status}. | `INVALID_MISSION_STATE`, `INVALID_ASSESSMENT_STATUS` |
| MSG-013 | Inline | Invalid geometry | The boundary or coordinates are invalid. | `INVALID_GEOMETRY` |
| MSG-101 | Status | Assessment Ready | The assessment is Ready until {validUntil}. You can create a mission. | – |
| MSG-102 | Status | Assessment Not Ready | The assessment is Not Ready: {reasons}. | Reason codes of Table 9 and Table 10 |
| MSG-103 | Inline | Asset outside region | Asset {code} does not belong to region {region}. | `ASSET_OUTSIDE_MANAGEMENT_SCOPE`, `ASSETS_NOT_FOUND_OR_OUTSIDE_SCOPE` |
| MSG-104 | Inline | Invalid asset | Asset {code} is inactive, has no coordinates or is outside the boundary. | `ASSETS_INACTIVE`, `ASSET_MISSING_GEOLOCATION`, `ASSET_OUTSIDE_BOUNDARY` |
| MSG-105 | Inline | Invalid scope | Select at least one asset and do not repeat assets. | `INVALID_PROPOSED_SCOPE` |
| MSG-106 | Error banner | Assessment expired | The assessment has expired. Create or re-evaluate a new assessment. | `ASSESSMENT_EXPIRED` |
| MSG-107 | Error banner | Assessment closed | The assessment has already been used or cancelled. | `ASSESSMENT_ALREADY_COMPLETED`, `ASSESSMENT_CANCELLED` |
| MSG-108 | Toast | Technical inspection saved | Technical inspection saved: {Passed/Failed}, health {health}. | – |
| MSG-109 | Error banner | Controller not registered | The connected flight controller is not registered. Pair it with the UAV first. | `FC_NOT_REGISTERED` |
| MSG-110 | Error banner | Controller mismatch | The connected flight controller belongs to {uav}, not to {expectedUav}. | `FC_DRONE_MISMATCH` |
| MSG-111 | Inline | Invalid metrics | Enter at least one metric and do not repeat a metric. | `METRICS_REQUIRED`, `DUPLICATE_METRIC` |
| MSG-112 | Toast | Controller paired | Flight controller {uid} is paired with {uav}. | – |
| MSG-201 | Error banner | Cannot create mission | Only a Ready assessment that has not been used or expired can create a mission. | `ASSESSMENT_EXPIRED_OR_NOT_READY` |
| MSG-202 | Inline | Resource conflict | {Resource} is already booked from {start} to {end}. | `RESOURCE_BOOKING_CONFLICT` |
| MSG-203 | Toast | Mission dispatched | Mission {code} dispatched. Assignment notifications have been sent. | – |
| MSG-204 | Inline | Resources missing | Select at least one person and one UAV. | `PERSONNEL_ASSIGNMENT_REQUIRED`, `DRONE_ASSIGNMENT_REQUIRED` |
| MSG-205 | Inline | Not a candidate | {Resource} is not an eligible candidate of the assessment. | `RESOURCE_NOT_IN_ASSESSMENT` |
| MSG-206 | Inline | Emergency reason | Enter an emergency reason of at least 10 characters. | `EMERGENCY_REASON_REQUIRED` |
| MSG-207 | Inline | UAV not usable | UAV {code} is not available or its technical health is Critical. | `DRONE_NOT_AVAILABLE`, `DRONE_UNAVAILABLE`, `DRONE_CRITICAL_HEALTH`, `DRONE_ALREADY_RESERVED` |
| MSG-208 | Toast | Assignment accepted | Mission assignment accepted. | – |
| MSG-209 | Inline | Postpone reason | Enter a reason for the postponement. | `POSTPONE_REASON_REQUIRED` |
| MSG-210 | Toast | Mission changed | Mission {action} completed. Affected users have been notified. | – |
| MSG-211 | Error banner | Mission started | The mission has started; its team, UAV, targets and priority can no longer be changed. | `MISSION_IMMUTABLE_AFTER_START` |
| MSG-212 | Inline | Inactive assignee | {User} is not active and cannot be assigned. | `ASSIGNEE_INACTIVE` |
| MSG-213 | Inline | Invalid targets | The targets are empty, repeated or outside the assessment scope. | `MISSION_TARGET_REQUIRED`, `DUPLICATE_ASSET`, `ASSET_OUTSIDE_REGION_OR_SCOPE`, `SCOPE_REASSESSMENT_REQUIRED` |
| MSG-214 | Toast | Reminder sent | A reminder has been sent to {user}. | – |
| MSG-301 | Error banner | Check-in blocked | You cannot check in: {reason}. | `CHECK_IN_INVALID_STATE`, `ASSIGNMENT_NOT_ACCEPTED`, `USER_NOT_ASSIGNED` |
| MSG-302 | Info | Already checked in | You have already checked in for this mission. | `DUPLICATE_CHECK_IN` |
| MSG-303 | Error banner | Handover blocked | The UAV handover is not allowed: {reason}. | `HANDOVER_INVALID_STATE`, `DRONE_NOT_ASSIGNED`, `RECEIVER_NOT_ASSIGNED`, `DUPLICATE_HANDOVER` |
| MSG-304 | Inline | Condition missing | Describe the condition of the UAV. | `DRONE_CONDITION_REQUIRED` |
| MSG-305 | Toast | Mission started | Mission started. Telemetry recording is on. | – |
| MSG-306 | Toast | Media received | {n} file(s) uploaded and queued for AI analysis. | – |
| MSG-307 | Inline | Unsupported file | {File}: only JPEG, PNG, WebP, TIFF or MP4 up to 50 MB are accepted. | Validation error |
| MSG-308 | Error banner | Upload not allowed | Media cannot be uploaded for this mission or asset now. | `MISSION_NOT_EXECUTING_OR_COMPLETED`, `MISSION_CANCELLED` |
| MSG-309 | Inline | Invalid flight log | The flight log is incomplete or invalid: {field}. | `FLIGHT_LOG_INVALID_STATE`, `GPS_TRACK_REQUIRED`, `INVALID_GPS_TRACK_JSON`, `INVALID_BATTERY_LEVEL`, `INVALID_ALTITUDE`, `INVALID_FLIGHT_DURATION` |
| MSG-310 | Inline | Invalid incident | Enter the incident type, severity and a description of at least 10 characters. | `INCIDENT_TYPE_REQUIRED`, `SEVERITY_REQUIRED`, `INVALID_SEVERITY`, `DESCRIPTION_TOO_SHORT`, `ASSET_REQUIRED` |
| MSG-311 | Critical alert | Critical anomaly | Emergency: {class} detected on mission {code} at {location}. | – |
| MSG-312 | Toast | Mission completed | Mission completed. Return the UAV and review the AI results. | – |
| MSG-313 | Error banner | Return blocked | The UAV can be returned only after the mission has started and an accepted handover exists. | `RETURN_HANDOVER_INVALID_STATE` |
| MSG-401 | Status | AI pending | The media is queued for AI analysis. | – |
| MSG-402 | Status | AI processing | AI analysis is in progress. | – |
| MSG-403 | Status | AI completed | AI analysis completed: {n} detection(s) need review. | – |
| MSG-404 | Error banner | AI failed | AI analysis failed: {error}. Request a reanalysis or contact the administrator. | AI `ErrorCode` |
| MSG-405 | Toast | Review saved | Detection {decision}. Maintenance ticket {code} has been created. | – |
| MSG-406 | Error banner | Invalid review | The detection or decision is invalid for this mission. | `INVALID_REVIEW_STATUS`, `ANOMALY_MISSION_MISMATCH` |
| MSG-407 | Toast | Reanalysis queued | Reanalysis requested with model {model}. | – |
| MSG-408 | Toast | Ticket updated | Ticket {code} is now {status}. | – |
| MSG-409 | Error banner | Proof required | Upload at least one repair proof photo first. | Business rule error |
| MSG-410 | Error banner | Ticket closed | This ticket is closed and cannot be changed. | Business rule error |
| MSG-411 | Toast | Report status | Report {code} {submitted / approved / returned to draft}. | – |
| MSG-412 | Toast | Export ready | The report has been exported as {format}. | – |

### 5.4 Other Requirements {#other-requirements}

#### 5.4.1 Configuration Parameters

Table 57 lists the system settings (UC-CF-08, BR-09) with their default values, followed by fixed parameters defined in configuration files. Values must be confirmed with the stakeholder before the release.

**Table 57.** *Configuration parameters and default values.*

| Key | Default | Meaning | Rule |
| :---- | :---- | :---- | :---- |
| `Otp:ExpiryMinutes` | 15 | OTP lifetime (minutes) | BR-04 |
| `Otp:CooldownSeconds` | 30 | Minimum time between OTP sends | BR-04 |
| `Otp:MaxAttempts` | 5 | Maximum OTP attempts | BR-04 |
| `Otp:BypassCode` | 123456 | Development/test bypass code; disabled in production | NFR-SEC-05 |
| `Auth:AccessTokenExpiryMinutes` | 60 | Access token lifetime | NFR-SEC-02 |
| `Auth:RefreshTokenExpiryDays` | 7 | Refresh token lifetime | NFR-SEC-02 |
| `Auth:DeviceTrustDays` | 30 | Trusted device lifetime | BR-04 |
| `Auth:StepUpTokenExpiryMinutes` | 5 | Step-up token for password change | UC-CF-01 |
| `Auth:VerificationTokenExpiryMinutes` | 10 | Verification token for password reset | UC-CF-01 |
| `Ai:EmergencyConfidenceThreshold` | 0.80 | Minimum confidence for an immediate alert | BR-53 |
| `Ai:CriticalScoreThreshold` | 90 | Priority score for Critical | BR-53 |
| `Ai:HighScoreThreshold` | 75 | Priority score for High | BR-53 |
| `Ai:MediumScoreThreshold` | 50 | Priority score for Medium | BR-53 |
| `Ai:DefaultPreferredModel` | SERVER | Default AI model route | BR-49 |
| `Ai:TimeoutSeconds` | 60 | AI processing time-out | NFR-AI-03 |
| `Drone:CriticalBatteryThreshold` | 30 % | Battery below which the check fails | BR-23 |
| `Drone:WarningBatteryThreshold` | 50 % | Battery below which a warning is given | BR-23 |
| `Drone:MinRfLinkThreshold` | 50 | Minimum RF link quality | BR-23 |
| `Drone:InspectionValidityDays` | 7 | Technical inspection validity | BR-22 |
| *Fixed:* assessment validity | 4 hours (or planned end) | – | BR-17 |
| *Fixed:* assessment expiry job | every 2 minutes | – | NS-01 |
| *Fixed:* emergency reason length | ≥ 10 characters | – | BR-27 |
| *Fixed:* `InspectionMedia:MaxFileSizeBytes` | 50 MB | Maximum media file size | BR-40 |
| *Fixed:* `Mqtt:LiveStateTtlSeconds` | 30 seconds | UAV live-state expiry | BR-39 |
| *Fixed:* AI result retry | 3 attempts, 15 seconds apart | – | BR-52 |
| *Fixed:* overdue and e-mail jobs | every minute | – | NS-02, NS-03 |
| *Fixed:* display time zone | Asia/Ho_Chi_Minh | – | BR-08 |

#### 5.4.2 State Tables

The tables below define the allowed statuses and transitions referenced by BR-05.

**Table 58.** *Readiness assessment states.*

| State | Meaning | Next states (trigger) |
| :---- | :---- | :---- |
| Draft | Created, not yet evaluated | Evaluating (UC-MF01-02) |
| Evaluating | Created; evaluation running | Ready, Not Ready (UC-MF01-02) |
| Ready | All components pass | Completed (UC-MF02-01), Expired (BR-17), Cancelled (UC-MF01-06), Ready/Not Ready (re-evaluation) |
| Not Ready | At least one component fails | Ready/Not Ready (re-evaluation), Expired, Cancelled |
| Completed | A mission was created from it | Ready or Expired when that mission is cancelled (BR-33) |
| Expired | Validity or planned end passed | – (final) |
| Cancelled | Cancelled by the manager | – (final) |

**Table 59.** *Mission states.*

| State | Meaning | Next states (trigger) |
| :---- | :---- | :---- |
| Pending Acceptance | Dispatched; waiting for the required members | Assigned (all required accepted), Postponed, Cancelled |
| Assigned | All required members accepted | Preparing, Ready (BR-37), In Progress (UC-MF03-03), Suspended, Postponed, Cancelled |
| Preparing | Some check-ins or handover done | Ready, In Progress, Suspended, Postponed, Cancelled |
| Ready | All field preconditions met (BR-37) | In Progress, Suspended, Postponed, Cancelled, Pending Acceptance (reassignment) |
| In Progress | Field execution running | Completed (UC-MF03-09), Suspended |
| Suspended | Execution paused | In Progress, Ready or Assigned (resume), Cancelled |
| Postponed | Postponed by the manager or a required member | Cancelled |
| Completed | Field work finished | – (final) |
| Cancelled | Mission cancelled; bookings cancelled | – (final) |

*Draft is used only by the deprecated `POST /missions` endpoint.*

**Table 60.** *Other state models.*

| Object | States and transitions |
| :------- | :------------------------------------ |
| Assignment response | Pending → Accepted or Postponed; reassignment resets to Pending; a replaced or removed member becomes Replaced or Cancelled. The assignment record is Active, Unavailable or Revoked. |
| Resource booking | Active → Released (mission completed, member replaced) or Cancelled (mission cancelled, member or UAV removed). |
| Technical inspection | Pending → In Progress → Passed, Failed or Incomplete; technical health Unknown, Healthy, Warning or Critical. |
| UAV handover | Accepted or Rejected → Returned. |
| AI analysis request | Pending → Processing → Completed or Failed. |
| Detection | Pending → Confirmed or Rejected (re-review allowed) → Resolved (ticket closed). |
| Emergency alert | Active → Confirmed, Dismissed or Escalated. |
| Maintenance ticket | Open → In Progress → Pending Verification → Closed (Resolved is also accepted as a final status). |
| Report | Draft → Pending → Approved; Pending → Draft on rejection. |

#### 5.4.3 Defect Categories

**Table 61.** *Canonical defect categories.*

| Code | Category (dataset class, Report 1 LI-5) | Emergency class |
| :---- | :---- | :----: |
| DC-01 | Cracked insulator | No |
| DC-02 | Broken insulator | Yes |
| DC-03 | Flashover-damaged insulator | No |
| DC-04 | Surge-arrester insulation damage | No |
| DC-05 | Damaged conductor strands | Yes |
| DC-06 | Damaged conductor insulation sheath | No |
| DC-07 | Bird nest / foreign object | No |

*The emergency-class flag (`IsEmergencyClass`) drives the priority score and the immediate-alert decision (BR-53). The values are the team’s proposal and must be confirmed with the EVN stakeholder.*

#### 5.4.4 Requirement Traceability

Table 62 links each main flow to its use cases, screens, non-screen functions and main entities (Report 2, Table 2, *Requirements and traceability*).

**Table 62.** *Traceability from main flow to screens and data.*

| Flow | Use cases | Screens | Non-screen functions | Main entities |
| :---- | :---- | :---- | :---- | :---- |
| CF | UC-CF-01 – UC-CF-08 | SCR-01 – SCR-04, SCR-06 – SCR-08, SCR-25, SCR-27, SCR-28 | NS-03, NS-05, NS-09, NS-11, NS-12 | User, Role, Region, Substation, Transmission Line, Tower, Asset, UAV, Flight Controller Binding, Notification, Audit Log |
| MF01 | UC-MF01-01 – UC-MF01-06 | SCR-08 – SCR-10 | NS-01, NS-11 | Pre-Mission Assessment, Technical Inspection, Technical Metric, Resource Booking |
| MF02 | UC-MF02-01 – UC-MF02-09 | SCR-11 – SCR-16 | NS-02, NS-03, NS-05 | Mission, Mission Assignment, Resource Booking, Communication Log |
| MF03 | UC-MF03-01 – UC-MF03-09 | SCR-15, SCR-17 – SCR-19 | NS-04, NS-06 | Field Check-in, Drone Handover, Flight Log, Incident Report, Inspection Media, Emergency Alert |
| MF04 | UC-MF04-01 – UC-MF04-08 | SCR-13, SCR-20 – SCR-24, SCR-26 | NS-07, NS-08, NS-10 | AI Analysis Request, Detected Anomaly, Defect Category, Maintenance Ticket, Maintenance Proof, Material Log, Report |

<div style="page-break-after: always;"></div>

# III. Literature Review {#iii.-literature-review}

## 1. Introduction {#1.-introduction}

Inspection of electrical transmission infrastructure is essential for maintaining power-system reliability, identifying deteriorated components at an early stage, and reducing the risk of unexpected failures. Conventional inspection methods commonly rely on ground patrols, manual visual observation, or direct physical access to transmission-line assets. Although these approaches remain widely used, they are often labor-intensive, time-consuming, and potentially hazardous when infrastructure is located in difficult or high-risk environments. Unmanned Aerial Vehicles (UAVs) have therefore become increasingly important for electrical-infrastructure inspection because they can collect high-resolution visual data from towers, conductors, insulators, and surrounding environments while reducing the need for direct human exposure to hazardous locations.    

The increasing availability of UAV imagery has also accelerated the application of computer vision and deep-learning techniques to automated infrastructure monitoring. Previous research has investigated convolutional neural networks, one-stage detectors from the YOLO family, transformer-based detectors such as RT-DETR, and multimodal inspection systems for tasks including asset localization, defect detection, foreign-object identification, and power-line monitoring. These approaches demonstrate the potential of artificial intelligence to support faster and more consistent inspection processes, particularly when large volumes of aerial imagery must be analyzed.    

However, UAV-based electrical-infrastructure inspection remains a challenging visual-recognition problem. Important defects may occupy only a small region of a high-resolution image, while the apparent size and orientation of components can vary significantly depending on flight altitude, camera angle, and viewing distance. In addition, vegetation, buildings, shadows, occlusion, motion blur, and changing illumination may interfere with object detection. Certain defect classes are also visually ambiguous. For example, an apparently discontinuous conductor may represent actual physical damage, but it may also result from temporary occlusion or an unfavorable viewpoint.

Another important issue is the reliability of the evaluation process itself. Model performance can be influenced by class imbalance, annotation quality, inconsistent bounding-box definitions, and differences in datasets, image resolution, or evaluation protocols. Consequently, comparing only headline accuracy values from different studies may lead to misleading conclusions about the relative effectiveness of detection architectures.

Accordingly, this literature review examines the main technical foundations and previous research relevant to UAV-based electrical-infrastructure inspection. Particular attention is given to object-detection architecture families, multi-scale and frequency-domain feature representation, structural context, temporal evidence, annotation quality, and evaluation methodology. The review also considers the trade-off between detection accuracy and computational efficiency, which is particularly important when models are intended for both embedded UAV hardware and server-side analysis.

The purpose of this review is therefore not only to identify high-performing detection models, but also to establish a foundation for a controlled comparative evaluation. Representative lightweight YOLO models are considered for edge-oriented deployment, while RT-DETR-R50 serves as the principal transformer-based reference for server-side experimentation. Frequency-domain enhancement, structural-context reasoning, and multi-frame verification are treated as targeted experimental extensions whose value must be demonstrated empirically rather than assumed in advance. This framing is consistent with the benchmark-oriented scope of the study.

## 2. Conceptual Foundations and Domain Challenges {#2.-conceptual-foundations-and-domain-challenges}

### 2.1 Conceptual Foundations and Core Theories {#2.1-conceptual-foundations-and-core-theories}

#### 2.1.1 UAV-Based Electrical-Infrastructure Inspection {#2.1.1-uav-based-electrical-infrastructure-inspection}

Unmanned Aerial Vehicles (UAVs) have become an important platform for electrical-infrastructure inspection because they can collect visual data from locations that are difficult, expensive, or unsafe for human inspectors to access directly. In transmission-line environments, UAVs can be equipped with RGB cameras, thermal sensors, LiDAR, positioning systems, and communication modules depending on the inspection objective.

Among these sensing modalities, RGB imagery is particularly suitable for visually observable defects and abnormalities, including damaged insulators, foreign objects, broken components, corrosion, and vegetation-related issues. Thermal imagery is more appropriate for detecting abnormal heat patterns, whereas LiDAR provides geometric information such as conductor sag, clearance, and three-dimensional structure.

The present study focuses primarily on RGB-based object detection from UAV imagery and video. Autonomous flight control, three-dimensional reconstruction, and thermal fault diagnosis are outside the core scope of the research. Limiting the study to a common visual input modality allows different detection architectures to be evaluated under a more consistent experimental setting.

#### 2.1.2 Object Detection Architecture Families {#2.1.2-object-detection-architecture-families}

Modern object-detection methods can be broadly grouped into several architectural families according to how candidate objects are represented, localized, and classified.

Two-stage detectors, such as Faster R-CNN, first generate candidate regions and then classify and refine those regions in a separate stage. This design has historically provided strong localization performance, but the additional processing stages generally increase computational cost and inference latency.

One-stage detectors, represented by the YOLO family, perform object localization and classification within a unified detection pipeline. Their relatively high inference speed, compact model variants, and efficient feature extraction make them attractive for real-time and edge-oriented applications. These characteristics are especially relevant to UAV inspection, where inference may need to operate under limited computing and power resources.

Transformer-based detectors adopt a different formulation. DETR and its derivatives use attention mechanisms and object queries to perform set-based prediction. RT-DETR extends this approach toward real-time detection, providing a transformer-based alternative to conventional one-stage detectors while retaining competitive computational efficiency. RT-DETR-based models have also been investigated for transmission-line and insulator-defect detection.

In this study, these architecture families are used primarily as representative benchmarks rather than as starting points for designing a completely new detector. Lightweight YOLO models represent the practical edge-deployment family, while RT-DETR-R50 is used as the principal transformer-based reference for controlled experiments.

#### 2.1.3 Multi-Scale Feature Representation {#2.1.3-multi-scale-feature-representation}

UAV imagery often contains objects with substantial variation in apparent size. Large electrical structures may occupy a significant portion of an image, whereas defects such as broken strands, damaged insulator elements, or small foreign objects may occupy only a limited number of pixels.    

This scale variation creates a fundamental challenge for object detection. High-resolution feature maps preserve local spatial details that are important for detecting small objects, while deeper and lower-resolution feature maps contain stronger semantic information. Effective detectors therefore commonly combine information from multiple feature levels.

Previous detection approaches have addressed this problem using strategies such as feature-pyramid networks, additional high-resolution detection layers, adaptive feature fusion, multi-scale neck structures, attention mechanisms, and higher input resolutions. 

For this study, multi-scale representation is important not only as a model-design consideration but also as an evaluation factor. A detector may achieve strong overall performance while continuing to perform poorly on small or fine-grained defect categories. Consequently, per-class and object-scale analysis is required in addition to global mAP.

#### 2.1.4 Frequency-Domain Feature Representation {#2.1.4-frequency-domain-feature-representation}

Most object detectors operate primarily in the spatial domain, where visual information is represented directly through image pixels and learned spatial feature maps. However, recent studies have investigated frequency-domain information as a complementary representation for object detection.

Frequency-domain representations can emphasize characteristics such as edges, repetitive patterns, high-frequency details, and texture transitions that may be difficult to preserve through conventional spatial feature extraction.

Recent works such as FES-DETR and DFIR-DETR have investigated the use of frequency-domain enhancement for UAV and small-object detection. These studies suggest that frequency information may provide complementary cues for fine-grained visual patterns and small targets.    

In the present study, frequency-domain enhancement is treated as a targeted modification to the RT-DETR baseline rather than as an assumed improvement. Its contribution must therefore be evaluated experimentally against the unchanged baseline under the same dataset and evaluation protocol.

The corresponding research question can be formulated as:

*Does frequency-domain feature enhancement provide measurable improvements for difficult or fine-grained electrical-infrastructure defect categories compared with the unchanged RT-DETR baseline?*

This formulation keeps the experiment focused on measurable improvement rather than architectural complexity itself.

#### 2.1.5 Structural Context and Component Relationships {#2.1.5-structural-context-and-component-relationships}

Electrical infrastructure has an important property that distinguishes it from many generic object-detection environments: its components are not positioned independently.

Conductors generally follow continuous paths, insulators occur in specific supporting configurations, and electrical components have relatively constrained spatial relationships with towers, cross-arms, fittings, and surrounding objects.    

These relationships provide information that may not be available from local visual appearance alone. Relevant structural relationships may include continuity, connectivity, support, expected component presence, relative geometry, and clearance.

For example, an apparent conductor discontinuity may be more suspicious if it violates an otherwise continuous conductor path. Similarly, the interpretation of a foreign object may depend not only on its appearance but also on its position relative to electrical components.

In this study, structural context is therefore considered as an additional source of evidence rather than as a replacement for conventional visual detection.

The proposed structural-context experiment is intended to evaluate whether learnable relationships between detected components can improve performance beyond appearance-based detection alone. Structural reasoning is consequently treated as a controlled extension of the RT-DETR baseline rather than as the basis of an entirely new architecture.    

This distinction is important because a negative result is still meaningful. If structural context improves only certain defect classes, or does not produce a statistically meaningful improvement, the experiment can still clarify where contextual reasoning is useful and where conventional visual features are sufficient.

#### 2.1.6 Temporal and Multi-Frame Evidence {#2.1.6-temporal-and-multi-frame-evidence}

UAV inspections frequently generate video sequences or multiple neighboring images rather than isolated single frames. As a result, the same electrical component may appear in several consecutive observations from slightly different viewpoints.

This temporal redundancy can provide additional evidence when a single frame is visually ambiguous. For example, a conductor may appear discontinuous in one frame because of vegetation, motion blur, or viewpoint occlusion. If the same conductor appears continuous in neighboring frames, the apparent defect may be more likely to represent a temporary visual artifact than a persistent structural failure.    

Previous work in related video-analysis domains has demonstrated that structural consistency and persistent object-state information can improve interpretation across frames. SSTM applies structural consistency and spatiotemporal matching to satellite-video detection, while StateTrace maintains object-level state and visibility information over extended video sequences.    

These methods do not directly solve electrical-infrastructure defect detection. Nevertheless, they support the broader principle that temporal observations can provide evidence unavailable from a single image.

Accordingly, the present study treats multi-frame information as a verification mechanism rather than as a complete video-understanding architecture. Its purpose is to determine whether neighboring frames can help distinguish persistent defects from temporary visual ambiguity while keeping the primary benchmark focused on object detection.

### 2.2 Problem Space and Domain Challenges {#2.2-problem-space-and-domain-challenges}

#### 2.2.1 Small and Fine-Grained Defects {#2.2.1-small-and-fine-grained-defects}

A major challenge in UAV-based electrical-infrastructure inspection is that many important defects occupy only a small portion of a high-resolution image. Examples include broken conductor strands, localized insulator damage, small foreign objects, missing fittings, and other fine-grained abnormalities.

When an object is represented by only a limited number of pixels, the detector has less visual information available for both classification and localization. Small defects can also disappear after repeated downsampling inside a deep neural network, particularly when the background contains strong textures such as vegetation, cables, or structural components.

This challenge is one reason why previous studies have explored high-resolution detection layers, multi-scale feature fusion, region-focused processing, and higher input resolutions. However, these strategies usually increase computational cost. Therefore, improving small-object sensitivity must be balanced against inference efficiency, especially for edge deployment.

#### 2.2.2 Viewpoint and Scale Variation {#2.2.2-viewpoint-and-scale-variation}

UAV imagery is captured under highly variable geometric conditions. The same electrical component may be observed from different altitudes, camera distances, angles, and flight trajectories. As a result, its apparent size, shape, orientation, and visible surface may change considerably across images.

This variation can make detection difficult even when the physical object itself does not change. For example, a defect that is clearly visible from a frontal view may become partially hidden or visually compressed when viewed from an oblique angle. Long and thin structures such as conductors are particularly sensitive to perspective distortion.

Viewpoint variation also complicates benchmarking. A model may perform well on images captured under a narrow set of conditions but generalize poorly when exposed to different flight paths or camera distances. Therefore, model performance should be interpreted in relation to the diversity of the evaluation data rather than as a purely architecture-dependent property.

#### 2.2.3 Complex Backgrounds and Environmental Interference {#2.2.3-complex-backgrounds-and-environmental-interference}

Electrical infrastructure is often surrounded by visually complex environments. UAV imagery may contain sky, trees, vegetation, roads, buildings, mountains, nearby towers, and other electrical structures within the same scene.

These backgrounds can introduce patterns that resemble target objects or partially obscure them. Vegetation is especially challenging because it can simultaneously act as background clutter, an occluding object, and an inspection concern when it approaches electrical assets.    

Environmental conditions further increase visual variability. Changes in brightness, shadow, haze, backlighting, motion blur, compression artifacts, and defocus can alter the appearance of the same component without any physical change in the infrastructure itself.

Consequently, a reliable inspection model must distinguish defect-related visual evidence from environmental variation. This motivates the use of representative data augmentation and robustness testing, but also highlights the need to evaluate models on realistic aerial imagery rather than on visually clean examples alone.

#### 2.2.4 Occlusion and Visual Ambiguity {#2.2.4-occlusion-and-visual-ambiguity}

Occlusion is a particularly important challenge in electrical-infrastructure inspection because many components are thin, elongated, or partially hidden by surrounding objects.

A conductor may temporarily disappear behind vegetation, another component, or a structural element. In a single frame, this missing visual segment may resemble a broken conductor even though the physical line remains intact. Motion blur, compression, and unfavorable viewpoints can produce similar visual ambiguity.

This creates an important limitation for purely appearance-based single-frame detection. The model must infer physical condition from incomplete visual evidence, even though multiple physical states may produce similar image patterns.

This challenge does not imply that conventional object detectors are unsuitable. Instead, it motivates more careful error analysis and provides a rationale for evaluating additional contextual or multi-frame evidence when single-frame predictions are uncertain.

#### 2.2.5 Intra-Class Variation and Inter-Class Similarity {#2.2.5-intra-class-variation-and-inter-class-similarity}

Objects belonging to the same electrical-infrastructure category may have substantially different visual appearances.

Insulators, for example, can vary in material, geometry, number of discs, installation configuration, surface condition, and viewing direction. Defects within the same category may also appear differently depending on their severity and location.

This phenomenon is known as intra-class variation. A detector must therefore learn a category that may contain many visually different examples.

At the same time, different categories may share similar visual features. This inter-class similarity can make it difficult to distinguish a defect from a normal structure or to separate visually related defect types.

The combination of high intra-class variation and inter-class similarity increases the risk of class confusion and makes per-class evaluation particularly important.

#### 2.2.6 Dataset Imbalance and Taxonomy Constraints {#2.2.6-dataset-imbalance-and-taxonomy-constraints}

Electrical defects do not occur with equal frequency in real inspection data. Common conditions are easier to collect, while severe or rare failures may appear only occasionally. This naturally produces imbalanced datasets in which some classes contain substantially more training examples than others.    

Class imbalance can cause a model to perform well on dominant categories while remaining weak on rare but operationally important defects. Aggregate mAP may therefore hide poor performance on individual classes.

A second issue concerns taxonomy consistency. For controlled benchmarking, the meaning of each class must remain stable across training and evaluation. The current project uses the following six-class taxonomy:  
1\. insulator  
2\. insulator\_broken  
3\. insulator\_flashover  
4\. bird\_nest  
5\. foreign\_object  
6\. broken\_strand

Maintaining a fixed taxonomy is necessary because changing class definitions between experiments would make direct comparison unreliable. A measured improvement could otherwise reflect a simpler or altered labeling scheme rather than a genuinely better detector.    

For this reason, class definitions, dataset versions, and train-validation-test splits should be documented and kept consistent during controlled comparisons.

#### 2.2.7 Annotation Quality and Bounding-Box Semantics {#2.2.7-annotation-quality-and-bounding-box-semantics}

Annotation quality is another major factor affecting both model training and evaluation.

In object detection, disagreement can arise not only from incorrect class labels or missing objects, but also from different interpretations of what a bounding box should represent. For the same defect, an annotation may enclose only the damaged region, the complete affected component, or the component together with surrounding contextual structure.    

This distinction becomes especially important for elongated or structurally ambiguous categories such as conductor damage. Two bounding boxes may refer to the same physical defect while having substantially different spatial extents.

Evaluation metrics based on Intersection over Union are sensitive to these differences. A detector may still obtain strong mAP@50 when its predicted box roughly overlaps the target, while stricter thresholds used in mAP@50:95 penalize inaccurate box extent much more heavily.    

Internal project benchmarks illustrate this issue. The V6.2E YOLO11n@640 baseline achieved an mAP@50 of 0.768 and an mAP@50:95 of 0.413, while YOLO11m@1024 achieved 0.815 and 0.457 respectively. Although the larger model and higher input resolution improved both metrics, the difference between loose and strict localization remained approximately 0.36.    

Further analysis indicated that object size alone did not fully explain this gap, suggesting that class-specific annotation semantics and bounding-box extent consistency also require attention.    

Therefore, evaluation in this study should not rely exclusively on a single aggregate mAP value. Per-class behavior, strict-IoU performance, annotation consistency, and qualitative error analysis are all necessary for distinguishing model limitations from dataset-quality limitations.

## 3. Existing Research Work {#3.-existing-research-work}

Research on UAV-based electrical-infrastructure inspection has developed across several major technological directions, ranging from conventional convolutional detectors to lightweight real-time models, transformer-based architectures, frequency-enhanced methods, and multimodal inspection systems.

### 3.1 Technology Landscape Review {#3.1-technology-landscape-review}

**3.1.1 Conventional CNN-Based Detection**

Earlier studies established the feasibility of using convolutional neural networks for visual inspection of electrical infrastructure. CNN-based approaches demonstrated that defects in power-line components could be detected directly from aerial imagery rather than relying entirely on manually designed image-processing rules.

For example, Tao et al. investigated power-line insulator defect detection from aerial images using convolutional neural networks. This line of work provided an important foundation for later object-detection systems by demonstrating that visual characteristics of damaged electrical components could be learned automatically from labeled data.    

However, conventional CNN-based architectures may become less effective when scenes contain large variations in object scale, complex backgrounds, or long-range relationships between components. These limitations motivated later work involving stronger multi-scale feature representation, attention mechanisms, and transformer-based detection.

**3.1.2 YOLO-Based Real-Time Detection**

The YOLO family has become widely adopted in UAV inspection because of its high inference speed and availability of compact model variants.

Previous transmission-line studies have adapted YOLO architectures for foreign-object detection and insulator-defect recognition. Gao et al. proposed a YOLOv11-based method for UAV foreign-object detection on power transmission lines. Their YOLOv11-SDI configuration focuses on small-object representation and improved feature fusion and reports strong performance on an expanded FOTL\_Drone dataset.    

Wang et al. proposed SCI-YOLO11 for transmission-line insulator defect detection, further demonstrating continued interest in adapting YOLO-family models to electrical-inspection tasks.    

The relevance of these works to the present study is primarily practical. Lightweight YOLO variants provide strong candidates for edge-oriented deployment because they offer a favorable trade-off between computational cost and detection performance.

Accordingly, YOLO models are used in the present study as practical baselines and edge-deployment references rather than as the main subject of architectural innovation.

**3.1.3 Transformer-Based Detection**

Transformer-based object detection has become another important research direction for electrical-infrastructure inspection.

RT-DETR provides a real-time end-to-end transformer detector and serves as a representative architecture for this family. Unlike conventional dense prediction pipelines, RT-DETR uses object queries and attention-based feature interaction while remaining suitable for real-time applications.    

Several studies have extended DETR-based models for transmission-line inspection.

Zhang and Zhao proposed TL-DETR, which incorporates multi-scale feature enhancement, sparse attention, and feature recalibration for transmission-line defect detection. Their reported results show an mAP@50 of 86.0%, compared with 83.1% for their RT-DETR baseline.    

Yao and Li also proposed an improved Detection Transformer for transmission-line insulator defects by incorporating window attention, an additional small-object detection layer, and improved feature aggregation.    

These studies support the use of RT-DETR as a strong transformer-based benchmark for the present research.

**3.1.4 Frequency-Enhanced Detection**

Frequency-domain enhancement has recently emerged as a complementary strategy for improving visual representation in UAV and small-object detection.

FES-DETR introduces frequency-domain enhancement into an efficient UAV object detector, while DFIR-DETR combines frequency-domain iterative refinement with dynamic feature aggregation for small-object detection.    

These approaches suggest that spatial-domain features may benefit from complementary frequency information, particularly for fine edges, repetitive patterns, and small targets.

For the present study, these works provide the motivation for evaluating frequency enhancement as a controlled modification to the RT-DETR baseline rather than assuming that the technique will automatically improve performance.

**3.1.5 State-Space and Sequence-Oriented Approaches**

Recent research has also explored architectures beyond conventional CNNs and transformers.

For example, Mamba-enhanced Detection Transformer models incorporate selective state-space mechanisms for transmission-line defect detection. Such approaches reflect broader interest in sequence-oriented modeling and efficient long-range dependency representation.    

Although these models are not used as the primary benchmark in this study, they demonstrate that the technology landscape continues to evolve and that no single architecture can be assumed to be universally optimal for all electrical-inspection tasks.

**3.1.6 Multimodal Inspection Systems**

Electrical-infrastructure inspection is not limited to RGB imagery.

Previous reviews describe UAV systems that combine RGB cameras with thermal sensors, LiDAR, positioning systems, GIS, wireless communication, and edge-computing components.    

Such multimodal systems can provide information that is unavailable from RGB imagery alone. Thermal sensing can reveal abnormal heat patterns, while LiDAR can provide geometric information such as conductor sag, spatial clearance, and three-dimensional structure.

However, multimodal systems also require additional hardware, calibration, synchronization, storage, and deployment complexity.    

Therefore, the present benchmark remains primarily focused on RGB-based object detection to ensure that model comparisons are performed under a consistent visual input modality.

### 3.2 Strategies Applied in Previous Research {#3.2-strategies-applied-in-previous-research}

Previous studies have proposed a wide range of techniques for improving detection performance in UAV and electrical-infrastructure imagery.

**3.2.1 Multi-Scale Feature Fusion**

Multi-scale feature fusion is one of the most common strategies used in previous object-detection research.

Feature maps at different network depths contain complementary information. High-resolution features preserve local spatial details, while deeper features provide stronger semantic representation.

Combining these representations can improve detection performance when scenes contain both large infrastructure components and small defects.    

This strategy is especially relevant to UAV inspection because object scale may vary substantially within the same image.

**3.2.2 High-Resolution Detection Layers**

Another common approach is to introduce additional high-resolution detection layers.

Small objects may lose important visual details as feature maps are repeatedly downsampled. Additional high-resolution layers preserve more local information and can therefore improve sensitivity to small targets.    

However, this improvement comes at the cost of increased memory consumption and computational complexity.

For edge-oriented systems, this creates a direct trade-off between small-object sensitivity and deployment efficiency.

**3.2.3 Attention Mechanisms**

Attention mechanisms are widely used to improve feature selection.

Common forms include spatial attention, channel attention, window attention, and cross-scale attention. These mechanisms are intended to emphasize informative features while reducing the influence of irrelevant background information.    

Attention is particularly relevant to electrical-infrastructure imagery because target defects often occupy only a small region within visually complex scenes.

**3.2.4 Adaptive Feature-Fusion Necks**

YOLO-based research frequently modifies the neck portion of the detection architecture.

Previous approaches include adaptive feature fusion, BiFPN-like structures, dynamic integration mechanisms, and local-global feature processing.    

These techniques aim to improve the flow of information between multiple feature scales.

However, increased architectural complexity does not guarantee improved performance. Therefore, such modifications should be evaluated experimentally rather than adopted solely because they are more sophisticated.

**3.2.5 Frequency-Domain Enhancement**

Frequency-domain enhancement represents another recent strategy.

FES-DETR and DFIR-DETR suggest that frequency features can provide complementary information for UAV and small-object detection.    

In the present study, frequency enhancement is therefore considered a candidate experiment whose value will be measured against the unchanged RT-DETR baseline.

The relevant question is not whether the model becomes more complex, but whether the modification produces a measurable and reproducible improvement.

**3.2.6 Region-of-Interest Hierarchical Detection**

Hierarchical or region-of-interest pipelines provide another method for dealing with small defects.

Rather than detecting a very small defect directly from a large UAV image, the system can first identify a relevant component or region and then analyze a higher-resolution crop.    

A simplified pipeline can be represented as:

UAV Image  
   ↓  
Component Detection  
   ↓  
Region-of-Interest Crop  
   ↓  
Defect Detection

This approach increases the effective visual resolution of the target and reduces irrelevant background information.

However, errors in the first detection stage can propagate through the pipeline. If the correct region is not identified initially, later defect detection cannot recover the missed object.    

**3.2.7 Data Augmentation and Environmental Perturbation**

Data augmentation is commonly used to improve robustness to UAV imaging conditions.

Frequently used transformations include brightness and contrast variation, rotation, scaling, motion blur, defocus blur, haze, compression artifacts, shadow, and backlighting.    

These transformations allow the training process to expose the detector to a wider range of visual conditions.

However, augmented samples are not equivalent to new independent real-world observations. Therefore, augmentation should improve robustness rather than be interpreted as a substitute for representative data collection.    

**3.2.8 Structural Consistency and Object-State Reasoning**

Research in related video-analysis domains has explored structural consistency and persistent object states across frames.

SSTM uses structural consistency and spatiotemporal matching, while StateTrace represents object trajectories, states, relationships, and visibility over time. These approaches provide conceptual evidence that information across frames can help distinguish persistent physical conditions from temporary visual ambiguity.

Although these methods were not designed specifically for electrical-infrastructure inspection, they motivate the controlled use of structural and temporal evidence in the present study.

**3.2.9 Controlled Model Adaptation**

A common pattern across previous research is that domain-specific detectors are usually created by adapting an existing architecture rather than designing a completely new model from first principles.

This observation aligns directly with the methodology of the present study.    

The research therefore follows a benchmark-first process:

1\. evaluate representative baseline models;  
2\. identify class-specific or localization-specific weaknesses;  
3\. introduce limited modifications related to those weaknesses;  
4\. measure whether the modifications produce reproducible improvements;  
5\. retain only changes supported by experimental evidence.

This strategy reduces the risk of adding architectural components without demonstrating that they solve an observed problem.

### 3.3 Evaluation Metrics Used in Previous Work {#3.3-evaluation-metrics-used-in-previous-work}

Reliable evaluation of object-detection systems requires more than a single accuracy value.

Different metrics capture different aspects of model behavior, including classification quality, missed detections, localization accuracy, class-specific performance, and computational efficiency.

**3.3.1 Precision, Recall, and F1-Score**

Precision measures the proportion of predicted detections that correspond to correct detections.

*$Precision=\frac{TP}{TP+FP}$*

*where $TP$ represents true positives and $FP$ represents false positives.*

High Precision indicates that the detector produces relatively few false alarms.

Recall measures the proportion of ground-truth objects that are successfully detected.

$Recall=\frac{TP}{TP+FN}$

where $FN$ represents false negatives.

High Recall indicates that relatively few true objects are missed.

The F1-score combines Precision and Recall using their harmonic mean:

$F1=2\times \frac{Precision\times Recall}{Precision+Recall}$

This metric is useful when both false positives and false negatives are important.

For infrastructure inspection, Recall may be particularly important when missing a genuine defect is more costly than sending an additional candidate for later verification.

**3.3.2 IoU and Mean Average Precision**

Intersection over Union (IoU) measures the spatial overlap between a predicted bounding box and its corresponding ground-truth box.

$IoU=\frac{AreaofIntersection}{AreaofUnion}$

A prediction is normally considered correct only if its IoU exceeds a specified threshold.

mAP@50 evaluates Average Precision using an IoU threshold of 0.50.

Because this threshold permits relatively loose spatial overlap, mAP@50 provides a useful measure of general detection capability.

mAP@50:95 evaluates performance over multiple IoU thresholds from 0.50 to 0.95.

This metric is considerably more sensitive to localization quality and bounding-box extent.

For this reason, both metrics should be reported. A model may achieve high mAP@50 while still producing imprecise bounding boxes, particularly when annotation boundaries are inconsistent. The current project specifically identifies strict bounding-box localization as an important evaluation concern.    

**3.3.3 Per-Class and Object-Scale Evaluation**

Overall mAP can hide substantial differences between classes.

A model may achieve strong aggregate performance while performing poorly on rare or difficult categories.

Therefore, evaluation should report per-class Precision, Recall, AP@50, and AP@50:95 where possible.

Object-scale analysis is also useful because UAV datasets often contain large differences in target size.

Performance on small, medium, and large objects can reveal whether a model improvement benefits the intended target categories or merely improves already-easy cases.

**3.3.4 Error Categorization**

Quantitative metrics do not explain why a detector fails.

Error analysis can distinguish between:

\- missed detections;  
\- incorrect class predictions;  
\- duplicate detections;  
\- inaccurate bounding-box extent;  
\- background false positives;  
\- occlusion-related errors;  
\- failures caused by blur or viewpoint variation.

This type of analysis is important before modifying a model because different errors require different solutions.

For example, increasing model capacity may help recover missed detections but may not correct inconsistent bounding-box semantics.    

**3.3.5 Edge-Deployment Metrics**

When a detector is intended for embedded deployment, model quality must be evaluated together with computational cost.

Relevant metrics include:

\- model parameters;  
\- model size;  
\- FLOPs;  
\- preprocessing time;  
\- inference latency;  
\- post-processing latency;  
\- end-to-end latency;  
\- RAM utilization;  
\- CPU or GPU utilization;  
\- FPS;  
\- device temperature;  
\- power consumption.

These metrics are particularly important for Raspberry Pi deployment because a model that performs well on a desktop GPU may not satisfy memory, thermal, or latency constraints on edge hardware.

**3.3.6 Video and System-Level Metrics**

If the final system processes UAV video rather than isolated images, frame-level mAP alone may not fully describe operational behavior.

Additional metrics may include:

\- false alarms per minute;  
\- missed events;  
\- event-level Recall;  
\- persistence of false detections;  
\- time from first observation to alert.

These metrics are especially relevant when the edge model is used for screening or urgent alert generation.

**3.3.7 Controlled Benchmark Protocol**

Meaningful model comparison requires consistent experimental conditions.

Important controls include:

\- fixed class taxonomy;  
\- fixed train, validation, and test splits;  
\- consistent evaluation metrics;  
\- documented image resolution;  
\- documented training schedule;  
\- reproducible random seeds;  
\- fixed evaluation thresholds where required.

Without these controls, an apparent model improvement may be caused by changes in data, preprocessing, resolution, or evaluation procedure rather than by the model architecture itself.

For this reason, the present study emphasizes reproducible dataset versions, controlled evaluation conditions, and a locked test set during model development.

## 4. Fit-Gap Analysis {#4.-fit-gap-analysis}

The reviewed literature demonstrates that UAV-based electrical-infrastructure inspection has been addressed using a wide range of object-detection architectures and enhancement strategies. These include conventional CNN-based detectors, lightweight YOLO models, transformer-based architectures such as RT-DETR, multi-scale feature fusion, attention mechanisms, frequency-domain enhancement, region-of-interest processing, and temporal or structural reasoning.

However, direct comparison among published methods remains difficult because different studies frequently use different datasets, defect taxonomies, image resolutions, training procedures, hardware platforms, and evaluation protocols. A model reporting higher mAP in one study cannot therefore be assumed to be universally superior to another model evaluated under different experimental conditions.    

Based on the reviewed literature, five main research gaps are identified.

**4.1 Lack of Controlled Comparability**

The first gap concerns comparability between model families.

Published studies often report strong detection results, but these results are commonly obtained under different datasets, class definitions, augmentation procedures, input resolutions, evaluation splits, and model scales.    

As a result, headline metrics alone do not provide sufficient evidence that one architecture is inherently more suitable than another.

The present study addresses this limitation through a controlled benchmarking methodology. Representative model families are evaluated using a consistent dataset taxonomy, fixed data splits, standardized evaluation metrics, and documented training conditions.

This design allows differences in model behavior to be interpreted more reliably and reduces the risk of attributing improvements to architecture when they may instead result from changes in data or evaluation settings.

**4.2 Accuracy–Efficiency Trade-Off for Edge Deployment**

The second gap concerns the relationship between detection accuracy and computational efficiency.

High-capacity models may provide stronger recognition performance but can be unsuitable for embedded hardware because of memory, latency, power, or thermal constraints. Conversely, lightweight models may satisfy real-time requirements while losing detection accuracy or Recall on difficult defect classes.    

This trade-off is particularly important in UAV inspection, where the computing resources available on the aircraft are substantially more limited than those available on a workstation or server.

The present research therefore separates the inspection system into two functional levels.

A lightweight YOLO-family model is investigated for on-board screening and rapid alert generation, while higher-capacity models are retained for server-side post-flight analysis.

This two-tier design avoids forcing a single model to simultaneously optimize for minimum latency and maximum analytical accuracy.

**4.3 Dataset Quality and Annotation Reliability**

The third gap concerns dataset quality and the reliability of evaluation labels.

Although previous work frequently focuses on improving model architecture, detection performance can also be strongly affected by missing labels, incorrect class assignments, inconsistent taxonomy, or inconsistent bounding-box definitions.

These problems are particularly important when strict localization metrics are used because different interpretations of the spatial extent of a defect may substantially affect IoU-based evaluation. The current literature review therefore identifies annotation quality and bounding-box semantics as important factors in benchmark reliability.    

Preliminary experiments in the present project further demonstrate that validation-label errors can substantially underestimate model performance. Correcting systematic annotation issues produced significant changes in class-level AP, particularly for bird\_nest and foreign\_object.

This finding indicates that improvements in model architecture should not be interpreted independently of dataset quality.

Accordingly, the proposed methodology includes model-assisted annotation auditing and dataset-version control as part of the benchmark process.

**4.4 Class-Specific and Localization-Specific Failure Analysis**

The fourth gap concerns the limited explanatory value of aggregate detection metrics.

A single overall mAP value may conceal substantial weaknesses in individual defect categories. Similarly, strong mAP@50 performance may coexist with poor performance under stricter IoU thresholds.

Existing project benchmarks show that increasing model size and input resolution improves overall performance but does not necessarily eliminate the difference between loose and strict localization metrics.    

This indicates that performance analysis should distinguish between several possible failure modes, including:

\- missed objects;  
\- incorrect classifications;  
\- inaccurate bounding-box extent;  
\- annotation inconsistency;  
\- small-object detection failure;  
\- occlusion-related ambiguity.

The present study therefore emphasizes per-class AP, mAP@50, mAP@50:95, Recall, localization quality, and qualitative error categorization instead of relying exclusively on a single aggregate score.

**4.5 Limited Validation of Targeted Architectural Enhancements**

The fifth gap concerns the way architectural improvements are evaluated.

Previous studies have proposed many potentially useful techniques, including attention mechanisms, feature-pyramid modifications, additional high-resolution layers, frequency-domain processing, adaptive feature fusion, region-focused analysis, and structural or temporal reasoning.    

However, multiple modifications are often introduced simultaneously.

When this occurs, it becomes difficult to determine which component is responsible for an observed performance improvement.

The present study addresses this problem through controlled ablation around a common RT-DETR-R50 baseline.

The planned experimental configurations are:

| Configuration | Description | Experimental Purpose |
| ----- | ----- | ----- |
| **B0** | RT-DETR-R50 baseline | Establish transformer reference performance |
| **B1** | RT-DETR-R50 \+ frequency-domain enhancement | Evaluate the contribution of frequency information |
| **B2** | RT-DETR-R50 \+ structural-context reasoning | Evaluate the contribution of component relationships |
| **B3** | Combined experimental configuration | Evaluate whether the selected enhancements provide complementary benefits |

**Table 6.** *Limited Validation of Targeted Architectural Enhancements.*
This structure follows the existing benchmark-first design of the project.    

The objective is not to present B3 as a completely new object-detection architecture. Instead, each modification is evaluated according to whether it provides measurable and reproducible improvement over B0.

**4.6 Visual Ambiguity and Limited Use of Multi-Frame Evidence**

The sixth gap concerns visual ambiguity in single-frame inspection.

A single UAV frame may contain incomplete or misleading evidence due to occlusion, motion blur, vegetation, or viewpoint changes. For example, a conductor that appears discontinuous in one image may be fully visible in neighboring frames.

Research such as SSTM and StateTrace demonstrates that temporal consistency and persistent object states can provide information that is unavailable from isolated frames. However, these methods were developed for satellite-video detection and general long-video reasoning rather than the specific electrical-infrastructure defect task considered in this research.    

Therefore, multi-frame information is treated as an optional verification mechanism rather than the central detection architecture.

The aim is to determine whether neighboring observations can reduce ambiguous false alarms or missed detections without significantly increasing system complexity.

**4.7 Fit-Gap Summary**

The relationship between previous work and the present study can be summarized as follows:

| Research Direction | Existing Strength | Remaining Limitation | Role in the Present Study |
| ----- | ----- | ----- | ----- |
| YOLO-based detection | High inference speed and practical deployment potential | Accuracy may decrease on difficult classes and constrained hardware introduces trade-offs | Edge-oriented baseline and on-board detector |
| RT-DETR-based detection | Strong transformer-based detection and global feature modeling | Additional computation and limited domain-specific contextual reasoning | Principal transformer benchmark |
| Multi-scale / attention methods | Improve representation of small and difficult objects | Often introduce several modifications simultaneously | Reference strategies for targeted experiments |
| Frequency-domain enhancement | May preserve fine-grained and high-frequency information | Benefit is not established for the target electrical dataset | Controlled B1 experiment |
| Structural reasoning | Can exploit relationships between components | Limited direct validation for electrical-infrastructure detection | Controlled B2 experiment |
| Temporal reasoning | Can reduce ambiguity across neighboring frames | Adds processing complexity and is not always required | Optional verification experiment |
| Annotation auditing | Can reveal hidden dataset-quality problems | Often treated as preprocessing rather than part of evaluation methodology | Integrated into benchmark reliability |
| Edge deployment | Enables real-time UAV-side inference | Accuracy, latency, memory, and thermal constraints must be balanced | Teacher–student and Raspberry Pi evaluation |

**Table 7.** *Fit-Gap Summary.*
Overall, the principal gap addressed by this study is not the absence of object-detection architectures. Numerous capable detectors already exist.

Instead, the main research need is a controlled and reproducible evaluation of model families, dataset quality, localization behavior, deployment constraints, and selected architectural extensions under a common UAV electrical-infrastructure inspection setting.

The expected contribution is therefore both comparative and experimental: first, to determine which model configurations are suitable for different operational roles, and second, to measure whether frequency-domain enhancement, structural context, and limited temporal verification provide meaningful improvements beyond established baselines.

## 5. Summary {#5.-summary}

This literature review examined the theoretical foundations, existing research, common improvement strategies, evaluation methods, and remaining challenges associated with UAV-based electrical-infrastructure inspection.

Previous research demonstrates that UAV platforms provide an effective means of acquiring visual data from electrical assets while reducing the need for direct human access to difficult or hazardous locations. RGB imagery is particularly suitable for visually observable defects, while thermal and LiDAR sensing provide complementary information for temperature-related and geometric inspection tasks.    Pasted markdown

Deep-learning-based object detection has become an important component of automated inspection. YOLO-family detectors are attractive for real-time and edge-oriented deployment because of their relatively high inference speed and availability of compact variants. Transformer-based detectors, particularly RT-DETR and its derivatives, provide another strong model family and have increasingly been adapted to transmission-line inspection tasks.    Pasted markdown

Previous studies have also investigated multi-scale feature fusion, attention mechanisms, high-resolution detection layers, region-of-interest processing, and frequency-domain enhancement. These methods aim to improve representation of small or visually difficult targets. Research in neighboring video-analysis domains further suggests that structural consistency and temporal evidence may help distinguish persistent physical conditions from temporary visual ambiguity.    Pasted markdown

At the same time, several persistent challenges remain. These include small and fine-grained defects, viewpoint and scale variation, complex backgrounds, occlusion, motion blur, intra-class variation, inter-class similarity, class imbalance, annotation inconsistency, and inaccurate bounding-box localization.    Pasted markdown

These challenges demonstrate that model evaluation should not rely exclusively on global mAP. A comprehensive benchmark should consider Precision, Recall, F1-score, mAP@50, mAP@50:95, per-class AP, localization quality, object-scale performance, false-positive and false-negative behavior, model size, inference latency, and computational requirements.    Pasted markdown

The literature also indicates that reported results from different studies cannot always be directly compared because datasets, class definitions, input resolutions, model scales, and evaluation protocols frequently differ.

For this reason, the present research adopts a benchmark-first methodology.

Lightweight YOLO models are investigated primarily for edge-oriented deployment and rapid UAV-side screening, while higher-capacity detectors support more detailed server-side analysis. RT-DETR-R50 serves as the principal transformer baseline for controlled architectural experiments.

Model customization remains secondary to this benchmark.

When a consistent weakness is identified, a limited modification is introduced and evaluated through controlled ablation. Frequency-domain enhancement, structural-context reasoning, and short-term multi-frame verification are therefore treated as targeted experimental extensions rather than assumptions about what must improve detection performance. This is consistent with the existing research scope, which explicitly prioritizes comparative evaluation over development of an entirely new detector.    Pasted markdown

In addition, the present study incorporates dataset-quality analysis as part of the experimental process. Preliminary findings indicate that annotation errors and taxonomy inconsistencies can substantially influence measured performance. Consequently, dataset provenance, annotation auditing, fixed evaluation splits, and reproducible benchmark conditions are considered essential to interpreting model improvements correctly.

Overall, the expected contribution of the study is a reproducible comparative assessment of detection accuracy, localization quality, class-specific behavior, computational efficiency, and deployment suitability, together with an evaluation of the incremental benefits of selected model enhancements.

Rather than seeking architectural complexity for its own sake, the research aims to determine which model, data, and reasoning strategies provide meaningful practical value for UAV-based electrical-infrastructure inspection under controlled experimental conditions.

# IV. References {#iv.-references}

\[1\] F. Ahmed, J. C. Mohanta, and A. Keshari, “Power transmission line inspections: Methods, challenges, current status and usage of unmanned aerial systems,” *Journal of Intelligent & Robotic Systems*, vol. 110, Art. no. 54, 2024\.

\[2\] A. L. B. Vieira e Silva, H. de Castro Felix, F. P. M. Simões, V. Teichrieb, M. dos Santos, H. Santiago, V. Sgotti, and H. Lott Neto, “InsPLAD: A dataset and benchmark for power line asset inspection in UAV images,” *International Journal of Remote Sensing*, vol. 44, no. 23, pp. 7294–7320, 2023, doi: 10.1080/01431161.2023.2283900.

\[3\] Y. Zhao, W. Lv, S. Xu, J. Wei, G. Wang, Q. Dang, Y. Liu, and J. Chen, “DETRs Beat YOLOs on Real-time Object Detection,” in *Proc. IEEE/CVF Conference on Computer Vision and Pattern Recognition (CVPR)*, 2024, pp. 16965–16974, doi: 10.1109/CVPR52733.2024.01605.

\[4\] X. Tao, D. Zhang, Z. Wang, X. Liu, H. Zhang, and D. Xu, “Detection of Power Line Insulator Defects Using Aerial Images Analyzed With Convolutional Neural Networks,” *IEEE Transactions on Systems, Man, and Cybernetics: Systems*, 2018\.

\[5\] R. Abdelfattah, X. Wang, and S. Wang, “TTPLA: An Aerial-Image Dataset for Detection and Segmentation of Transmission Towers and Power Lines,” in *Proc. Asian Conference on Computer Vision (ACCV)*, 2020\.

\[6\] Y. Zhang and R. Zhao, “TL-DETR: Efficient transmission line defect detection for edge deployment,” *PLOS ONE*, vol. 21, no. 6, Art. no. e0351470, 2026, doi: 10.1371/journal.pone.0351470.

\[7\] R. Qian and C. You, “FES-DETR: Efficient UAV Object Detector With Frequency-Domain Enhancement Across Spatial-Domain,” *Journal of Real-Time Image Processing*, vol. 23, Art. no. 51, 2026, doi: 10.1007/s11554-026-01854-8.

\[8\] B. Gao, J. Tong, X. Chen, H. Yu, and Z. Li, “DFIR-DETR: Frequency-domain iterative refinement and dynamic feature aggregation for small object detection,” *Neural Networks*, vol. 204, Art. no. 109256, 2026, doi: 10.1016/j.neunet.2026.109256.

\[9\] Y. Zhang, Y. Tang, F. Liu, Y. Chen, and T. Han, “Structural Consistency Based Multiobject Detection for Satellite Video and Benchmark With Real Scene Simulation Dataset,” *IEEE Journal of Selected Topics in Applied Earth Observations and Remote Sensing*, vol. 18, pp. 23728–23743, 2025, doi: 10.1109/JSTARS.2025.3603130.

\[10\] K. Li, G. Wan, G. Cheng, L. Meng, and J. Han, “Object detection in optical remote sensing images: A survey and a new benchmark,” *ISPRS Journal of Photogrammetry and Remote Sensing*, vol. 159, pp. 296–307, 2020, doi: 10.1016/j.isprsjprs.2019.11.023.

\[11\] D. Gao, Y. Yin, H. Zhang, and B. Wang, “YOLOv11-based UAV foreign object detection for power transmission lines,” *Electronics*, vol. 14, no. 18, Art. no. 3577, 2025, doi: 10.3390/electronics14183577.

\[12\] E. İnce, “Transmission line inspection with UAVs: A review of technologies, applications, and challenges,” *International Journal of Energy and Smart Grid*, vol. 9, no. 1, pp. 60–69, 2024, doi: 10.55088/ijesg.1596043.

\[13\] T. Santos, T. Cunha, A. Dias, A. P. Moreira, and J. Almeida, “UAV visual and thermographic power line detection using deep learning,” *Sensors*, vol. 24, no. 17, Art. no. 5678, 2024, doi: 10.3390/s24175678.

\[14\] S. Xiong, Y. Liu, Y. Yan, L. Pei, P. Xu, X. Fu, and X. Jiang, “Object recognition for power equipment via human-level concept learning,” *IET Generation, Transmission & Distribution*, vol. 15, no. 10, pp. 1578–1587, 2021\.

\[15\] P. Jia, “Mamba-enhanced detection transformer with selective state space models for multi-category defect detection in transmission lines,” *Discover Computing*, vol. 29, no. 1, Art. no. 489, 2026\.

\[16\] X. Yao and S. Li, “An object detection method based on improved detection transformer for insulator defect detection of electrical transmission lines,” *Electric Power Systems Research*, Art. no. 112929, 2026, doi: 10.1016/j.epsr.2026.112929.

\[17\] Y. Han, W. Li, Y. Cao, H. Xu, S. Yang, S. You, and X. Su, “StateTrace: An object-centric framework for hidden-state spatiotemporal reasoning in long videos,” *arXiv preprint*, arXiv:2608.18532, 2026\.

\[18\] J. Wang, Y. Wang, X. Li, B. Yuan, and M. Wang, “SCI-YOLO11: An improved defect detection algorithm for transmission line insulators based on YOLO11,” *PLOS ONE*, vol. 20, no. 10, Art. no. e0322561, 2025\.

[image1]: <data:image/png;base64,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>

[image2]: <data:image/png;base64,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>

[image3]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAlsAAAE0CAIAAACU7WE0AABMQ0lEQVR4Xu2deXxU1fn/56u41N1u2Nparbb219a2lkxCQgIJm4CERSAhsoPBxAASBDRI0BQUCCEKGBAssljWGhURKbIGDBgR2UFAwLCFsIcEQhIm8/vknnJ7cyaZTGa9d+bz/iOvuc9z7s3MOfeez3nOPYvJSgghhBCr1SQbCCGEkICEikgIIYRUQUUkhBBCqqAiEkIIIVVQEQkhhJAqqIiEEEJIFVREQgghpAoqIiFGIicnZ+LEiQkJCbEKiYmJGRkZW7ZskdMRQuoPFZEQY3D8+PG0tLTMzMy8vLyCggJhPHXqVG5u7ptvvjl9+vTi4uLqZxBC6gcVkRC906tXrwULFshWG4qKipKSkmbMmCE7CCGOQUUkRNfk5+fv3btXttaCxWIZN26cbCWEOAYVkRD9cubMmdjYWNlqF4ji6tWrZSshxAGoiITolxdffDE3N1e21kW3bt1KSkpkKyGkLqiIhOiUZ599trCwULY6QHl5eZcuXcrKymQHIcQuVERC9MjBgweTk5Nlq8OMHz9+8+bNspUQYhcqIiF6JCsra+XKlbLVYXJzc9PT02UrIcQuVERC9MiwYcOOHTsmWx2msLAwKSlJthJC7EJFJESPdOvWTTbVE9evQEigQUUkRI/07NnTlaExFRUVcXFxspUQYhcqIiF6hL2mhHgfKiIheoQjawjxPlREQnQK5yMS4mWoiITolPXr16ekpMhWB1iyZMn06dNlKyGkLqiIhOgXruJGiDehIhKiX7jSNyHehIpIiN7h/oiEeAcqIiF6p6ysbOLEib17987JyZF9N1iyZEnXrl0ZHRLiClREQozB8ePH09LSMjMz8/LyCgoKhPHUqVO5ublvvvnm9OnTi4uLq59BCKkfVERCjATCRMSLCQkJsQqJiYkZGRlbtmyR0xFC6g8VkRBCCKmCikgIIYRUQUUkhBBCqqAiEkIIIVVQEQkhhJAqqIiEEEJIFVREQgghpAoqIiGEEFIFFZEQQgipgopICCGEVEFFJMT3vPTSSwM9D/6L/I8JIRqoiIQQQkgVVERCCCGkCioiIYQQUgUVkRBCCKmCikgIIYRUQUUkhBBCqqAiEkIIIVVQEQkhhJAqqIiEEEJIFVREQgghpAoqIiGEEFIFFZEQQgipgopICCGEVEFFJIQQQqqgIhJCCCFVUBEJIYSQKqiIhBBCSBVUREIIIaQKKiIhhBBSBRWREEIIqYKKSAghhFRBRSSEEEKqoCISQgghVVARCSGEkCqoiIQYj4sXL8bFxfXs2bOkpET2EUKchYpIiMG4du3agAEDcnJyVqxYMWTIEIvFIqcghDgFFZEQIwH9Gz58+NKlS8VhVlZWRkZG9SSEECehIhJiJNLT06dOnaoeVlZWjhgxIjs7W5OEEOIkVERCfMzy5cuHDRv27bffyg4bVq1aVV5eLlut1sLCwvz8fNlqA+RTNhFCNFARCfEN586dQ2yXmJjYqFGjadOmyW4btm3b1rNnT9l6g7i4uKKiItlanf379w8dOvTTTz+9fPmy7COEUBEJ8T4FBQXx8fFms7mRQt++fSsqKuRE1Tly5EhMTAxOlB03WLNmDcS1zutMnz4d/7Fx48bURUJsoSIS4iWOHTs2e/bsXr16QZPCw8NfeeWVhQsXNm3a9OTJk3LS6pw9ezY2NvbAgQOyozq4+Lhx42RrdSCZCDSFEkMXBw8eDGmUExESqFARCfEgU6dO7dixo1CgsWPHfvXVV2oYN3/+fGGsfobM9evXHRetsrKyAQMGyFYboMTjx4/HB4SJuDikURVIBo4kkKEiEuJmLBbLtm3bJk2a1K5dOygN/uIzLNo0e/fuhQJ17dq1tLRUa7clLS1NNtnl/Pnzhw4dkq3VqaysDAoKys3NFYdCFyGHjRUYOJKAhYpIiNtA/IcosFWrViIoRHS4e/duOZECXKGhoXVK16xZs+qriCA2Nha6KFurExkZ2aZNm0uXLmmNkEahi/jyQhcZMpKAgopIiBvYsGHDmDFjIDPQkpiYmJkzZx48eFBOpAHJ1Fn2tSHituvXr8uOuti0adOAAQPKyspkh4YVK1bgO7z88suyg12pJIChIhLiDEVFRcnJyYjzIBtms3nBggV2BoJq+eGHHyIiIuqcKYE47+zZs7K1PowbN2727NmyVQMCRISJUHHZUR1t4MhBqsS/oSIS4gxTpkwJDg5OTEzMzs4+d+6c7K4FxG1xcXFQUNlRHYjrkSNHZGs9qaiowNeTrdXJzc0NCgpyZOa++q5RDRz5rpH4H1REQpzh+vXr0JKNGzfKDrtMmjQJijJr1izZoQHaY2cmfr1AJLpv3z7ZWp3x48cvXLhQttaO0EW0BvBDZB8hBoeKSIiTNFc4ffq07KiFnJwciGh8fLyd3SrKy8sTEhJWrVolO5wlJiamsLBQtmq4evVqWFhYvUJStAPwQ/r16yc7CDE4VERCnGfKlCkIlfBXdtgAFYSKQBRlh4apU6emp6fLVpfJzs4eMWKEna5RsZAN/sqOmoAQOhEcE2IIqIiEOM/169eFQsgOG2bNmjVp0iTZWp3hw4fbCR9dISMjIysrS7beQCxkExwcvGvXLtlng4MtAEKMCBWREJc4ffp0nX2n27dvN5vN9qdDIHy8du2abHUTENohQ4bIVg1HjhwJCwvr3Lmz7KgOQkO0AJyYEEKIIaAiEuIq0Ino6Oja9KyoqKhdu3YRERGyQ8OHH34YFxcnW91KSUnJjh07ZKuGhQsX2h8s44j2E2JoqIiEuMSyZcugdm3btq3x7drSpUshM6+++qpkV7lw4QISfPbZZ7LDA+Tl5cmm6kDtbBeyEYhdq+CVHYT4EVREQpwEwd+IESOgE08//bSIn2xDqNDQ0I4dOyI+0xpV9u/fj3MhpWfOnJF9HmD8+PETJkyw3+dZ40I2YnAp5NBsNq9du1byEuI3UBEJcRLEhdCPV155RazhYjsnobS0tHHjxnv37v3fORpWrFgRptClSxfZ5xlWrlyJL4xoz86KOZBDpMF30xpVsd+8eTO8UFb770QJMShURELqTUVFxZQpUyIiIpYvX661i8kY6uHYsWPnz5+v8f+PjIwMEVyuXr0acZvs9gynTp1qpGBnBI3t0m7SWgSi+7R79+71msJIiCGgIhJSD7Kzs8PDw81m84wZM2Sfgug+3bZtW7NmzVq3bi27b/SUQhFF7+WoUaNqfG/nIVJSUvDl16xZExwcbGepcXUhGxH42na07tixA79i9OjRV65ckVyEGBcqIiGOAulCeBQdHb19+3bZpwESYlb46quvJJfaU6paVNWMjY399ttvVbsAkvPwww9LRldYsmQJfkJubi7CRHyo7bWiWMhmy5Ytor9UdisUFRWJcBMaL/sIMSZUREIcIi8vr02bNq+++mpxcbHsq47oO502bZpkV3tKVQk5fPiw2suKmHLz5s3/S62QmJhoMrnzIT1w4AD+Y1ZWFvRM9H/Wtho4gsggBdsBtCqLFy8OVVi0aJHsI8SAuPNhI8QvKS8vz8zMRMwH0ZJ9NYGoa/LkyRUVFVqjmGUxcOBAfFCNImITn72jiBaLpWnTpvHx8VbleyJGxBfIz8+X091Y2q3O5Wm+++47EW7aGa1DiFFw58NGiP+xdOlSREuzZs1yen01255SAS4YFRXVt29fcVinIpaVlcXExJg0aFM+8sgjqv2vf/0rok9h/+STT2BJS0vr06eP8E6dOlWVYQG+hv3Xio4QHh6Oi8yePdvpjCLE51ARCakZBHPJyckQD0dW+6wN255SlX379okOTHFYpyIiXMNnfJnS0tIffvhBO0Him2+++cUvfvHBBx+cOnWqoKDg9ttv//3vfy/GvAhFbNiw4eOPP3706NFVq1Z9+eWXkiIiRlRfK2rt9QIXj4uLs9MNS4j+oSISUgMQp9atW6N+HzNmjOxzmBp7SlXmz58Pr7qOTJ2KGBsbq36WaNKkiXawz+TJk5FSjIYVinjXXXedPHlSeCGoISEhamKB+lrRlc5PRLHjx4/HRWyHFBFiCGp+wAgJZFCzBwUFRUZGurJPoTTLwpbBgweHhoaqq6HWqYgJCQn4bNsnefz4cUkpCwsLYXnmmWesNxQRaqpN0Lt3b+2hQLxWRLBY42tFx1m7dq3ZbH7nnXdq++GE6BYqIiH/QwyeRAhlKzyOY6enVGXHjh1I895776mWFi1abNiwQZOkigEDBmjV7ocffvjTn/6kvA00NWjQQGx5+OGHH+Kwrw0iuhWKOG7cOPUiVkW0YNdaVBCziteKsqM+VFRUvP3222hV4FKyjxAdQ0Uk5L8MGTIEKtWpU6fa1l1zBPs9pSrQQiTTbkaBMM52DkPbtm1te0px5fnz5z/00ENiEn1OTo5tGpUaFfH8+fOvv/661qJFvFasbaqi43z55ZctW7acNGlSeXm57CNEl9T6IBESUGzatAkSlZaW5soiLHX2lKpAMiMiIrQzNKAcMGqSVE2Tv/fee2+66SatUWXJkiUvvvgiPuAL/+hHP6ptMfEaFRHYWcjNemP2vf0VUB2hsLAQ1+nRo8exY8dkHyH6g4pIAp1r165NnDgRFbeLuzrUNsuiRkJDQwcPHqy1nDp16rbbblu3bp04hFhCICFm0dHRwrJq1arS0lI1fXp6+vjx48XnYcOGIcTUbtB45MiR3bt3W2tXxKCgIESKklGLmKro+mvFmTNnms3mpk2buvJSlhDvQEUkgUtlZaV4azhv3jxXXhxalU2UUO8j0JQdtYD0+/btk4xff/119+7dH3jgAWjYE088MWrUKK1oLV68uFOnTuIl4m9+85sXXnhBc2qV8DRp0gTBIrzNmzdHnFpQUGBVYt/IyMgPPvhAm9iqfIHMzEzJaIt4rejiVEWr0k87aNAgyDbXByd6hopIApSzZ88mJSU988wztsrkBO+9917jxo0hio7IjFWZFO+iBrtIy5YtaxxxagtixEa1r4DqOGh/BAcHh4eHf/rpp7KPEH1ARSSBSE5OTosWLVDRX716VfY5y+HDh/v164drItSTfdUpKSkZPny4bPUuw4YNQ3Cs7Ya1QyMFtZPWaXbu3Pn000/jUqmpqbKPEB1ARSQBB2QAlXLz5s3Xr18v+1wDYd+iRYtw8bFjx4pthGtk06ZNS5Yska3eRawPsHXrVtlRnWPHjg0dOjQiImLu3LluGTJaVFQkVgI6cOCA7CPE11ARSaBQWVkJGWisIGbyeY7ly5eHhYWh3q9x9EpcXJxsqj+fKshWh4G84RsOGjRIdtygbdu2+P7PPvus/a2vnKZDhw64fkpKSm2jZAnxPlREEiiIVcq6du3qhejk8OHDoaGhzZo1i4qKgjpqXZcuXTKbzVqLcwxRkK31IT4+vmnTpravM/ft29epUyfE0NnZ2bZed1FcXDxy5EiUSMeOHWUfIT6CikgCgnXr1ok3YQ6+OXMFqEi/fv0ge3v27GnTpg3+b1JSkrqs6Nq1axtVX2jbCYqKikSw68p8waysLNveywkTJgQFBTVybYFTx1m6dCmaDj7vQyZEQEUkfs7Vq1fHjRuHKj4nJ0f2eQbxKnHy5Mn4fPny5bFjx+IwIiJCxFtinp98Tj359NNPGym40nGam5uLK6hqhK+HoBCW5557rkOHDidOnKie3FOITYyHDx9u580rId6Bikj8FlTxISEhCEEWLFjg6ReHWhBjqTtaqCA2RdSIqA7etLQ0yVsvRIAoek1dDBO7d+/erl271NRUaBIySt2ayqqML12zZo0mrWf56KOPwsLCbDflIMSbUBGJf1JYWPj8889369bt4MGDss+TfPbZZ7UJ3t69e2NjY6E02q0NnUAEiGJkjYthoti8CQwdOlRaaK158+Zt27Z1ZU27+nLo0KGuXbsmJye7ovGEuAIVkfgha9eujYqKQkVfVlYm+zzJ+fPnISR2KvSKior33nvvzJkzsqM+qKGhGizKKRxm5cqVyKXc3FzZYbV+/PHHcGVkZMgOT3L16lX80/bt27uySzMhTkNFJP4GQjTUqq1atXJ8TTV3kZKS0sjld4T2kVTQxY7T06dPz507V7YqVFZW9u/f32w2f/fdd7LPkyxbtqxJkyYhISG2K88R4mmoiMRPsFgs77//PmpS2z2VvEBJSUnbtm0jIyPPnj0r+9yKJIGuh4n2mTJliqc1vkYQLL722mtPP/30zp07ZR8hHoOKSPwBxDrx8fGou6XN4r2GeCH38ccfyw53Y6t/QiO1FjcCZWrfvv25c+dkh1cIDw8X67DLDkI8AxWR+AMIzoKCgiZNmuTlF4cqZrP5+eef98KIVtuhNGJ8jdbiXnJyckaNGiVbvcLhw4e7deuGX3fp0iXZR4gHoCISA7Nnz56OHTs2qmWxNK8xffp078QxRUVFNXaQwuj0q0RHiIiI6Ny5s69aG9YbweL8+fNlByFuhYpIjMrs2bODg4PDwsJ8u+LJoUOHGjdu7OJOSQ5S21qmtdndhVgWfObMmbLDW6jBYnJysuwjxH1QEYkhKSgoQP3YvXt31JWyz4tYLJY+ffpAmGWHZ6gtFqwtdnQX0PvY2NjQ0FBpzqI3uXr16pgxY1Dou3fvln2EuAkqIjEeq1atioyMnDx5sg/78QQLFixAHT1lyhTZ4QHEsFLZegNX5mA4wo4dO8xm8wsvvCA7vMuyZctCQkKQ7bKDEHdARSRGQmwhNHTo0AsXLsg+rzNo0KCgoKA69wd2F/bHlNqOQXU7ly5datGihc83b7py5cro0aNxG0RHR8s+QlyDikiMgcViee+998LCwpYuXSr7fAQq5doWbHM7dc47dHGqvoMgREtPT5etvkCsg7p48WLZQYgLUBGJATh16tSAAQOgQL59a6gFQWrr1q09rUAqdS5hWmcCt1BZWWk2m/ft2yc7fMHBgwfxk0eOHOnzsJX4DVREondWrVrVrFmzoKAgsb+SThg9evTq1atlq8eoMwSsM4h0FyEhIb169fLcTsL1Qiyb16lTJy/sAk0CASoi0S+7du3q0KFDcnKyHt4aqly9ejU6Orpp06ayw2M4GP85mMxFxCo2aKP4aiEbWxCzokTQRvHmTh3EL6EiEj0i3hoGBwc3adJE9vkahKoQHm++zqwzQBR4LUzMyclBDrz66quyw3cUFxfjK3Xt2lU//erEiFARiR4Rbw3j4uL0VsHt3bvXbDb379/fCwu2qTiuc/bHo7qRYcOGNfLk0nFOsGDBgpCQkPDw8OXLl8s+QhyDikh0x6pVq8Rbw/Lyctnna7p37x4aGuplnXa8L1R0nMpWD3Dq1Cloj8/ng0rs2rWrXbt2yIFr167JPkIcgIpI9IJ4a9hIxyt15efnd+vWzcs6XVRU1KlTp+U2fKAgW5cvR+I6+1fdQkVFRVhY2PHjx2WHr0H4Hhwc3LRpU28OfSL+ARWR6AKLxSLeGnrz/Vy9QD07cODAHTt2yA4PI8K+euFgQOk6COUHDx4sW3XAN99889RTTyErdDJ7khgFKiLxPWK6oQ7fGmr5+OOPG3mlQ1Ji586dchioMFFBtip4bZfd119/HXmyZs0a2aEDzp07hxYMvl5BQYHsI6QWqIjEx4hFShFteLk3sl6geo2Kimrbtq3s8B1rFGSrd7lw4YLIFn1Oe7BYLFlZWc2bN//yyy9lHyE1QUUkPmP37t2NlLeGuppuaIuYbDBy5EjZ4VP0oIhWpTO5X79+Xtv9wwkQI/bu3RsliJBR9hFSHSoi8QFovP/zn/9ENerbrQ0dBDEQolj9TEgX6EQRwYEDB1CU3pyOUl8qKirS09Mhit98843sI0QDFZF4G7TZn3vuuUbKdEPZp0vwVT/66CPZ6mv0o4hWZdUCHWaRxOrVq6Hcc+bM0bN4E99CRSTeRrw11MPuho6wc+fO+Ph4HdahulLEK1euREVF6bz3G8TGxjZSdhO7fPmy7COEiki8xp49ezp27Ij66Pz587JPr7z33ns+GV/qCLpSROuN3SK3bdsmO/THxx9/HBoamp2dLTtIwENFJN7AYrGEhISEhYUZ4sWh4IcffkC9GRMTIzv0gd4U8fjx48iubt26VVRUyD798d1336Gtk5qaWlpaKvtIAENFJB7n9OnT8fHxsbGx33//vezTK5WVlfjOZrN5165dsk8f6E0RwaxZsyAz8+bNkx26JDk5Gd8WLR40fWQfCVSoiMSzoNaOiooKCgoyxFtDlezs7Eb6XvFEh4qIIu7cuXN4eLjs0CsQ7+Dg4IiICNlBAhUqIvEUe/fuhagMHjzYQC8OVfDNN27cKFv1hA4VUZCSkpKTkyNbdcy5c+dQ3P369SssLJR9JMCgIhL3Y7FY5s6dGxISsmjRItlnBDZs2PDKK6/IVp2hW0U8e/ZsdHS0sd7PTZs2LSgoqGXLlnl5ebKPBBJUROJm0NBOSEhAo7tbt26yzwiUlJS0bdtWb/PxbdGtIlqVCPudd96RrfoGcW2zZs3MZrMOZ9oQr0FFJG4mKioKFeLEiRMNukfdhAkTdDvjQoueFbFHjx6NGzc+cuSI7NA3x48fj4uLe/HFF72znRbRIVRE4h6+++67Ll26QEv0H13VRnl5OeLa0NDQ/Px82ac/9KyIYPz48bgZVqxYITt0z0cffRSq8Mknn8g+4u9QEYkb+OCDDxor/Otf/5J9xkFMHpg9e7bs0CU6V8TLly+3UpAdRmD//v3R0dG4GQzaz0GchopIXOXs2bOoO7p27XrgwAHZZygQFsTGxhpigrlV94oIECAaov+5RoqKioYMGRIXF3fixAnZR/wXKiJxiZycnBYtWrzxxhvGGltoS2Vlpdls3r17t+zQK/pXRDBw4MA9e/bIVoMgbonIyEhjTSYhrkBFJE7SrVs3RACJiYl+MIvrzJkzzZo10+e2t7VhCEW0KuNOFy9eLFuNw+nTp8X2ihcvXpR9xO+gIhJnWLhwYUhIyLx58ywWi+wzICNGjDBc/55RFDE6OhqtjbNnz8oO41BeXv7mm2+2a9fOuPEucRAqIqkf58+fHzx4MPRj3759ss+YrFu3Dj8nJSVFdugboyjixo0bkb2jRo2SHUajSZMm3DHD76EiknqQm5vbqlUrVHBpaWmyz5iI+fhRUVGGW2rOKIoIXnrpJdwzRl8O5sCBA2I7Mw5A9WOoiMQhDh8+HBcXFx8ff/r0adlnZMxmc0JCghGXKTGQIlqVNbUN1y9dI+pwG50ve0ucg4pI6ubf//53WFhYcHCwf7w1VCkrK8PvOnbsmOwwAsZSxIqKipiYmOPHj8sOA/LVV1+1aNECujh9+nTZRwwOFZHUgdhGrkOHDgaameAg77777pw5c2SrQTCWIoJvv/128ODBstWYFBQU9OrVC8/FpUuXZB8xMlREYo+8vLxGylbjxpqZ4AiHDx9u3Ljx9evXZYdBMJwiWpWZGIb7zrVRVlb2xhtvtG/ffv/+/bKPGBYqIqmB/Pz8nj17NlI2jZN9fsGSJUvw6yZPniw7jIMRFVHsw5yZmSk7DIu63hvUUfYRA0JFJDUQERFhNptnzJhh3BDKPk2bNkVFdvXqVdlhHIyoiJWVlX379g0ODjb6gn9aLl26lJSUBFH0g6UqCBWRVOPy5csjR458+umnv/32W9nnR6D+2rx5s2w1FEZURKsyhwGK6Gd9DxaLJSsrq1WrVlu3bpV9xFBQEcn/gApCC6EW0EXZ50d88cUXqampstVoGFQRweTJk/1jJoZEs2bNIPbz5s2THcQ4UBFJFSdPnkQl1bNnT0NsDegKaWlp/lEdG1cRwerVq/1mkQctV69eTUlJwQ129OhR2UeMABWRWFeuXNm0adMpU6YYZSMkp9m6dWtQUNCgQYNkhwExtCICFMT27dtlq1+wYMECPFBr166VHUT3UBEDmitXrqSmpqJJ26ZNG9nnd1y7dq1Tp07h4eEIiGWfATG6IoaGhsbExPhrI6xVq1aQ/HfeecfPFrXwe6iIAU2HDh0gh8nJyYGw083UqVPxY9F+lx3GxOiKOGvWLBSHv751Kyws7Nu3L35gUlKS7CM6hooYiOBxHThwIB7Xw4cPyz4/BU31kJAQfxr0b3RFtN5Y/+Gll16SHX7EJ5980qRJkxUrVsgOokuoiAHHunXroqKiUBOlp6fLPv9l/vz5WVlZstXI+IEiglGjRvnHQCc7tGvXTjxu/jq715+gIgYWpaWleDhbtmy5adMm2ee/nDhxAu30srIy2WFk/EMRz50716xZM9yWssOPOH/+/PPPP4/n7rnnnpN9RGdQEQOIAwcOdO3aNSkpCdWQ7PNrEhMTg4KCZKvB8Q9FtCor6k2bNk22+heIDjMzMyGKu3btkn1ET1ARAwKx6/0zzzwTaKsSo3keFRXVpk2b4uJi2Wdw/EYRrcoulfHx8UbcpbK+iD2H//GPf/hZj4XfQEX0f3Jzc/EQjh071tDLeDqHmC69bt062WF8/EkRJ0yYgGJavny57PA7Ll++LJqnvXr1kn1EB1AR/Rm0QydPnhwUFOQ3VWd9QdUzYsQI2eoX+JMiIoJv3bp1q1atioqKZJ/fYbFYZsyYgady27Ztso/4GiqiPxMXFxfI7/NLSkoiIyPPnj0rO/wCf1JEq7JwUqNA2lNp/fr1ISEhS5YskR3Ep1AR/RA0tIcPH476ZefOnbIvYNi+fbvZbPbjV1N+pohWZVfO0NDQwJmigBbb0KFD2YOqK6iI/sa2bdvE/KeUlBTZFzCUlZV16dIlLCxMdvgR/qeIYMaMGX6zqJAjoMX27rvvounGHlSdQEX0K9C+xtMVERERCIMU7DB9+vRG/rtCmMAvFRFNGdy9gbb1bk5OTnBw8KJFi2QH8TpURP/h5MmTffv27dWr17Fjx2RfIHHw4MGQkJAePXr4d/+bXyqi1a8HQ9mhS5cu+OFjxozhrAzfQkX0B/AgNVL2r8jLy5N9gQeyYurUqbLV7/BXRTxz5kzTpk07dOhw7do12efXVFZWzpw502w2b926VfYRb0FFNDx79+5tFDD7V9TJiRMnOnfuHAgNbX9VRLBw4ULc0n62Dq2DsAfVt1ARDYzFYpk3bx7HcGtJTEwMkEEKfqyIuLGfffbZxo0by47AQPSgvvbaa4HQsNMbVESjcubMGdT+eHJiYmJkX6CybNmyRv6+kYKKHysi2LNnj9lslq2BQUlJSXJyMmdl+AQqovFAy3HixInNmzfPycmRfQHMyJEjUYls2LBBdvgp/q2IVmXBswBZxaY21q5dGxISkp2dLTuIx6AiGozDhw/Hxsai6g+0Eer2WbduHfLk5Zdflh3+i98rolUZJBU4q9jUSKdOnUQmVFRUyD7iAaiIRmLp0qVhYWFoNs6ZM0f2BTZPPfVUVFTU+fPnZYf/EgiKmJiYaDabA3npJYTISUlJEMX+/fvLPuIBqIiG4dKlS3gwOnTosGfPHtkX8DQKjJ0TtASCIop13eLi4mRHIGGxWKZMmYI7nA++F6Ai6p3r169PmzYNLeWmTZvKPmK1lpaWduzY8eTJk7LD3wkERbQqb807dep0+vRp2RF4tG/fHro4adIk2UHcBxVR7/Tu3RuPAf6eOHFC9hGr9a233gqc8aVaAkQRwVdffTV8+HDZGnhcvHjx+eefx91+6dIl2UfchMEUEQ3GLVu2ZGRkxMbGdu/eHX8TExNh8deJO59//jmiQ8SIOl+QTC0XFIdaLjj0dLns3bsX+dOvXz/ZEQAEjiJalV7xjRs3ytbAA/VAenp6dHT0oUOHZJ/uuXr1ak5OTkJCglp7o4rQW+1tJEWcPn16ly5d3nzzzdzcXNV46tQpWGCHt7i4WJPc2Fy5ciU1NRUVwddffy37dAayXS0XFIcw4gMOPVouFRUVeKhCQ0MPHz4s+wKAgFLEZs2aQQZKS0tlR0CCez4iImLt2rWyQ68cP348LS0tJiYmMzOzoKBAtaOKUGtvTXJfYgBFrKysnDFjRlJSkuywoaioCCmNviWexWIJCQnBTa/zlWjUcqlzxpgYL+f2coEiIosCdlR6QCkiHopevXrhufj+++9lX0BSWFiIDEGLGU1n2acn8vPzHVxnAFWEHmpvAygismncuHF4JGRHTSAl0stW43D27NnExMRu3brpv1ekXuWCZIYuFx0SUIoI9u/fbzabBwwYIDsClbKystTUVNQViMBknz6AHOLr7d27V3bUBKoIPdTeelfE1atXo+3gYLVrVbIV6XGW7DACGzdubNmyJdp9hlj1v17lYlXSG7Rc9EmgKSKYNGlSYI6isgNaCc2bN9fhu5UzZ87ExsaiHSM7akcPtbfeFRFNjDo75SSQHmfJVt0jnvaoqKj169fLPv1RUlLiXLngRNlBnCIAFRE3T5s2bbjHi5bNmzdHRkYGBwfr7SXLiy++qB3w4SA+r711rYhlZWXl5eWy1QFwlq7GL9knOzu7SZMms2fPrlfI5UOQt126dJGtDoByce5EYksAKqIgKCgoQLY3cZzi4uJBgwbFx8dfuHBB9vkIp5eZ9G3trWtFRPNHNjmMK+d6DTSIRowYgdAwOjpa9ukY5O348eNlq2M4fSKRCFhFDAsLQxjhXFvZj0F7GjVJ+/btDx48KPu8jovfwYe1t64VMT09XTY5jCvneo127drhJk5JSfHQ/AQPgbx1oj9E4PSJRCJgFXH27Nl4avTWSagHli9frpOJGS7u9uzD2lvXiujKeoaunOtpCgoK+vfvj6c6Pz9f9hkB5K3Tcx6cPpFYlf5qsQyCmOOckJAwceLEnJycq1evykmJw/hZrhYWFuIJTU1N9WHfY8+ePWVTffBh7a1rRXTlFasr53oUtOsjIyODgoIyMzNln0HQbd76McXFxWKFCu0yCGha5eXl4UaKiYnR7RB8PeOvuQotR4O7X79+vtoNxsUqwsXTXUHXiuhKS8GVcz1HaWkp7tRWrVr5sKPcdZC3Tq8q5/SJgczq1au7du1qfzmC3r17I7LxYVhgOPw7V6dNmyZeK/pkZnOPHj1kU33wYe2ta0V84YUXZJPDuHKuhzhw4ACewMGDB/uq4eYukLdODyRz+sSApbKy0pGFgcCCBQt69epl0K54LxMIufrZZ5+J14qyw/MkJyfLpvrgw9pb14qYk5MjmxzGlXPdztChQ9Fe69y58759+2SfAUHeOt3l6/SJgYlYoUK22mX//v1nzpyRrURDQOXqyZMnUfmkpaXZCYXdjotbOfqw9ta1Il69etW5Ze5wln7eiufl5eGOfO2113S+AqHjIG9jY2NlqwOgXJw7MTApKSlxYoUKqzI5WjaRGwRgrg4ZMgRVUHx8vNe2kcKT7nR159vaW9eKaHW2seDcWW5H3ex35cqVss/goMkpmxwA5eLciYHJ9OnTnZtjkJKSYoiVj3xCAOaqxWKZPHkyRLFjx46yz2O88847sskxfFt7610R+/TpI5scwLmz3M7ixYtxF+rky7gX58bgISucOzEAEQsDOTcPvbCw8Nlnn5WtJLBz9aOPPgoJCdmyZYvs8AxxcXHq8N164dsKU++KiDu4V69ejvdKIyXSG3FsmOGoV7lYlfQsF8dxZRkEsHLlShdnSfslzNVnnnkGzfTx48d7YdT3woULU1NTZWvt6KH21rsiWpUtRWJiYhwck4KURhwVZkTqVS5IxnKpF0lJSa6Myz127NiwYcNka8DDXL18+XJiYiJEcfHixbLPA2RkZDgoiqgi9FB7G0ARrUrl68iIDLT+fJ6hAYUolzob3UjgSPERLa4sDGRVOldcXDfEL2GuWpXxDZBDL8SIAkSKjnSfoorQQ+1tDEW0KrttpaSk1Na+gx3eIUOGyA7iYVAuyPY6y8WgI9d9iOvLdrh+Bf/D9Txx/QoBCBoiWVlZtY0+FbWETqoIwyiiysGDB5G5PXr0CA8Px9/k5GQX11knbkGUC4pDLRejv3HxLezf8wTMVd+ye/du1Axq7Y0qQm+1t/EUUYWNNX3CcnELHAPiCZirOkG3tQQVkbgZlotbcGUTSquyjJbeWt96gLmqE3RbS1ARiZthubiFQJ455zmYqzpBt7UEFZG4GZaLuwjA9ca8AHNVD+i2lqAiEjfDcnEjAbUmtddgrvoc3dYSVETiZlgu7mXGjBkWi0W21sLevXuZ/47AXPUtus1PKiJxMywX9xIIO/l5H+aqb9FtLUFFJG6G5eIJiouLp0+f3qVLl9zcXHUFkIKCgry8vMzMzJiYGC6h7gTMVV+h21qCikjcDMvFc5SVlWVkZCQmJsbGxkZERCQkJEycODEnJ8eH+8n5AcxV76PbWoKKSNwMy8U7MJ89AXPVO+g2n/WiiAPrT3h4uGxyAPkfE7u89NJLcg7WhXPlgn8k/29iF93WKYaGuVpfnKgiBjpVS3initCLIjoB7119wnLxDsxnT8Bc9Q66zWcqInEzLBfvwHz2BMxV76DbfKYiEjfDcvEOzGdPwFz1DrrNZyoicTMsF+/AfPYEzFXvoNt8piISN8Ny8Q7MZ0/AXPUOus1nKiJxMywX78B89gTMVe+g23ymIhI3w3LxDsxnT8Bc9Q66zWcqInEzLBfvwHz2BMxV76DbfKYiEjfDcnECJ6Y563aOs35gruoW3dYSVETiZlgu3oH57AmYq95Bt/lMRSRuhuXiHZjPnoC56h10m89UROJmWC7egfnsCZir3kG3+UxFJG6G5eIdmM+egLnqHXSbz1RE4mZYLt6B+ewJmKveQbf5TEUkbobl4h2Yz56AueoddJvPVETiZlgu3oH57AmYq95Bt/lMRSRuhuXiHZjPnoC56h10m89UROJmWC7egfnsCZir3kG3+UxFJG6G5eIdmM+egLnqHXSbz1RE4mZYLt6B+ewJmKveQbf5TEUkbobl4h2Yz56AueoddJvPVETiZlgu3oH57AmYq95Bt/lMRSRuhuXiHZjPnoC56h10m89UROJmWC7egfnsCZir3kG3+WxURczPz4+MjMRf2UF8DcvFO+i2TjE0zFUvIGrvEydOyA4dYFRFjIuL+/e//42/Fy9elH3Ed6A4WC7egXW3J2CuehrUDKgfFi9e3LNnT9mnAwypiNeuXcvJycEH/B0wYIDsJj4C5SKKQ5QLDuUUxH2w7vYEzFVPg5pB1N4rVqywWCyy29cYTxGRicOHD1cPly5dqsNsDUBEuaA4xCE+aIuJuB3W3Z6AuepRUEuoVQTIyMjQOHWBwRRx/vz5r776amVlpdYIC+xaC/EyNZYLDlkunoN1tydgrnoOUUtoLaWlpX369MnLy9MafYuRFHHVqlUJCQnl5eWSHRbY4ZXsxGvUWC7CznLxEKy7PQFz1UPUVnsXFhbGxMToZyyekRSxZ8+ely9flq0KsMO7bds22UE8D7Kd5eJ9WHd7AuaqJ0ANYKf23rdvX1xcnGz1EYZRxCNHjhQUFMhWDfCirSFbiYdBudjPdlEuSCY7iGuw7vYEzFVPgBrAfu29Zs2aiooK2eoLDKCIV65c6d2795YtW2RHTSAl0stW4gHqVS5IxnJxL6y7PQFz1b2IWkK21sTo0aMXLFggW72O3hXx+vXrgwcP/vTTT2VHLSAl0uMs2UHcTb3KxaqkZ7m4ixMnTkRGRspW4hrMVfdSr9q7rKxswIABmzZtkh3eRe+KmJaWNmvWLNlqF6THWbKVuBuWi68oKSnp2bPn4sWLuQyCG2Guup361t7nz5+PjY09dOiQ7PAiBlBE2eQAzp1FHKdeN7pKfZ8QYovFYhkyZMiKFSusymRnLoPgFpirbse5FjDkEKIoW72IfhVxy5YtDnZA14jjr7hIvRDl4vRLQZzIcnGFESNGaOd9Tp06NT09XeMnzsBcdS8ujhsYPXq0bPIWOlXEAwcOoKVw9uxZ2eEwOBdXwHVkB3EB18sFsFycJi8vr7S0VGtBPc4VKlyEuepeRC0hW+vDuHHjZs+eLVu9gh4V0V3j9cXEAPujfkm9YLn4kPz8/BonuogVKmQrcQzmqntxS+1dUVGRmJi4Zs0a2eF59KiIbpzTLWaGylbiFJcvX3ZvudQ2Y5fYUlRUFBcXt2/fPtmh4MaiCSiYq+7FjSty2C8az6EvRRSdFbLVZWyX3CT1wkOdSLggy8URHOlEckuHdkDBXHUvnljH2LZD29PoSxE99EIb18SVZStxGA+Vi1W5smwiNjg40MDFQU+BBnPVvXjoWZYGPXkaHSmi2D/IE1s7SRsVkXrhuXIBLJc62bRpU1lZmWytBbFChWwlNjBX3Yvndn/LyMjIysqSrR5DL4ro6T1mxWa2YqdKUi9YLj7EielZnPFZJ8xV9+LR2ls7VdQL6EIR8ZuPHz8uWz0A/ouHYh2/BHk1cOBA2eoBUC7e+UcGwpVFrRxfOivQYK66HTy5Xqi9T5486Z0qQheKSHSL13rwvfaPjIIrCx/XaxH2gIK56na89uR65x9REQnRI+PGjZNN9YErVNSIW3JVthI/gopIiO5wy3ZxdW5dGWi4K1e5uIQfQ0UkNVBZWXn06FFXnny0pnEF2UrqorCwEDKWn58vO5wlISGhvLxctgYY7s3VVatWMVf9lToUcc+ePa+88krr1q0jIyOTkpIWLlx46tQpORExDidOnHj99ddrHM38usL58+fx+erVqyaTKSIiQk7kMD179sQVZCuxS2lpaZ8+ffLy8mSHC8yfPz/AV6hgrtZISUlJZmambFVmb9uOGl25ciUqB60F7V1YZs6cqTUK0BqWEhuIOuqsm2++GfXaT3/600cffdSk0L59ezlRfcAV+vXrJ1uJt/jqq69QBDfddJPUXt65c6co34MHD1qpiD5ixIgR2dnZstVlAnyFCuZqjZw+ffpXv/qVbLVaUdtfunRJMv7+97/H46xdUw3tjPvvv79BgwaaVP/l7bffNu6zb+97v/jii/htstVhKioqjipojXYUEdEJEtuZHSF14qFI7KcntkAR27Zt+5e//OWxxx5TW7jl5eU/+9nPGjdurCpijYiuVNv2o1VpMDqy0tWZM2d++OEH2XoDfA1cB2IsOwKDFStWeO5mFvM+ZWsA4OlcNe5UWscVETU2agaEiQ888ID2RezWrVttlW/79u0wQkElu1GQf4+WuLi4xYsXy1YFtI8efvhhqeX1yCOPdO3aVXzu3LnzrbfeKsKOJ554YuPGjTC+9dZbOLzrrrseVvjPf/4jEuOW/e1vfysS33nnnefOnVOviWR9+vTZv39/VFQUvA899NBHH30E+1NPPSXSo/zUxKROhCJmZmYi60ShgI8//hiHWkVEawM5361bN5EAJdK3b9877rhD5Pnjjz/+4YcfCtecOXPUsrvvvvu6d+8u7EOHDsUVxGcBDkWygQMHir5ZQYgC/oW4YW6//fbnn39ec15AsGPHDk8vSX/x4sU9e/bIVr/GC7mKStKgueqgIhYXF6PGRsWLBjEeT2lGJtrW0nQUxFFI9uabb2qNBsKeIo4aNap169Y1LuiHevP//u//mjdvrjUiI9577z2r0ti/+eabe/fu/e67786dOzcoKEisqLt+/XqkefLJJ8UrKzUGf/bZZxs1aoRqGsl69Ojx5z//uaSkRL0mMv3nP/85JHbQoEGoLm+55ZYNGzbgiyHThw0bds8996CWv/EVSB0IRSwsLEQ2qkFDhw4d7r77bq0iSr2mTZs2xWGXLl1Qmqmpqc2aNRPvCZYsWQL7o48+CvuUKVN69eqltg2lXtPBgwdDOCdMmDBjxgyT0kjCkyZcv/zlL/EQ/v3vfx8zZkxaWpron1FPDATESggnT56UHe4G/8VzAZPe8E6uisUljJirDiri+++/j+dRbMCAD6iH/5dUCXK07VfR24TK/8SJE5pURsJe1XP06FFkwf333x8bG4tfLg1cbtWqFURR7WSDF+IklGzz5s3SGndqV5vJptd03rx5MGpvKRyiclQ/g0mTJolDUZ9CF9XECFYQvqiHxD5CEa2KCqK8rMqDIdSxNkXEM4/P0jh+UaAdO3a86aabcAXJbq2uiLgf8PnChQviEOWFw5dfflkcQhFxqAaXCB/xTNbYDvNjvDNGwzv/RT945/d657+4HQcVMTw8HPW8mNj6//7f/0NdcebMGdV79uzZe++9V33TIXqbRA1jUOwpoiA/P3/+/PkiFgYQQqFekEBkKFoEIhlyYefOneIzdBEpW7ZsOXXq1N27d6uXstooYllZWYMGDX784x/P1YCYA60M8V+QPioqSk2Pmw/FgyBStYDf/OY32kNiB1URkfOiwxmZ+bvf/Q4falNE5Pmf/vQnHIaGhn7zzTfatktWVpZJ6bj+5JNPpHcPWkUMCQlRPwuefPJJWERDEor4k5/8ROuFWPpks1BCAgcHFRHPKeIQ8RkPLGrmJ554QvWKBBAF8Rl6iWfZ8SXUdUjdiqiyatWqO++8E78fDQFhSUtLEzUdoknECtrErVu3rtJPhbFjx6rxpam6IqJU1GQSIkrAhzZt2qjpARSxUaNGWstDDz2kPSR2UBURDBkyBBE2clgs5FGbIoJjx4517twZbRcY0XwZPny46AyAWGZkZDzwwAMmZfwq2kDqGHetIj7++OPqZwEeIVjEC5hfKmi9KSkpq1ev1loIIe4FoZ703Anuv/9+dSvv69ev4zl966231HDlkUcegWXr1q1q+gcffFAduw6X0TcJqYcigkGDBuE3jxgxQhyeOnUKjQKrsiWvVOVZleEYy5YtQ7ULF/4Ko6m6IiLrYZFiPi0mKqJb0Sritm3b7rvvPjT6IHhWu4ooKCoqglb9+te/Ntm8TkCwKNpAd9xxh7BoFfHvf/+7dHuI/yXGIVMRCfE+165dw9MqdfkidEHDV+0H+uyzz0w18cILL6in4Gk1KWGPVamuv/32W9VlRGQZs09qaip+s/YdYdeuXfft2/eLX/wCdasmYTVuu+02tVsMp/fp00frRaMDlbLWosVERXQrWkW0KtkLJROf61REARoxDRs2lLoEBGgqmW4on1YRpVE2iP7vvPPOu+++Gy1QKxWR6JIlS5YYd5q5g+Cp3L9/v9byzTffaB/VLl264GHUvNGaO2fOnLvuugtxpJrmwIEDqJMfe+wxnPu3v/1NtRsUe4qIYK5jx45TpkxBS+GLL74QfV9/+ctftN3EYkiu6UYPmAAZl56evmbNGojlypUr4U1ISBAu6B/aIIg11bGmqHwRQ6Be/vDDD3NyctavX9++ffv+/fuL9FRE9yIpopbaFHHt2rUoerQB//Of/0Co4uPj4WrXrp1VCf769u2LuuPrr7+eOXPmAw88cOutt4qraVUQN0nz5s3xfz///HMUMRpAP/7xj9UlAvxMEfEzdfsSFM+ObDI+iHXQ4DZp3ua4hWeeeUarDU7wq1/9Cve5bNUT+IYmZdGVefPmQepEN486aUpMlqt+RhUrVqyQ7GI8KtBOnDMoNfxglezsbLFmjQCVHao524nYcIWHh2stq1atUs8Czz33nDoYCVVnaGiouKyYWWhVuuNuueUWNf3vfvc7FI9wmaiIbsUJRdy+fbt4cgTIf7QcxcDRbt26ae+Q3/zmN2gAiatJcWFxcTFOFMnQqFIHYVmpiF7ELxVRTAGKjo7u0KGD7HOBQFBEtEpRu6rPr8hGdeIEntMac6CiouJnP/uZeNUiEHMWa0xsOOr4DZcvX0YdukQBuiW7FZARH3zwgWRExYqAAMFibVXb+fPntSPs8Y8QPcyfP1+dti84evSodnC/sEhrq3phv0q/AQ1qKT9VkKvIWzEGSixPo10kCA8PGjoQPCnzS0pKcIcsXbp027Zt2ncStit940Scrk7tVzmuoLVAbktLS7UWAyEpohhqdOnSpUWLFqEJKK34gxzbuHHj66+/jidFu42tOAvPCM7KzMysMTeOHDmSnp6OBLYFiqdp2bJlU6dOReAu/iOusH79eiji+hto06ONn5GRgRawOqQCfPvtt0iGb4hz8R10uziLqNP37dvXoEEDaVmrvXv34ifglkZdNHny5H/9619aLzh8+DBuyHfeeQfVjjRCUlVEZC8ugpRa7549e2AUVeL169fXrVuXlZWFQtyyZYtYARxeyMY999wjclvqnNQV+IH4/viS2mkVogaQHmEVnCLNjxK1h9ZiUOpQxDpBhYiCr/GJJSQAkRTx97///WeffXbvvfeKNvijjz6qrmOHxvjf/vY3tXku6l/1LLQeUJ8K+29/+1vtWwlE8P369VNj7ttuu+2tt95SvUD9dyZliSgoK+RTtQhESrTuO3furBq174dCQ0Nhyc3NFT0E6osMXYG21M0332w2m61K0xxNBK03JibGpAzcU38gGgqqt1OnTqod/PrXv9Yuv6IqItqC+BchISGqC5rXsGFD5BUK4vvvv//DH/6gvU6XLl2sypfRIg2eILrFeUXEky8K29CzTwhxL5Ii4gFB6CA+WyyWxx577KabbhKxBZQGlamaUgsUUR2ebVVW/MF1EMbhM+p0fO7Ro4fqvXjxojot+Ny5c3feeWdt2x5JvaYbNmzApf7+97+rli+++OLJJ58Un4Ui6nxFPeTnrbfeKl5fCYXTrmAlFPF/qZXi+Pe//621qCCk1haWttdUDIZQU7Zo0QI5iQAUn5HbyCjVpUX/vabEFucVcdeuXQi07SwMTUgAIikiakztYk+IFVC37tixA5+feOKJ22+/HbKkelWgiNqgcN++fTgrMjLSqix5iM/SYpIIGQ8dOmRVdimSNECLpIgvvfSS6cbKiyqwiFdEQhG3bdum9eoNk2Yi0EcffSRJeI2KOGHCBK3l+vXronvw888/h/fFF18Udq0iVlZWIkxX+wmRjU2bNhWfIXuIyLXz81SoiEak1oeHEOIEkiI++uijGud/oz2hZ//6178QL+IwPj5+wYIF2nd4UETt2kCokREJPfjgg9Yb4x3CwsIiNTz00ENCWUeNGuW4Inbs2BGJpY0DYVm7dq31hiKqy8/qE3zDTz75RHwWyzBpFxWrURFfe+018RntjOjoaHVDAoG6JKQ0siY9Pf39998Xn2FXX0m+88474sTHH38c0af27RoV0YjU+vAQQpzA9j2ixvlfRdy8ebM4/Oqrr3r37i2qVLHMrMB2M527775brLondipFrNm3OiKmTE5O1tbjEpIiig1kdu3apTXCsmLFCusNRdTzTvFifQ8EzWomoGWglSv7igi5uuOOOxAoQ8nWr18/Z84ceFEcwisp4tmzZxs3bmy9obvaEVI4F9+hYcOGJmVFJ3XsGBXRiNT68GjB3SBGncmO6nzzzTcPP/xwWlqa7HCAL7/8EudOnDhRdtREaWmpulERqReoRFCUNa4rIQbFBez2hO6iXoqoZciQIUuXLhWfcZY6N8l6YwFlUVmLZRBq27h006ZNdhQRLrEqgmDKlCmm6gtuICL80Y9+JAbK6V8RESjb7nuDrFOXj7CviPj89ddfq67Ro0fbUUSrkj4pKQkRuVj2ukZ69eqFZGKA/SOPPIJ2jJxCH2zZskU87zUip3YY3FGoxmubX2AfqAzOrW33XC0TJkyQdppzI7U+PFqGDRuGYq7xhYcWqJpJ0xFfL1CJ4NxXXnlFdtQEau0al1PRcunSJdy7+Oayo57gIurWVH4AahCTsgmlto/Oqllgli+GXcRxRVy2bJm2ldmxY0d1x0qc9ec//1ldcPmPf/yj+gAePXoUonXvvfdqxUCrW0FBQZMnT1YP8/Pz1VaOpAFnzpy56667cDOIQ3yZgQMHqotp6FwR9+/fLymW4PXXX1ftdSqiWHvMqqxoKBZttqOIf/vb35BGirNXrVqlfrZYLKguTDeWKxF7qIn3u3pDrMVYG3Jqh/nHP/5hstlD0UFEFfT000/LDhtefvllV76kfeq+Lh6Jn//857fffnudA4hdUcRTp07NnTsXUabsqAm0cz///HPZWh087fgyLVq0kB31BBeRdnUwNKhGxca/6ksRQUZGhqgRqIgu4rgi4sMvf/nLZ599FlEaZMyk2RMNZ7Vq1erBBx9ERDhy5EiTZtUnq7IDGp5HREKoGpCgc+fO2t45RDAmZQTp0KFDEdPcfPPN6iw92KGmISEhYpCOVXmXiQQ9evTAfxHfQb3bda6IIltk642tW0VTw74iijkq7dq1g+W2224TQ5bsKOLMmTNNClqjSVmYAnUjlPiJJ57AIU7UpsdjhdweP3689iyfs3Tp0v8uyzZ3rpgCpB7OvTHa1glcUcTS0lL8a0dWt4BMuPIl7VPDLSUhRnDh6bINLCRcUUS3Q0WsESgiqtqGDRuqg+UEf/rTn8SeTVREF4FKaRt20h7uU6ZMQf0oBu4jQEFZoLkJbUNE+MYbb6jJoIhXrlzBo/SLX/wChYKYT9qTFlcQw1ZxLiJIaY4ETvztb38LL9RuzJgxaoyIdiRSisE4auKcnByx4Mgjjzyi7UEVPYTaXlZd0b17d+2v0NK3b1+xQiRUSkqDQ3U9LMSFzZs3v/XWW5FLs2fP/uGHH+BVN39PTU2Vzj137hy0FuWlNUJNkeynP/0pMhDSgpLSDi1G5dm/f38dKqKW9u3bSzLvNK4ook6oIyPQumzQoMFf//pXfMaz16RJE60XZd+mTRu0c9EsatSokdhDUVVEWHAD4cYKCwtDbYvTcefhzsAFcQgjLOqEWanXNCIiAofIX7PZjLobDVttmUm9piZli74OHTrExsbecsstog0o3rHjxIcVREiE/4I24K9+9auf/OQnuE2PHDliVYYqIGyKi4vD44Gmt/qPIIQ4EYe4lLiIsKO+w+nIE/xqMXlZ262KQzHeDwnQ6lTtOkGs4rZr1y6TZlvmxMREFI12FTfwwAMPiLlWzZo1QwsaRTlt2jT1OlDQlJQU1CYoStwVJqXWVr1If88994SHh8OFEsHF58+fr3pFkIqS7dKly+OPPx4dHW1S1hwR3g0bNqDxDi9KE8IAl/bVJsIm1He4Jv6v2MTKL7EdWUN8zl/+8hfUXf6xMouWGhURdRrCCZMyuSUqKgpPutq7LjAp63qi0njyySeRLWKyrFBENApFZCz2iUNVr56FWnH06NHi+UX9AK86dkTqNUWUL5ZQRlXfqVMnZL7aXy31muJhwddABS6+sNqmsd7o58C3Cg4OxnUkHamROtz4umplh1+I7/Tdd9+pXti1FZZYREOriCZluQ1xKEYHQD7VsW1QRKG11loUUV3u8vDhwxAhdUlVrSJeuHABv1Yd9AwZE3VrjTEi/gu+JJrJYlUB0bWCL6aefvLkSbS4tROcTdVjxPLycvwitWxwnZiYGO2WYEh/3333iRW56hyL5H3UdU1ROkLjS0tL77///qeeekpSRDRv1aWIkCFoN0DnblymShGRD2piPACQQPVO+PDDD9V1G3DDoBwhcuIQdgikOmK+W7duJgVRamiB/fjHP1bnm1uVNri6+5hVucd+97vfiSW1dJi97oKKqCvWrVuHdpip+i5IfkONijhgwAAYX331VXG4e/duRB1qPblo0SK0Zb///ntxCDETU2yFIkIgxbqeqBDEy1SRzKoo4iOPPKKKyN13333XXXeJiEJSxMzMTByiblfPVRd7khRRu8L7n//8Z1QvaqtFKKK6MfKhQ4dQ14k1mWtDzgiJP/zhD/h5YuFEMTpZO/hFbGShHqLJb7JRRLXusyrRmHY6MKpgVHAXL1601qKI2nUIk5OT1WBcq4j79+8fOHCgmkylNkX89a9/bf/VyOLFi9U9o602ipidnW2qvjtgUVERmh5qf7JJ0+rRIaoiIuATdxV+Lz7gr6SIErNnz9behVBE7atc0YP35ZdfqhYtaD+q5+J+0K63grtTNNyEIorxfvhfagLIHloYat8dvNqxDP4KWuWyifgOVERoPqIKkpal9Q9sFfHYsWPQlaCgIK3xrbfeUtevfuKJJ2pcukEoonbEiVj3QD2EImr7VIXuiiFjWkW0WCwNGzZEKFnj6kt2RtaILdDV3iyhiKjc1ASIXuyPQan5uirqV7QqA/cRCqC9L6qnEydOwCvm6AjEehmSImrXB2/ZsqXarLDeWH1DDGWuURG16/ZCpf75z3+Kz1pFRBxz++23I5KQplXVpog1jg/CHfD6DVBII0eOVF2m6ooo5ntp+wCtShq0ItXPormkT1RFPHfunIj5EB2i3YRstFVENLXeffddkS1iWLnqgiJqa4c33njDpNnMxKoMcMjKyhLnikBQ2FNSUqQFlyOV4XlCEcW7zK+//losIyJQbxKr0h+rfU9DCHERW0WcO3euSdnmXfsYLlu2bOjQofAWFhZK6VWEImqrR4Rl2sRQRO0iBqIPUkw60irizp078bl58+ZqSi22ighpEFXNc889B9dLL70k7EIRtfs1QSztj8qp+YcJEMyaaqJr167wot7H506dOqnpV69ebbJ5j6h6rcoq9dovJ+Ymi1EGNSqi9gUSfjNqZ/FZeo+IQ4RuKC1kN84Sr5dqU0RpgodYJVLbJN+4caPUC6pVRCEMYlEPFdONQhWfccdovbpCuxvU8uXLxeg4sWqJVhEXLlxoUvbhOnqj/0Fa2hGKqH62Kit6qJkAIcRnqKC6o8WCBQvUc5OSkj777DP1RCBWmhaK+OCDDzZo0ECsxK9FbVeJdVsIIe7CVhFRhZqUiarSYyh28Ni9e7eUXsV2ZI1o0aqH6msygXjvtmjRImt1RRRSUmP0Yq2uiGKzRrUdn5uba9J0btuOl4aIqJFVjdT8wwTQ0ieffFJdD0Lw05/+VMQWFy5cgOD98Y9/VNOLBY28r4ha7lAoKyvDPzI5oIgDBw5EMu2yhHPmzLGjiKmpqbCg0lctViWNOs3LZBxFRLCFb6tqm1YRe/ToYdKMdrHeKFz10I4iiiXgtfH92LFj1XPffvtt7atv641FWMT/Em8d7GydQ0UkxL3YKqJoEGvXmtdy+fJlNFtrfIvvLkUUW7Vo16DXolXExMRE7fU/+OADk+cUsVmzZraz8sXqwOKz2WzWfhvR5eVbRQwODjYpWzkjNsd/F9vEqNgqotjYVl3DBcWMK0uKqN29DxEkLNqtYVB4DRs2VDPdZBxFtCrD09XXclpFFHGb2ldpsVhEH7h6oh1FFL2g6l5raJ2IIaPi8Pvvv9cumox40aQgFFE8IXb2W6AiEuJebBUR9SfiCu1gRomWLVtmZ2fLVvcpolWpYUzKZmTa9AKtIooRT6pLVD4eUcRDhw6pE121bNu2Tf0GYqqiaNFDpcTUBS8r4pYtW1DLi9la+Hvrrbc+/vjjwoWK+JZbblmwYAFKRXS72SqiGGAiiuHixYsIGXErSIqIw927d6v9hyLTxQXx21HGU6ZM0aY3kCJq0SrihAkTTMpOb8gTPB4oKTFlQk1sRxHF/Yo7tbi4GHc5xFXM/VcT4zPu1OXLl6Mhcu+994opLkIRS0pKxG5z6r7E58+fRzNQPZeKSIh7sVVE8Oabb5qUKWTiEKKyefPm7du3i0NUI/fccw+q1mvXrqEmhKSJVyFuVETUD5CPBx54QIwbuHDhgjoqU6uI06dPN914U3jp0iVRTXlEEfELa3sDiazRblUzfPjw/v37v/XWW1ACnKIupY+fJF1h9erV2uEYmzZtQgLRJymtWfP555/jUDs7GDW1OmZXWrNmxowZQ4cORRWclJQkrSGAw3Xr1uFSQnfxX2yXxUEQM2fOnISEBFwH+Y7gRrs+FlR269atixcv1v4WGJOTk/Ef09LSpDVCkUzP+yfj1qlt1cEVK1bgy6tv7PArcAsOGDBg1qxZKCNRQGpi7RBiq/JqAV610QAtRGb269fv/fffRzgIu3Qn5Ofn4wpffPGFVWn93HzzzdJSeWPGjEH2xsfHa6c5WpW1NrSHhBAXQZxQW1V//PhxVIx4EhFISPPu0VRFzQDtGT169IYNG0TstGPHDlxK7CYmQFWgvbh2x2YAHYFXzAuvcc2agoKC559/Hl8AWqtWy9KaNajAUVGIChz1G1zqXmmiTtOubgERqW04vaBWRSTEC+DubNCggTTOmxBCfAIVkXibcePGoaVWUVFx7dq1xx57zFR9whAhhPgKKiLxNiYNt956qzT0lBBCfAUVkRBCCKmCikgIIYRUQUUkhBBCqqAiEkIIIVVQEQkhhJAqqIiEEEJIFf8f0zq3ue+LffUAAAAASUVORK5CYII=>

[image4]: <data:image/png;base64,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mD755BP+51y4cCF9+eWX9NVXX9E333zDs5MWLVpEP/zwA/3444+0dOlSWrZsGZ166qnca23VqlX022+/cQ4OEp/Xr19PGzdupM2bN3P/ta1bt9K2bdto+/bttGPHDtq1axft2bOH9u3bp74VguagDQ9q0SVDybJliIwqRrkO2wO7bJkY+7BT5LgPHEv4uYptkSYIl7NQuV+2AM4DgrcR2dIf9UzlCy5873dq8/6vXDMrHh544AG677776N5776V+/frRPffcQ3369KHevXvTHXfcQT169KDbbruNunfvzheqG2+8kW644Qbq0qULXX/99dxU9rrrruOiqu3ataM2bdrQ1VdfTa1bt+Y6Rpdffjk1b96cmjVrRpdddhldcsklXL3+ggsuoPPOO4/OOeccaty4Mf9TorghIlZo41G/fn0WqlNOOYVOOukkOuGEEzhxtm7dunTsscdyD7saNWrQUUcdRUceeSRVq1aNDj/8cKpatSpVqVKFZ5tVqlSJKlasyAnRZcuWpQMOOMA2bIPfy5Urx/s66KCDePuDDz6YDj30UDrssMN4n9WrV6cjjjiCatasSUcffTRX6q9Tpw4PR6E2EI4Jfd5ycnL4mBs0aEANGzakM844gxo1asSv6+yzz6Zzzz2XmjRpQueffz43Or744oupadOm/L60aNGCWrZsSa1ateL37ZprrqG2bdty0U28rx07duTim507d+b3vWvXrnTTTTfRzTffTLfeeiv/bXr27Mn5SGhIjr/f3XffTX379qX+/ftzwUhU6Uau0oABA1iuBw0axMPOEOsnnniCnnzySRo+fDjPCIOUPP/88/TCCy/Qiy++yGKNoZ1x48ZxLaRXXnmFXn31VS6fMHXqVBbr6dOn09tvv03vvPMOi/V7773Hyc6zZs2iOXPm0EcffcSFdBcsWBASa0Qm0fAZYo2hvW+//Za+//57Wrx4MSey//zzz/TLL7/Q8uXLqbi4mFauXEmrV68OiTUiIGixs2nTJpbqv/76i/755x/6999/WawRHdm9ezft329EevE5x+tPhhJli6NQsQ+vhWTLOkwY3EcsqEJl/VldF6Yk2UKkzPjfCL9C+9Aj5M6InBmvk98PjqIV2SN6SYLPk+BtRLb0x5eyNWTidBr/8gTq990OWhRndMuLJDqMiAvv3r17ObqFCzIuzLhA//3333zBRjQM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A5QmQKsgtAlq3769qlu3rqpWrZq65JJLJC8JuV/z589Xa9asUbt27VInT57UL5HyfP/992rFihVq1qxZ6rHHHpNNBcizwqYCiBV2DeK5zpw5U33wwQciYISYDmXLfChbFkDZSl++/vprtWrVKjVq1Ch11113qeuuu07+PyFikJN69eqJrNx9990SJXvhhRckf2njxo36pZIO5G/37t3q/ffflxpkaOqNYrCdO3dWLVq0UNWrV5ddglg+vfnmm2XHJnYMvvzyy7KB4Mcff9QvSYgVULbMh7JlAZQtO0FUCDXB9u7dq3bu3Cl5YagJtnLlSvX666+refPmSb4Ydu89+eSTUvATgoTaYdhNiRZFEyZMkJpiWN5btGiRLG9C/hB12rBhg9q2bZuUXPjvf/+rTp06pT8EQkgcULbMh7JlAZQtQghJXShb5kPZsgDKFiGEpC6ULfOhbFkAZYsQQlIXypb5ULYsgLJFCCGpC2XLfChbFkDZIoSQ1IWyZT6ULQugbBFCSOpC2TIfypYFQLbeffddHjx48OCRogdly2woWxaACuQ8ePDgwSO1D2IulC1CCCGEkCRC2SKEEEIISSKULUIIIYSQJELZIoQQQghJIuX279+vePDgwYMHDx48eCTnoGzx4MGDBw8ePHgk8aBs8eDBI3J89dVXavfu3Tx48ODBI4HH/wEOff7dT9ZbowAAAABJRU5ErkJggg==>

[image5]: <data:image/png;base64,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>

[image6]: <data:image/png;base64,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>

[image7]: <data:image/png;base64,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FiJbJkRkSxAEobkR2bIWIlsmRGRLEAShuRHZshYiWyZEZGvk+OSTT2jJkiX06quv0jPPPENPPfUUvfTSS/TOO+/QRx99RMVi0fgUQRAsiMiWtRDZMiEiW8PDiy++SPfddx+dfPLJtMsuu9COO+5Im2++Of30pz+l1VZbjdra2mq+2Ww22nTTTWn77benjo4OOvLII2nWrFm0YMEC48sKgmBCRLashciWCRHZGhyffvopTZs2jSVIl6I999yTjj/+eIrH4xy1agSPPPIInXXWWeTz+egnP/kJH8faa6/Nx/HKK68YNxcEoQUR2bIWIlsmRGSrfyBPbreb2tvbacstt6QbbriBXnjhBeNmTce7777LIjZmzBiWsCOOOIKuu+46+v77742bCoLQxIhsWQuRLRMistU75513Hm2xxRYcKbrwwgs5itSqQA6POuoolsXVV1+dHnjgAfr222+NmwmC0ISIbFkLkS0TIrLVxb///W/adtttaffdd6fPP//c2G06/ve//9GkSZP41soiKQhmR2TLWohsmRCry9bll19Oa621Fp144onGLstx9dVX87k47rjjjF2CIIwgIlvWQmTLhFhVtl577TUeVkNiudAFzss555zDMx8FQWgORLashciWCbGSbGHYbO7cubTeeuvRf/7zH2O3YODBBx+k3XbbjS666CJjlyAIDURky1qIbJkQq8gWoljRaJR++OEHY5dQA5jR6HQ6jc2CIDQAkS1rIbJlQswuW//97385F0lRFGOXMEDuuOMOOvTQQ43NgiAMMyJb1kJky4SYWbZQdLSzs5OHD4X6sdFGG3FFfDPy9ddfU6FQoPfee49ef/11Xj4JSyk9//zz5aWUli1bZnyaIAwrIlvWQmTLhJhRtj744APabrvt6P333zd2CXUCArvqqqsam5sGlO64+eabaerUqbTXXnuVq+vjtuuuu3KE7thjj6ULLriAi9Tefvvt9MQTT9Df//53ymQy9MYbb1A+n6fPPvuM9/fVV1/xqgD/+te/KJ1O05NPPsn5f3iNa6+9lvPaDj/8cNp7772rVhXAbZtttuHVBU477TR6+OGH6e233zYcrSD0jciWtRDZMiFmlC0kwE+ePNnYLNSZxx57jC655BJj84iBtSJPP/10litIzs9//nPad999uSDtQw89xPLUSCD9kLKZM2fSZZddVq7kj9sv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1Vf0hz/8gYdOV111VZozZw4Vi8N3cV9nnXUoEqkepqs/KFxaKVup8lI5+YRSWpIoRM72NtKai1xMNJKMl/O8KulNtpLREAWDAXLbIU5dspWBbBVzFFOFCrvTZSsb8ZSPI67uEyKGQqVKMkfBUrl6FKPFsYWCfoo3ULYQuYRoC62FyJa1ENkyIVaVLZQyQBL4lltuyfdrARcqVEjHxQrVtyEuEDSITLOA9/Laa69xZAlrFB500EGcn1PLTMJ68+yzz9JNN91kbK4rAaetLEhRv7sciQpW5GNhzUQsf6RRoHa7r9xXSc+yVSS7O8SPtfUNDbJFSP/y84LQumwVc3EKl2zLV7GckrO9vSR9xFGySt9rhGzh+yt5Wq2JyJa1ENkyIVaULQyjXH311YNKAO8NDDdedtlldPLJJ7OMod4ULmyotD59+nS68sorWdQQIZs3bx49/fTT9Morr3Btqo8//ri8n08//ZTeeustWrRoEcvKI488wkN9EENE4MLhMB133HFcxgLDgFtssQUdffTRNG3aNM43W7p0acVRjTw4HpyPXC5n7BIayM9+9jP+LgqticiWtRDZMiFWki3kY+2xxx4sRY0AInX77bfT73//ey6sesghh9DYsWNphx124IWAtYWOe7/hArntttuS2+2mAw88kHPIzjzzTIpGo5TJZOiTTz4xvmRTAhmEGNYj+V4YOEjUv/baa43NQgshsmUtRLZMiBVk64033qD29nb68MMPjV1CAznttNNo8uTJIzKkaUX22msvnvRR6zC50LyIbFkLkS0TYmbZwsUd0STIltAc3HnnnRzdW3vttZsq180s4A8KRFFxjgXzILJlLUS2TIgZZatQKPBMwvHjxxu7hBEAZTUefPBB2mabbWjfffelq666im677TaesdioIV0rsGTJEp4U8atf/crYJbQ4IlvWQmTLhJhJtpDw3lMJB6GxvPrqq7T//vtzkVhEWT7//HPjJmUwxHXooYeyINxzzz3GbqEf8IcFFge32Wz04osvGrsFkyCyZS1EtkyIGWQrHo9zCYe7777b2CUMM5999hn9+c9/5gr0etRqKGsjYgIA1mQ85phjZBmlXkgmkyyyKLwrQ+TWQGTLWohsmZBWl61EIkEOh4NLIwiNAwVRMcsSw1a77bYb/e1vfzNuMmhQEuOSSy7h4quYzfmf//zHuIklQXmR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d96ZXnvtNWNX81MhW83IAw88QMcccwz997//NXYJwpAR2bIWIlsmRGRrZMCsRdTqQs2uRnP22WfTeeedR8uXLzd2NS3ZeJDcSoxSVWGxkefjjz+mww47TGYeCsOKyJa1ENkyISJbIweKoqIa/UgIF4Y1R40aZWwWBgg+vx122MHYLAh1RWTLWohsmRCRrZGnUCjwRRuLXmMdxkay9dZbj4jstTLfffcdTZ8+nfbZZx9jlyAMCyJb1kJky4SIbDUX5557LhchffTRR41dwwZKQ5x55pn0y1/+0tglVADJwszSK6+8kiccCEKjENmyFiJbJkRkq/mAcK2++uqUSqWMXcPKRRddxENif/7zn41dlueaa66hDTfcUC56wogg3ztrIbJlQkS2mhdEnFZddVXaY489jF3DzpgxY+iAAw6g2bNnG7ssw6xZs+jXv/417bfffsYuQWgoIlvWQmTLhIhstQannHIK7bvvvpROV69f1gjOP/98WmeddUhRlIbnlDWaqVOn0iabbEJTpkwxdgnCiNEKsoUJP/gDzW638/3HH3/cuIlQIyJbJkRkq3WAcK2yyiojIlx///vf6YwzzuCinQsXLjR2tzRffPEFJRIJ2mqrrWjixImUTKLIqiA0D80uW3vvvTcdddRR1NnZyT9LRx55JK2//vr03nvvGTcVakBky4SIbLUe7777Lq288sp00EEHGbsaAgp3QrjwCxXHgcT6u+++27hZU4LI3O23387V9DFEe+yxx9Knn35q3EwQmopmla2HH36Y2tp6VwOsnvGXv/zF2Cz0Q+9nVGhZRLZam0mTJnE0ZtGiRcauhvPCCy/QddddRw6Hg4fixo4dSxdccAE9+eST9Pbbbxs3H1YwmxMJ/1gTco011qD999+f5s2bR++//75xU0FoeppVtpxOJ3377bfG5ipQXmak/jBsVUS2TIjIVutz+OGH00orrdQUwqXz8ssv0yOPPEKhUIhLWay33nosYEcffTRdeOGF9Nhjj3F07PXXX6d8Pk/F4sAqw6NyO94vZk5edtllLFYHH3wwrbvuuvyX9ujRo+mss86i66+/nrcVhFamGWXro48+4kkk/XHzzTf3Gf0SuiNny4SIbJkHyAdyujA01mpAiN566y169tlnWaAw1IcljS6//HK66qqr+GKDmZFz586l+fPn8/qO+GUvCFagGWUrEokY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rcWB7T1wafqFF15I/QCXX355CIVCsMcee8CkSZNotRWuyhoNWLYYhmF6G5atYMGy1UQ3Zas/sKktJs+ffPLJcMABB1A19+985zsgyzLVBepWVIxli2EYprdh2QoWLFtNjLZsdcprr71G/emwP+HOO+8MSy65JPUmxMrd5557Ljz00EPw+uuvuw+rw7LFMAzT27BsBQuWrSZ6RbY6AetyYYV5nKY0TZNqd2EZipVWWglWXnlluOiiiyivDKczGYZhmN6CZStYsGw14SXZ6g+cpsQK9Zi0j9OUW221FQnYUKJiDMMwzOjDshUsWLaa+P73vw+HH364e9hzLG4aEdsApdNp+P3vfw+HHHIIVbevRcX23XdfjooxQ2bu3Llwzz33UMNqwzDqkdbabbXVVqNG1qIowrbbbgs/+MEPaOEItqfC6fLjjjsOTj/9dJg4cWLLDfu34e8o9j7EdlYbbbRRvSF282255ZaDvffeG4499li46aabqIciw/QyLFvBgmWrylJLLUVRIcyVeuWVV9ybPcXiZKtT3n777bYVlfjBFolE4OCDD6YG2XfccQd90DL+ZNGiRfB///d/cNVVV4GqqhQlrUVLFUWplzvBHoa9BK70xQbYuPIXVwBvvPHG9LpxQco+++wDU6dOdR/CMGMKy1awYNlykCSp5TFePf/85z9vGfMS3ZKtTnnxxRfh7rvvphWV22+/PckYfrDh9xF7QGJz4X//+99QKBTchzI9RLlcpjZUq666Kv38UKawdycu2PAbmPN42223kSyuscYa1HJru+22g3g8Dl9++aV7d4bpOixbwYJlywGn0ZrByBa++XqVsZat/sC8sL/85S80rbTLLrvUm3B/97vfpWkjbMJ9//3305QmM75ks1nK6cM6cJtvvjl9CDz//PPu3XwNToGiaK6//vqw9NJLk2gG7XvAjC0sW8Ei0LKFkRbMUxoIjMx4kV6QrU7A6MKTTz5JEYYTTzwRtthiC/p5oJRhcv/+++8Pl156KU3vMt3h66+/hvvuu4+kF5un/+pXv4I5c+a4d2OgsmAGc8swxWCnnXaix/g7yzDdgGUrWARatjCiVSqV3MN1MMJ14403uod7Hq/IVqfgtA4KAk7x1BL6sfYYRsswaoaLGjCKNnPmTJg/f7778MBz8803w1577QUrrrgiJY9//PHH7l2YDnj11Vfh8ssv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>

[image8]: <data:image/png;base64,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REOvz68qN4OSY4abTyPytRj++/J18ApCvDSPqfvXGTVJ939WFv8561+rTl4qT2U/2A2FdP/9HD+42ZSc7HHGafrFL36hboIB0St1FXoHwlabELZq3nzzTVqyZIks6PvII4/QH//4R7rtttskhFx++eX061//ms4991yKx+PSdf+4446TbuXcfX/vvfemHXfcUUZjeLmj9ddfX4IPB6BGGFphhRVolVVWoXXWWUdGerhbP98O49tifI7999+fvv3tb8vtHX469Ec/+lFz9ObKK6+kTCYjI0v33XcfPfroo/SPf/xDbsXxdUPNdtttp26aE0Z9ZCuI3njjDRkJhcE01boKgw9hq02dDFs8F+df//qXzMXh204cDG666Sa5zZNKpeTWE4+o8MgKj7DwraeDDjqIbNumPfbYQ9Zt3HrrrWV9u/nz59Oqq64qywipt5waHyuuuKKM8vDoD48C8ejN0UcfLefm21KN0Rt+72uuuUYWKb7nnnskSBWLRfr73/9OL774oow8Qf/jv985Vyn6P5OGzJ0KohNOOEHdBAMEYQtUCFttmmrY+s1vfkNbbbWVzMXhUZqlzcVZaaWVmre3eNSHRx8WLlwoc3G+9KUv0cEHHywjOnz7jG+j8e20X/3qV3J77frrr5fbbby47UMPPSSdqZ999ll69dVX1csaA0UheN5++23KZrPqZpgl77//vtQEGFyoq6BC2GrTVMNWv0BRCCaey8ZzzmD2bbHFFnT66aerm2GAoK6CCmGrTQhbMEh4/hyPlsLsW3755WUaAQwu1FVQIWy1aaphi+c79cOkbBSF4OKHF9577z11c1eUUlEKhy35vFrKSKPTzplC49FqiXKzvXBji5///Of01ltvqZthwKCuggphq01TDVvspZdeoquvvlpaEfATdzwR/U9/+pN62JxCUQg2fvpzNrSGLW7JEDK4hUOtHQMvocPL+dTCUpnMqO1/HZV+V+VcnEK6SWZEJz0al5YNvAyPZdtk+cfVYlMjbFVIGxomw+WWE7z0ToQK5bK0hXAt/xx2koqZGFn1vl8x1yPPdcjjhlyzYNddd6VoNKpuhgGEugoqhK02TSdstXruueekJxQ/PcgtDLj7dy9AUQg2brDazoMUMzUqbHHAShb5k+YSOtx5npfQ4W38NXeCt9Ml+drwchLEbF0jy99WLXJT1CoVk7VA1jqy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oa6CCmEr4FAUYDbdcMMNtMMOO5Bt98hEdOU2YrWYkqVvKvkklasVysdN6TQ/ErYqlIvxEjj+vphZXwqnIvu44X1r2CrIcfUTz7G77rqLPvShD9GnP/1pdRd0AeoqqBC2Ag5FAebCYYcdRptuuin94he/UHfNnjFPI0bI0DUq5ZJkR0zidQpLGZd0O0n5cpXCmkmeY5Hl1JfPqZb8z2tztjKytFBZ1ni04pn66ROytE9yZHHFWccjiRtvvDGtuuqq9Kc//UndDV2CugoqhK2AQ1GAucZPLa622mp04okn0gMPPKDu7hEjtxF73eLFi2U9R/6+Pv744+pumAWoq6BC2Ao4FAXoBe+99x65rktrrrkmnXTSSb0Xusp5GtYsucXYqxohi+fG3XnnnepumEWoq6BC2Ao4FAXoRe+//z4tWrSIFi5cKI1STzjhBHrrrbfUwwLr5ptvpuOOO07mYe24444STvl7Br0BdRVUCFsBh6IAve66666jI444QloVLFiwgK699lr1kEB44YUX6Pjjj5e5Zfx055FHHiktHKD3oK6CCmEr4FAUoB/df//9dO6550pDzqGhIVk4+eijj5an7p5//nn18L7B1/7LX/6SjjrqKFp33XXpE5/4BO277770xz/+UT0UehjqKqgQtgIORQH63WuvvSZPNR5yyCG04oorSvjadttt6YADDqArr7xSutn3Ir4t+tBDD0nfq/3331+eGGxcOy+fc99996kvgT6BugoqhK2AQ1GAICgWizJpnEfDvvnNb0rn9PXXX5/mzZtHq6++uixXY5om7bXXXjJR/+STT6ZTTz11Sh88sX+//faTc2yxxRZyfg5P/B7bb7+9vC930r/88ssxgX3Aoa6CCmEr4FAUAAA6C3UVVAhbAYeiAADQWairoELYCjgUBQCAzkJdBRXCVsChKAAAdBbqKqgQtgIORQEAoLNQV0GFsBVwKAoAAJ2FugoqhK2AQ1EAAOgs1FVQIWwFHIoCAEBnoa6CCmEr4FAUAAA6C3UVVAhbAYeiAADQWairoELYCjgUBQCAzkJdBRXCVsChKAAAdBbqKqgQtgLuhBNOoE1ufo2WLFmCjw598PeTP15++eUx+/AxvQ/+Xr7yyiv4We3gB39P8bPa+Q/+fnJdBWiFsBVw+AXW+Q/8Auv8R6VSwc9qhz/4e4qf1c5/8PeTf1YBWiFsBRgXBegefH87B9/Lzmv9nuL723n4nkIrhC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOgihC0AAACALkLYAgAAAOii/x85uLrIiPJnjwAAAABJRU5ErkJggg==>
