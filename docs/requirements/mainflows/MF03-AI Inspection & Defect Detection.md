# MF03 - AI Inspection & Defect Detection

## Priority

**P0 - Critical / Core Business Flow**

## Description

MF03 has one authoritative normal inspection-media ingestion flow. An assigned Inspector uploads an image or video through OperationsService. OperationsService validates and persists one `InspectionMedia`, then reliably publishes `InspectionMediaUploadedEvent`. AIInspectionService consumes that event, creates one `AIAnalysisRequest`, sends the existing media to the AI worker, validates the callback, and stores `0..N` `DetectedAnomaly` records for Analyst review.

Cloud AI processing is separate from Edge emergency handling. Confirmed Cloud AI detections may be consumed by MF04, but MF04 health-scoring logic is not part of this flow.

### Roles and Permissions

- **Inspector:** upload media only for a mission assigned to that Inspector and an asset in that mission's inspection scope; view permitted mission status/results. Inspector cannot review detections.
- **Analyst:** view media/results and bounding boxes; confirm or reject detections with notes; request reanalysis of existing media. Analyst cannot use the normal upload endpoint.
- **Manager:** view results and request reanalysis for a mission managed by that Manager. Manager is not an Inspector or Analyst by default.
- **Admin:** administrative/audit visibility only; Admin has no implicit MF03 mutation override.
- **Technician:** no MF03 mutation responsibility.
- **AI worker:** authenticated machine integration that consumes analysis requests and reports processing/results; it is not a human RBAC role.

### Main Flow

1. An authenticated Inspector calls `POST /api/v1/inspections/upload` in OperationsService with `missionId`, `assetId`, `capturedAt`, the media file, and optional GPS.
2. OperationsService obtains `uploaderId` from authentication context and validates the mission, asset, assignment, mission target, file, timestamp, and GPS.
3. OperationsService stores exactly one `InspectionMedia` and an outbox message in the same database transaction.
4. The outbox publishes `InspectionMediaUploadedEvent` after the media transaction commits.
5. AIInspectionService consumes the event, verifies it against the stored media, creates one `PENDING` `AIAnalysisRequest`, and records `AIAnalysisRequestedEvent` in the outbox.
6. The AI worker starts inference and reports `PROCESSING` before a terminal result.
7. AIInspectionService validates the terminal callback and persists zero or more anomalies linked to the media and asset, then marks the request `COMPLETED` or `FAILED`.
8. An Analyst may view bounding boxes and confirm or reject each detection with notes.
9. Confirmed Cloud AI defects may be passed downstream to MF04 through integration events.

### YOLO Model Labels

- corrosion
- insulator damage / broken insulator
- wire sagging

### RT-DETR Model Labels

- an-mon
- broken_strand
- corrosion
- iso_broken_glass
- lo-loi
- set-danh

Model labels are integration data, not independent business taxonomies. The AI integration must provide a canonical `categoryCode`; MF03 resolves that code through `DefectCategory`. Model-label mapping must remain outside mission/inspection business logic.

## Input

### 1. Inspection Context

- Required `missionId`.
- Required `assetId`.
- `uploaderId` from authentication context; it is not accepted from the client.

### 2. Inspection Data

- Required image or MP4 video.
- Required real `capturedAt`; upload time is recorded separately by persistence auditing.
- Optional latitude and longitude, supplied together.
- Flight logs are not sent to the object-detection worker by this flow.

### 3. AI Request Data

- Existing `mediaId`, media storage URL, `missionId`, required `assetId`, media type, analysis type, and preferred model/version.
- Manual reanalysis uses an existing `mediaId` and never uploads or creates another `InspectionMedia`.

## Output

### 1. Inspection Media

- `mediaId`
- `missionId`
- `assetId`
- `uploaderId`
- `capturedAt`
- media type and storage location
- optional GPS metadata

No separate `inspectionId` is created by this flow.

### 2. AI Detection Result

- Canonical defect category.
- Confidence score.
- Normalized bounding box.
- Frame index/timestamp for video when applicable.
- GPS when applicable.
- Traceability through `DetectedAnomaly -> InspectionMedia -> Mission + Asset`.

### 3. Processing Result

- `PENDING -> PROCESSING -> COMPLETED`
- `PENDING -> PROCESSING -> FAILED`
- `COMPLETED` with an empty detection list is a successful result.
- Bounding-box data is available to the Analyst review UI.

## Validation

### V01 - Authentication and Authorization

Protected endpoints require authentication and the endpoint-specific role. Resource authorization is also required: Inspector assignment and Manager ownership checks cannot be replaced by role membership alone.

### V02 - Mission, Asset, and Scope

