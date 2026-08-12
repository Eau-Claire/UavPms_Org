# MQTT drone simulator contract

OperationsService subscribes to simulator messages through Mosquitto.

Environment:

```env
MQTT_HOST=mosquitto
MQTT_PORT=1883
DRONE_CODE=UAV-001
```

Status heartbeat topic:

```text
uav/UAV-001/status
```

Payload:

```json
{
  "droneCode": "UAV-001",
  "status": "online",
  "battery": 92,
  "timestamp": "2026-08-12T01:20:00Z"
}
```

Telemetry topic:

```text
uav/UAV-001/telemetry
```

Payload:

```json
{
  "droneCode": "UAV-001",
  "timestamp": "2026-08-12T01:20:10Z",
  "latitude": 10.8411,
  "longitude": 106.8098,
  "altitude": 35.0,
  "battery": 87,
  "speed": 8.4,
  "heading": 120.5
}
```

Backend behavior:

- `droneCode` in the payload must match the code in the topic when both are present.
- OperationsService looks up `UAVs.UavCode`; unknown codes are logged and ignored.
- Simulators do not create database UAV records.
- Live state is stored in Redis at `uav:status:{DRONE_CODE}` with the configured TTL.

Local publish examples:

```bash
mosquitto_pub -h localhost -p 1883 -t uav/UAV-001/status -m '{"droneCode":"UAV-001","status":"online","battery":92,"timestamp":"2026-08-12T01:20:00Z"}'
mosquitto_pub -h localhost -p 1883 -t uav/UAV-001/telemetry -m '{"droneCode":"UAV-001","timestamp":"2026-08-12T01:20:10Z","latitude":10.8411,"longitude":106.8098,"altitude":35.0,"battery":87,"speed":8.4,"heading":120.5}'
```
