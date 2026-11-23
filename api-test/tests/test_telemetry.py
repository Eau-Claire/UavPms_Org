from __future__ import annotations

import uuid
from datetime import datetime, timedelta, timezone
import httpx

from tests.conftest import assert_json_response


def test_drone_latest_telemetry_requires_authentication(client: httpx.Client) -> None:
    response = client.get("/api/v1/drones/GIS-TEST-MISSION-UAV/telemetry/latest")
    assert response.status_code == 401, response.text


def test_mission_telemetry_requires_authentication(client: httpx.Client) -> None:
    fake_mission_id = "00000000-0000-0000-0000-000000000099"
    response = client.get(f"/api/v1/missions/{fake_mission_id}/telemetry")
    assert response.status_code == 401, response.text


def test_drone_latest_telemetry_and_mission_telemetry_history_and_flight_log_aggregation(
    gis_seed: dict,
    create_gis_mission,
    client: httpx.Client,
    role_headers,
    db_connection,
) -> None:
    """
    End-to-End Telemetry & FlightLog Aggregation via Ocelot API Gateway:
    1. Create Mission from READY Assessment -> Inspector accepts -> Check-in within 200m geofence -> Start Mission
    2. Seed periodic telemetry frames in DroneTelemetries while mission is InProgress
    3. Query GET /api/v1/drones/{droneCode}/telemetry/latest and GET /api/v1/missions/{id}/telemetry
    4. Complete Mission -> Verify GET /api/v1/missions/{id}/flight-logs returns auto-aggregated MissionFlightLog
    """
    mission = create_gis_mission(f"GIS API Test Telemetry {uuid.uuid4()}")
    mission_id = mission["id"]
    drone_id = gis_seed["mission_uav"]
    drone_code = "GIS-TEST-MISSION-UAV"

    # 1. Inspector accepts assignment -> Mission becomes Confirmed
    accept_res = client.post(
        f"/api/v1/missions/{mission_id}/assignments/accept",
        headers=role_headers("inspector"),
    )
    assert accept_res.status_code == 200, accept_res.text

    # 2. Inspector checks in near Tower A (10.844, 106.804) -> Mission becomes Ready
    checkin_res = client.post(
        f"/api/v1/missions/{mission_id}/check-in",
        json={"latitude": 10.8441, "longitude": 106.8041},
        headers=role_headers("inspector"),
    )
    assert checkin_res.status_code == 200, checkin_res.text

    # 3. Inspector starts mission -> Mission becomes InProgress
    start_res = client.post(
        f"/api/v1/missions/{mission_id}/start",
        headers=role_headers("inspector"),
    )
    assert start_res.status_code == 200, start_res.text

    # 4. Insert 2 periodic telemetry frames linked to the active mission
    t1 = datetime.now(timezone.utc) - timedelta(minutes=3)
    t2 = datetime.now(timezone.utc) - timedelta(minutes=1)
    t1_id = str(uuid.uuid4())
    t2_id = str(uuid.uuid4())

    with db_connection.cursor() as cursor:
        cursor.execute(
            """
            INSERT INTO "DroneTelemetry" (
                "Id","UavId","MissionId","SourceTimestamp","ReceivedAt","FlightMode","Armed",
                "Location","AltRelativeM","BatteryRemainingPct","CreatedAt","IsDeleted"
            )
            VALUES
            (%s,%s,%s,%s,now(),'AUTO',true,ST_SetSRID(ST_Point(106.8041,10.8441),4326),32.5,94,now(),false),
            (%s,%s,%s,%s,now(),'AUTO',true,ST_SetSRID(ST_Point(106.8079,10.8479),4326),58.0,82,now(),false)
            """,
            (t1_id, drone_id, mission_id, t1, t2_id, drone_id, mission_id, t2),
        )
        cursor.execute(
            'UPDATE "UAVs" SET "BatteryLevel"=82, "LastTelemetryAt"=%s WHERE "Id"=%s',
            (t2, drone_id),
        )

    try:
        # 5. Query GET /api/v1/drones/{droneCode}/telemetry/latest through Ocelot Gateway
        latest_res = client.get(
            f"/api/v1/drones/{drone_code}/telemetry/latest",
            headers=role_headers("inspector"),
        )
        assert latest_res.status_code == 200, latest_res.text
        latest_body = assert_json_response(latest_res)
        assert latest_body["droneId"] == drone_code
        assert latest_body["batteryRemainingPct"] == 82
        assert latest_body["altRelativeM"] == 58.0

        # 6. Query GET /api/v1/missions/{mission_id}/telemetry through Ocelot Gateway
        hist_res = client.get(
            f"/api/v1/missions/{mission_id}/telemetry",
            params={"page": 1, "pageSize": 50},
            headers=role_headers("inspector"),
        )
        assert hist_res.status_code == 200, hist_res.text
        hist_body = assert_json_response(hist_res)
        assert hist_body["totalCount"] >= 2
        assert len(hist_body["items"]) >= 2

        # 7. Complete Mission -> Auto-aggregates MissionFlightLog from DroneTelemetry
        complete_res = client.post(
            f"/api/v1/missions/{mission_id}/complete",
            headers=role_headers("inspector"),
        )
        assert complete_res.status_code == 200, complete_res.text

        logs_res = client.get(
            f"/api/v1/missions/{mission_id}/flight-logs",
            headers=role_headers("inspector"),
        )
        assert logs_res.status_code == 200, logs_res.text
        logs_data = assert_json_response(logs_res)["data"]
        assert isinstance(logs_data, list) and len(logs_data) >= 1
        flight_log = logs_data[0]
        assert flight_log["minBatteryRecorded"] == 82
        assert flight_log["maxAltitudeM"] == 58.0
        assert flight_log["flightDurationSeconds"] == 120
    finally:
        with db_connection.cursor() as cursor:
            cursor.execute('DELETE FROM "DroneTelemetry" WHERE "MissionId"=%s', (mission_id,))
            cursor.execute('DELETE FROM "MissionFlightLogs" WHERE "MissionId"=%s', (mission_id,))
            cursor.execute('UPDATE "UAVs" SET "FlightStatus"=\'Idle\', "BatteryLevel"=100 WHERE "Id"=%s', (drone_id,))
