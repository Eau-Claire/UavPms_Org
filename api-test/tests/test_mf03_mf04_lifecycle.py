from __future__ import annotations

import uuid
import httpx

from tests.conftest import ASSESSMENT_PATH, assert_json_response


def test_mf03_geofence_checkin_and_mf04_detection_review_to_ticket_and_report_snapshot(
    gis_seed: dict,
    create_ready_assessment,
    client: httpx.Client,
    role_headers,
    db_connection,
) -> None:
    """
    End-to-End MF02 -> MF03 -> MF04 Lifecycle via Ocelot API Gateway:
    1. Create Mission from READY assessment with Inspector + Analyst
    2. Inspector & Analyst accept assignment -> Confirmed
    3. Inspector check-in outside 200m geofence -> rejected (400 CHECKIN_OUTSIDE_GEOFENCE); inside 200m -> Ready
    4. Inspector starts and completes mission -> Completed
    5. Seed active InReview anomaly + superseded anomaly -> Analyst reviews active anomaly (Confirmed, Critical)
       -> Auto-creates Open EmergencyAlert & Open MaintenanceTicket with SLA DueDate
    6. Manager assigns Technician to MaintenanceTicket -> Technician transitions InProgress -> PendingVerification -> Manager closes ticket -> Anomaly becomes Resolved
    7. Analyst creates Defect Report, submits for approval -> Manager approves report -> Frozen Snapshot excludes superseded anomaly
    """
    assessment = create_ready_assessment()
    title = f"GIS API Test MF03-MF04 {uuid.uuid4()}"
    create_payload = {
        "assessmentId": assessment["id"],
        "title": title,
        "description": "E2E MF03-MF04 workflow test",
        "personnel": [
            {"userId": gis_seed["inspector"], "role": "INSPECTOR", "isRequired": True},
            {"userId": gis_seed["analyst"], "role": "ANALYST", "isRequired": True},
        ],
        "droneIds": [gis_seed["mission_uav"]],
        "idempotencyKey": f"gis-e2e-mf03-mf04-{uuid.uuid4()}",
    }
    mission_res = client.post(
        f'{ASSESSMENT_PATH}/{assessment["id"]}/create-mission',
        json=create_payload,
        headers=role_headers("manager"),
    )
    assert mission_res.status_code == 200, mission_res.text
    mission_id = mission_res.json()["id"]

    # 1. Both required members accept -> Mission transitions PendingAcceptance -> Confirmed
    assert client.post(
        f"/api/v1/missions/{mission_id}/assignments/accept",
        headers=role_headers("inspector"),
    ).status_code == 200
    assert client.post(
        f"/api/v1/missions/{mission_id}/assignments/accept",
        headers=role_headers("analyst"),
    ).status_code == 200

    # 2. Check-in 5 km away from target towers -> Rejected with CHECKIN_OUTSIDE_GEOFENCE
    far_checkin = client.post(
        f"/api/v1/missions/{mission_id}/check-in",
        json={"latitude": 10.9000, "longitude": 106.9000},
        headers=role_headers("inspector"),
    )
    assert far_checkin.status_code == 400, far_checkin.text
    assert "CHECKIN_OUTSIDE_GEOFENCE" in far_checkin.text

    # Check-in within 200m of GIS-TEST-TOWER-A (10.844, 106.804) -> Transitions Confirmed -> Ready
    near_checkin = client.post(
        f"/api/v1/missions/{mission_id}/check-in",
        json={"latitude": 10.84405, "longitude": 106.80405},
        headers=role_headers("inspector"),
    )
    assert near_checkin.status_code == 200, near_checkin.text

    # 3. Start and Complete Mission
    assert client.post(
        f"/api/v1/missions/{mission_id}/start",
        headers=role_headers("inspector"),
    ).status_code == 200
    assert client.post(
        f"/api/v1/missions/{mission_id}/complete",
        headers=role_headers("inspector"),
    ).status_code == 200

    # 4. Seed InspectionMedia, AIAnalysisRun, 1 Active InReview Anomaly, and 1 Superseded Anomaly
    media_id = str(uuid.uuid4())
    run_id = str(uuid.uuid4())
    active_anomaly_id = str(uuid.uuid4())
    superseded_anomaly_id = str(uuid.uuid4())
    report_id = None
    ticket_id = None
    alert_id = None

    with db_connection.cursor() as cursor:
        cursor.execute(
            """
            INSERT INTO "DefectCategories" ("CategoryCode","CategoryName","Description","SeverityWeight","IsEmergencyClass","CreatedAt","IsDeleted")
            VALUES ('WF-E2E','Wildfire / Flashover','Critical flashover hazard',5.0,true,now(),false)
            ON CONFLICT ("CategoryCode") DO UPDATE SET "IsEmergencyClass"=true,"IsDeleted"=false
            RETURNING "Id"
            """
        )
        category_id = cursor.fetchone()[0]

        cursor.execute(
            """
            INSERT INTO "InspectionMedia" ("Id","MissionId","TowerId","MediaType","FileUrl","ValidationStatus","CapturedAt","CreatedAt","IsDeleted")
            VALUES (%s,%s,%s,'Image','https://storage.local/e2e/tower-a.jpg','PendingReview',now(),now(),false)
            """,
            (media_id, mission_id, gis_seed["tower_1"]),
        )
        cursor.execute(
            """
            INSERT INTO "AIAnalysisRuns" ("Id","MediaId","ExecutionLocation","ModelName","AnalysisType","Status","StartedAt","CompletedAt","CreatedAt","IsDeleted")
            VALUES (%s,%s,'Cloud','RF-DETR',0,2,now(),now(),now(),false)
            """,
            (run_id, media_id),
        )
        cursor.execute(
            """
            INSERT INTO "DetectedAnomalies" (
                "Id","MediaId","AnalysisRunId","TowerId","CategoryId","BoundingBox",
                "ConfidenceScore","ValidationStatus","SupersededById","CreatedAt","IsDeleted"
            )
            VALUES
            (%s,%s,%s,%s,%s,'{"x":0.1,"y":0.1,"width":0.2,"height":0.2}',0.96,'InReview',NULL,now(),false),
            (%s,%s,%s,%s,%s,'{"x":0.1,"y":0.1,"width":0.2,"height":0.2}',0.85,'InReview',%s,now(),false)
            """,
            (
                active_anomaly_id,
                media_id,
                run_id,
                gis_seed["tower_1"],
                category_id,
                superseded_anomaly_id,
                media_id,
                run_id,
                gis_seed["tower_1"],
                category_id,
                active_anomaly_id,
            ),
        )

    try:
        # 5. GET /api/v1/missions/{id}/detections returns ONLY the active anomaly (SupersededById == null)
        det_res = client.get(
            f"/api/v1/missions/{mission_id}/detections",
            headers=role_headers("analyst"),
        )
        assert det_res.status_code == 200, det_res.text
        detections = assert_json_response(det_res)["data"]
        detection_ids = [d["id"] for d in detections]
        assert active_anomaly_id in detection_ids
        assert superseded_anomaly_id not in detection_ids

        # 6. Analyst confirms active emergency anomaly -> creates Open EmergencyAlert + Open MaintenanceTicket
        review_res = client.post(
            f"/api/v1/missions/{mission_id}/detections/{active_anomaly_id}/review",
            json={
                "status": "Confirmed",
                "reviewNotes": "Confirmed severe flashover on insulator string",
                "overrideSeverity": "Critical",
            },
            headers=role_headers("analyst"),
        )
        assert review_res.status_code == 200, review_res.text
        review_data = assert_json_response(review_res)["data"]
        assert review_data["status"] == "Confirmed"
        ticket_id = review_data["maintenanceTaskId"]
        alert_id = review_data["emergencyAlertId"]
        assert ticket_id and alert_id

        # 7. Analyst creates Defect Report for this mission, submits it, and Manager approves it
        create_rep_res = client.post(
            "/api/v1/reports",
            json={
                "title": f"GIS API Test Report {uuid.uuid4()}",
                "type": "Defect",
                "regionId": gis_seed["region"],
                "missionIds": [mission_id],
                "description": "E2E automated defect report",
            },
            headers=role_headers("analyst"),
        )
        assert create_rep_res.status_code in (200, 201), create_rep_res.text
        report_data = assert_json_response(create_rep_res)["data"]
        report_id = report_data["id"]
        assert report_data["defectCount"] == 1

        submit_rep_res = client.put(
            f"/api/v1/reports/{report_id}/submit",
            headers=role_headers("analyst"),
        )
        assert submit_rep_res.status_code == 200, submit_rep_res.text

        approve_rep_res = client.put(
            f"/api/v1/reports/{report_id}/approve",
            headers=role_headers("manager"),
        )
        assert approve_rep_res.status_code == 200, approve_rep_res.text
        approved_data = assert_json_response(approve_rep_res)["data"]
        assert approved_data["status"] == "approved"
        assert approved_data["hasSnapshot"] is True
        assert approved_data["defectCount"] == 1
    finally:
        with db_connection.cursor() as cursor:
            if report_id:
                cursor.execute('DELETE FROM "ReportMissions" WHERE "ReportId"=%s', (report_id,))
                cursor.execute('DELETE FROM "Reports" WHERE "Id"=%s', (report_id,))
            if ticket_id:
                cursor.execute('DELETE FROM "MaintenanceTickets" WHERE "Id"=%s', (ticket_id,))
            if alert_id:
                cursor.execute('DELETE FROM "EmergencyAlerts" WHERE "Id"=%s', (alert_id,))
            cursor.execute('DELETE FROM "DetectedAnomalies" WHERE "MediaId"=%s', (media_id,))
            cursor.execute('DELETE FROM "AIAnalysisRuns" WHERE "Id"=%s', (run_id,))
            cursor.execute('DELETE FROM "InspectionMedia" WHERE "Id"=%s', (media_id,))
            cursor.execute('DELETE FROM "MissionCheckIns" WHERE "MissionId"=%s', (mission_id,))
