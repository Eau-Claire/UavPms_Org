# Mock AI worker

Temporary demo mode for the mission AI analysis flow. It is built into the WebApi process as an optional hosted service.

Enable it only when the real AI worker is unavailable.

## Enable

In `.env` on the PMS server:

```env
MOCK_AI_ENABLED=true
MOCK_AI_CATEGORY_CODE=
MOCK_AI_CONFIDENCE=0.92
```

`MOCK_AI_CATEGORY_CODE` is optional. If empty, the mock worker uses the highest-severity existing `DefectCategory`.

Restart WebApi:

```bash
docker compose up -d --build webapi
```

## Behavior

The mock worker consumes RabbitMQ queue:

```text
ai.analysis.server.requested
```

For each `AIAnalysisRequestedEvent`, it calls the normal `ProcessAiAnalysisResultCommand` path and creates one deterministic detection with a bounding box.

This means the normal APIs work without a GPU model:

```text
POST /api/v1/missions/{missionId}/ai-analysis
GET  /api/v1/missions/{missionId}/ai-analysis/detections
```

## Disable

When the real AI worker is available:

```env
MOCK_AI_ENABLED=false
```

Then rebuild/restart WebApi.
