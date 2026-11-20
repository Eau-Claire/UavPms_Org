













**CAPSTONE PROJECT REPORT**

**Report 3 – Software Requirements Specification**











**Table of Contents**

**Table of Figures**

Figure 1. UAV-PMS system context diagram	8

Figure 2. UAV-PMS unified use case diagram	10

Figure 3. UAV-PMS screens flow	19

Figure 4. UAV-PMS conceptual entity relationship diagram…………………………………………..26




**Table of Tables**

[Table 1. UAV-PMS actors	8](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.7plxvrvxo4ia)

[Table 2. Use case descriptions	15](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.xyfbc6ipwefc)

[Table 3. Screen descriptions	20](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.kk5lscw38xqo)

[Table 4. Screen authorization matrix	23](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.29zvf1jr41ia)

[Table 5. Non-screen functions	24](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.oeid8lwhacz2)

[Table 6. Entity descriptions	26](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.n0ra6v8dgsfw)

[Table 7. Create readiness assessment data elements and input validation	28](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.b3e741htjeyp)

[Table 8. Evaluation engine criteria matrix	31](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.ywptyfu7r0em)

[Table 9. Personnel eligibility outcome codes	34](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.zgbjx94nm8g6)

[Table 10. Subsystem checklist and metric verification	36](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.la6sks4xly9p)

[Table 11. Create inspection mission data elements and input validation	42](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.azjvoqu3w7tn)

[Table 12. Assign personnel data elements and input validation	45](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.13ae3ojn6w88)

[Table 13. Assign UAV data elements and input validation	48](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.cu5qrjukdp47)

[Table 14. Preview mission targets data elements and input validation	51](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.ea2ywyaelpi6)

[Table 15. Respond to mission assignment data elements and input validation	54](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.43dqsmm8vm5m)

[Table 16. Allowed lifecycle transitions and operational impacts	57](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.gme5f9tj28k9)

[Table 17. Check in at mission site data elements and input validation	60](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.2x041xfenowf)

[Table 18. Record asset inspection progress data elements and input validation	63](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.sip9sb1yn4qg)

[Table 19. Handle critical alert data elements and input validation	66](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.8059oh6jevza)

[Table 20. Finish field inspection data elements and input validation	68](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.hqem1qv1aflp)

[Table 21. Submit inspection data elements and input validation	71](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.yo8c26rhpn45)

[Table 22. AI model defect classification and default severity mapping	73](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.6qwi9h6srmkb)

[Table 23. Review AI detections data elements and input validation	77](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.kbiaagakxaj5)

[Table 24. View defect report and export data elements and input validation	81](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.qh7j8mjvu4mu)

[Table 25. Manage maintenance ticket data elements and input validation	83](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.jjexhdswvtdo)

[Table 26. Software interfaces	87](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.760sp2el6bnf)

[Table 27. Hardware interfaces of the team-built UAV	87](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.760sp2el6bnf)

[Table 28. Usability requirements	88](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.3jwffvs7hqr2)

[Table 29. Reliability requirements	88](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.djnyyx5tvbhx)

[Table 30. Performance requirements	89](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.sa6pak2uvb9q)

[Table 31. Security and privacy requirements	89](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.52xi80mng84y)

[Table 32. Maintainability, observability and compatibility requirements	90](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.6j2pqbx5xr27)

[Table 33. AI model quality requirements	90](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.6m0ovac09c6r)

[Table 34. Business rules	91](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.wnirbyj9wbg2)

[Table 35. Common requirements	94](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.gqehtjftzqsd)

[Table 36. Application messages	95](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.lersq2io4kd0)

[Table 37. Configuration parameters and default values	97](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.fyh8n7wgf6l8)

[Table 38. Readiness assessment states	98](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.byr79mfp5ukf)

[Table 39. Mission states	98](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.byr79mfp5ukf)

[Table 40. Assignment states	98](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.byr79mfp5ukf)

[Table 41. AI analysis job states	98](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.byr79mfp5ukf)

[Table 42. Detection review states	98](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.byr79mfp5ukf)

[***Table 43.***](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.byr79mfp5ukf) [**Maintenance ticket states**](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.byr79mfp5ukf)

[***Table 44.***](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.byr79mfp5ukf) [**Emergency alert states**](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.byr79mfp5ukf)

[***Table 45.***](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.kk1k6qv04b6y) [**Canonical defect categories and default severity	99**](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.kk1k6qv04b6y)