The mission and asset must exist. For normal upload, `MissionTargets` must contain the requested mission and asset, and the authenticated Inspector must be assigned to that mission.

### V03 - File Validation

The file must be non-empty, within the configured maximum size, use a supported extension and declared MIME type, have a matching valid file signature/container, and be minimally decodable. Supported inputs are JPEG, PNG, WebP, TIFF, and MP4.

### V04 - Metadata Validation

`capturedAt` must be present and cannot be unreasonably in the future. Latitude must be in `[-90, 90]` and longitude in `[-180, 180]`; both coordinates must be supplied together.

### V05 - AI Callback Validation

`requestId`, `mediaId`, `missionId`, and `assetId` are required and must agree with both `AIAnalysisRequest` and `InspectionMedia`. Every completed detection requires a stable detection ID, canonical category code, confidence in `[0,1]`, and a normalized bounding box satisfying:

- `0 <= x < 1`, `0 <= y < 1`
- `0 < width <= 1`, `0 < height <= 1`
- `x + width <= 1`, `y + height <= 1`

Applicable frame, timestamp, and GPS fields are also range-validated.

## Business Rules

### BR01 - Single Ingestion Owner

OperationsService is the only normal public media-ingestion owner. AIInspectionService exposes no public file-upload or mission batch-upload path.

### BR02 - Identity and Traceability

`mediaId` is the concrete inspection-media identity. Normal mission media and anomalies require an asset association; normal Cloud AI detections cannot persist with a null `AssetId`.

### BR03 - Submission Idempotency

An upload event is idempotent by `SourceEventId`. Active normal analysis is unique for `mediaId + analysisType + model`; database partial unique indexes enforce both guarantees.

### BR04 - Callback Idempotency

Terminal callbacks are idempotent. Stable worker detection IDs are unique per media, preventing duplicate anomaly rows and duplicate downstream side effects.

### BR05 - Analyst Review

Only Analyst may confirm/reject a detection and add notes. Review is a post-detection subflow and does not ingest media.

### BR06 - Reanalysis

Analyst or the mission's Manager may request reanalysis through `POST /api/v1/missions/{missionId}/ai-analysis/from-media/{mediaId}`. Reanalysis creates a new analysis request for existing media and does not create media.

### BR07 - Reliable Messaging

Media creation plus `InspectionMediaUploadedEvent`, analysis-request creation plus `AIAnalysisRequestedEvent`, and callback result plus downstream/status events use the existing database unit of work with an outbox dispatcher. Consumers remain idempotent because delivery is at least once.

### BR08 - Downstream MF04 Integration

Confirmed/stored defects may be passed to MF04. MF03 does not calculate asset health scores.

### BR09 - Edge Separation

Edge emergency detections belong to the Emergency Alert flow. Cloud callback processing does not create Edge emergency alerts.

## Exceptions

Synchronous API failures use the shared `ApiResponse` shape and the appropriate HTTP status:

| HTTP Status | Error Code | Meaning |
|---|---|---|
| 401 | `UNAUTHORIZED` | Authentication is required. |
| 403 | `FORBIDDEN` | Role or resource authorization failed. |
| 404 | `MISSION_NOT_FOUND` | Mission was not found. |
| 404 | `ASSET_NOT_FOUND` | Asset was not found. |
| 400 | `INVALID_MISSION_ASSET` | Asset is outside the mission inspection scope. |
| 400 | `INVALID_FILE` | Media or metadata validation failed. |
| 500 | `STORAGE_FAILURE` | Media storage failed. |
| 500 | `DATABASE_FAILURE` | Database persistence failed. |
| 409 | `DUPLICATE_REQUEST` | A duplicate logical analysis was submitted. |

Asynchronous worker failures are stored on `AIAnalysisRequest` rather than returned by the original upload request. Supported failure details include `AI_SERVICE_UNAVAILABLE`, `AI_PROCESSING_FAILED`, and `INVALID_AI_RESPONSE`.

## Related Backend Modules

- `OperationsService`: upload command/controller, mission/asset/assignment/scope validation, media storage, media outbox publication.
- `AIInspectionService`: upload-event consumer, AI request lifecycle, worker callback, anomaly persistence, queries, reanalysis, and Analyst review.
- `Shared.Contracts`: `InspectionMediaUploadedEvent` and shared outbox persistence contract.
- RabbitMQ AI request/result topology and background outbox dispatchers.
- Authentication/RBAC, audit interceptor, file/media storage, Mission, and Asset modules.

## Next Main Flow

- **Emergency Alert Management** for critical Edge AI anomalies.
- **MF04 - Asset Health Assessment** for confirmed Cloud AI defects.
