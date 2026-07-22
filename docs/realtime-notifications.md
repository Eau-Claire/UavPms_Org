# Realtime notifications

## Endpoint

SignalR notification hub is available at:

```text
/hubs/notifications
```

This is a SignalR hub, not a REST API. It is intentionally not exposed through Swagger.

## Authentication

The hub requires the existing JWT Bearer authentication. Anonymous connections are rejected.

Browser/WebSocket clients should provide the same access token used for REST APIs through SignalR access token handling. The server accepts `access_token` from the query string only for the notification hub path:

```text
/hubs/notifications?access_token=<jwt>
```

Typical frontend usage:

```ts
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${apiBaseUrl}/hubs/notifications`, {
    accessTokenFactory: () => accessToken
  })
  .withAutomaticReconnect()
  .build();
```

## Connection lifecycle

When a user connects:

1. The JWT is validated by the existing bearer authentication configuration.
2. `NotificationHub` reads the authenticated user id from `ClaimTypes.NameIdentifier`.
3. The connection joins a user-specific group:

```text
user:{userId}
```

Each browser tab and each device has its own SignalR connection. Since every active connection for the same user joins the same group, all active tabs/devices receive the same notification event.

The hub does not expose client-callable business methods. It only manages authenticated connections and groups.

## Notification lifecycle

PostgreSQL remains the source of truth.

1. Application code creates a `Notification` entity.
2. The notification is saved to PostgreSQL.
3. The database transaction is committed through `SaveChangesAsync`.
4. `RealtimeNotificationService` pushes a lightweight SignalR event to connected clients.

If realtime delivery fails, the exception is logged and the database notification remains available through the existing history APIs.

Existing REST APIs remain the fallback/source-of-truth APIs:

```text
GET    /api/v1/notifications/history
GET    /api/v1/notifications/{id}
PUT    /api/v1/notifications/{id}/read
DELETE /api/v1/notifications/{id}
```

## Event names

Current event:

```text
NotificationReceived
AiAnalysisStatusChanged
```

Reserved future events:

```text
NotificationUpdated
NotificationDeleted
UnreadCountChanged
```

## Payload

`NotificationReceived` sends a lightweight payload:

```json
{
  "id": "...",
  "type": "EmergencyAlert",
  "title": "...",
  "body": "...",
  "referenceType": "Mission",
  "referenceId": "...",
  "priority": "High",
  "createdAt": "...",
  "isRead": false
}
```

The payload intentionally omits unnecessary database fields. Clients should call the REST detail/history APIs when they need full persisted data.

`AiAnalysisStatusChanged` is sent to the uploading user's group when a mission AI request is queued and when the AI callback marks it completed or failed:

```json
{
  "requestId": "...",
  "batchId": "...",
  "missionId": "...",
  "mediaId": "...",
  "mediaType": "Video",
  "status": "Pending",
  "savedDetections": 0,
  "createdAlerts": 0,
  "errorCode": null,
  "errorMessage": null,
  "createdAt": "...",
  "completedAt": null
}
```

Frontend clients should listen for `AiAnalysisStatusChanged`. When `status` is `Completed`, call the mission detections API to refresh timeline/frame/crop results. When `status` is `Failed`, show `errorMessage`.

## Scalability note

Business handlers depend on `IRealtimeNotificationService`, not directly on `IHubContext`. This keeps business logic independent from SignalR infrastructure and allows adding a Redis SignalR backplane later without changing application handlers.
