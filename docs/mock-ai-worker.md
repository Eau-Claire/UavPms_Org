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

The mock worker consumes separate RabbitMQ queues by media type:

```text
ai.analysis.server.image.requested
ai.analysis.server.video.requested
```

Routing keys:

```text
identity.event.aianalysisrequestedevent.server.image
identity.event.aianalysisrequestedevent.server.video
```

Real AI workers should bind image and video workers to separate queues with the same routing-key pattern. Edge workers use the `edge.image` and `edge.video` suffixes.

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