[***Table 46.***](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.rtmk0yzh79be) [**Traceability from main flow to screens and data	100**](https://docs.google.com/document/d/1skTm2hIHmmGhzZcgsawQtQVzuw2G9ukjNX1nNYHl9l0/edit#heading=h.rtmk0yzh79be)





























**ABBREVIATIONS**

A list of abbreviations and acronyms used in the thesis, with their full forms.

| **Acronym** | **Meaning** |
| :---: | :---: |
| AI | Artificial Intelligence |
| AMQP | Advanced Message Queuing Protocol (used by RabbitMQ) |
| API | Application Programming Interface |
| BR | Business Rule identifier (Section 5.1) |
| CF | Common Function identifier (Section 3.2) |
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
| OTP | One-Time Password |
| RPO / RTO | Recovery Point Objective / Recovery Time Objective |
| SCR | Screen identifier (Section 3.1) |
| SRS | Software Requirements Specification |
| TLS | Transport Layer Security |
| UAV | Unmanned Aerial Vehicle |
| UAV-PMS | Integrated UAV-AI Inspection and Maintenance Management System for Electrical Infrastructure |
| UC | Use Case identifier |
| UTC | Coordinated Universal Time |
| WBS | Work Breakdown Structure (Report 2, Table 1) |
| WKT | Well-Known Text (geometry format) |
| YOLO | You Only Look Once |


# I. Record of Changes

| **Date** | **A\*M, D** | **In charge** | **Change Description** |
| :---: | :---: | :---: | :---: |
| 24/09/2026 | A | SE193418 - Nguyễn Quốc Khánh | Created SRS draft v0.1 from Report 1, MF01–MF04, supplied use-case diagrams, and verified backend behavior. |
| 07/10/2026 | M | SE193338 - Nguyễn Nhật An | Unified actor names; redrew the context, use-case, screen-flow and ER diagrams; renumbered use cases and aligned them with the Report 2 WBS; added Common Functions. |
| 07/10/2026 | A | SE193338 - Nguyễn Nhật An | Added screen list, screen authorization, non-screen functions, entity descriptions and detailed use-case specifications for CF and MF01–MF04. |
| 07/10/2026 | M | SE193338 - Nguyễn Nhật An | Revised NFRs to measurable targets, aligned AI rules with the seven-class dataset and the YOLO11s/SDI/SCI/StructDETR models, reorganized business rules and messages, and added traceability and state tables. |
| 14/10/2026 | M | SE193338 - Nguyễn Nhật An | v0.3 – Aligned the SRS with the implemented backend (commit 124d764): microservice architecture, six system roles, email/OTP login, Region–Substation–Line–Tower–Asset hierarchy, UAV status model and flight-controller pairing, automatic assessment evaluation with GIS suitability, combined create-and-dispatch of missions, mission/assignment/booking states, MQTT topics, media ingestion with EXIF, Vision Bridge, RabbitMQ AI pipeline with gRPC severity evaluation, analyst review with automatic maintenance tickets, reports and system settings. |
| 14/10/2026 | A | SE193338 - Nguyễn Nhật An | v0.3 – Added UC-CF-08 Manage System Settings, UC-MF04-08 Manage Maintenance Ticket, screens SCR-25 – SCR-27, booking/alert/ticket state tables and the backend error code of every message. |
| 14/10/2026 | D | SE193338 - Nguyễn Nhật An | v0.3 – Removed requirements that conflict with the implemented design: username login and account lockout, Needs Review assessment result and override, automatic weather thresholds, flight altitude/speed parameters, Field Completed state, assignment Expired state, automatic confidence-based acceptance/discarding, Edge-Device metric import and the technical-inspection expiry job. |
|  |  |  |  |
|  |  |  |  |
|  |  |  |  |
|  |  |  |  |

\*A - Added M - Modified D - Deleted

## II. Software Requirements Specification

### 1. Product Overview

UAV-PMS is a web-based system that supports UAV-assisted inspection of electrical infrastructure as an independently developed academic capstone prototype. The system manages an inspection from the pre-mission readiness assessment, through mission assignment and field execution, to AI-based defect detection, analyst review and maintenance follow-up.

The product covers four main flows and a set of common functions:

- MF01 – Pre-Mission Feasibility and Resource Readiness: defines the proposed inspection scope (region, asset components and optional boundary) and time window, then automatically evaluates site feasibility, personnel availability and eligibility, UAV operational availability, technical health and GIS scope suitability, and decides whether the assessment is Ready for a mission.
- MF02 – Create and Assign Inspection Mission: supports the standard workflow from a Ready assessment through mission creation, eligible personnel/UAV assignment, dispatch, acceptance and lifecycle changes. Urgent physical deployment may occur before the administrative mission is persisted or approved; the system must not fabricate an approved mission or assignment for this exception. Emergency observations are recorded independently and reconciled after the fact.
- MF03 – Mission Execution and Field Inspection: supports check-in, mission lifecycle, media uploads, flight logs, field incidents and server-side AI analysis results. 
- MF04 – AI Inspection and Defect Detection: queues uploaded inspection media for server-side analysis, stores class, bounding box, confidence, model and dataset version, applies configurable preliminary severity rules, supports analyst confirmation/correction, reanalysis, maintenance follow-up and exports. An unreviewed model prediction is not an automatically certified physical defect.
- Common Functions (CF): login with OTP, user accounts and geographic scope, infrastructure assets, UAV inventory and flight-controller pairing, notifications, the audit log and system settings.

The research foundation follows Report 2: a harmonized six-class UAV RGB dataset, quality curation and a controlled Raw-versus-Curated benchmark across YOLO11n/m, YOLO26n/m and RT-DETR-R50/R101. Deployment candidates are selected based on measured accuracy, latency, memory and hardware compatibility, not assumed research outcomes.

- **Server AI mode: one selected, validated detector runs exclusively on the central AI server. The UAV captures and transfers inspection images; no onboard AI inference is performed.**

The backend is built as ASP.NET Core 9 microservices behind an Ocelot API gateway: Identity Service (authentication, users), Operations Service (assets, UAVs, assessments, missions, field execution, media, alerts, reports, maintenance, audit, settings), AI Inspection Service (AI analysis orchestration), Notification Service (in-app/e-mail notifications, SignalR hub, background jobs) and the internal Inspection Evaluation Service (gRPC severity evaluation). The services share one PostgreSQL/PostGIS database and communicate asynchronously through RabbitMQ.

The system does not control the UAV in flight, generate flight waypoints, perform physical repair, certify UAV hardware, or replace aviation licensing and safety procedures. Flight control and flight-condition decisions remain with the flight controller and the pilot.


***Figure 1.*** *UAV-PMS system context diagram.*

### 2. User Requirements

#### 2.1 Actors

***Table 1.* *UAV-PMS actors.*

| **\#** | **Actor** | **Description** |
| :---: | :---: | :---: |
| 1 | System Administrator | Manages user accounts, roles, geographic scopes and system settings; has access to every function and data scope; reviews the audit log. |
| 2 | Mission Manager | Maintains infrastructure assets and the UAV inventory, creates readiness assessments, creates and dispatches missions, assigns personnel and UAVs, changes missions, monitors field execution, requests reanalysis, manages maintenance tickets and approves reports. Limited to the regions in their geographic scope. |
| 3 | Inspector | Field operator who also acts as the UAV pilot. Responds to assignments, checks in, starts and completes the mission, uploads media and flight logs and reports field incidents. |
| 4 | Analyst | Reviews AI detections (confirm or reject), reviews and escalates emergency alerts, requests reanalysis and prepares reports. |
| 5 | UAV Technician | Records UAV technical inspections, pairs flight controllers with UAVs, maintains UAV records and executes maintenance tickets (status, proof, materials). |

#### 2.2 Use Cases

##### 2.2.1 Diagram(s)


***Figure 2.*** *UAV-PMS unified use case diagram.*

Figure 2 presents the unified Use Case diagram of UAV-PMS, consolidating all system functions, user interactions, and external integrations across the four main inspection flows (MF01–MF04) and common platform functions (CF) into a single comprehensive interaction model. Primary human actors (System Administrator, Mission Manager, Inspector/Pilot, UAV Technician, and Analyst) and secondary system actors (Cloud AI Service, Map Service, and Notification Provider) interact with the system boundary according to their assigned roles and operational duties.


##### 2.2.2 Descriptions

Table 2 describes each use case shown in Figure 2. The WBS column refers to Report 2. Training, annotation curation and Raw/Curated model benchmarking are research activities rather than end-user use cases; inference, review, emergency routing and traceability are system requirements. Emergency field deployment before mission approval is an exception to the normal mission lifecycle.

***Table 2.*** *Use case descriptions.*

| **ID** | **Use Case** | **Primary Actor(s)** | **Secondary Actor(s)** | **Description** | **WBS** |
| :---: | :---: | :---: | :---: | :---: | :---: |
| UC-CF-01 | Log In | User | Notification Provider | Authenticate with e-mail and password; on an untrusted device verify an e-mailed OTP; open the role-based dashboard. | CF-01 |
| UC-CF-02 | Manage User Accounts | System Administrator | – | Create, update and suspend accounts, assign one or more roles and the geographic scope. | CF-02 |
| UC-CF-03 | Manage Infrastructure Assets | System Administrator, Mission Manager | Map Service | Maintain regions, towers with coordinates; import towers from Excel. | CF-03 |
| UC-CF-04 | Manage UAV Inventory | Mission Manager, UAV Technician, System Administrator | – | Maintain UAV records, operational status and technical health; pair a flight controller (Betaflight UID) with a UAV. | CF-04 |
| UC-CF-05 | View Notifications | User | – | View and mark as read in-app notifications, received in real time, and open the linked record. | CF-05 |
| UC-CF-06 | View Audit Log | System Administrator, Mission Manager | – | Search audit records of data changes and material actions. | CF-05 |
| UC-CF-07 | Manage System Settings | System Administrator | – | View and change the configurable thresholds and parameters (Table 37). | CF-05 |
| UC-MF01-01 | Create Readiness Assessment | Mission Manager | Map Service | Define the region, planned window, asset components and optional boundary; the assessment is evaluated immediately (includes UC-MF01-02). | MF01-01 |
| UC-MF01-02 | Evaluate Pre-Mission Readiness | – (included) | – | Included by UC-MF01-01. Run the site-feasibility, personnel and UAV checks and obtain the result Ready or Not Ready; re-evaluate or cancel an assessment (includes UC-MF01-03, UC-MF01-04). | MF01-05, MF01-06 |
| UC-MF01-03 | Check Personnel Availability and Eligibility | – (included) | – | Included by UC-MF01-02. Check field personnel who are active, in geographic scope and not booked in the window. | MF01-02 |
| UC-MF01-04 | Check UAV Availability and Technical Readiness | – (included) | – | Included by UC-MF01-02. Check UAVs that are operationally available, not booked, technically healthy and suitable for the scope. | MF01-03 |
| UC-MF01-05 | Record UAV Technical Inspection | UAV Technician | – | Record the basic checklist or detailed metrics, verify the paired flight controller and obtain Passed or Failed with a technical health. | MF01-04 |
| UC-MF01-06 | View Readiness Assessment Result | Mission Manager | – | View the overall result with the status and reason of each component. | MF01-06 |
| UC-MF02-01 | Create Inspection Mission | Mission Manager | – | Create a mission from a Ready assessment with title, priority, objective, personnel and UAVs (includes UC-MF02-02, UC-MF02-03). | MF02-01 – MF02-03 |
| UC-MF02-02 | Assign Personnel to Mission | Mission Manager | – | Included by UC-MF02-01. Assign eligible personnel with a mission role and required flag; add, remove or reassign later. | MF02-04 |
| UC-MF02-03 | Assign UAV to Mission | Mission Manager | – | Included by UC-MF02-01. Assign an available, healthy and unbooked UAV; change it later. | MF02-05 |
| UC-MF02-04 | Dispatch Mission | Mission Manager | Notification Provider | Dispatch a created mission, reserve resources with bookings, set the mission to Pending Acceptance and notify the assignees. | MF02-06 |
| UC-MF02-05 | Preview Mission Targets on Map | Mission Manager, Inspector, Analyst, UAV Technician | Map Service | Display the mission targets in sequence, their towers and the scope boundary on the map. | MF02-07 |
| UC-MF02-06 | View Assigned Mission | Inspector, Analyst, UAV Technician | – | View scope, schedule, deadline, instructions and assigned UAV of own assigned missions. | MF02-08 |
| UC-MF02-07 | Respond to Mission Assignment | Inspector, Analyst, UAV Technician | – | Accept, or postpone with a reason, before the confirmation deadline. | MF02-08 |
| UC-MF02-08 | Monitor Assignment Status | Mission Manager | – | Track responses and confirmation deadlines of assigned team members, send reminders and messages. | MF02-06 |
| UC-MF02-09 | Change Dispatched Mission | Mission Manager | Notification Provider | Cancel, suspend, resume, postpone, reschedule, reassign personnel, change the UAV or change priority with re-validation (extends UC-MF02-04). | MF02-09 |
| UC-MF03-01 | Check In at Mission Site | Inspector | – | Record arrival at the site with server time and GPS location. | MF03-01 |
| UC-MF03-03 | Start Mission Execution | Inspector | – | Start a Ready mission; the UAV becomes Flying and the mission In Progress. | MF03-02 |
| UC-MF03-04 | Record Asset Inspection Progress | Inspector | – | Mark each mission target as Inspected or Skipped with operational notes and reasons. | MF03-04 |
| UC-MF03-05 | Monitor Field Execution | Mission Manager | Map Service | View live UAV state and telemetry, mission lifecycle events, targets and alerts. | MF03-03 |
| UC-MF03-06 | Report Critical Anomaly | Inspector | Notification Provider | Create an emergency alert from a field incident report or manual observation during asset inspection (extends UC-MF03-04). | MF03-06 |
| UC-MF03-07 | Handle Critical Alert | Analyst, Mission Manager | – | Review an emergency alert (confirm or dismiss) or escalate it to a responsible manager. | MF03-06 |
| UC-MF03-08 | Finish Field Inspection | Inspector | – | Submit the flight log and complete the mission; the UAV becomes Idle and bookings are released. | MF03-07 |
| UC-MF04-01 | Submit Inspection Media | Inspector | – | Upload media for a mission target; each valid file is queued for analysis automatically (includes UC-MF04-02). | MF04-01, MF04-02 |
| UC-MF04-02 | Process AI Analysis | – (included) | Cloud AI Service | Included by UC-MF04-01 and UC-MF04-06. Publish analysis request to Cloud AI, receive detections and evaluate defects. | MF04-03 |
| UC-MF04-03 | Monitor AI Processing Status | Inspector, Analyst | – | Track analysis requests in Pending, Processing, Completed or Failed state. | MF04-03 |
| UC-MF04-04 | View AI Analysis Results | Analyst | – | View detections with category, bounding box, confidence, severity, risk and review status on the image. | MF04-08 |
| UC-MF04-05 | Review AI Detections | Analyst | – | Confirm, reject or correct detections; a confirmed detection updates the asset health and creates a maintenance ticket. | MF04-09, MF04-11 |
| UC-MF04-06 | Request Media Reanalysis | Analyst, Mission Manager | – | Request a new analysis of existing media with model and analysis type (extends UC-MF04-05, includes UC-MF04-02). | MF04-10 |
| UC-MF04-07 | View Defect Report & Export | Mission Manager, Analyst | – | View defect reports, statistics, and export to PDF or Excel format. | MF04-12 |
| UC-MF04-08 | Manage Maintenance Ticket | Mission Manager, UAV Technician | – | Create, assign, execute (status, proof, materials) and close maintenance tickets for confirmed defects. | MF04-11 |

### 3. Functional Requirements

#### 3.1 System Functional Overview

##### 3.1.1 Screens Flow

Figure 3 shows the main screens of the web client and the navigation between them. After login, every user lands on SCR-02 Dashboard, which only shows the menu entries allowed by ***Table 4.*** Rectangles are full screens, rectangles divided into rows are screens with tabs, and ellipses are pop-up dialogs. Horizontal functional tracks organize the screens by operational domain, with a yellow background distinguishing pre-login flows from post-login authorized screens.


***Figure 3.*** *UAV-PMS screens flow.*

##### 3.1.2 Screen Descriptions

Table 3 describes each screen in Figure 3 and the use cases it serves. 

***Table 3.*** *Screen descriptions.*

| **\#** | **Feature** | **Screen** | **Description** | **Use Case(s)** |
| :---: | :---: | :---: | :---: | :---: |
| SCR-01 | Common | Login | E-mail and password form; shows error messages and opens SCR-27 when an OTP is required. | UC-CF-01 |
| SCR-02 | Common | Dashboard | Role-based home: mission and assessment counts by status, active alerts, recent defects, defect statistics and shortcuts to allowed screens. | UC-CF-01 |
| SCR-03 | Common | Notification Center | List of in-app notifications with read status and link to the related record; unread count updated through SignalR. | UC-CF-05 |
| SCR-04 | Common | User Management | Searchable list of accounts with a create/edit form for full name, e-mail, phone, roles, status and geographic scope. | UC-CF-02 |
| SCR-05 | Common | Infrastructure Assets | List and map of regions, towers; create/edit forms with coordinates; Excel tower import pop-up. | UC-CF-03 |
| SCR-06 | Common | UAV Inventory | List and detail of UAVs with status, operational status, technical health, live state, technical-inspection history and flight-controller pairing (tabs: Detail, Technical Inspections, Flight Controller). | UC-CF-04 |
| SCR-07 | MF01 | Technical Inspection Form | Basic checklist (frame, propellers, motors, camera payload, battery, GPS satellites, RF link, flight controller) or detailed metric entry, with the flight-controller UID. | UC-MF01-05 |
| SCR-08 | MF01 | Readiness Assessment List | Assessments with code, region, window, status and validity; filter by status; opens SCR-08A form to create a new assessment. | UC-MF01-01, UC-MF01-06 |
| SCR-09 | MF01 | Readiness Assessment Detail | Workspace with tabs: Scope (assets, boundary, map), Personnel (candidates with eligibility and reason), UAV (candidates with availability, technical health and GIS suitability), Result (components and reasons); Evaluate, Cancel (pop-up) and Create Mission buttons. | UC-MF01-01 – UC-MF01-04, UC-MF01-06 |
| SCR-10 | MF02 | Mission List | Missions with code, status, priority, planned window and assignment progress; search, filter by status and priority. | UC-MF02-08 |
| SCR-11 | MF02 | Create Mission | Form opened from a Ready assessment: title, description, priority, objective, priority defects, emergency reason, confirmation deadline, instructions, personnel (role, required) and UAVs from the eligible candidates; resource-conflict check; Create and Dispatch button. | UC-MF02-01 – UC-MF02-04 |
| SCR-12 | MF02 | Mission Detail | Tabs: Overview & Telemetry, Targets Map, Assignments (responses and deadline), Communications, Timeline (activities and audit), Detections. | UC-MF02-05, UC-MF02-08, UC-MF04-04 |
| SCR-13 | MF02 | Change Mission (pop-up) | Choose cancel, suspend, resume, postpone, reschedule, reassign personnel, change UAV or change priority; enter reason and new values. | UC-MF02-09 |
| SCR-14 | MF02 | My Missions | Assigned missions with status, planned window and confirmation deadline for field operator and assigned team members. | UC-MF02-06 |
| SCR-15 | MF02 | Respond to Assignment (pop-up) | Accept, or postpone with reason, before confirmation deadline. | UC-MF02-07 |
| SCR-16 | MF03 | Field Execution Console | Workspace with tabs: Check-in, Targets (per-asset progress), Media (upload and AI status), Flight Log, Incidents; Start and Complete buttons. | UC-MF03-01 – UC-MF03-05, UC-MF03-07, UC-MF03-08 |
| SCR-17 | MF03 | Live Monitoring | Map with live UAV position, status, battery, telemetry and last-seen time, mission targets, lifecycle events and alert list. | UC-MF03-05 |
| SCR-18 | MF03 | Emergency Alert (pop-up) | Alert image, category, confidence, asset, mission, priority and time; review (confirm/dismiss) and escalate. | UC-MF03-06, UC-MF03-07 |
| SCR-19 | MF04 | Inspection Data and AI Jobs | Upload area with validation result per file and the analysis requests of the mission with status. | UC-MF04-01, UC-MF04-03 |
| SCR-20 | MF04 | AI Result Viewer | Image with bounding boxes, category, confidence, severity, risk level and review status. | UC-MF04-04, UC-MF04-06 |
| SCR-21 | MF04 | Detection Review Queue | Detections waiting for review; confirm, reject or correct with notes and optional severity override. | UC-MF04-05 |
| SCR-22 | MF04 | Request Reanalysis (pop-up) | Analysis type, preferred model and notes for selected media. | UC-MF04-06 |
| SCR-23 | MF04 | Defect Reports | Report list and form (type, missions, content), submit/approve/reject, statistics, generate and download PDF or Excel. | UC-MF04-07 |
| SCR-24 | Common | Audit Log | Searchable audit records by text, table and action type. | UC-CF-06 |
| SCR-25 | MF04 | Maintenance Tickets | Ticket list and detail with status, priority, due date, technician, proofs and material logs; “My Tickets” view for technicians. | UC-MF04-08 |
| SCR-26 | Common | System Settings | Settings grouped by category (Auth, OTP, Drone, AI) with value, data type and description. | UC-CF-07 |
| SCR-27 | Common | OTP Verification (pop-up) | Six-digit OTP entry with resend (after cooldown) and “trust this device” option. | UC-CF-01 |

##### 3.1.3 Screen Authorization

Table 4 states which actor may open each screen and which actions they may perform. X means full access to the actions of that screen; V means view only; a blank cell means no access. Access is also limited by the user’s region and, for Inspectors, by their own assignments (BR-001). 

***Table 4.*** *Screen* *authorization* *matrix.*

| **Screen** | **System Administrator** | **Mission Manager** | **Inspector** | **Analyst** | **UAV Technician** |
| :---: | :---: | :---: | :---: | :---: | :---: |
| SCR-01 Login, SCR-02 Dashboard, SCR-03 Notification Center, SCR-27 OTP Verification | X | X | X | X | X |
| SCR-04 User Management | X | – | – | – | – |
| SCR-05 Infrastructure Assets | X | X | V | V | V |
| SCR-06 UAV Inventory | X | X | V | V | X (no delete) |
| SCR-07 Technical Inspection Form | X | X | V (latest) | – | X |
| SCR-08 Readiness Assessment List, SCR-09 Readiness Assessment Detail | X | X | – | – | – |
| SCR-10 Mission List | X | X | – | V | – |
| SCR-11 Create Mission, SCR-13 Change Mission | X | X | – | – | – |
| SCR-12 Mission Detail | X | X | V (own) | V | V (own) |
| SCR-14 My Missions, SCR-15 Respond to Assignment | V | V | X | X (own) | X (own) |
| SCR-16 Field Execution Console | X | X | X (own) | – | – |
| SCR-17 Live Monitoring | X | X | V (own) | V | – |
| SCR-18 Emergency Alert | V | V | X (trigger) | X | – |
| SCR-19 Inspection Data and AI Jobs | V | V | X (own) | V | – |
| SCR-20 AI Result Viewer | V | V | V (own) | V | – |
| SCR-21 Detection Review Queue | – | – | – | X | – |
| SCR-22 Request Reanalysis | – | X (own missions) | – | X | – |
| SCR-23 Defect Reports | X | X | – | X (create, submit) | – |
| SCR-24 Audit Log | X | V | – | – | – |
| SCR-25 Maintenance Tickets | X | X | V | V | X (own) |
| SCR-26 System Settings | X | V | – | – | – |

##### 3.1.4 Non-Screen Functions

Table 5 lists the functions that run without a user screen: background jobs, message consumers and integrations. Each one is part of a use case in Sections 3.2–3.5. 

***Table 5.*** *Non-screen functions.*

| **\#** | **Feature** | **System Function** | **Description** | **Use Case** |
| :---: | :---: | :---: | :---: | :---: |
| NS-01 | MF01 | Assessment Expiry Job | Background service of the Operations Service that runs every 2 minutes and sets Ready assessments whose validity has passed to Expired; the same expiry rule is also applied when an assessment is read, evaluated or used (BR-028). | UC-MF01-02 |
| NS-02 | MF02 | Mission Confirmation Overdue Job | Hangfire recurring job (every minute) that finds Pending Acceptance missions whose confirmation deadline has passed, notifies the manager (MISSION\_CONFIRMATION\_OVERDUE), writes an OVERDUE communication log and sends a real-time event (BR-040). | UC-MF02-08 |
| NS-03 | CF | Notification Dispatcher | RabbitMQ consumers that turn integration events (mission created, defect detected, notification push, AI status changed) into in-app notifications, and the Hangfire job push-notifications-sync (every minute) that e-mails unsent notifications through SendGrid and marks them pushed (BR-052). | UC-CF-05 |
| NS-04 | CF | Outbox Dispatcher | Publishes integration events written to the OutboxMessages table (for example media uploaded, mission created from assessment) to the RabbitMQ topic exchange every 2 seconds. | UC-MF02-04, UC-MF04-01 |
| NS-06 | MF04 | AI Request Publisher | Consumes the media-uploaded event, creates an analysis request and publishes it to the server, image or video request queue (BR-059). | UC-MF04-02 |
| NS-07 | MF04 | AI Result Consumer | Consumes AI results, validates them, evaluates each detection through NS-09, stores detections, retries transient errors and moves failed messages to the dead-letter queue (BR-060 – BR-064). | UC-MF04-02 |
| NS-08 | CF | Audit Logger | Records every insert, update and soft delete of audited entities with user, table, record, old and new values, IP address and user agent, plus explicit business actions (BR-007). | UC-CF-06 |
| NS-09 | MF04 | Inspection Evaluation (gRPC) | Internal gRPC service that calculates severity, risk level, priority score and the immediate-alert decision for a detection (BR-068). | UC-MF04-02 |
| NS-10 | CF | Real-time Relay | Consumes mission-lifecycle and assessment real-time events and pushes them to SignalR groups (user, role, mission, assessment). | UC-MF01-02, UC-MF03-05 |
| NS-11 | CF | Housekeeping Jobs | Daily-summary-job (daily summary to administrators and managers) and auto-cleanup-job (weekly removal of old temporary files and expired refresh tokens). | UC-CF-05, UC-CF-06 |

##### 3.1.5 Entity Relationship Diagram

Figure 4 presents the Conceptual Entity Relationship Diagram (Conceptual ERD) of UAV-PMS in crow’s foot notation. At this conceptual stage, the diagram focuses on high-level business entities and their semantic domain relationships across the inspection and maintenance lifecycle, omitting low-level relational keys and technical storage tables to ensure clarity. A bar (|) denotes exactly one, a circle (o) denotes zero, and a crow’s foot denotes many. Table 6 describes each conceptual entity.

***Figure 4. UAV-PMS conceptual entity relationship diagram.*

***Table 6.*** *Entity descriptions.*

| **\#** | **Entity** | **Description** |
| :---: | :---: | :---: |
| 1 | REGION | Geographic operational jurisdiction defining administrative boundaries and spatial coverage for infrastructure assets and inspection missions. |
| 2 | TOWER | Power grid transmission or distribution structure identified by code, line code, voltage level, and geographical coordinates. |
| 3 | USER | System user account (Administrator, Mission Manager, Inspector, Analyst, Technician) with role-based access control and assigned regional scope. |
| 4 | PRE\_MISSION\_ASSESSMENT | Pre-flight feasibility assessment evaluating site clearance, weather conditions, crew eligibility, and UAV operational availability. |
| 5 | UAV | Unmanned Aerial Vehicle hardware unit tracking model, serial number, technical health status, flight controller pairing, and flight readiness. |
| 6 | MISSION | Inspection mission defining operational objectives, scheduled window, target towers, assigned personnel, allocated UAV, and execution lifecycle. |
| 7 | INCIDENT\_REPORT | Field incident documentation recorded during flight operations regarding unexpected hazards, equipment anomalies, or weather disruptions. |
| 8 | INSPECTION\_MEDIA | High-resolution aerial photographs or video evidence captured during field inspection, geotagged and bound to specific mission targets. |
| 9 | AI\_ANALYSIS\_RUN | Cloud AI batch analysis execution processing inspection media to detect, localize, and classify power grid component defects. |
| 10 | DETECTED\_ANOMALY | Defect anomaly localized on a tower or component with bounding box, category, confidence score, risk severity, and analyst review status. |
| 11 | DEFECT\_CATEGORY | Standardized power-grid defect catalog (e.g., broken insulator, corrosion, flashover, bird nest, foreign object) with baseline severity. |
| 12 | EMERGENCY\_ALERT | Critical priority alert triggered by urgent field anomalies or high-severity safety hazards requiring immediate triage and supervisor escalation. |
| 13 | MAINTENANCE\_TICKET | Corrective maintenance work order generated from confirmed defects, tracking repair tasks, assigned technician, and execution proof. |
| 14 | REPORT | Comprehensive inspection defect summary and operational analytics report scoped by region and mission set, exportable to PDF or Excel. |

*Sections 3.2–3.5 specify each function in Table 2 and Table 5 using a uniform template: function trigger, function description, screen layout, and function details (data validation, business rules in Section 5.1, normal/alternative/exception flows, and application messages in Section 5.3).*

#### 3.2 MF01 – Pre-Mission Feasibility and Resource Readiness

The Pre-Mission Feasibility and Resource Readiness module (MF01) supports the assessment of operational feasibility and resource readiness for planned UAV-based inspections of electrical infrastructure. The module evaluates the proposed inspection scope, geographical constraints, and operational schedule, UAV operational status, and technical readiness. The assessment results provide a basis for determining mission feasibility and supporting subsequent mission planning and resource allocation.

##### 3.2.1 Create Readiness Assessment (UC-MF01-01)

- Function trigger:
    - Navigation Path: From the primary navigation sidebar, select Pre-Mission (/pre-mission) to open the assessment list (SCR-08), then click the “+ Create new assessment” button on the top-right toolbar.
    - Direct Route: /pre-mission/new (SCR-08 / SCR-09 Setup).
- Function description:
    - Actors / Roles: Primary: Mission Manager, System Administrator.
    - Purpose: Initialize a new pre-mission assessment docket by defining the operational geographical region, the scheduled inspection execution window, the target power grid assets (towers and spans), an optional custom GIS boundary polygon, and an idempotency key. Upon initial creation, the assessment is placed in the Evaluating state and automatically initiates algorithmic readiness evaluation.
    - Interface: Web Client
    - Data Processing: Validates that the planned time window is strictly in the future with end time after start time; verifies manager regional authorization; checks that all selected transmission towers are active and located within the declared region; parses GIS polygon boundaries into spatial geometry; generates an assessment record with status Evaluating; triggers the automated evaluation pipeline.

**Screen layout:**

    - Screen ID: SCR-08 / SCR-09: Create Readiness Assessment & Scope Setup Form
    - Route: /pre-mission/new
- (Screen mockup will be inserted here)
- Function Details:
    - Data Elements and Input Validation:

***Table 7.*** *Create readiness* *assessment* *data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| regionId | UUID | Dropdown Select | Required. Must belong to the manager's active geographic management scope (UserGeographicScopes). |
| plannedStart | ISO DateTime | Date/Time Picker | Required. Must be  ≥UtcNow+1 hour and strictly before plannedEnd (BR-017, BR-021). |
| plannedEnd | ISO DateTime | Date/Time Picker | Required. Must be strictly greater than plannedStart (BR-017). |
| assetIds | List | Multi-select / Map Click | Required. Non-empty list (N≥1); all assets must exist in the canonical database, have status Active or Operational, and reside inside the chosen region (BR-017, BR-018). |
| boundaryWkt | String (WKT) | Map Drawing Polygon | Optional. Valid Well-Known Text (WKT) Polygon representation (e.g., POLYGON). Must enclose or intersect the target towers (BR-011). |
| idempotencyKey | String (UUID) | Hidden / Client Header | Optional client-generated UUID to guard against duplicate network re-submissions. |

- Business Rules Applied:
    - BR-001: Access strictly governed by role and regional geographic scope.
    - BR-016: Only an authenticated Mission Manager authorized for the region may create an assessment.
    - BR-017: Assessment must contain ≥1 asset and a valid future time window.
    - BR-018: All assets in the scope must belong to the selected region.
    - BR-021: Planned window cannot be in the past.
- Normal Flow (Step-by-step):
    - Manager navigates to /pre-mission/new. The system loads the authorized regions and available power grid assets.
    - Manager selects the target Region. The asset list and map filter to display only towers within this region.
    - Manager selects the Planned Start and Planned End date-time.
    - Manager selects target assets either from the searchable data table or by clicking tower pins on the Leaflet map.
    - (Optional) Manager uses the spatial polygon tool to delineate an operational flight boundary.
    - Manager clicks "Initialize & Evaluate". The client generates an idempotency key and submits the payload.
    - Backend verifies authorization and inputs, creates the PreMissionAssessment record and associated PreMissionAssessmentAsset rows, audits the action as ASSESSMENT\_CREATED, transitions status to Evaluating, and automatically executes the multi-dimensional evaluation pipeline (UC-MF01-02).
    - The UI navigates directly to the Assessment Workspace (/pre-mission/:id), displaying the evaluation progress.
- Alternative & Exception Flows:
    - A1 (Idempotent Resubmission): If the client re-submits with the same idempotencyKey, backend detects the existing record and immediately returns the cached assessment without duplicating entities.
    - E1 (Invalid Planned Window): If plannedEnd <= plannedStart or plannedEnd <= UtcNow, system halts execution with INVALID\_PLANNED\_TIME (MSG-002: "{Start} must be earlier than {End}").
    - E2 (Asset Outside Region Scope): If any selected tower ID does not belong to the region, system rejects with ASSET\_OUTSIDE\_MANAGEMENT\_SCOPE (MSG-18: "Asset {code} does not belong to region {region}").
    - E3 (Site Feasibility Boundary Error): If the custom polygon excludes target assets, system returns SITE\_FEASIBILITY\_FAILED.

##### 3.2.2 Evaluate Pre-Mission Readiness (UC-MF01-02)

- Function trigger:
    - Automatic: Triggered immediately upon completion of assessment creation (UC-MF01-01).
    - Manual / Navigation Path: In the Assessment Workspace (/pre-mission/:id), click the "Re-evaluate" button located on the top header action toolbar.
- Function description:
    - Actors / Roles: Primary: Mission Manager, System Administrator. Secondary: Background Scheduler / System.
    - Purpose: Execute comprehensive, multi-criteria algorithmic verification of pre-mission readiness across four essential dimensions:
    - Site Feasibility: Geospatial envelope, tower line topology, and weather clearance.
    - Personnel Readiness: Availability, regional authorization, and schedule eligibility of field personnel (UC-MF01-03).
    - UAV Hardware Readiness: Fleet availability, battery capacity, range endurance, and GIS scope fit (UC-MF01-04).
    - Technical Airworthiness: Active, passed technical inspection status and flight controller bindings (UC-MF01-05).
    - Interface: Angular Assessment Workspace, displaying real-time component health cards, candidate breakdowns, and evaluation summaries.
    - Data Processing: Evaluates all criteria in parallel; computes component statuses (Passed, Failed, Warning, Incomplete); computes the overall status: Ready if all 4 components pass, or NotReady if any component fails; establishes the assessment validity deadline (ValidUntil); increments entity version; audits action as ASSESSMENT\_EVALUATED; broadcasts a real-time SignalR event to mission participants.

**Screen layout:**

    - Screen ID: SCR-09: Readiness Assessment Workspace - Overview & Component Header
    - Route: /pre-mission/:id
    - (Screen mockup will be inserted here)
- Function Details:
    - Evaluation Engine Criteria Matrix:

***Table 8.*** *Evaluation* *engine* *criteria matrix.*

| **Dimension** | **Component Code** | **Evaluated Conditions** | **Pass Criteria** | **Failure Code / Action** |
| :---: | :---: | :---: | :---: | :---: |
| Site Feasibility | Site | Asset topology within region boundary; environmental weather clearance (wind speed  ≤10 m/s, visibility ≥1 km, no precipitation). | ReadinessCheckStatus.Passed | SITE\_FEASIBILITY\_FAILED (BR-025) |
| Personnel Readiness | Personnel | At least 1 qualified Inspector/Pilot, 1 Analyst, and 1 Technician available without booking collision. | ≥1 Inspector and ≥1 Analyst and ≥1 Technician | PERSONNEL\_NOT\_READY (MSG-101) |
| UAV Hardware | Uav | At least 1 operational UAV available without calendar collision; Sufficient battery health and endurance for inspection corridor. | ≥1 DroneCandidate.IsEligible | UAV\_NOT\_READY (MSG-102) |
| Technical Health | Technical | Drone technical inspection status is Passed; inspection record within 72h validity; overall health ∈{Healthy,Warning}. | Passed and  ValidUntil > PlannedStart | TECHNICAL\_HEALTH\_CRITICAL (MSG-106) |

    - Business Rules Applied:
        - **BR-025**: Site conditions must meet safe flight weather criteria
        - **BR-026**: Assessment is Ready only when all four dimensions (site, personnel, UAV, technical) pass.
        - **BR-027**: Failure reasons must be logged and displayed for every failing component.
        - **BR-028**: Assessment expires when ValidUntil or PlannedEnd elapses.
    - Normal Flow:
        - System initiates evaluation for assessment ID.
        - Backend checks that assessment is neither Completed nor Cancelled nor Expired.
        - Site feasibility engine verifies spatial bounds and weather parameters.
        - Personnel eligibility engine filters active personnel and evaluates schedule overlaps (UC-MF01-03).
        - UAV eligibility engine evaluates fleet availability, health, and range suitability (UC-MF01-04).
        - System updates component statuses, sets overall status (Ready or NotReady), and calculates ValidUntil.
        - Database saves updates with concurrency control; audit log records ASSESSMENT\_EVALUATED.
        - Real-time notification dispatched to SignalR group (AssessmentStatusChanged). Workspace UI updates instantly.
    - Alternative & Exception Flows:
        - E1 (Assessment Already Completed): If the assessment has already been consumed to create an inspection mission, re-evaluation is blocked (ASSESSMENT\_ALREADY\_COMPLETED).
        - E2 (Assessment Cancelled): Cancelled assessments cannot be evaluated (ASSESSMENT\_CANCELLED).
        - E3 (Assessment Expired): If current time exceeds ValidUntil or PlannedEnd, status transitions to Expired and re-evaluation is rejected (ASSESSMENT\_EXPIRED).

##### 3.2.3 Check Personnel Availability and Eligibility (UC-MF01-03)

- Function trigger:
    - System Trigger: Executed internally during pre-mission evaluation (UC-MF01-02).
    - UI Navigation: Within Assessment Workspace (/pre-mission/:id), select the "Personnel Candidates" tab (Tab 2).
- Function description:
    - Actors / Roles: Primary: Mission Manager, System Administrator. Evaluated Entities: Field Personnel (Pilots/Inspectors, Analysts, Technicians).
    - Purpose: Inspect and verify the operational eligibility of field personnel for the declared time window and geographical region. Guarantees that only qualified, active crew members who have no conflicting mission bookings are proposed as eligible candidates.
    - Interface: Personnel candidate data grid with status chips, role filter pills, availability tags, and discrepancy notes.
    - Data Processing: Evaluates candidate users against four sequential verification criteria:
        - Account active status and verified identity.
        - Operational role assignment (excluding administrative-only roles).
        - Regional geographic authorization match (UserGeographicScopes).
        - Schedule conflict detection against the active ResourceBookings table.

**Screen layout:**

    - Screen ID: SCR-09: Readiness Assessment Workspace - Personnel Candidates Tab
    - Route: /pre-mission/:id (Active Tab: personnel)
    - (Screen mockup will be inserted here)
- Function Details:
    - Resource Collision Detection: A candidate has a schedule collision (ResourceAvailabilityStatus.Conflict) if there exists any active booking
    - Eligibility Outcome Codes:

***Table 9.*** *Personnel* *eligibility* *outcome codes.*

| **Status Code** | **Meaning** | **Outcome Flag** |
| :---: | :---: | :---: |
| ELIGIBLE | User is active, authorized for the region, and has no schedule conflict. | IsEligible = true |
| SCHEDULE\_CONFLICT | User is booked on another concurrent mission. | IsEligible = false |
| OUT\_OF\_SCOPE | User's geographic scope does not include the assessment region. | IsEligible = false |

    - Business Rules Applied:
        - BR-014: Deactivated or suspended personnel cannot be assigned.
        - BR-019: An Inspector must be Active, be authorized for the region, and have no schedule conflict.
        - BR-020: A resource collision occurs when any active booking overlaps the requested window.
        - PreMissionAssessmentPersonnelDto: Id, UserId, FullName, Role, IsEligible, EligibilityStatus, AvailabilityStatus, ReasonCode, Findings.

##### 3.2.4 Check UAV Availability and Technical Readiness (UC-MF01-04)

- Function trigger:
    - System Trigger: Executed internally during pre-mission evaluation (UC-MF01-02).
    - UI Navigation: Within Assessment Workspace (/pre-mission/:id), select the "UAV Candidates" tab (Tab 3).
- Function description:
    - Actors / Roles: Primary: Mission Manager, UAV Technician. Evaluated Entities: UAV Fleet Airframes.
    - Purpose: Assess drone fleet units to identify candidate aircraft capable of executing the inspection scope safely. Analyzes operational availability, calendar booking conflicts, airworthiness technical health ratings, and performs automated GIS calculations to verify battery endurance against transmission corridor distance.
    - Interface: Interactive UAV fleet table with technical health indicators, battery state of health, flight range capacity, and scope suitability tags.
    - Data Processing: Evaluates operational readiness (Available), cross-references ResourceBookings, verifies latest DroneTechnicalInspection within 72 hours, and runs algorithmic estimation of corridor flight endurance and required sorties.
- **Screen layout:**
    - Screen ID: SCR-09: Readiness Assessment Workspace - UAV Candidates Tab
    - Route: /pre-mission/:id (Active Tab: uav)
    - (Screen mockup will be inserted here)
- Function Details:
    - Business Rules Applied:
        - BR-013: Only a UAV with status Available can be booked for flight.
        - BR-021: Eligible UAV must be Available, unbooked, and have a Passed technical inspection valid at planned start.
        - BR-022: Technical inspection is valid for 72 hours after recording.
        - BR-024: Scope exceeding flight capability marks drone as Unsuitable.
        - PreMissionAssessmentDroneDto: Id, DroneId, UavCode, Model, IsEligible, OperationalStatus,TechnicalHealth,BatteryLevel,Suitability,EstimatedFlightsNeeded,CoverageDistanceKm,SuitabilityReason,ReasonCode.

##### 3.2.5 Record UAV Technical Inspection (UC-MF01-05)

- Function trigger:
    - Navigation Path A: From Assessment Workspace (/pre-mission/:id), select the "Technical Inspection" tab (Tab 4), choose a candidate UAV, and connect the flight controller.
    - Navigation Path B: From sidebar, navigate to UAV Inventory (/drones), click on a drone record, and open the Technical Inspection modal (SCR-07).
    - Direct Route: /pre-mission/:id (Tab: technical) or /drones modal.
- Function description:
    - Actors / Roles: Primary: UAV Technician, Mission Manager. Secondary: Flight Controller Hardware (Betaflight STM32).
    - Purpose: Execute pre-flight airworthiness diagnostic and Built-In Self-Test (BIST) telemetry measurements across 6 critical subsystems; read and verify the hardware MSP\_UID from the onboard flight controller via Web Serial API (USB-VCP) or virtual MSP interface; enforce safety rule BR-05 to verify that connected hardware belongs to the specific candidate UAV assigned in the assessment.
    - Interface: Comprehensive hardware diagnostic console featuring live Web Serial connection status, hardware UID card, 6-subsystem health grid, quantitative metric telemetry table, and inspection sign-off actions.
    - Data Processing: Communicates with Betaflight flight controller; retrieves unique 12-byte hardware UID (MSP\_UID); validates UID against DroneFlightControllers bindings; evaluates quantitative sensor metrics against safety thresholds; updates drone technical health to Healthy, Warning, or Critical; generates DroneTechnicalInspection and child DroneTechnicalMetric records.

**Screen layout:**

    - Screen ID: SCR-07: Drone Technical Inspection Dashboard & Flight Controller Pairing Console
    - Route: /pre-mission/:id (Active Tab: technical)
    - (Screen mockup will be inserted here)
- Function Details:
    - Hardware UID Safety Lock (BR-04, BR-05):
        - The system queries the connected Flight Controller using MSP command MSP\_UID (0x0036).
        - Case 1 (Matched): Connected UID matches the binding for drone.Id. System allows testing.
        - Case 2 (Mismatch - BR-05 Violation): Connected UID is bound to a different drone in the fleet. The console displays a prominent red warning banner:"EQUIPMENT MISMATCH WARNING (BR-05 Violation): Connected Flight Controller belongs to UAV-02, but this assessment requires UAV-01. The 'Run technical inspection' button has been safety locked."
        - Case 3 (Unregistered - BR-04): Connected UID has never been registered. Console offers a secure pairing interface (PairFlightControllerAsync) for authorized technicians.
    - 6 Subsystem Checklist and Metric Verification:

***Table 10.*** *Subsystem checklist* *and* *metric verification.*

| Subsystem | Metric Code | Description | Threshold / Rule | Critical |
| :---: | :---: | :---: | :---: | :---: |
| IMU & Sensors | IMU\_ACCEL\_VIBE | Accelerometer vibration level | Within safe limit | Yes |
| IMU & Sensors | IMU\_GYRO\_DRIFT | Gyroscope zero-bias drift | Within calibration limit | Yes |
| Propulsion & Motors | PROP\_MOTOR\_ESC | ESC telemetry & RPM balance | Motor delta  <8% | Yes |
| Power & Battery | BATT\_VOLTAGE\_CELL | Individual LiPo cell voltage | Nominal cell voltage | Yes |
| Power & Battery | BATT\_CELL\_DELTA | Cell balance delta voltage | Balanced cell voltage | No |
| RF & Telemetry | RF\_LINK\_RSSI | RC receiver signal strength | Sufficient signal strength | Yes |
| GPS & Navigation | GPS\_SATS\_COUNT | Number of locked GNSS satellites | Sufficient satellite lock | Yes |
| GPS & Navigation | GPS\_HDOP\_ACC | Horizontal Dilution of Precision | Within precision limit | No |
| Camera Payload | PAYLOAD\_GIMBAL | Gimbal 3-axis stabilization & stream | Status == Operational | No |

- Business Rules Applied:
    - BR-004: Flight controller hardware bindings are versioned and audited; unbinding preserves historical audit trail.
    - BR-005: Hardware UID match is strictly enforced; inspection recording is blocked upon hardware mismatch.
    - BR-022: Inspection validity is exactly 72 hours (3 days) from completion timestamp.
    - BR-023: A technical inspection fails if any mandatory checklist item or critical metric fails.

##### 3.2.6 View Readiness Assessment Result (UC-MF01-06)

- Function trigger:
    - Navigation Path: From Assessment List (/pre-mission), click on any assessment row; or select the "Assessment Result" tab (Tab 5) in the Assessment Workspace (/pre-mission/:id).
- Function description:
    - Actors / Roles: Primary: Mission Manager, System Administrator. Read-Only: Inspector/Pilot, Analyst.
    - Purpose: Synthesize and review the multi-factor readiness evaluation summary; inspect failure reasons and remediation guidelines for any non-compliant component; manage assessment lifecycle cancellation; and initiate direct handoff to the Mission Creation and Dispatch flow (MF02) when status is Ready.
    - Interface: Executive readiness scorecard with high-level outcome badge, component breakdown tiles, cancellation reason dialog, and the primary "Create flight mission" call-to-action button.
    - Data Processing: Verifies assessment freshness against expiration policies; renders detailed diagnostic messages for NotReady or Expired states; if Ready, bundles the approved scope, target towers, and eligible personnel/drone pools, transitioning seamlessly to the Mission Creation wizard (SCR-11).

**Screen layout:**

    - Screen ID: SCR-09: Readiness Assessment Workspace - Result Tab & Mission Dispatch Handoff
    - Route: /pre-mission/:id (Active Tab: result)
    - (Screen mockup will be inserted here)
- Function Details:
    - The "Create flight mission" button is conditionally enabled if and only if assessment.status === 'Ready'.
        - The "Tạo nhiệm vụ bay" button is conditionally enabled if and only if assessment.status === 'Ready'.
        - Clicking the button routes the user to /missions/new?assessmentId={id} (SCR-11), pre-populating:
            - Inspection Title (generated from assessment code and target assets).
            - Scope Region and Target Towers in ordered sequence.
            - Filtered candidate lists showing only personnel and UAVs assessed as IsEligible = true.
            - Planned flight window constraints matching the evaluated timeframe.
    - Cancellation Protocol:
        - A manager may cancel an active assessment before it is consumed by clicking "Cancel assessment".
        - A modal prompt requires entering an explicit cancellation reason (CancelAssessmentRequest.Reason).
        - Backend transitions status to Cancelled, releases any soft reservation locks, records an audit log entry (ASSESSMENT\_CANCELLED), and emits a SignalR update.
    - Business Rules Applied:
        - BR-026: The overall result is Ready when all four components pass; NotReady when any component fails.
        - BR-027: NotReady results must list explicit diagnostic explanations for each failing factor.
        - BR-028: Assessment expires when its window start passes; transitions to Completed when a mission is created.
        - BR-029: Only an unexpired, unconsumed Ready assessment can create an inspection mission. Exactly one mission can be spawned per assessment.

##### 3.2.7 Automated Assessment Expiry Background Service (NS-01)

- Function trigger:
    - Timing / Frequency: Scheduled recurring background job running periodically via background worker service.
    - On-Demand Trigger: Applied synchronously during any read or evaluate operation on PreMissionAssessmentService (BR-028).
- Function description:
    - Actors / Roles: System Background Worker (Operations Service).
    - Purpose: Enforce temporal validity boundaries on all active readiness assessments. Guarantees that time-sensitive pre-flight assessments cannot be used to spawn missions if the operational flight window or the 4-hour assessment validity window has elapsed without dispatch.
    - Interface: Non-screen automated background process. Operational logs emitted to Serilog / OpenTelemetry; real-time notifications pushed to connected web clients via SignalR.
    - Data Processing: Queries the database for all records in status Ready or Evaluating where ValidUntil <= UtcNow or PlannedEnd <= UtcNow; transitions matching records to Expired; updates record version; persists audit trail entries (ASSESSMENT\_EXPIRED); dispatches SignalR message AssessmentExpired to notify managers currently viewing the affected workspace.
- Screen layout:\[Non-Screen Function\]
    - Function ID: NS-01: Assessment Expiry Job
    - Note: This function operates as an autonomous backend worker service without a dedicated user interface screen. State updates reflect dynamically on SCR-08 (Assessment List) and SCR-09 (Assessment Workspace).
- Function Details:
    - Business Rules Applied:
        - BR-005: State transition adheres strictly to the lifecycle model (Ready → Expired).
        - BR-007: Expiry actions are audited with timestamp and system actor ID.
        - BR-028: A Ready assessment automatically expires when its validity window or planned end passes.

#### 3.3 MF02 – Create and Assign Inspection Mission 

The Create and Assign Inspection Mission module (MF02) translates approved, unexpired pre-mission readiness assessments (MF01) into formally dispatched operational missions. The module orchestrates the complete dispatch lifecycle: setting operational objectives and priorities, assembling available flight and inspection crews (pilots/inspectors, analysts, technicians), binding capable and unreserved UAV hardware, establishing calendar-level resource reservations to prevent concurrent operational collisions, and establishing interactive assignment workflows with confirmation deadlines. Furthermore, it supports administrative lifecycle modifications (cancellation, suspension, resumption, postponement, reschedule, personnel reassignment, and drone replacement) while safeguarding transactional consistency across resource bookings.

##### 3.3.1 Create Inspection Mission (UC-MF02-01)

- **Function trigger:**
    - Navigation Path A: In the Assessment Workspace (/pre-mission/:id), on the "Assessment Result" tab (Tab 5), click the primary action button "Create flight mission" (enabled only when status is Ready).
    - Navigation Path B: From Mission List (/missions), click "+ Create new mission", then select an approved Ready assessment docket.
    - Direct Route: /missions/new?assessmentId={id} (SCR-11).
- **Function description:**
    - Actors / Roles: Primary: Mission Manager, System Administrator.
    - Purpose: Create an inspection mission docket from an approved, unexpired Ready assessment; configure operational metadata (title, priority, operational objective, priority defect classes, confirmation deadline, manager instructions); allocate qualified crew members and healthy UAV airframes from the assessment candidate pool; atomically commit resource bookings; and immediately transition the mission into Pending Acceptance state (includes UC-MF02-02, UC-MF02-03, UC-MF02-04).
    - Interface: Web Client (Angular 21 reactive multi-step mission setup wizard with live conflict validation).
    - Data Processing: Verifies that the source assessment is Ready, unexpired, and not previously consumed; copies target power grid towers and spans in exact geometric sequence into MissionTarget entities; verifies that selected personnel and UAVs were evaluated as IsEligible in the assessment; verifies that no resource collisions have occurred in the interim; creates ResourceBooking records for all assigned resources; sets mission status to Pending Acceptance; generates unique mission code (MS-yyyyMMddHHmmssfff-nnn); sets the assessment status to Completed; emits integration event MissionCreatedFromAssessment.

**Screen layout:**

    - Screen ID: SCR-11: Create & Dispatch Inspection Mission Wizard
    - Route: /missions/new
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 11.*** *Create* *inspection* *mission data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| assessmentId | UUID | Hidden / Param | Required. Must refer to an existing assessment with Status == Ready, IsDeleted == false, and ValidUntil > UtcNow (BR-029). |
| title | String (255) | Text Input | Required. Non-empty string, 5–255 characters. |
| description | String (1000) | Textarea | Optional. Contextual overview or operational instructions. |
| priority | Enum | Radio / Select | Required. Values: Normal, High, Emergency. Default: Normal. If Emergency, emergencyReason is mandatory (BR-027, BR-041). |
| emergencyReason | String (500) | Textarea | Conditional. Required if priority == Emergency; minimum length 10 characters (BR-027). |
| objective | Enum | Dropdown Select | Required. Values: PeriodicInspection, TargetedReinspection, IncidentFollowUp, EmergencyInspection. |
| priorityDefects | List<String> | Tag Multi-select | Optional. Selected defect classes to highlight for server detection. |
| confirmationDeadline | ISO DateTime | Date/Time Picker | Optional. If provided, must be ≥UtcNow+30 mins\ge \text{UtcNow} + 30\text{ mins}≥UtcNow+30 mins and ≤plannedStart−2 hours\le \text{plannedStart} - 2\text{ hours}≤plannedStart−2 hours (BR-031). |
| managerInstructions | String (1000) | Textarea | Optional. Operational directives displayed on inspector mobile console. |
| assignedPersonnel | List<Object> | Candidate Grid | Required. Minimum 1 Inspector/Pilot (BR-033). Each item: userId, role, isRequired. Must be members of assessment candidate pool. |
| assignedDroneId | UUID | Radio Grid | Required. Exactly 1 operational UAV airframe (BR-034). Must belong to assessment candidate pool with status Available. |
| idempotencyKey | String (UUID) | Client Header | Optional client-generated UUID to ensure idempotent submission. |

- Business Rules Applied:
    - BR-029: Only an unexpired Ready assessment can be used to spawn a mission. Exactly one mission may be created per assessment.
    - BR-030: Mission targets are cloned directly from the assessment scope in sequential order.
    - BR-031: Planned flight window must be within the assessment envelope; confirmation deadline must precede planned start by at least 2 hours.
    - BR-033: A mission must have at least one Inspector/Pilot designated as the pilot-in-command.
    - BR-034: A mission must have exactly one assigned UAV whose battery health is ≥80%.
    - BR-035: Resource eligibility and calendar availability are strictly re-verified at the instant of creation.
    - BR-036: Mission creation, assignment records, and calendar ResourceBookings are committed atomically in a single database transaction.
- Normal Flow (Step-by-step):
    - Manager accesses /missions/new?assessmentId={id}. System loads the assessment metadata, target towers, and eligible candidates.
    - Manager enters the mission Title, Objective, and optional Instructions.
    - Manager verifies or adjusts the Priority. If Emergency is selected, the manager provides the operational justification.
    - Manager selects crew members from the eligible candidates, assigning roles (Inspector, Analyst, Technician) and marking mandatory participants.
    - Manager selects the primary UAV airframe from the list of available, healthy aircraft.
    - (Optional) Manager clicks "Check Resource Conflicts"; system validates that candidates have not been booked by a concurrent process.
    - Manager clicks "Dispatch Mission". System executes validation, persists Mission, MissionTarget, MissionAssignment, and ResourceBooking entities, transitions assessment status to Completed, and publishes MissionCreatedFromAssessment to RabbitMQ.
    - System redirects manager to Mission Detail workspace (/missions/:id) with confirmation toast MSG-203.
- Alternative & Exception Flows:
    - A1 (Idempotent Resubmission): If a request with an identical idempotency key is submitted, the backend returns the already created mission without generating duplicates.
    - E1 (Assessment Expired or Already Consumed): If the assessment is expired or has already spawned a mission, the system rejects creation with ASSESSMENT\_EXPIRED\_OR\_NOT\_READY (MSG-201).
    - E2 (Resource Booking Conflict): If any assigned personnel or UAV has become reserved during the planning process, transaction rolls back with RESOURCE\_BOOKING\_CONFLICT (MSG-202).
    - E3 (Missing Mandatory Resource): If no Inspector or no UAV is selected, submission is blocked with PERSONNEL\_ASSIGNMENT\_REQUIRED or DRONE\_ASSIGNMENT\_REQUIRED (MSG-209).
    - E4 (Emergency Reason Missing): If priority is Emergency but emergencyReason is missing or <10 characters, system halts with EMERGENCY\_REASON\_REQUIRED.

##### 3.3.2 Assign Personnel to Mission (UC-MF02-02) 

- **Function trigger:**
    - Setup Trigger: Embedded within the Mission Creation Wizard (SCR-11).
    - Post-Dispatch Trigger: From Mission Detail workspace (/missions/:id), navigate to the "Assignments & Team" tab (Tab 3) or click "Reassign" on any member card (SCR-12 / SCR-13).
- **Function description:**
    - Actors / Roles: Primary: Mission Manager, System Administrator. Assigned Entities: Field Personnel (Inspectors, Analysts, Technicians).
    - Purpose: Formulate, adjust, and reassign the mission crew roster prior to mission commencement; enforce operational role coverage; manage individual assignment states (Pending, Accepted, Postponed); and dynamically update calendar bookings when crew changes occur.
    - Interface: Interactive crew management table with role chips, required flags, assignment status indicators, and modal reassignment dialogs.
    - Data Processing: Verifies that the mission has not yet started (BR-041); checks target user active status and regional authorization; evaluates calendar availability; safely cancels previous ResourceBooking and creates a new booking; resets assignment status to Pending; and emits real-time assignment notifications.

**Screen layout:**

    - Screen ID: SCR-12 / SCR-13: Mission Assignments & Crew Reassignment Modal
    - Route: /missions/:id (Assignments Tab)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 12.*** *Assign personnel* *data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| missionId | UUID | Path Variable | Required. Mission must exist and status must be in {Draft, PendingAcceptance, Confirmed, Preparing, Ready} (BR-041). |
| assignmentId | UUID | Hidden / Select | Required when reassigning or revoking an existing member. |
| newUserId | UUID | User Dropdown | Required. User must be Active, belong to mission region scope and have no overlapping bookings (BR-015, BR-019, BR-020). |
| role | Enum | Dropdown Select | Required. Values: Inspector, Pilot, Analyst, Technician. |
| isRequired | Boolean | Checkbox | Default: true. Determines whether this member's acceptance is mandatory for mission readiness. |
| reason | String (500) | Textarea | Mandatory when modifying or reassigning a dispatched mission (BR-041). |

- Business Rules Applied:
    - BR-015: Inactive or suspended user accounts cannot be assigned.
    - BR-019: An Inspector must be Active, and belong to the regional scope.
    - BR-020: Calendar overlap with any active ResourceBooking is strictly prohibited.
    - BR-033: The mission must retain at least one Inspector/Pilot designated as pilot-in-command.
    - BR-035: Full eligibility and collision checks are executed upon reassignment.
    - BR-041: Modifying assignments requires a documented justification, is logged in the audit trail, and reverts mission status from Confirmed to Pending Acceptance if a required member changes.
- Normal Flow (Reassignment):
    - Manager opens the Assignments tab on SCR-12 and clicks "Reassign" for a specific team member.
    - System opens SCR-13 (Reassign Modal) and queries the user service for eligible, unbooked personnel in the region.
    - Manager selects the replacement user and enters the operational justification.
    - Manager clicks "Confirm Reassignment".
    - System checks concurrency, cancels the existing user's ResourceBooking, creates a new ResourceBooking for the replacement user, updates the MissionAssignment row, and sets status to Pending.
    - If the mission was previously Confirmed, system reverts status to Pending Acceptance (BR-041).
    - System dispatches in-app and email notifications to the revoked user and replacement user, audits the event as ASSIGNMENT\_REASSIGNED, and broadcasts a real-time SignalR event.
- Alternative & Exception Flows:
    - A1 (Add Additional Crew Member): Manager clicks "Add Member" on SCR-12. System verifies eligibility, creates assignment and calendar booking, and notifies user.
    - A2 (Remove Non-Critical Crew Member): Manager clicks "Remove" on an optional member. System cancels booking, marks assignment Revoked, and updates mission team count.
    - E1 (Mission Already Started): If mission status is In Progress, Completed, or Cancelled, modification is blocked with MISSION\_IMMUTABLE\_AFTER\_START (MSG-211).
    - E2 (Candidate Inactive or Collision): If replacement user has conflicting commitments, system aborts with RESOURCE\_BOOKING\_CONFLICT (MSG-202).

##### 3.3.3 Assign UAV to Mission (UC-MF02-03)

- **Function trigger:**
    - Setup Trigger: Embedded within the Mission Creation Wizard (SCR-11).
    - Post-Dispatch Trigger: From Mission Detail (/missions/:id), click "Change UAV" in the UAV Equipment card (SCR-12 / SCR-13).
- **Function description:**
    - Actors / Roles: Primary: Mission Manager, System Administrator. Secondary: UAV Technician.
    - Purpose: Allocate an airworthy, available UAV aircraft to the mission corridor, or substitute the assigned UAV prior to takeoff if maintenance issues or battery limitations arise.
    - Interface: UAV hardware selector card with technical inspection indicators, flight endurance estimates, and swap confirmation modal.
    - Data Processing: Verifies that the mission has not started; validates that the replacement UAV has operational status Available, technical health Healthy or Warning, a Passed technical inspection within 72 hours, and battery state of charge ≥80%; cancels existing drone booking; creates new ResourceBooking; updates Mission.DroneId; and emits real-time updates.

**Screen layout:**

    - Screen ID: SCR-13: Change UAV Equipment Modal
    - Route: /missions/:id (Overview / Equipment Card)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 13.*** *Assign UAV data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| missionId | UUID | Path Variable | Required. Mission must exist and must not be In Progress or Completed (BR-041). |
| newDroneId | UUID | Dropdown / Grid | Required. Aircraft must have Status == Available, TechnicalHealth in {Healthy, Warning}, BatteryLevel >= 80%, and no booking conflict (BR-013, BR-021, BR-034). |
| reason | String (500) | Textarea | Required. Justification for drone swap (e.g., hardware fault, maintenance cycle) (BR-041). |

- Business Rules Applied:
    - BR-013: Only a UAV with status Available can be booked for flight operations.
    - BR-021: Eligible aircraft must have a Passed technical inspection valid through planned start.
    - BR-034: A mission must have exactly one assigned UAV whose battery health is ≥80%.
    - BR-041: Swapping aircraft requires documented justification, is logged in audit trail, and dispatches notifications to assigned pilot.
- Normal Flow:
    - Manager opens Mission Detail (SCR-12) and clicks "Change UAV".
    - System queries active fleet units and displays eligible replacement airframes.
    - Manager selects replacement UAV and enters swap justification.
    - Manager clicks "Confirm Drone Swap".
    - System checks booking collisions, cancels previous drone's ResourceBooking, creates new booking for replacement drone, updates Mission.DroneId, and updates Mission.AssignedDroneCode.
    - Audit trail records DRONE\_REPLACED; SignalR notification pushed to mission participants.
- Alternative & Exception Flows:
    - E1 (Drone Unavailable or Critical Health): If selected drone is in maintenance, retired, or has Critical health rating, swap is rejected with DRONE\_NOT\_AVAILABLE or DRONE\_CRITICAL\_HEALTH (MSG-207).
    - E2 (Booking Collision): If selected drone is booked on another mission in the same window, system rejects with RESOURCE\_BOOKING\_CONFLICT (MSG-202).
    - E3 (Mission Already Started): If flight operations have commenced, swap is prohibited with MISSION\_IMMUTABLE\_AFTER\_START (MSG-211).

##### 3.3.4 Dispatch Mission (UC-MF02-04)

- **Function trigger:**
    - System Trigger: Invoked synchronously during the completion of the Create Mission workflow (UC-MF02-01).
- **Function description:**
    - Actors / Roles: Primary: System Orchestrator / Operations Service. Secondary: Notification Provider (SendGrid / Brevo).
    - Purpose: Formally transition a newly created mission from specification into operational scheduling: lock calendar resource bookings, transition status to Pending Acceptance, generate multi-channel dispatch notices (in-app SignalR, push, email), and activate assignment response tracking with countdown timers.
    - Interface: Non-screen backend orchestration flow triggered by "Dispatch Mission" action on SCR-11.
    - Data Processing: Verifies integrity of mission targets and crew assignments; sets mission status to PendingAcceptance; sets each assignment status to Pending; commits ResourceBooking records; creates Notification records for all assigned crew; publishes MissionDispatched integration event via Outbox pattern.
- **Screen layout:** \[Non-Screen Orchestration Function\]
    - Screen ID: Transition handler on SCR-11 → SCR-12.
    - Note: The user experiences immediate feedback via a progress spinner on SCR-11 transitioning to the Mission Detail page (SCR-12) with toast notification MSG-203.
- **Function Details:**
    - Business Rules Applied:
        - BR-036: Bookings and assignments are committed atomically within one transaction.
        - BR-037: Every assignee receives an in-app and email notification containing mission scope, flight window, confirmation deadline, and deep link to inspector console.
        - BR-042: The mission remains in Pending Acceptance until all mandatory assignees have formally accepted.

##### 3.3.5 Preview Mission Targets on Map (UC-MF02-05)

- **Function trigger:**
    - Navigation Path A: In Mission Detail (/missions/:id), select the "Towers & Map" tab (Tab 5) or view the overview corridor map.
    - Navigation Path B: In Inspector Console (/missions/:id/inspector), view the center GIS panel (Column 2).
    - Direct Route: /missions/:id (Towers tab) or /missions/:id/inspector.
- **Function description:**
    - Actors / Roles: Primary: Mission Manager, Inspector/Pilot. Read-Only: Analyst, Technician.
    - Purpose: Render interactive geospatial mapping of the mission inspection corridor, displaying the ordered transmission towers, spatial span polylines, operational flight boundary, and inspection status markers.
    - Interface: High-performance Leaflet GIS canvas featuring layer switching (Satellite vs. Street), interactive tower waypoints, GPS coordinate chips, and zoom-to-corridor controls.
    - Data Processing: Queries MissionTarget records joined with Tower geolocations (NetTopologySuite Point); computes corridor bounding box; renders numbered waypoint sequence pins; overlays mission polygon boundary WKT; and reflects real-time target status (Pending in gray, Inspected in green, Skipped in amber).

**Screen layout:**

    - Screen ID: SCR-12 / SCR-14: Interactive GIS Corridor & Tower Sequence Viewer
    - Route: /missions/:id (Towers tab) & /missions/:id/inspector (GIS Map)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:


***Table 14.*** *Preview mission targets data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| missionId | UUID | Path Variable | Required. Mission must exist. |
| targets | List<TargetDto> | Map Layer | Displayed in ascending order of target sequence number (1..N1..N1..N) (BR-030). |
| layerType | Enum | Toggle Button | Values: Satellite, Streets. Default: Satellite. |
| selectedTargetId | UUID | Map Pin Click | Highlights selected tower and centers map viewport. |

- Business Rules Applied:
    - BR-001: Access strictly constrained by role and regional geographic scope.
    - BR-030: Target sequence must reflect canonical transmission line topology.
    - GIS Engine: Leaflet.js with OpenStreetMap / Esri World Imagery tile providers.

##### 3.3.6 View Assigned Mission (UC-MF02-06)

- **Function trigger:**
    - Navigation Path A: From main sidebar, select Missions (/missions), switch to the "Assigned to me" tab, and click any mission row.
    - Navigation Path B: Click on an in-app or email assignment notification (MISSION\_DISPATCH).
    - Direct Route: /missions/:id/inspector (SCR-14).
- **Function description:**
    - Actors / Roles: Primary: Inspector/Pilot, Analyst, UAV Technician.
    - Purpose: Present field assignees with a comprehensive, role-tailored mission dossier detailing operational scope, schedule constraints, confirmation deadline countdown, manager instructions, assigned UAV aircraft specifications, and target tower waypoints.
    - Interface: 3-column Inspector Console (SCR-14) optimized for field mobile tablets and field laptops, featuring a prominent Acceptance Hero Banner and quick-access operational checklist.
    - Data Processing: Verifies that requesting user has an active assignment on the mission; loads mission metadata, assigned UAV health data, and targets; calculates remaining time until confirmation deadline; evaluates current user's assignment response state (awaiting, accepted, postponed, suspended, closed).

**Screen layout:**

    - Screen ID: SCR-14: Inspector Console & Assignment Acceptance Dossier
    - Route: /missions/:id/inspector
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Business Rules Applied:
        - BR-001: Field operators may only open missions where they hold an active assignment.
        - BR-037: Full mission scope and deadline parameters are visible to assignees.
        - BR-038: Only the assigned user may view and respond to their specific assignment docket.
        - Frontend Service: MissionsApiService.getMyMissions(), MissionsApiService.getMissionById(id).

##### 3.3.7 Respond to Mission Assignment (UC-MF02-07) 

- **Function trigger:**
    - Action Path A (Accept): On SCR-14 (Inspector Console), within the Acceptance Hero Banner, click "Xác nhận tiếp nhận" (Accept Assignment).
    - Action Path B (Postpone): On SCR-14, click "Báo bận / Yêu cầu hoãn", enter postponement justification in modal SCR-15, and click "Gửi yêu cầu hoãn".
- **Function description:**
    - Actors / Roles: Primary: Inspector/Pilot, Analyst, UAV Technician (any assigned crew member).
    - Purpose: Record formal crew response to a dispatched mission assignment prior to the confirmation deadline; transition individual assignment state; and automatically transition overall mission status to Confirmed when all mandatory members have accepted.
    - Interface: Interactive hero banner buttons on SCR-14 and modal dialog SCR-15 for postponement justification.
    - Data Processing: Verifies that assignment exists, belongs to caller, and deadline has not passed (BR-038, BR-040); if Accept: updates assignment to Accepted and sets ResponseTime to UtcNow; evaluates team acceptance completeness: if all required members have accepted, transitions mission status to Confirmed (BR-042); if Postpone: updates assignment to Postponed, records reason, sets mission status to Postponed if member is mandatory, and dispatches ASSIGNMENT\_POSTPONED alert to manager.

**Screen layout:**

    - Screen ID: SCR-14 / SCR-15: Mission Assignment Response & Postponement Dialog
    - Route: /missions/:id/inspector (Modal: SCR-15)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 15.*** *Respond to mission assignment data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| missionId | UUID | Path Variable | Required. Mission must be in PendingAcceptance state. |
| response | Enum | Button Click | Required. Values: Accepted, Postponed. |
| reason | String (500) | Textarea | Mandatory if response == Postponed; non-empty, 5–500 characters (BR-039). |

- Business Rules Applied:
    - BR-038: Only the assignee may respond, before the confirmation deadline, exactly once per assignment revision.
    - BR-039: Postponement requests strictly require an operational justification.
    - BR-040: Unanswered assignments after the confirmation deadline are flagged as overdue.
    - BR-042: Mission transitions to Confirmed if and only if all mandatory assignees have Accepted.
- Normal Flow (Accept):
    - Inspector opens /missions/:id/inspector and clicks "Xác nhận tiếp nhận".
    - System verifies user assignment and checks deadline validity.
    - Backend sets MissionAssignment.Status to Accepted and updates ResponseTime.
    - System checks if all required assignees have accepted. If true, transitions Mission.Status to Confirmed and emits real-time event CONFIRMED.
    - Inspector Console banner updates dynamically to green "Đã tiếp nhận vai trò" card (MSG-205).
- Alternative & Exception Flows:
    - A1 (Postponement Request): Member clicks "Báo bận / Yêu cầu hoãn", inputs reason on SCR-15, and submits. System sets assignment status to Postponed, records reason, notifies Mission Manager, and sets mission status to Postponed if member is mandatory.
    - E1 (Deadline Elapsed): If caller submits response after confirmation deadline, system rejects with ASSIGNMENT\_DEADLINE\_EXPIRED (MSG-207).
    - E2 (Reason Missing on Postpone): Submitting postponement without reason returns POSTPONE\_REASON\_REQUIRED (MSG-204).

##### 3.3.8 Monitor Assignment Status (UC-MF02-08)

- **Function trigger:**
    - Manual Trigger: Manager navigates to Mission List (/missions, SCR-10) or opens the "Overview" / "Activity" tabs of Mission Detail (/missions/:id, SCR-12).
    - Scheduled Trigger: Automated background job (NS-02) running periodically.
- **Function description:**
    - Actors / Roles: Primary: Mission Manager, System Administrator. Secondary: System Background Worker.
    - Purpose: Provide operational managers with continuous visibility over crew confirmation milestones, monitor approaching deadlines, dispatch automated or manual reminder nudges, and record team communication streams.
    - Interface: Manager Dashboard progress widgets on SCR-12 featuring confirmation progress bars, countdown chips, and "Send Reminder" buttons.
    - Data Processing: Aggregates crew responses; calculates confirmed percentage; evaluates deadline countdown; handles reminder dispatching (in-app and email); logs communications in CommunicationLog.

**Screen layout:**	

    - Screen ID: SCR-10 / SCR-12: Mission Monitoring & Crew Confirmation Tracker
    - Route: /missions/:id (Overview & Activity tabs)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Business Rules Applied:
        - BR-037: Assignees receive automated reminders leading up to the deadline.
        - BR-040: Confirmation overdue conditions are flagged automatically without altering mission state.

##### 3.3.9 Change Dispatched Mission (UC-MF02-09)

- **Function trigger:**
    - Navigation Path: In Mission Detail (/missions/:id), click on the action dropdown or specific lifecycle action buttons: "Cancel Mission", "Suspend", "Resume", "Postpone", "Reschedule", or "Change Priority" (SCR-12 / SCR-13).
- **Function description:**
    - Actors / Roles: Primary: Mission Manager, System Administrator.
    - Purpose: Execute administrative and operational lifecycle modifications on scheduled, dispatched, or active missions to accommodate changing field conditions, equipment failures, severe weather events, or operational reprioritization.
    - Interface: Contextual lifecycle modal dialogs (SCR-13) with state transition checks and mandatory justification capture.
    - Data Processing: Verifies that requested transition conforms to the Mission Lifecycle State Machine (BR-005, Table 39); validates manager regional scope; executes corresponding database updates and calendar ResourceBooking adjustments; writes immutable entries to CommunicationLog and AuditLog; and broadcasts real-time SignalR notifications.

**Screen layout:**

    - Screen ID: SCR-13: Mission Lifecycle Modification & Cancellation Modals
    - Route: /missions/:id (Modal Dialogs)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Allowed Lifecycle Transitions and Operational Impacts:

***Table 16.*** *Allowed lifecycle transitions and operational impacts.*

| **Action** | **Permitted From States** | **Target State** | **System Effect & Booking Impact** |
| :---: | :---: | :---: | :---: |
| Cancel | Draft, Pending, Confirmed,, Ready, Postponed, Suspended | Cancelled | Releases all active personnel and UAV ResourceBookings; returns assessment to Ready if ValidUntil > UtcNow, else Expired; notifies assignees (BR-041). |
| Suspend | Confirmed, Preparing, Ready, InProgress | Suspended | Freezes field operations; blocks UAV takeoff; logs suspension rationale; dispatches urgent safety notice to pilot (BR-041). |
| Resume | Suspended | InProgress or Ready | Re-verifies resource bookings and technical airworthiness; restores active state; alerts field team (BR-041). |
| Postpone | PendingAcceptance, Confirmed, Preparing, Ready | Postponed | Retains tentative bookings; flags docket for manager rescheduling; logs postponement reason. |
| Reschedule | Any status prior to InProgress | PendingAcceptance | Modifies plannedStart and plannedEnd; re-verifies availability; moves ResourceBookings to new window; re-notifies crew. |
| Change Priority | Any status prior to InProgress | Unchanged | Updates priority and priority defects; requires emergencyReason if changed to Emergency (BR-027). |

- Business Rules Applied:
    - BR-005: State transitions adhere strictly to the formal lifecycle model.
    - BR-027 / BR-041: All lifecycle overrides and emergency designations require an explicit operational justification.
    - BR-031 / BR-035: Rescheduling re-evaluates all candidate availabilities and calendar collisions.

##### 3.3.10 Mission Confirmation Overdue Background Service (NS-02)

- **Function trigger:**
    - Timing / Frequency: Recurring background job running periodically.
- **Function description:**
    - Actors / Roles: System Background Worker (Operations Service).
    - Purpose: Autonomously scan for dispatched missions where the crew confirmation deadline has elapsed without receiving all mandatory acceptances, alerting managers to initiate manual intervention or crew reassignment.
    - Interface: Non-screen background worker service. Generates system notifications and real-time alerts on SCR-02, SCR-10, and SCR-12.
    - Data Processing: Queries active missions with Status == PendingAcceptance and ConfirmationDeadline <= UtcNow; verifies whether an overdue notification has already been generated; dispatches MISSION\_CONFIRMATION\_OVERDUE in-app notification to Mission Manager; writes OVERDUE entry to CommunicationLog; broadcasts SignalR event to mission channel.
- **Screen layout:** \[Non-Screen Function\]
    - Function ID: NS-02: Mission Confirmation Overdue Job
    - Note: State reflections appear dynamically as amber "Overdue" chips on SCR-10 and SCR-12.
- **Function Details:**
    - Business Rules Applied:
        - BR-040: Overdue assignments trigger automated manager warnings; the mission state is preserved to permit manual resolution.
        - Job Class: MissionConfirmationOverdueJob in UavPms.OperationsService.Infrastructure.BackgroundJobs.

#### 3.4 MF03 – Mission Execution and Field Inspection 

The Mission Execution and Field Inspection module (MF03) governs all physical field operations and live flight supervision across power transmission corridors. The module enforces strict pre-flight safety protocols (on-site GPS check-in of crew members), regulates mission takeoff execution, tracks periodic UAV telemetry (transmitted over cellular or field network), captures aerial inspection photography and video tagged with EXIF metadata, monitors live tower progress, and incorporates server-side analysis of uploaded inspection media and manually reported field incidents. Upon flight conclusion, it coordinates telemetry log submission, resource release.

##### 3.4.1 Check In at Mission Site (UC-MF03-01)

- **Function trigger:**
    - Action Path: On Inspector Console (/missions/:id/inspector, SCR-14 / SCR-16), in the pre-flight checklist panel, click "Xác nhận có mặt tại hiện trường" (Check-in at site).
    - Direct Route: /missions/:id/inspector (Check-in Action).
- **Function description:**
    - Actors / Roles: Primary: Inspector/Pilot, Crew Members. Secondary: Geolocation Sensor (HTML5 Geolocation API / GPS).
    - Purpose: Formally record the physical arrival of the flight crew at the target power corridor site; capture authenticated server timestamp and device GPS coordinates; and calculate proximity against target transmission towers to ensure safety compliance.
    - Interface: Field Execution Console (SCR-16) check-in card displaying live GPS fix status, coordinates, accuracy radius, and submission button.
    - Data Processing: Verifies that caller has an Accepted assignment and that mission status is Confirmed or Preparing (BR-043); retrieves device coordinates; verifies proximity against target towers: if location is outside target corridor proximity, system flags a spatial warning (BR-044); persists FieldCheckIn record; updates mission readiness state (BR-046); broadcasts check-in event to manager dashboard via SignalR.
- **Screen layout:**\[None-Screen Function\]
    - Screen ID: SCR-16 / SCR-14: Field Site Check-In & Proximity Verification Card
    - Route: /missions/:id/inspector (Pre-flight Checklist)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 17.*** *Check in at mission site data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| missionId | UUID | Path Variable | Required. Mission must be Confirmed or Preparing (BR-043). |
| latitude | Double | Device Sensor | Required. WGS84 latitude (-90.0 to +90.0). |
| longitude | Double | Device Sensor | Required. WGS84 longitude (-180.0 to +180.0). |
| accuracyMeters | Double | Device Sensor | Optional. GPS horizontal accuracy radius in meters. |

- Business Rules Applied:
    - BR-043: Only an Inspector with an Accepted assignment on a Confirmed mission may check in, from 2 hours prior to planned start until planned end.
    - BR-044: Check-in records server time and GPS coordinates; distance outside the expected corridor proximity triggers a warning alert.
    - BR-046: Successful check-in of pilot is a mandatory prerequisite for mission takeoff.
- Normal Flow:
    - Inspector arrives at the power corridor and opens /missions/:id/inspector.
    - Inspector clicks "Xác nhận có mặt tại hiện trường". Browser requests geolocation permission and captures current GPS fix.
    - System validates caller assignment, verifies mission state, and computes distance to closest mission tower.
    - Backend creates FieldCheckIn row, marks member checked-in, and updates mission readiness.
    - Inspector Console displays confirmed check-in timestamp (MSG-304); manager receives real-time SignalR notification.
- Alternative & Exception Flows:
    - A1 (Proximity Warning): If GPS position is outside target corridor proximity, check-in is logged with a spatial warning flag (MSG-302).
    - E1 (Premature or Unauthorized Check-In): Attempting check-in when mission is not Confirmed or user is not assigned returns CHECK\_IN\_INVALID\_STATE (MSG-301).
    - E2 (Duplicate Check-In): Submitting check-in twice by same user returns DUPLICATE\_CHECK\_IN.

##### 3.4.2 Start Mission Execution (UC-MF03-03)

- **Function trigger:**
    - Action Path: On Inspector Console (SCR-14 / SCR-16), within the flight control action bar, click the primary button "Bắt đầu nhiệm vụ bay" (Start Flight Mission).
- **Function description:**
    - Actors / Roles: Primary: Inspector/Pilot. Read-Only: Mission Manager, System Administrator.
    - Purpose: Initiate field execution of the inspection flight; transition mission status to In Progress; transition UAV operational state to Flying; and activate periodic telemetry recording.
    - Interface: Flight execution toolbar on SCR-16 with green status beacon and flight time counter.
    - Data Processing: Verifies that mission is Confirmed or Ready; verifies that pilot check-in; verifies that UAV technical inspection remains valid; sets Mission.Status to InProgress; records ActualStart timestamp; sets Uav.FlightStatus to Flying; audits action as MISSION\_STARTED; emits IN\_PROGRESS SignalR event to mission monitoring channels.

**Screen layout:**

    - Screen ID: SCR-16 / SCR-14: Flight Execution Active Console & Takeoff Trigger
    - Route: /missions/:id/inspector
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Business Rules Applied:
        - BR-043: Valid check-in are mandatory preconditions.
        - BR-046: A mission can start only once; technical inspection of UAV must still be valid; current time must fall within the operational window.
        - BR-047: Activating InProgress status permits ingestion of UAV telemetry streams.
    - Normal Flow:
        - Pilot verifies all pre-flight safety items are satisfied and clicks "Bắt đầu nhiệm vụ bay".
        - System displays confirmation modal verifying airspace clearance and pilot readiness.
        - Pilot confirms takeoff.
        - Backend checks prerequisites, updates Mission.Status to InProgress, sets Uav.FlightStatus to Flying, and records ActualStart.
        - Console updates to active flight monitoring view; toast notification MSG-304 is displayed.
        - System broadcasts SignalR status change to connected Manager dashboards.
    - Alternative & Exception Flows:
        - E1 (Prerequisites Incomplete): Attempting to start before check-in is completed halts execution with START\_PREREQUISITES\_FAILED (MSG-303).
        - E2 (UAV Technical Inspection Expired): If the 72-hour airworthiness window has elapsed, start is blocked with TECHNICAL\_INSPECTION\_EXPIRED.

##### 3.4.3 Record Asset Inspection Progress (UC-MF03-04) 

- **Function trigger:**
    - Action Path: On Inspector Console (SCR-14 / SCR-16), in the Tower Waypoints checklist (Column 3), click on a tower row and select "Đánh dấu đã kiểm tra" (Mark Inspected) or "Bỏ qua cột" (Skip Tower).
- **Function description:**
    - Actors / Roles: Primary: Inspector/Pilot. Read-Only: Mission Manager.
    - Purpose: Track granular asset inspection completion as the drone navigates from tower to tower along the transmission line; record tower status as Inspected or Skipped; and capture mandatory justification when a tower cannot be inspected (e.g. tree obstruction, bird attack, extreme local turbulence).
    - Interface: Tower waypoints checklist on SCR-16 with progressive status chips and skip justification dialog.
    - Data Processing: Verifies mission is InProgress; updates MissionTarget.InspectionStatus (Pending, Inspected, Skipped); records SkipReason if skipped; updates target completion count; emits progress update event via SignalR.

**Screen layout:**

    - Screen ID: SCR-16: Tower Waypoints Checklist & Progress Tracker
    - Route: /missions/:id/inspector (Column 3)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 18.*** *Record asset inspection progress data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| missionId | UUID | Path Variable | Required. Mission must be InProgress. |
| towerId | UUID | Path Variable | Required. Must be a valid target tower in the mission scope. |
| status | Enum | Button Click | Required. Values: Inspected, Skipped. |
| skipReason | String (500) | Textarea | Mandatory if status == Skipped; non-empty, 5–500 characters (BR-054). |

- Business Rules Applied:
    - BR-030: Inspection targets must belong to the mission scope.
    - BR-054: Skipping an inspection target strictly requires a documented operational reason.
    - BR-055: Field completion requires every target to be either Inspected or Skipped.

##### 3.4.4 Monitor Field Execution & Telemetry (UC-MF03-05) 

- **Function trigger:**
    - Navigation Path A: From sidebar, select Live Monitoring (/monitor/dashboard, SCR-17).
    - Navigation Path B: In Mission Detail (/missions/:id, SCR-12), view the live corridor map and telemetry widget.
    - Direct Route: /monitor/dashboard or /missions/:id (Overview).
- **Function description:**
    - Actors / Roles: Primary: Mission Manager, Analyst, System Administrator. Secondary: Map Service.
    - Purpose: Provide mission command staff with real-time telemetry and spatial tracking of active UAV flights; display live aircraft coordinates, flight altitude, ground speed, heading, battery SoC, and RSSI link quality; and alert managers to telemetry link outages.
    - Interface: Live Monitoring Dashboard (SCR-17) featuring dynamic Leaflet GIS map with animated drone marker, flight path breadcrumbs, numeric telemetry gauges, and mission timeline events.
    - Data Processing: Ingests periodic telemetry via background consumer (NS-04) caches latest telemetry snapshot in Redis for live tracking; calculates link freshness: if no heartbeat is received, flags connection warning or Telemetry Link Lost; pushes updates to connected clients via SignalR hub.

**Screen layout:**

    - Screen ID: SCR-17: Real-Time UAV Live Telemetry & Mission Flight Tracker
    - Route: /monitor/dashboard & /missions/:id (Live Monitoring)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Business Rules Applied:
        - BR-047: Telemetry data is accepted and processed exclusively for UAVs assigned to an InProgress or Suspended mission.
        - BR-050: Duplicate telemetry packets are discarded by sequence timestamp.

##### 3.4.5 Handle Critical Alert (UC-MF03-07)

- **Function trigger:**
    - Navigation Path A: In top header, click the flashing red Siren Icon (SCR-02 / SCR-17).
    - Navigation Path B: From sidebar, select Emergency Alerts (/emergency-alerts, SCR-18).
    - Direct Route: /emergency-alerts (SCR-18).
- **Function description:**
    - Actors / Roles: Primary: Analyst, Mission Manager. Secondary: Escalation Recipient (Grid Dispatcher / Senior Supervisor).
    - Purpose: Review, triage, and action urgent electrical defect alerts detected during flight; review evidence imagery and bounding box accuracy; confirm legitimate hazards (spawning urgent maintenance tickets); dismiss false positives with documented justification; or formally escalate unhandled hazards to senior grid dispatchers.
    - Interface: Dedicated Emergency Alerts Review Console (SCR-18) featuring Active vs. History tabs, zoomable evidence preview, and triage action controls.
    - Data Processing: Verifies reviewer permissions (Analyst, Manager, Admin); displays active alerts sorted by priority and arrival time; upon Confirm: updates alert status to Confirmed, creates maintenance work order, and marks anomaly verified; upon Dismiss: updates status to Dismissed and records reason; upon Escalate: updates status to Escalated, creates AlertEscalation record, and dispatches urgent notification to designated supervisor (BR-053).

**Screen layout:**

    - Screen ID: SCR-18: Emergency Alerts Triage & Escalation Console
    - Route: /emergency-alerts
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:



***Table 19.*** *Handle critical alert data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| alertId | UUID | Path Variable | Required. Alert must exist and Status == Active. |
| action | Enum | Button Click | Required. Values: Confirm, Dismiss, Escalate. |
| notes | String (1000) | Textarea | Mandatory if action == Dismiss or Escalate (BR-053). |
| targetUserId | UUID | User Dropdown | Mandatory if action == Escalate. Must refer to an active Manager or Admin. |

- Business Rules Applied:
    - BR-051: Critical alerts follow formal states: Active → Confirmed | Dismissed | Escalated.
    - BR-053: Acknowledging, resolving, or dismissing an alert requires capturing actor ID, timestamp, and disposition notes.

##### 3.4.6 Finish Field Inspection (UC-MF03-08)

- **Function trigger:**
    - Action Path: On Inspector Console (SCR-14 / SCR-16), in the post-flight checklist, submit the flight telemetry log and click "Hoàn thành nhiệm vụ thực địa" (Finish Field Inspection).
- **Function description:**
    - Actors / Roles: Primary: Inspector/Pilot. Read-Only: Mission Manager.
    - Purpose: Conclude field flight operations; upload the verified flight telemetry log (GPS flight track, flight duration, maximum altitude, minimum battery level); transition mission status to Field Completed (or Completed); reset UAV operational status to Idle; release calendar ResourceBookings.
    - Interface: Post-flight closure card on SCR-16 with flight log metrics form and completion confirmation button.
    - Data Processing: Verifies mission is InProgress; validates flight log metrics (duration >0, altitude ≤120m, battery 0–100%, valid GPS track JSON) (BR-041); checks completion prerequisites: all targets must be Inspected or Skipped (BR-055); persists FlightLog record; sets Mission.Status to FieldCompleted (or Completed); records ActualEnd timestamp; resets Uav.FlightStatus to Idle; marks ResourceBookings as Released; broadcasts SignalR completion event.

**Screen layout:**

    - Screen ID: SCR-16: Post-Flight Summary & Mission Completion Console
    - Route: /missions/:id/inspector (Post-flight Section)
    - (Screen mockup will be inserted here)

**Function Details:**

    - Data Elements and Input Validation:

***Table 20.*** *Finish field inspection data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| missionId | UUID | Path Variable | Required. Mission must be InProgress (BR-045). |
| flightDurationMinutes | Integer | Number Input | Required. Must be strictly positive (>). |
| maxAltitudeMeters | Double | Number Input | Required. Must be >0 and ≤120.0 meters (BR-032). |
| minBatteryPercentage | Integer | Number Input | Required. Range: 0 to 100%. |
| gpsTrackJson | Text / JSON File | File Upload | Required. Valid JSON array of GPS track points. |

- Business Rules Applied:
    - BR-032: Flight altitude must not exceed 120 meters above ground level.
    - BR-045: Flight log submission is mandatory for completing field flight operations.
    - BR-055: Field completion requires every target tower to be either Inspected or Skipped, and all pending emergency alerts resolved.
- Normal Flow:
    - Drone lands safely. Pilot opens /missions/:id/inspector.
    - Pilot enters flight metrics (duration, max altitude, ending battery) and uploads flight log file.
    - Pilot clicks "Nộp nhật ký & Hoàn thành nhiệm vụ".
    - System validates metrics, saves FlightLog entity, sets Mission.Status to FieldCompleted, and updates Uav.FlightStatus to Idle.
    - Active ResourceBookings are marked Released; toast MSG-310 is displayed.
- Alternative & Exception Flows:
    - E1 (Uninspected Targets Remaining): If one or more targets remain Pending without being Skipped, completion is blocked with TARGETS\_INCOMPLETE (MSG-309).
    - E2 (Invalid Flight Log Metrics): Altitude >120m or invalid GPS JSON returns FLIGHT\_LOG\_INVALID\_METRICS.

##### 3.4.7 Periodic Telemetry Ingestion Service (NS-04) 

- **Function trigger:**
    - Timing / Frequency: Autonomous background worker ingesting periodic telemetry messages and caching status snapshots.
- **Function description:**
    - Actors / Roles: Background Daemon Worker (Operations Service).
    - Purpose: Consume periodic telemetry messages published by flying UAVs; validate aircraft registration; maintain latest live flight states in Redis; and forward telemetry frames to connected web clients via SignalR.
    - Interface: Non-screen MQTT network client (ManagedMqttClient / MQTTnet).
    - Data Processing: Extracts droneCode from topic; checks that droneCode exists in database; validates sensor ranges; stores the latest flight telemetry snapshot; checks mission binding: if drone is bound to an active mission, relays telemetry to mission monitoring dashboards.
- **Screen layout:** \[Non-Screen Function\]
    - Function ID: NS-04: Periodic Telemetry Consumer
- **Function Details:**
    - Business Rules Applied:
        - BR-047: Telemetry is accepted only from registered UAVs assigned to InProgress or Suspended missions.

#### 3.5 MF04 – AI Inspection and Defect Detection

The AI Inspection and Defect Detection module (MF04) constitutes the automated visual analysis, defect evaluation, expert human review, and maintenance execution pipeline of UAV-PMS. The module ingests high-resolution aerial imagery and video captured during field missions, performs automated cryptographic hash deduplication and EXIF geospatial parsing, and publishes asynchronous inference jobs to RabbitMQ. The Cloud AI Service executes deep object detection models (YOLO11, RT-DETR), evaluates detections through an internal gRPC severity engine, and presents results in interactive review workspaces. Certified Analysts review, confirm, reject, or correct detections. Confirmed physical defects automatically spawn maintenance work orders (maintenance tickets) for assigned technicians, complete with photographic proof verification, material tracking, and supervisory closure.

##### 3.5.1 Submit Inspection Data (UC-MF04-01)

- **Function trigger:**
    - Mission Upload Path: In Mission Detail (/missions/:id, SCR-12), select the "Tải dữ liệu" tab (Tab 2: upload); or click "Upload Media" on Inspector Console (SCR-14).
    - Standalone Trial Path: From sidebar, navigate to AI Analysis (/ai-analysis/upload, SCR-19).
    - Direct Route: /missions/:id (activeTab=upload) or /ai-analysis/upload.
- **Function description:**
    - Actors / Roles: Primary: Inspector/Pilot, Mission Manager. Secondary: System Administrator.
    - Purpose: Upload raw high-resolution inspection media (photographs and video footage) captured by the UAV; extract EXIF metadata (capture timestamp, GPS latitude, longitude, altitude); perform cryptographic SHA-256 deduplication to prevent redundant storage; link media to target transmission towers; and automatically enqueue inspection jobs for server-side AI inference (includes UC-MF04-02).
    - Interface: Drag-and-drop media upload dropzone on SCR-12 / SCR-19 with thumbnail grid, EXIF metadata chips, tower binding dropdown, and upload progress bars.
    - Data Processing: Validates file mime-type (JPEG, PNG, TIFF, WebP, MP4) and file size (≤50 MB per file, BR-048); computes SHA-256 hash: if duplicate hash exists for this mission, skips ingestion (BR-058); extracts embedded EXIF DateTimeOriginal and GPS coordinates; stores file in object storage; persists InspectionMedia record; inserts outbox event MediaUploaded; transitions media status to PendingAnalysis; triggers background AI queueing.

**Screen layout:**

    - Screen ID: SCR-19 / SCR-12: Inspection Media Upload & EXIF Extraction Console
    - Route: /missions/:id (Upload tab) & /ai-analysis/upload
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 21.*** *Submit inspection data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| missionId | UUID | Path Variable | Required for mission uploads. Mission must be InProgress or FieldCompleted (BR-049). |
| towerId | UUID | Dropdown Select | Optional. Must refer to a valid target tower within the mission corridor. |
| files | Multipart / Binary | File Input | Required. Allowed extensions: .jpg, .jpeg, .png, .webp, .tiff, .mp4 (BR-048). |
| fileSize | Long | File System | Maximum 50 MB per file (BR-048). |
| checksum | String (SHA-256) | Server Computed | Unique per mission; duplicate files are discarded (BR-058). |

- Business Rules Applied:
    - BR-048: Supported media formats are strictly JPEG, PNG, TIFF, WebP, and MP4; file size limit is 50 MB.
    - BR-049: Media must be associated with the mission and preserve immutable capture timestamp and GPS geolocation.
    - BR-056: Only an assigned Inspector or Mission Manager may submit inspection media for analysis.
    - BR-057: Media must pass integrity validation, mission authorization, and readable content checks.
    - BR-058: Identical files (matching SHA-256 hash) within the same mission are rejected to prevent duplicate AI compute.
- Normal Flow:
    - Inspector selects the target tower (or leaves blank for corridor sweep) and drops image files onto SCR-12 (Upload tab).
    - Client reads files, previews thumbnails, and sends multipart POST request to /missions/{id}/media.
    - Backend validates mime-types and file sizes, checks user permissions, and computes SHA-256 hashes.
    - Server reads EXIF metadata (timestamp, GPS); uploads binaries to object storage.
    - Server saves InspectionMedia rows, writes MediaUploaded event to OutboxMessages, and commits transaction.
    - System displays upload success toast MSG-306; files appear in "Xử lý dữ liệu" (Processing) tab as Pending.
- Alternative & Exception Flows:
    - A1 (EXIF Geolocation Missing): If image lacks EXIF GPS coordinates, system assigns the coordinates of the selected target tower.
    - E1 (Unsupported Format or Oversized File): Uploading files >50 MB or invalid extensions returns INVALID\_MEDIA\_FORMAT (MSG-307).
    - E2 (Duplicate Checksum Detected): File matching an existing hash is skipped with DUPLICATE\_MEDIA\_SKIPPED (MSG-409).

##### 3.5.2 Process AI Analysis (UC-MF04-02)

- **Function trigger:**
    - Automated Trigger: Outbox Dispatcher (NS-05) detects MediaUploaded event and publishes message to RabbitMQ exchange.
    - Consumer Trigger: AI Request Publisher (NS-07) consumes event, creates AnalysisJob, and publishes to Cloud AI queue.
- **Function description:**
    - Actors / Roles: Primary: System Worker (AI Inspection Service). Secondary: Cloud AI Service (FastAPI / PyTorch), Inspection Evaluation Service (gRPC).
    - Purpose: Asynchronously execute automated deep object detection inference on inspection images and video frames; detect and localize power line anomalies across canonical defect classes; invoke gRPC severity scoring; and record structured defect predictions.
    - Interface: Non-screen AMQP message pipeline with durable RabbitMQ queues and internal gRPC channels.
    - Data Processing: Consumes analysis request; dispatches to worker queue (ai.analysis.server.image.requested); Cloud AI worker loads image, executes deep detection model (YOLO11, RT-DETR), and emits detection list (defect class, confidence, bounding box coordinates; NS-08 consumes results; validates confidence and bounding box bounds (BR-061); invokes internal gRPC service (NS-10) to compute severity, risk score, and priority; persists DetectedAnomaly entities; updates AnalysisJob status to Completed; emits SignalR status event.
- **Screen layout:** \[Non-Screen Backend Pipeline Function\]
    - Function ID: NS-07 / NS-08 / NS-10: Automated AI Inference & Evaluation Pipeline
    - Note: State transitions (Pending →Processing → Completed | Failed) and detection badges stream dynamically to SCR-12 (Processing & Results tabs).
- **Function Details:**
    - Canonical AI Detection Classes and Defect Mapping (Section 5.4.3):

***Table 22.*** *AI model defect classification and default severity mapping.*

| **Class Code** | **Category Name** | **Vietnamese Description** | **Default Severity** | **High Voltage Critical** |
| :---: | :---: | :---: | :---: | :---: |
| INSULATOR\_BROKEN | Broken Insulator | Cách điện vỡ / rạn nứt | High | Yes |
| FLASHOVER\_DAMAGE | Flashover Damage | Vết phóng điện / cháy xém | Critical | Yes |
| BIRD\_NEST | Bird Nest | Tổ chim hành lang an toàn | Medium | Conditional |
| FOREIGN\_OBJECT | Foreign Object | Dị vật vướng mắc (diều, bạt) | Medium | Conditional |
| CONDUCTOR\_BROKEN | Broken Strand / Conductor | Tưa sợi / Đứt dây dẫn | Critical | Yes |
| TOWER\_CORROSION | Tower Rust / Corrosion | Rỉ sét / Ăn mòn kết cấu | Low / Medium | No |

- Business Rules Applied:
    - BR-059: AI analysis is strictly asynchronous; each job maintains a correlation ID and trackable status.
    - BR-060: Detections are mapped to canonical defect classes; unrecognized labels are flagged as Pending Review.
    - BR-061: Confidence must be in \[0.0, 1.0\] and bounding box must lie within normalized image boundaries \[0,0,1,1\].
    - BR-062: Preliminary severity and risk levels are assigned algorithmically by the evaluation service.
    - BR-063: Transient model inference failures are retried up to 3 times before transitioning to Failed.
    - BR-064: Callbacks must carry valid correlation IDs; duplicate responses are ignored idempotently.
    - AMQP Queues: ai.analysis.server.image.requested, ai.analysis.result, ai.analysis.result.dlq
    - Internal gRPC: InspectionEvaluation.EvaluationEngineClient.EvaluateDetectionAsync(...)

##### 3.5.3 Monitor AI Processing Status (UC-MF04-03)

- **Function trigger:**
    - Navigation Path: In Mission Detail (/missions/:id, SCR-12), select the "Xử lý dữ liệu" tab (Tab 3: processing).
    - Direct Route: /missions/:id (activeTab=processing).
- **Function description:**
    - Actors / Roles: Primary: Mission Manager, Analyst, Inspector/Pilot.
    - Purpose: Provide operational users with live visibility into asynchronous AI inference job queues, displaying processing states (Pending, Processing, Completed, Failed), model version tags, inference runtimes, detection counts, and error retry diagnostics.
    - Interface: AI Job Monitor Table on SCR-12 featuring real-time status pills, progress spinners, detection count badges, and error inspection alerts.
    - Data Processing: Loads AnalysisJob records for the mission; listens to SignalR event AiAnalysisStatusChanged; dynamically updates status badges without full page reloads; when a job transitions to Completed, automatically triggers re-querying of detection counts and enables the "Kết quả giám định" (Results) tab.

**Screen layout:**

    - Screen ID: SCR-12: AI Job Processing Queue & Execution Monitor
    - Route: /missions/:id (Processing Tab)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Business Rules Applied:
        - BR-059: Live tracking of correlation IDs and job execution states.
        - BR-063: Failed jobs display diagnostic error reason and retry history.

##### 3.5.4 View AI Analysis Results (UC-MF04-04)

- **Function trigger:**
    - Navigation Path A: In Mission Detail (/missions/:id, SCR-12), select the "Kết quả giám định" tab (Tab 4: results).
    - Navigation Path B: In Analyst Review (/ai-review, SCR-21), click on any detection row to open the Inspection Viewer (SCR-20).
    - Direct Route: /missions/:id (activeTab=results) or /ai-review/:id.
- **Function description:**
    - Actors / Roles: Primary: Analyst, Mission Manager. Read-Only: Inspector/Pilot, UAV Technician.
    - Purpose: Inspect visual defect localization overlays on high-resolution aerial imagery; examine detected defect classes, confidence scores, algorithmic severity ratings, and risk levels; filter detections by category and severity; and review spatial component crops.
    - Interface: Interactive High-Resolution Inspection Canvas (SCR-20) featuring zoom/pan controls, toggleable SVG/Canvas bounding box overlays, color-coded severity tags, and defect attribute inspection sidebars.
    - Data Processing: Fetches DetectedAnomaly records with related InspectionMedia; scales normalized bounding box coordinates \[x,y,w,h\] to image resolution; renders color-coded bounding boxes (Critical in red, High in orange, Medium in yellow, Low in blue); computes detection summary metrics; filters by validation status (PendingReview, Approved, Rejected).

**Screen layout:**

    - Screen ID: SCR-20 / SCR-12: Interactive AI Defect Inspection Viewer
    - Route: /missions/:id (Results Tab) & /ai-review/:id
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:
        - Read-only projection of DetectedAnomalyDto joined with DefectCategoryDto and TowerDto.
    - Business Rules Applied:
        - BR-002: Access governed by user's assigned geographic scope and mission membership.
        - BR-060 / BR-061: Validated bounding box geometry and canonical defect category mapping.
        - BR-068: Display of algorithmic severity and risk scores computed by the inspection evaluation engine.
        - Image Storage: Static File Server / MinIO Object Storage with signed URLs.

##### 3.5.5 Review AI Detections (UC-MF04-05)

- **Function trigger:**
    - Navigation Path: From main sidebar, select AI Review (/ai-review, SCR-21) to view the queue of pending anomalies, then click "Thẩm định" on any detection row to open the Review Workspace (/ai-review/:id, SCR-20).
    - Direct Route: /ai-review/:id (SCR-20).
- **Function description:**
    - Actors / Roles: Primary: Analyst. Secondary: Mission Manager, System Administrator.
    - Purpose: Execute rigorous technical human-in-the-loop review of AI defect detections; confirm physical defects (which automatically triggers the creation of a maintenance work ticket, BR-055); reject false positives with documented reason; or correct misclassified defect categories or severity ratings.
    - Interface: Dedicated Analyst Review Workspace (SCR-20) featuring side-by-side evidence inspection, category adjustment selectors, severity override sliders, notes input, and decision action buttons.
    - Data Processing: Verifies reviewer has Analyst role and regional authority (BR-066); on Confirm: updates anomaly status to Approved, records reviewer ID and timestamp, invokes MaintenanceTicketService to automatically generate a MaintenanceTicket entity assigned to the mission technician, and emits MaintenanceTaskCreated event (BR-055); on Reject: updates status to Rejected and records mandatory justification (BR-065); audits action as DETECTION\_APPROVED or DETECTION\_REJECTED.

**Screen layout:**

    - Screen ID: SCR-20 / SCR-21: Technical Defect Review & Maintenance Ticket Trigger
    - Route: /ai-review/:id
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 23.*** *Review AI detections data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| detectionId | UUID | Path Variable | Required. Anomaly must exist and belong to an accessible mission. |
| reviewStatus | Enum | Button Click | Required. Values: Approved, Rejected. |
| categoryOverride | String | Dropdown Select | Optional. If AI misclassified defect, analyst selects correct canonical category (BR-065). |
| severityOverride | Enum | Dropdown Select | Optional. Values: Low, Medium, High, Critical. |
| notes | String (1000) | Textarea | Mandatory if reviewStatus == Rejected; optional if Approved (BR-065). |

- Business Rules Applied:
    - BR-054 / BR-055: A confirmed defect automatically generates a maintenance ticket with priority and due date matching defect severity, assigned to the mission technician.
    - BR-065: Rejecting or correcting a detection strictly requires documented technical justification; the original AI model prediction is immutably preserved.
    - BR-066: Only an authenticated Analyst may sign off on defect review outcomes; re-reviewing an existing decision is audited.
    - BR-068: High or Critical defects trigger automated email alerts to the regional Mission Manager.
- Normal Flow (Confirm Defect):
    - Analyst opens /ai-review, filters for Pending Review, and selects detection \#104.
    - Analyst inspects high-resolution evidence, verifies that broken insulator is genuine, and confirms severity is High.
    - Analyst enters operational notes: "Insulator disc fracture confirmed on phase A tension string."
    - Analyst clicks "Xác nhận khuyết tật".
    - Backend marks DetectedAnomaly as Approved, sets ReviewerId and ReviewTime, updates tower risk profile, and automatically creates MaintenanceTicket
    - System displays success banner MSG-405 with direct link to the newly generated maintenance ticket.
- Alternative & Exception Flows:
    - A1 (Reject False Positive): Analyst determines image artifact or sun glare was detected. Analyst clicks "Bác bỏ", enters rejection notes, and confirms. System sets status Rejected. No maintenance ticket is created.
    - E1 (Missing Notes on Rejection): Attempting to reject without entering notes returns REJECTION\_NOTES\_REQUIRED.

##### 3.5.6 Request Media Reanalysis (UC-MF04-06)

- **Function trigger:**
    - Action Path: In Detection Review (/ai-review/:id, SCR-20) or Mission Detail (/missions/:id, SCR-12), click the "Yêu cầu phân tích lại" (Reanalyze) action button to open modal SCR-22.
- **Function description:**
    - Actors / Roles: Primary: Analyst, Mission Manager. Secondary: Cloud AI Service.
    - Purpose: Request a secondary AI inference run on an existing inspection image or video file using an alternate detection model architecture (e.g. RT-DETR vs. YOLO11s) or specialized analysis parameters to resolve ambiguous visual findings.
    - Interface: Reanalysis Configuration Modal Dialog (SCR-22).
    - Data Processing: Verifies that media exists and caller has authority (BR-066); validates model selection; creates a new AnalysisJob record (version N+) without deleting previous detection history (BR-067); publishes request to RabbitMQ; updates media status to PendingAnalysis.

**Screen layout:**

    - Screen ID: SCR-22: Request Media Reanalysis Modal Dialog
    - Route: /ai-review/:id (Modal Dialog)
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Business Rules Applied:
        - BR-066 / BR-067: Reanalysis creates a new job revision and never overwrites or purges prior analysis versions; duplicate active requests are blocked.

##### 3.5.7 View Defect Report and Export (UC-MF04-07)

- **Function trigger:**
    - Navigation Path: From main navigation sidebar, select Reports (/reports, SCR-23).
    - Direct Route: /reports (SCR-23).
- **Function description:**
    - Actors / Roles: Primary: Mission Manager, Analyst, System Administrator. Read-Only: Inspector, Technician.
    - Purpose: Compile, review, submit for managerial approval, and export formal engineering inspection defect reports across towers,, and temporal ranges in standard PDF and Excel formats.
    - Interface: Comprehensive Reports & Analytics Console (SCR-23) featuring KPI status bars, filter toolbars, report generation wizards, and export buttons.
    - Data Processing: Aggregates confirmed defects, tower geolocations, inspection flight metadata, and repair recommendations; supports report lifecycle states: Draft → Pending → Approved | Rejected; generates formatted PDF document (executive summary, defect tables, evidence crops, engineering sign-off) and Excel spreadsheets; provides secure download endpoints.

**Screen layout:**

    - Screen ID: SCR-23: Defect Reports & Operational Analytics Console
    - Route: /reports
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 24.*** *View defect report and export data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| title | String (255) | Text Input | Required. Non-empty, 5–255 characters. |
| reportType | Enum | Dropdown Select | Required. Values: DefectReport, PeriodicInspection, CorridorOverview, ThermalInspection. |
| regionId | UUID | Dropdown Select | Required. Must belong to manager's authorized scope. |
| missionIds | List<UUID> | Multi-select | Required. At least 1 completed mission. |
| dateFrom / dateTo | ISO Date | Date Range Picker | Required. dateFrom must precede dateTo. |
| status | Enum | State Badge | Lifecycle: Draft, Pending, Approved, Rejected. Only Manager can approve (BR-059). |

- Business Rules Applied:
    - BR-059: Any user may create and submit a draft report; only an authorized Mission Manager may approve or reject a report.
    - BR-060 / BR-069: Reports and data exports enforce regional geographic boundaries and role-based permissions.
- Normal Flow (Create & Export):
    - User opens /reports and clicks "Lập báo cáo mới".
    - User enters Report Title, selects Type == DefectReport, specifies Region, selects completed missions, and clicks "Lưu bản nháp".
    - User reviews report content and clicks "Gửi duyệt". Status transitions to Pending.
    - Mission Manager reviews findings and clicks "Phê duyệt" (Approve). Status becomes Approved (MSG-411).
    - User clicks "Xuất PDF" or "Xuất Excel". Backend generates formatted document and initiates browser download (MSG-412).
    - Export Engine: QuestPDF (vector PDF generation) and ClosedXML (Excel workbook generation).

##### 3.5.8 Manage Maintenance Ticket (UC-MF04-08)

- **Function trigger:**
    - Automated Trigger: Generated automatically upon defect confirmation in UC-MF04-05 (BR-055).
    - Navigation Path A: In Mission Detail (/missions/:id, SCR-12), select the "Phiếu bảo trì" tab (Tab 6: maintenance).
    - Navigation Path B: From main sidebar, select Maintenance (/maintenance, SCR-25).
    - Direct Route: /maintenance/:id (SCR-25).
- **Function description:**
    - Actors / Roles: Primary: UAV Technician, Mission Manager. Secondary: System Administrator.
    - Purpose: Manage the complete field maintenance and physical repair lifecycle for confirmed infrastructure defects; track technician assignments, work order stages (Open → Assigned → InProgress → PendingVerification → Closed, or Cancelled), photographic repair proof upload (before, during, and after repair), material replacement logs, and supervisory verification sign-off.
    - Interface: Dedicated Maintenance Ticket Workspace (SCR-25) featuring status progression timeline, defect evidence preview, proof photo uploader, material log table, and verification controls.
    - Data Processing: Verifies technician assignment or manager permissions (BR-056); on Start: updates status to InProgress and records actual start; on Upload Proof: stores photographic evidence categorized by stage (BeforeRepair, DuringRepair, AfterRepair) (BR-057); on Log Materials: records material name, quantity, and unit; on Request Verification: transitions status to PendingVerification; on Close: verifies that mandatory "AfterRepair" photographic proof exists (BR-057), marks ticket Closed, sets defect status to Resolved, and emits completion notifications.

**Screen layout:**

    - Screen ID: SCR-25: Maintenance Work Order & Field Repair Workspace
    - Route: /maintenance/:id & /maintenance
    - (Screen mockup will be inserted here)
- **Function Details:**
    - Data Elements and Input Validation:

***Table 25.*** *Manage maintenance ticket data elements and input validation.*

| **Field Name** | **Type** | **UI Control** | **Validation & Constraints** |
| :---: | :---: | :---: | :---: |
| ticketId | UUID | Path Variable | Required. Ticket must exist. |
| status | Enum | Lifecycle Stepper | Lifecycle: Open → Assigned → InProgress → PendingVerification → Closed, or Cancelled from Open/Assigned (Table 43). |
| assignedTechnicianId | UUID | Dropdown Select | Required. Must refer to an active user with Technician role. |
| priority | Enum | Badge / Select | Values: Low, Medium, High, Critical. Inherited from defect severity (BR-055). |
| dueDate | ISO DateTime | Date Picker | Required. Computed based on defect priority level (Critical, High, Medium). |
| proofImages | List<File> | File Upload | Mandatory for closure. Must include at least 1 image tagged AfterRepair (BR-057). |
| materials | List<MaterialItem> | Data Form Grid | Optional. List of items: materialName, quantity, unitOfMeasure. |

- Business Rules Applied:
    - BR-055: A confirmed defect automatically spawns a maintenance ticket with priority and due date matching defect severity.
    - BR-056: Tickets adhere strictly to the sequential lifecycle: Open → Assigned → InProgress → PendingVerification → Closed, with cancellation allowed only from Open or Assigned (Table 43).
    - BR-057: Closing a maintenance ticket strictly requires photographic proof of completed repair (AfterRepair image).
    - BR-058: Closing a ticket updates the parent DetectedAnomaly to Resolved and logs the resolution timestamp.
- Normal Flow (Technician Execution & Closure):
    - Technician opens /maintenance, views "My Tickets", and selects assigned ticket MT-20261106-004.
    - Technician arrives at site and clicks "Bắt đầu sửa chữa". Status transitions to In Progress.
    - Technician completes physical repair (e.g., replaces fractured insulator string).
    - Technician uploads photographic proof tagged as "Sau sửa chữa" (AfterRepair) with notes, and logs materials used.
    - Technician clicks "Yêu cầu nghiệm thu". Status becomes Pending Verification.
    - Mission Manager reviews uploaded proof photos and clicks "Nghiệm thu & Đóng phiếu".
    - Backend transitions ticket to Closed, sets resolution time, marks associated anomaly as Resolved, and displays toast MSG-408.
- Alternative & Exception Flows:
    - E1 (Missing Proof Photo on Close): Attempting to close without an AfterRepair proof image returns PROOF\_PHOTO\_REQUIRED (MSG-409).
    - E2 (Modifying Closed Ticket): Attempting to modify a Closed ticket returns TICKET\_ALREADY\_CLOSED (MSG-410).

##### 3.5.9 AI Request Publisher & Result Consumer Service (NS-07, NS-08)

- **Function trigger:**
    - Publisher (NS-07): Ingestion of media uploaded event from RabbitMQ topic exchange uavpms.events.
    - Consumer (NS-08): Ingestion of model inference results from queue ai.analysis.result.
- **Function description:**
    - Actors / Roles: Asynchronous Message Queue Consumers (AI Inspection Service).
    - Purpose: Orchestrate reliable, decoupled message queuing between the .NET microservice core and the Python AI inference engine; manage job scheduling, result deserialization, error handling, and dead-letter queue (DLQ) failover.
    - Interface: Asynchronous RabbitMQ AMQP message workers.
    - Data Processing: NS-07 creates AnalysisJob and publishes job payload to appropriate queue; NS-08 consumes JSON detection payloads; validates schema; on transient error (timeout, network hiccup), retries up to 3 times with 15-second exponential backoff; if retries exhausted, routes message to ai.analysis.result.dlq and marks job Failed (BR-063).
- **Screen layout:** \[Non-Screen Function\]
    - Function ID: NS-07 / NS-08: AI Message Queue Publisher & Consumer
- **Function Details:**
    - Business Rules Applied:
        - BR-059 / BR-063: Asynchronous job queueing, retry limits, and dead-letter queue isolation.

##### 3.5.10 Inspection Severity & Risk Evaluation gRPC Service (NS-10)

- **Function trigger:**
    - Service Trigger: Synchronous internal gRPC call executed by AI Result Consumer (NS-08) upon receiving raw model detections.
- **Function description:**
    - Actors / Roles: Internal High-Performance Microservice (Inspection Evaluation Service).
    - Purpose: Compute standardized numerical severity scores, categorical severity ratings (Low, Medium, High, Critical), risk levels, and automated emergency alert decisions for individual defect bounding boxes based on electrical infrastructure safety rules.
    - Interface: High-speed binary gRPC channel over HTTP/2 (Proto contract: evaluation.proto).
    - Data Processing: Inputs defect class code, confidence score, bounding box dimensions, and tower voltage rating (kV); executes decision matrix (Section 5.4.3); returns evaluated severity, priority score, and boolean flag requiresImmediateAlert.
- **Screen layout:** \[Non-Screen Function\]
    - Function ID: NS-10: Inspection Severity Evaluation gRPC Service
- **Function Details:**
    - Business Rules Applied:
        - BR-062 / BR-068: Standardized severity mapping and immediate alert classification.
        - Proto Service: InspectionEvaluation.EvaluationEngine
        - Implementation Class: EvaluationServiceImpl in UavPms.InspectionEvaluationService.

### 4. Non-Functional Requirements

#### 4.1 External Interfaces

Table 26 lists the software interfaces between UAV-PMS and the actors in Figure 1, and the internal infrastructure the system depends on. Table 27 lists the hardware of the team-built UAV that UAV-PMS communicates with.

***Table 26.*** *Software interfaces.*

| **ID** | **Interface** | **Protocol** | **Requirement** |
| :---: | :---: | :---: | :---: |
| UI-01 | Web client (Angular) | HTTPS, JSON REST; SignalR for real-time updates | Used by all human actors for every screen in Table 3. |
| EXT-01 | Cloud AI Service | RabbitMQ (AMQP) request; HTTPS callback with shared token | Asynchronous; every request and callback carries the job correlation ID. |
| EXT-02 | Map Service | Leaflet with OpenStreetMap tiles (HTTPS) | Base map only; UAV-PMS is the source of truth for tower and asset coordinates (WGS 84). |
| EXT-03 | Notification Provider | SMTP over TLS | Email delivery; the in-app notification is stored even if email fails. |
| INT-01 | API Gateway | Ocelot, HTTPS | Single entry point for the web client; forwards the access token and correlation ID to the services. |
| INT-02 | Message broker | RabbitMQ (AMQP) | Durable queues for notifications, AI jobs; consumers are idempotent. |
| INT-03 | Cache | Redis | Caches reference data and SignalR connections; a cache failure must not change stored data. |
| INT-04 | Database | PostgreSQL with PostGIS | Stores all business and geospatial data; schema managed by EF Core migrations. |


***Table 27.*** *Hardware interfaces of the team-built UAV.*

| **ID** | **Component** | **Connection to UAV-PMS** | **Requirement** |
| :---: | :---: | :---: | :---: |
| HW-01 | Flight Controller | USB / UART (MSP / MAVLink protocol) | Pixhawk 4 / STM32F405 controller; pairs hardware UID and streams live telemetry (orientation, battery, status) to ground control. |
| HW-02 | Telemetry & RC Transceiver | Serial UART / 4G LTE Companion Link | 433/915 MHz radio & cellular link; transmits bi-directional telemetry and command overrides up to 5 km range. |
| HW-03 | Camera Payload | CSI / USB Gimbal Interface | 4K / 20MP RGB and radiometric thermal sensor; captures inspection imagery with synchronized GPS coordinates and timestamps. |
| HW-04 | GNSS / GPS Module | UART / I2C connection to Flight Controller | Multi-constellation GNSS (GPS, GLONASS, Galileo); provides accurate geospatial positioning for telemetry and EXIF metadata. |

#### 4.2 Quality Attributes

##### 4.2.1 Usability

***Table 28***. *Usability requirements**.*

| **ID** | **Requirement** |
| :---: | :---: |
| NFR-USA-01 | A trained user reaches any MF01–MF04 primary screen from SCR-02 in at most three clicks. |
| NFR-USA-02 | Validation errors name the field and the corrective action (Section 5.3); stack traces are never shown to users. |
| NFR-USA-03 | Uploads and AI jobs show their state (Queued, Processing, Completed, Failed); reloading the page does not lose the state. |
| NFR-USA-04 | Cancel, suspend, reject and override actions require confirmation (MSG-012) and a reason. |
| NFR-USA-05 | The web client works on the current stable Chrome and Edge; SCR-14 and SCR-16 are usable on a 360-pixel-wide mobile screen for field use. |

##### 4.2.2 Reliability

***Table 29***. *Reliability requirements**.*

| **ID** | **Requirement** |
| :---: | :---: |
| NFR-REL-01 | Mission, assignment and booking changes are transactional; a failed operation leaves no partial booking (BR-036). |
| NFR-REL-02 | Message consumers and AI callbacks are idempotent: processing the same message twice gives the same result. |
| NFR-REL-03 | Transient AI failures are retried at most three times; failed messages are moved to a dead-letter queue visible to the administrator. |
| NFR-REL-04 | The system is available 99% of the time during scheduled test and demonstration windows. |
| NFR-REL-05 | The database is backed up daily; restore is tested once before the release freeze (RPO 24 hours, RTO 4 hours). |

##### 4.2.3 Performance

***Table 30***. *Performance requirements*.

| **ID** | **Requirement** |
| :---: | :---: |
| NFR-PER-01 | With 50 concurrent users, 95% of read requests complete within 2 seconds and 95% of write requests within 3 seconds (excluding file transfer). |
| NFR-PER-02 | An upload is acknowledged within 5 seconds after the file transfer ends. |
| NFR-PER-03 | A new AI job is visible as Queued within 3 seconds; a received callback is visible on SCR-19 within 5 seconds. |
| NFR-PER-04 | An critical alert is stored and shown to the manager within 5 seconds after the server receives it; email delivery time is measured separately. |
| NFR-PER-05 | Lists are paginated with 20 rows by default and at most 100 rows per page. |

##### 4.2.4 Security and Privacy

***Table 31***. *Security and privacy requirements.*

| **ID** | **Requirement** |
| :---: | :---: |
| NFR-SEC-01 | All external traffic uses TLS. Credentials and signing keys are not stored in source control. |
| NFR-SEC-02 | Passwords are stored with a one-way password-hashing algorithm; access tokens expire after 60 minutes. |
| NFR-SEC-03 | Every protected API checks the user’s role and region scope on the server (BR-001). |
| NFR-SEC-04 | Uploaded files are checked for type and size before storage; user-supplied file names are never used as storage paths. |
| NFR-SEC-05 | AI callbacks without valid credentials are rejected and logged. |
| NFR-SEC-06 | Passwords, tokens and keys are never written to logs; administrative and security actions are audited (BR-007). |
| NFR-SEC-07 | The login API is rate-limited and returns HTTP 429 when the limit is exceeded. |

##### 4.2.5 Maintainability, Observability and Compatibility

***Table 32***. *Maintainability, observability and compatibility requirements.*

| **ID** | **Requirement** |
| :---: | :---: |
| NFR-MNT-01 | Each service writes structured logs with timestamp, level, service name, correlation ID and message. |
| NFR-MNT-02 | Each service exposes a /health endpoint that returns HTTP 200 when it is running. |
| NFR-MNT-03 | Changes to API request or response formats stay backward compatible or are released as a new API version. |
| NFR-MNT-04 | Database schema changes are made only through version-controlled EF Core migrations. |
| NFR-MNT-05 | A release is accepted only if the solution builds without errors and all P0/P1 test cases pass. |
| NFR-MNT-06 | Business thresholds are configuration values and can be changed without code changes (BR-010). |

##### 4.2.6 AI Model Quality

***Table*** ***33.* *AI model quality requirements.*

| **ID** | **Requirement** |
| :---: | :---: |
| NFR-AI-01 | Every deployed model is evaluated on the fixed, versioned test split of the six-class dataset and reported with precision, recall, mAP50, mAP50-95 and per-class results (Report 2, Table 2). |
| NFR-AI-02 | Each detection stores the model name and version that produced it, so results can be traced to an evaluation report. |
| NFR-AI-03 | The Server/Deep analysis of one image completes within 5 seconds under normal load, excluding queue waiting time. |
| NFR-AI-04 | The final accuracy targets of the Server/Deep models are set from the AI evaluation results and recorded in Part III before the release freeze. |

### 5. Requirement Appendix

#### 5.1 Business Rules

Business rules are grouped by function area. The use cases that apply each rule are given in their specification tables in Sections 3.2–3.5.

***Table 34.*** *Business rules.*

| **ID** | **Rule Definition** |
| :---: | :---: |
| – | General |
| BR-001 | Only authenticated users with an Active account may use protected functions. The server checks the user’s role (Table 4) and region scope; an Inspector may access only missions in which they are assigned. |
| BR-002 | Each user has exactly one role and one or more authorized regions. |
| BR-003 | An account is locked for 15 minutes after 5 consecutive failed logins. |
| BR-004 | Records referenced by history (users, assets, UAVs, categories) are deactivated, never deleted. |
| BR-005 | Status changes follow the state tables in Section 5.4.2; other transitions are rejected. |
| BR-006 | Every update carries the record version; an update on an older version is rejected. |
| BR-007 | Material actions are audited with actor, time, target, old value, new value and reason. |
| BR-008 | A Mission Manager override (confirm Needs Review as Ready, finish with an unresolved alert) requires a reason and is audited. |
| BR-009 | Times are stored in UTC and displayed in the configured time zone (default Asia/Ho\_Chi\_Minh). |
| BR-010 | Business thresholds are configuration values (Table 37), not hard-coded. |
| – | Assets, personnel and UAVs |
| BR-011 | Coordinates use WGS 84 (EPSG:4326); latitude is between −90 and 90 and longitude between −180 and 180. Each tower belongs to one power line and each line to one region. |
| BR-012 | A tower code is unique within its power line; an asset code is unique within its tower. |
| BR-013 | A UAV status is Available, In Maintenance or Retired; only an Available UAV can be booked. |
| BR-014 | A deactivated user cannot be assigned; their future assignments are flagged for reassignment. |
| – | MF01 – Readiness |
| BR-016 | Only a Mission Manager authorized for the region may create or evaluate an assessment. |
| BR-017 | An assessment has at least one asset and a time window whose start is in the future and before its end. |
| BR-018 | All assets in an assessment belong to its region. |
| BR-019 | An Inspector is eligible if Active, authorized for the region and not booked in the window. |
| BR-020 | A resource is booked if any active booking overlaps the requested interval. |
| BR-021 | A UAV is eligible if Available, not booked, and its latest technical inspection is Passed and valid at the planned start. |
| BR-022 | A technical inspection is valid for 72 hours after it is recorded. |
| BR-023 | A technical inspection fails if any mandatory checklist item or controller metric fails. |
| BR-024 | Manually entered controller metrics require a justification and make the UAV component Needs Review. |
| BR-025 | Site conditions are acceptable when wind speed ≤ 10 m/s, there is no rain, visibility ≥ 1 km and no flight restriction is active. Unknown values make the site component Needs Review. |
| BR-026 | The overall readiness assessment is Ready when all required components, including site feasibility, personnel readiness, UAV availability, and technical readiness, satisfy their evaluation criteria. Otherwise, the assessment is Not Ready. |
| BR-027 | For a Not Ready assessment, the system records and displays the reasons for each readiness component that does not satisfy its evaluation criteria. |
| BR-028 | A Ready assessment expires when its window start passes; it becomes Consumed when a mission is created from it. |
| – | MF02 – Mission and assignment |
| BR-029 | Only a Ready assessment that is not Consumed or Expired can create a mission; one assessment creates at most one mission, and the mission keeps the link. |
| BR-030 | A mission has at least one target, and all targets belong to the assessment scope. |
| BR-031 | The planned start is before the planned end and both are inside the assessment window; the confirmation deadline is at least 2 hours before the planned start. |
| BR-032 | Flight altitude is at most 120 m above ground and flight speed at most 10 m/s. |
| BR-033 | A mission has at least one assigned Inspector, and exactly one of them is the pilot-in-command. |
| BR-034 | A mission has exactly one assigned UAV whose battery state of health is at least 80%. |
| BR-035 | Eligibility and collisions are re-checked when assigning, dispatching, rescheduling and reassigning. |
| BR-036 | Bookings and assignments are created or changed in one transaction. |
| BR-037 | On dispatch, each assignee receives an in-app and email notification with scope, schedule, deadline and a link to the mission. |
| BR-038 | Only the assignee may respond to an assignment, only before the deadline, and only once per assignment version. |
| BR-039 | A postponement request requires a reason and a proposed available time. |
| BR-040 | A reminder is sent 1 hour before the confirmation deadline; an unanswered assignment becomes Expired at the deadline and the manager is notified. |
| BR-041 | Cancel, suspend, resume, reschedule and reassign require a reason, are audited and notify affected users. Completed, Field Completed and Cancelled missions cannot be changed; an In Progress mission can only be suspended. |
| BR-042 | A mission becomes Confirmed when all its assignments are Accepted. |
| – | MF03 – Field execution |
| BR-043 | Only an Inspector with an Accepted assignment on a Confirmed mission may check in, from 2 hours before the planned start until the planned end. |
| BR-044 | Check-in stores server time and GPS location; a location more than 1 km from the nearest target is flagged and the manager is notified. |
| BR-046 | A mission can start only once and only when it is Confirmed, pilot check-in is completed, the UAV’s technical inspection is still valid and the current time is inside the planned window. |
| BR-047 | Accepted media types are JPEG, PNG, TIFF and MP4, with at most 50 MB per file. |
| BR-048 | Media associated with planned inspections are linked to one mission and at most one inspection target. Each media record retains its capture time and GPS position for subsequent inspection and traceability. |
| BR-049 | A critical alert records its type, severity, source, time, location, and associated mission when available. |
| BR-050 | An alert or notification is stored before any email is sent. |
| BR-051 | Acknowledging and resolving an alert record actor, time and disposition. |
| BR-052 | Skipping a target requires a reason. |
| BR-053 | Field completion requires every target to be Inspected or Skipped, all buffered media synchronized and no unresolved critical alert (unless overridden, BR-008). |
| BR-068 | UAV must maintain a minimum safety standoff distance from energized transmission conductors based on voltage rating: ≥3.0 m for 110 kV, ≥4.0 m for 220 kV, and ≥6.0 m for 500 kV corridors. |
| BR-069 | The UAV companion flight system must automatically trigger a Return-to-Home (RTH) or controlled landing failsafe when remaining battery capacity reaches the configured emergency threshold (default 25%). |
| BR-070 | A communication link loss (telemetry or RC command) exceeding 15 consecutive seconds triggers the automated flight failsafe (hover for 10s, then execute autonomous RTH). |
| BR-071 | Operational flight ceiling is strictly restricted to 120 m Above Ground Level (AGL) and within the declared GIS corridor geofence boundary. |
| BR-072 | If in-flight weather deteriorates beyond operational limits (wind gust >10 m/s or rainfall detected), the pilot must immediately abort the sortie and recall the aircraft. |
| BR-073 | For multi-span corridors requiring multiple battery cycles, each individual flight segment is logged as a separate sortie tied to the master mission docket. |
| BR-074 | Onboard flight logs (BIN/CSV telemetry files) must be uploaded within 2 hours of field landing, carrying an automated cryptographic checksum to prevent post-flight tampering. |
| BR-075 | Any field incident resulting in emergency landing, thermal runaway, or line contact requires filing an Emergency Incident Report within 1 hour. |
| BR-076 | Flight operations require active airspace clearance and mission coordination with the designated regional military flight control office prior to takeoff. |
| BR-077 | Visual Line of Sight (VLOS) must be continuously maintained between the certified Pilot-in-Command and the designated Visual Observer throughout flight. |
| BR-078 | Night flight operations are prohibited unless the UAV is equipped with active anti-collision strobe lighting and FAA/ICAO compliant navigation beacons. |
| – | MF04 – AI analysis and review |
| BR-054 | Only an Inspector assigned to the mission or a Mission Manager may submit media for analysis. |
| BR-055 | Media must pass type, size, readable content, mission ownership and required metadata checks before analysis. |
| BR-056 | A file with the same SHA-256 checksum in the same mission is not stored twice. |
| BR-057 | AI analysis is asynchronous; each job has a correlation ID and a status the user can query. |
| BR-058 | Detections are classified using the six canonical AI classes defined in Section 5.4.3. Detections with labels that cannot be mapped to these classes are marked as Pending Review. |
| BR-059 | Confidence is between 0 and 1, and the bounding box is inside the image; otherwise the detection is invalid and Pending Review. |
| BR-060 | A detection with confidence ≥ 0.70 is Auto-Accepted; a lower one is Pending Review. Detections below 0.25 are discarded. |
| BR-061 | A transient AI error is retried at most three times; a job that has not finished in 10 minutes counts as an error; after the last retry the job is Failed. |
| BR-062 | AI callbacks must carry a valid token and a known correlation ID; repeated callbacks for the same job are ignored. |
| BR-063 | Rejecting or correcting a detection requires a reason; the original AI output is kept. |
| BR-064 | Only an Analyst may change a review outcome; re-reviewing a detection requires a reason. |
| BR-065 | Reanalysis creates a new job version and never overwrites earlier versions; an identical active request is not duplicated. |
| BR-066 | A defect record receives the default severity defined for its category in Section 5.4.3. If the severity is Critical, the system notifies the Mission Manager. |
| BR-067 | Reports and exports apply the same permissions and filters as the screen. |
| – | Inspection Media and Evidence Integrity |
| BR-079 | All aerial imagery and video must undergo SHA-256 cryptographic hashing immediately upon SD card ingestion; files matching existing hashes are deduplicated. |
| BR-080 | Inspection images must meet minimum sensor resolution requirements (≥12 MP) and pass an automated Laplacian variance blur detection check prior to ingestion. |
| BR-081 | EXIF metadata must contain valid WGS 84 GPS coordinates, barometric altitude, camera pitch/yaw gimbal angles, and UTC timestamp. |
| BR-082 | Corridor survey photography must maintain a minimum forward overlap of 60% and side overlap of 30% to support photogrammetric asset alignment. |
| BR-083 | Media files are stored in isolated, partition-structured object storage paths structured as: /{Region}/{Year}/{MissionId}/{TowerCode}/. |
| BR-084 | Raw inspection media files are immutable (WORM - Write Once, Read Many); deletion or alteration of original raw images is strictly blocked. |
| BR-085 | Batch uploads from field clients are capped at 500 images or 5 GB per submission to guarantee network ingestion stability. |
| BR-086 | Infrared thermal imagery must include temperature calibration metadata and raw radiometric delta (ΔT) values relative to ambient temperature. |
| BR-087 | EXIF GPS coordinates of every uploaded image must fall within the maximum allowable spatial tolerance (50 m) of the assigned transmission tower. |
| BR-088 | When field cellular connectivity is unavailable, the mobile console must buffer encrypted media locally and execute automated resumable sync upon reconnecting. |
| – | AI Inference, Severity Scoring and Quality Control |
| BR-089 | Every AI detection record must permanently record the model identifier, model version, and inference pipeline commit hash that produced it. |
| BR-090 | Overlapping bounding box predictions for the same object class are filtered using Non-Maximum Suppression (NMS) with an IoU threshold of 0.45. |
| BR-091 | The gRPC inspection evaluation engine computes an overall defect risk score using: Risk = Severity x AssetCriticality x EnvironmentalExposure. |
| BR-092 | When an image contains multiple overlapping defect types (e.g., broken insulator and bird nest), each defect is tracked as an independent detection. |
| BR-093 | Predictions with confidence falling in the marginal review band (0.25 ≤ confidence < 0.70) are automatically routed to the Analyst review queue. |
| BR-094 | Detections classified as foreign\_object or bird\_nest located within the flashover hazard zone of energized conductors are automatically upgraded to Critical. |
| BR-095 | Any broken\_strand detection involving more than 3 severed outer aluminum conductor strands must trigger an immediate high-priority alert. |
| BR-096 | AI inference requests sent via RabbitMQ must complete within a 60-second processing SLA; unacknowledged jobs are requeued up to 3 times before DLQ routing. |
| BR-097 | Reanalysis of media generates an incremental result version (v2, v3) without purging or mutating the historical detection records. |
| BR-098 | Insulator defect detection algorithms must distinguish between toughened glass, porcelain ceramic, and composite polymer insulator string materials. |
| – | Maintenance Tickets and Field Remediation |
| BR-099 | A maintenance work order (Ticket) is automatically generated whenever an Analyst confirms a Critical or Major defect. |
| BR-100 | Maintenance ticket remediation deadlines follow strict SLA policies: Critical defects within 24 hours, Major defects within 72 hours, Minor within 14 days. |
| BR-101 | Technicians can only be assigned to maintenance tickets within their authorized regional operational scope and required electrical safety certifications. |
| BR-102 | All replacement materials (insulator bells, dampers, armor rods) used during remediation must be logged with serial and batch numbers. |
| BR-103 | Closing a maintenance ticket strictly requires uploading photographic before-and-after proof with verified GPS coordinates and timestamps. |
| BR-104 | Ticket closure requires dual authorization: technician field completion followed by formal sign-off from the Mission Manager. |
| BR-105 | If a subsequent UAV inspection detects recurrent degradation at a repaired tower within 90 days, the original ticket is flagged for audit review. |
| BR-106 | External contractor work orders must be bound to a certified EVN supervisory technician before issuance. |
| BR-107 | Overdue maintenance tickets automatically trigger escalation notifications to the Regional Power Transmission Director. |
| BR-108 | Ticket scheduling must synchronize with national grid dispatchers (A0/Ax) to verify approved line outage schedules before field crew deployment. |
| – | Reporting, Compliance and Data Archival |
| BR-109 | A Mission Inspection Summary Report can only be generated after 100% of captured media and AI detections have completed analyst review. |
| BR-110 | Once submitted for approval, an inspection report is locked into a read-only state, preventing further data additions or edits. |
| BR-111 | Exported PDF and Excel inspection dossiers must embed a cryptographic digital signature and verification QR code conforming to EVN standards. |
| BR-112 | All official defect classifications, severity rankings, and maintenance forms must strictly conform to EVN technical standard quy chuẩn kỹ thuật. |
| BR-113 | System audit trails, flight logs, and inspection evidence must be retained in immutable storage for a minimum statutory period of 5 years. |
| BR-114 | Public or external data exports must automatically redact high-precision coordinates of critical national energy security infrastructure. |
| BR-115 | Database snapshots and geospatial inspection registries must be backed up daily with encrypted off-site geographic replication. |

#### 5.2 Common Requirements

***Table 35.* *Common requirements.*

| **ID** | **Requirement** |
| :---: | :---: |
| CR-001 | Mandatory fields are marked with an asterisk and validated on both client and server. |
| CR-002 | List screens provide search, sorting, filters and pagination (NFR-PER-06). |
| CR-003 | An empty result shows an empty-state message (MSG-003), not an error. |
| CR-004 | Dates use the format dd/MM/yyyy HH:mm in the configured time zone (BR-009). |
| CR-005 | Identifiers are never reused after a record is deactivated. |
| CR-006 | A version conflict returns the current record and shows MSG-005. |
| CR-007 | User-visible errors show a message code and a correlation ID; technical details are only in logs. |
| CR-008 | Notifications link only to records the recipient may open and contain no password or token. |
| CR-009 | An external-service failure is shown as MSG-007, not as a user input error. |
| CR-010 | Every request and message between services carries a correlation ID. |
| CR-011 | Controls are keyboard-operable, have labels and a visible focus; status is never shown by colour alone. |

#### 5.3 Application Messages List

Messages are numbered by area: MSG-0xx common, MSG-1xx MF01, MSG-2xx MF02, MSG-3xx MF03 and MSG-4xx MF04. Text in braces is replaced at run time.

***Table 36.*** *Application messages.*

| **\#** | **Message code** | **Message Type** | **Context** | **Content** |
| --- | :---: | :---: | :---: | :---: |
| 1 | MSG-001 | Inline | Required input missing | {Field} is required. |
| 2 | MSG-002 | Inline | Invalid date or time range | {Start} must be earlier than {End}. |
| 3 | MSG-003 | Empty state | No result | No records match the selected filters. |
| 4 | MSG-004 | Toast | Save succeeded | Changes saved successfully. |
| 5 | MSG-005 | Error banner | Version conflict | This record was changed by another user. Refresh and try again. |
| 6 | MSG-006 | Error banner | Not authorized | You do not have permission to perform this action. |
| 7 | MSG-007 | Error banner | External service unavailable | {Service} is temporarily unavailable. Try again later. |
| 8 | MSG-008 | Error banner | Wrong credentials | Username or password is incorrect. |
| 9 | MSG-009 | Error banner | Account locked or inactive | Your account is locked or inactive. Try again after {minutes} minutes or contact the administrator. |
| 10 | MSG-010 | Inline | Duplicate value | {Field} “{value}” already exists. |
| 11 | MSG-011 | Inline | Invalid coordinates | Latitude or longitude is out of range. |
| 12 | MSG-012 | Confirm dialog | Confirm critical action | Are you sure you want to {action} {record}? |
| 13 | MSG-013 | Warning | Email delivery failed | The notification was saved, but the email could not be delivered. |
| 14 | MSG-101 | Error banner | No eligible personnel | No eligible Inspector is available for the selected region and time window. |
| 15 | MSG-102 | Error banner | No eligible UAV | No technically ready UAV is available for the selected time window. |
| 16 | MSG-103 | Status | Assessment Ready | The pre-mission assessment is Ready. You can create a mission. |
| 17 | MSG-104 | Status | Assessment Not Ready | The pre-mission assessment is Not Ready. Review the listed reasons. |
| 18 | MSG-105 | Status | Assessment Needs Review | The assessment needs review: {reasons}. |
| 19 | MSG-106 | Inline | UAV not technically ready | The technical inspection of UAV {serial} is expired, failed or missing, or its battery health is below {limit}%. |
| 20 | MSG-107 | Toast | Technical inspection saved | Technical inspection saved with result {Passed/Failed}. |
| 21 | MSG-108 | Inline | Asset outside region | Asset {code} does not belong to region {region}. |
| 22 | MSG-201 | Error banner | Cannot create mission | Only a Ready assessment that has not been used or expired can create a mission. |
| 23 | MSG-202 | Inline | Resource collision | {Resource} is already booked from {start} to {end}. |
| 24 | MSG-203 | Toast | Mission dispatched | Mission dispatched. Assignment notifications have been sent. |
| 25 | MSG-204 | Inline | Postpone reason missing | Enter a reason and a proposed available time. |
| 26 | MSG-205 | Toast | Assignment accepted | Mission assignment accepted. |
| 27 | MSG-206 | Warning | Assignment changed | This assignment has changed. Review the latest mission details. |
| 28 | MSG-207 | Error banner | Deadline passed | The confirmation deadline has passed. Contact the Mission Manager. |
| 29 | MSG-208 | Toast | Mission changed | Mission {action} completed. Affected users have been notified. |
| 30 | MSG-209 | Error banner | Dispatch blocked | Add at least one target, a pilot-in-command and a UAV before dispatching. |
| 31 | MSG-301 | Error banner | Check-in blocked | You cannot check in: {reason}. |
| 32 | MSG-302 | Warning | Check-in far from site | Your location is {distance} km from the nearest target. The Mission Manager has been notified. |
| 33 | MSG-303 | Error banner | Start blocked | The mission cannot start: {failed conditions}. |
| 34 | MSG-304 | Toast | Mission started | Mission started. |
| 35 | MSG-305 | Status | Synchronization pending | {n} items are waiting to synchronize from the UAV. |
| 36 | MSG-306 | Toast | Evidence received | Inspection evidence received. |
| 37 | MSG-307 | Error banner | Unsupported evidence | {File}: type, content or size is not supported. |
| 38 | MSG-308 | Critical alert | Critical anomaly | Critical anomaly reported at {location}. Follow the emergency procedure. |
| 39 | MSG-309 | Error banner | Finish blocked | Resolve the listed targets, uploads or critical alerts before finishing. |
| 40 | MSG-310 | Toast | Field work completed | Field inspection completed. You can now submit data for analysis. |
| 42 | MSG-312 | Inline | Skip reason missing | Enter a reason for skipping this target. |
| 43 | MSG-401 | Status | AI queued | Inspection data is queued for AI analysis. |
| 44 | MSG-402 | Status | AI processing | AI analysis is in progress. |
| 45 | MSG-403 | Status | Review required | {n} detections require Analyst review. |
| 46 | MSG-404 | Error banner | AI failed | AI analysis failed after {n} attempts. Request reanalysis or contact the administrator. |
| 47 | MSG-405 | Inline | Invalid detection | The detection has an invalid category, confidence or bounding box and needs review. |
| 48 | MSG-406 | Toast | Review saved | Detection review saved. |
| 49 | MSG-407 | Inline | Reanalysis reason missing | Enter a reason for reanalysis. |
| 50 | MSG-408 | Info | Reanalysis already active | An identical reanalysis request is already in progress. |
| 51 | MSG-409 | Info | Duplicate media | {File} was already submitted for this mission and has been skipped. |
| 52 | MSG-410 | Toast | Export ready | The report has been exported. |

#### 5.4 Other Requirements

##### 5.4.1 Configuration Parameters

Table 37 lists the default values of the configurable business rules (BR-010). The values come from the team’s flight-safety checklist and the AI evaluation plan and must be confirmed with the stakeholder before the release.

***Table 37.*** *Configuration parameters and default values.*

| **Parameter** | **Default** | **Rule** |
| :---: | :---: | :---: |
| Login lockout | 5 failed attempts, 15 minutes | BR-003 |
| Technical inspection validity | 72 hours | BR-022 |
| Maximum wind speed / minimum visibility | 10 m/s / 1 km | BR-025 |
| Minimum confirmation lead time | 2 hours before planned start | BR-031 |
| Maximum flight altitude / speed | 120 m / 10 m/s | BR-032 |
| Minimum battery state of health | 80% | BR-034 |
| Assignment reminder | 1 hour before deadline | BR-040 |
| Check-in window / maximum distance | From 2 hours before start to planned end / 1 km | BR-043, BR-044 |
| Maximum media file size | 50 MB | BR-048 |
| Auto-accept / discard confidence | 0.70 / 0.25 | BR-062 |
| AI retry limit / job timeout | 3 attempts / 10 minutes | BR-063 |
| Display time zone | Asia/Ho\_Chi\_Minh | BR-009 |

##### 5.4.2 State Tables

The tables below define the allowed status values and transitions referenced by BR-005.

***Table 38.*** *Readiness assessment states.*

| **Assessment State** | **Meaning** | **Next states (use case)** |
| :---: | :---: | :---: |
| Evaluating | Automated multi-criteria evaluation pipeline actively checking site, personnel, UAV, and technical health | Ready (Pass evaluation, UC-MF01-02), NotReady (Fail evaluation, UC-MF01-02), Cancelled (cancelAssessment, UC-MF01-06), Expired (ValidUntil elapsed, NS-01) |
| Ready | All 4 readiness criteria verified; authorized for mission creation | Completed (createMission, UC-MF02-01), Evaluating (Re-evaluate condition, UC-MF01-02), Cancelled (cancelAssessment, UC-MF01-06), Expired (Window start elapsed, NS-01) |
| NotReady | At least one readiness criterion failed (schedule collision, weather, UAV unready) | Evaluating (Re-evaluate after resource correction, UC-MF01-02), Cancelled (cancelAssessment, UC-MF01-06), Expired (Window start elapsed, NS-01) |
| Completed | Assessment consumed to spawn an Inspection Mission docket | – (Terminal state; immutable record) |
| Expired | Validity deadline or planned flight start elapsed without dispatch | – (Terminal state; preserved for audit trail) |
| Cancelled | Assessment manually cancelled by Mission Manager before mission dispatch | – (Terminal state; all temporary resource locks released) |


***Table 39.*** *Mission states.*

| **Mission State** | **Meaning** | **Next states (use case)** |
| :---: | :---: | :---: |
| Draft | Initial mission docket created; awaiting crew/UAV assignment and formal dispatch | PendingAcceptance (Dispatch mission, UC-MF02-04), Cancelled (cancelMission, UC-MF02-09) |
| PendingAcceptance | Mission dispatched to assignees; awaiting formal responses before confirmation deadline | Confirmed (All assignees accept, UC-MF02-07), Postponed (Postponement requested & accepted, UC-MF02-07), Cancelled (Manager cancels/reassigns, UC-MF02-09) |
| Confirmed | All mandatory assignees accepted; flight roster locked; awaiting on-site crew arrival | Ready (Site check-in verified & checklist passed, UC-MF03-01), PendingAcceptance (Rescheduled/reassigned, UC-MF02-09), Cancelled (cancelMission, UC-MF02-09) |
| Ready | Pilot on-site GPS check-in verified; pre-flight safety checklist cleared for takeoff | InProgress (Start mission execution, UC-MF03-03), Cancelled (Emergency cancellation, UC-MF02-09) |
| InProgress | UAV actively airborne executing flight corridor; real-time telemetry streaming | Suspended (Temporary weather pause / battery swap, UC-MF02-09), Completed (All targets inspected & finished, UC-MF03-08), Cancelled (Critical abort, UC-MF02-09) |
| Suspended | Flight temporarily paused due to adverse wind gust, battery replacement, or corridor hold | InProgress (Resume flight, UC-MF02-09), Cancelled (Mission aborted, UC-MF02-09) |
| Postponed | Flight rescheduled to a new future window following crew postponement or managerial delay | PendingAcceptance (Dispatched with new schedule, UC-MF02-09), Cancelled (cancelMission, UC-MF02-09) |
| Completed | All towers inspected or skipped, media uploaded, and flight logs submitted | – (Terminal state; triggers AI analysis pipeline) |
| Cancelled | Mission cancelled by Mission Manager; all reserved resource bookings released | – (Terminal state) |


***Table 40.*** *Assignment states.*

| **Assignment State** | **Meaning** | **Next states (use case)** |
| :---: | :---: | :---: |
| Pending | Dispatched to crew member; awaiting response before confirmation deadline | Accepted (Accept assignment, UC-MF02-07), Postponed (Request postponement with reason, UC-MF02-07), Expired (Deadline elapsed, NS-02), Cancelled (Reassigned/Cancelled, UC-MF02-09) |
| Accepted | Crew member accepted assignment for the scheduled flight window | Pending (Mission rescheduled, UC-MF02-09), Cancelled (Mission cancelled, UC-MF02-09) |
| Postponed | Crew member submitted postponement request citing valid schedule conflict | Pending (Manager accepts postponement & reschedules, UC-MF02-09), Cancelled (Manager reassigns alternative crew, UC-MF02-09) |
| Expired | Confirmation deadline elapsed without response; triggers overdue alert to manager | Cancelled (Reassigned by manager, UC-MF02-09), Pending (Rescheduled with extended deadline, UC-MF02-09) |
| Cancelled | Assignment revoked due to crew replacement, postponement handling, or mission cancellation | – (Terminal state) |


***Table 41.*** *AI analysis job states.*

| **Job State** | **Meaning** | **Next states** |
| :---: | :---: | :---: |
| Queued | Inference request published to RabbitMQ queue; awaiting available AI worker | Processing (Consumed by Cloud AI Service, NS-07), Failed (Broker delivery failure, BR-096) |
| Processing | Deep neural network (YOLO11, RT-DETR) running inference on aerial imagery | Completed (Inference completed & gRPC severity scored, NS-08), Queued (Retry transient failure <=3 times, BR-096), Failed (Retries exhausted, BR-096) |
| Completed | Inference and severity evaluation complete; detections populated in database | – (Terminal state for this job version; reanalysis creates new version, UC-MF04-06) |
| Failed | Inference failed after 3 retry attempts; message routed to Dead-Letter Queue (DLQ) | Queued (Manual retry requested by Analyst/Admin, UC-MF04-06) |


***Table 42.*** *Detection review states.*

| **Anomaly State** | **Meaning** | **Next states (UC-MF04-05)** |
| :---: | :---: | :---: |
| Pending | Initial anomaly detection generated by AI inference; awaiting confidence triage or analyst review | InReview (Cloud detection with marginal confidence \[0.25 <= conf < 0.70\], UC-MF04-05), Confirmed (Auto-accepted high confidence \[conf >= 0.70\], BR-093), Rejected (Sub-threshold false positive discard, BR-060) |
| InReview | Queued in Detection Review Workspace (SCR-21); awaiting expert review by certified Analyst | Confirmed (Analyst confirms defect category & bounding box, UC-MF04-05), Rejected (Analyst marks false positive with reason, UC-MF04-05) |
| Confirmed | Defect formally validated; gRPC risk score computed; triggers automated maintenance ticket | Resolved (Maintenance repair completed & proof verified, UC-MF04-08), InReview (Re-evaluated on secondary audit, BR-064) |
| Rejected | Flagged as false positive / benign artifact; excluded from defect reports | InReview (Re-evaluated if disputed during supervisory audit, BR-064) |
| Resolved | Physical defect repaired on power corridor; before/after photographic proof verified | – (Terminal state; archived in asset health ledger) |


***Table 43.*** *Maintenance ticket states.*

| **Ticket State** | **Meaning** | **Next states (use case)** |
| :---: | :---: | :---: |
| Open | Maintenance work order automatically created when a defect is confirmed; awaiting technician assignment | Assigned (Assign technician, UC-MF04-08), Cancelled (Cancel duplicate/void ticket, UC-MF04-08) |
| Assigned | Ticket assigned to a certified Technician; awaiting physical site arrival and repair start | InProgress (Start maintenance, UC-MF04-08), Cancelled (Cancel ticket before work starts, UC-MF04-08) |
| InProgress | Technician actively executing physical repair on the power corridor and recording materials used | PendingVerification (Submit AfterRepair proof photo, UC-MF04-08) |
| PendingVerification | Repair completed and photographic proof submitted; awaiting Mission Manager sign-off | Closed (Manager approves closure & resolves parent anomaly, UC-MF04-08), InProgress (Manager rejects proof for rework, UC-MF04-08) |
| Closed | Repair verified and accepted; parent DetectedAnomaly automatically marked Resolved (BR-058) | – (Terminal state; immutable audit record) |
| Cancelled | Ticket cancelled by Mission Manager prior to physical repair execution | – (Terminal state) |



***Table 44.*** *Emergency alert states.*

| **Alert State** | **Meaning** | **Next states (use case)** |
| :---: | :---: | :---: |
| Open | Critical telemetry threshold breach or field safety hazard alert triggered during active flight; awaiting immediate triage | Acknowledged (Mission Manager or Pilot acknowledges alert, UC-MF03-07) |
| Acknowledged | Responsible operator has confirmed receipt and initiated corrective or emergency flight action | Resolved (Operator records resolution action and closes alert, UC-MF03-07) |
| Resolved | Hazard mitigated and resolution notes recorded; cleared for mission completion sign-off | – (Terminal state; retained in mission safety log) |

##### 5.4.3 AI Detection Classes and Default Severity Levels


***Table 45.*** *Canonical defect categories and default severity.*

| **AI Class** | **Description** | **Default severity** |
| :---: | :---: | :---: |
| insulator | Intact insulator | Not Applicable |
| insulator\_broken | Broken insulator | Critical |
| insulator\_flashover | Flashover-damaged insulator | Major |
| bird\_nest | Bird nest | Major |
| foreign\_object | Foreign object | Major |
| broken\_strand | Broken conductor strand | Critical |

##### 5.4.4 Requirement Traceability

Table 46 links each main flow to its use cases, screens, non-screen functions and main entities, so each requirement can be traced to design and data (Report 2, Table 2, *Requirements and traceability*).

***Table 46.*** *Traceability from main flow to screens and data.*

| **Flow** | **Use cases** | **Screens** | **Non-screen functions** | **Main entities** |
| :---: | :---: | :---: | :---: | :---: |
| CF | UC-CF-01 – UC-CF-08 | SCR-01 – SCR-07, SCR-24 | NS-03, NS-09 | User, Role, Region, PowerLine, Tower, InfrastructureAsset, UAV, Notification, AuditLog |
| MF01 | UC-MF01-01 – UC-MF01-06 | SCR-07 – SCR-09 | NS-01 | ReadinessAssessment, AssessmentTarget, TechnicalInspection, ResourceBooking |
| MF02 | UC-MF02-01 – UC-MF02-09 | SCR-10 – SCR-15 | NS-02, NS-03 | Mission, MissionTarget, MissionAssignment, ResourceBooking |
| MF03 | UC-MF03-01 – UC-MF03-08 | SCR-16 – SCR-18 | NS-04 – NS-06 | FieldSession, MediaEvidence, CriticalAlert |
| MF04 | UC-MF04-01 – UC-MF04-08 | SCR-19 – SCR-23, SCR-25 | NS-07, NS-08 | AnalysisJob, Detection, DetectionReview, DefectRecord, DefectCategory |


