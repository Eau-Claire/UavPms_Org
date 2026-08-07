# Hangfire Smoke Test

Manual fallback smoke test for `NotificationService` when Docker/Testcontainers-based verification is unavailable.

## Preconditions

- `NotificationService` is running with valid `ConnectionStrings:HangfireConnection` or `ConnectionStrings:DefaultConnection`.
- RabbitMQ may be enabled or disabled; this smoke test is focused on Hangfire liveness while the app also hosts RabbitMQ consumers.
- Gateway or direct service access to `NotificationService` is available.

## 1. Confirm startup

Check startup logs for all of the following:

- Hangfire storage configured against PostgreSQL.
- `AddHangfireServer` started.
- Recurring jobs registered:
  - `auto-cleanup-job`
  - `daily-summary-job`
  - `push-notifications-sync`
- RabbitMQ hosted consumers started without crashing:
  - `MissionCreatedConsumer`
  - `DefectDetectedConsumer`
  - `NotificationPushConsumer`
  - `AIAnalysisStatusChangedConsumer`

## 2. Enqueue a simple job

Send a simple email enqueue request:

```bash
curl -X POST "http://localhost:5194/api/v1/notifications/enqueue-email" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <jwt>" \
  -d '{
    "email": "test@example.com",
    "subject": "hangfire smoke",
    "body": "job execution probe"
  }'
```

Expected:

- HTTP 200
- Response contains a Hangfire `JobId`

## 3. Check job execution

Open the Hangfire dashboard through the gateway:

- `http://localhost:5194/hangfire`

Expected:

- The enqueued job moves to `Succeeded`
- Retry count remains `0` for the successful case

## 4. Check retry behavior

Trigger a known failing email configuration or temporarily point SendGrid to an invalid key in a non-production environment, then enqueue another email job.

Expected:

- The job moves through `Failed` and retry states
- Hangfire retry attempts are visible in the dashboard

## 5. Check recurring jobs

In the dashboard, confirm these recurring jobs are present:

- `auto-cleanup-job`
- `daily-summary-job`
- `push-notifications-sync`

Expected:

- They appear under `Recurring Jobs`
- They have next execution times scheduled

## 6. Concurrency sanity check

While Hangfire jobs are visible in the dashboard, confirm `NotificationService` logs still show healthy RabbitMQ consumer startup and no host shutdown/restart loop.

Expected:

- Hangfire server remains active
- API remains responsive
- RabbitMQ consumers do not block app startup
