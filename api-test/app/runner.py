from __future__ import annotations

import asyncio
import os
import re
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

import httpx

from app.models import TestCase, TestFailure, TestRun
from app.storage import ResultStore


class RegressionRunner:
    def __init__(self, store: ResultStore) -> None:
        self.store = store
        self.lock = asyncio.Lock()
        self.current_run_id: str | None = None

    @property
    def is_running(self) -> bool:
        return self.lock.locked()

    async def wait_for_backend(self) -> bool:
        target_api = os.getenv("TARGET_API", "http://gateway:8080").rstrip("/")
        health_path = os.getenv("BACKEND_HEALTH_PATH", "/health")
        health_url = f"{target_api}{health_path if health_path.startswith('/') else '/' + health_path}"
        timeout = float(os.getenv("TEST_TIMEOUT", "30"))

        async with httpx.AsyncClient(timeout=5) as client:
            deadline = asyncio.get_running_loop().time() + timeout
            while asyncio.get_running_loop().time() < deadline:
                try:
                    response = await client.get(health_url)
                    if response.status_code < 500:
                        return True
                except httpx.HTTPError:
                    pass
                await asyncio.sleep(3)
        return False

    async def run(self, reason: str = "manual") -> TestRun:
        if self.lock.locked():
            raise RuntimeError("test run already in progress")

        async with self.lock:
            return await asyncio.to_thread(self._run_sync, reason)

    def _run_sync(self, reason: str) -> TestRun:
        run_id = uuid.uuid4().hex
        self.current_run_id = run_id
        started = datetime.now(timezone.utc)
        reports_dir = Path(os.getenv("REPORTS_DIR", "/app/reports"))
        reports_dir.mkdir(parents=True, exist_ok=True)
        junit_path = reports_dir / f"{run_id}.xml"
        log_path = reports_dir / f"{run_id}.log"

        env = os.environ.copy()
        env.setdefault("PYTHONPATH", "/app")
        cmd = [
            sys.executable,
            "-m",
            "pytest",
            "tests",
            "-q",
            "--tb=short",
            f"--junitxml={junit_path}",
        ]

        try:
            completed = subprocess.run(
                cmd,
                cwd=Path(__file__).resolve().parents[1],
                env=env,
                text=True,
                capture_output=True,
                timeout=float(os.getenv("PYTEST_TIMEOUT", "300")),
                check=False,
            )
            output = (completed.stdout or "") + "\n" + (completed.stderr or "")
            log_path.write_text(output, encoding="utf-8")
            run = self._build_result(
                run_id=run_id,
                started=started,
                exit_code=completed.returncode,
                junit_path=junit_path,
                log_path=log_path,
                reason=reason,
                output=output,
            )
        except Exception as exc:
            ended = datetime.now(timezone.utc)
            run = TestRun(
                id=run_id,
                status="ERROR",
                started_at=started,
                ended_at=ended,
                duration_seconds=(ended - started).total_seconds(),
                errors=1,
                failures=[TestFailure(name="pytest runner", message=str(exc))],
                target_api=os.getenv("TARGET_API", "http://gateway:8080"),
                backend_build_id=os.getenv("BACKEND_BUILD_ID"),
                message=f"Pytest runner crashed during {reason} run.",
            )
        finally:
            self.current_run_id = None

        self.store.save(run)
        return run

    def _build_result(
        self,
        run_id: str,
        started: datetime,
        exit_code: int,
        junit_path: Path,
        log_path: Path,
        reason: str,
        output: str,
    ) -> TestRun:
        ended = datetime.now(timezone.utc)
        total = passed = failed = skipped = errors = 0
        failures: list[TestFailure] = []
        cases: list[TestCase] = []

        if junit_path.exists():
            root = ET.parse(junit_path).getroot()
            suite = root if root.tag == "testsuite" else root.find("testsuite")
            if suite is not None:
                total = int(suite.attrib.get("tests", 0))
                failed = int(suite.attrib.get("failures", 0))
                errors = int(suite.attrib.get("errors", 0))
                skipped = int(suite.attrib.get("skipped", 0))
                passed = max(total - failed - errors - skipped, 0)
                for case in suite.iter("testcase"):
                    name = f"{case.attrib.get('classname', '')}.{case.attrib.get('name', '')}".strip(".")
                    duration = _float_or_none(case.attrib.get("time"))
                    problem = case.find("failure")
                    status = "PASS"
                    if problem is None:
                        problem = case.find("error")
                        if problem is not None:
                            status = "ERROR"
                    else:
                        status = "FAIL"
                    skipped_problem = case.find("skipped")
                    if problem is None and skipped_problem is not None:
                        status = "SKIP"
                        problem = skipped_problem
                    message = ""
                    if problem is not None:
                        message = _clean_problem_message(problem.attrib.get("message"), problem.text)
                    cases.append(
                        TestCase(
                            name=name,
                            status=status,
                            duration_seconds=duration,
                            message=message[:2000],
                            endpoint=_endpoint_from_text(name + " " + message),
                            location=_location_from_text(message),
                        )
                    )
                    if problem is None:
                        continue
                    if status == "SKIP":
                        continue
                    failures.append(
                        TestFailure(
                            name=name,
                            message=message[:2000],
                            endpoint=_endpoint_from_text(name + " " + message),
                            location=_location_from_text(message),
                        )
                    )

        if (failed or errors) and not failures:
            failures = _failures_from_pytest_summary(output)

        status = "PASS" if exit_code == 0 else "FAIL"
        return TestRun(
            id=run_id,
            status=status,
            started_at=started,
            ended_at=ended,
            duration_seconds=(ended - started).total_seconds(),
            total=total,
            passed=passed,
            failed=failed,
            skipped=skipped,
            errors=errors,
            failures=failures,
            cases=cases,
            target_api=os.getenv("TARGET_API", "http://gateway:8080"),
            backend_build_id=os.getenv("BACKEND_BUILD_ID"),
            report_path=str(junit_path),
            log_path=str(log_path),
            exit_code=exit_code,
            message=f"Completed {reason} regression run.",
            raw={"summary": _summary_from_output(output)},
        )


def _endpoint_from_text(text: str) -> str | None:
    match = re.search(r"(GET|POST|PUT|PATCH|DELETE)\s+(/[^\s:]+)", text)
    if match:
        return f"{match.group(1)} {match.group(2)}"
    return None


def _float_or_none(value: str | None) -> float | None:
    if value is None:
        return None
    try:
        return float(value)
    except ValueError:
        return None


def _location_from_text(text: str) -> str | None:
    match = re.search(r"([A-Za-z0-9_./\\-]+\.py):(\d+)", text)
    if match:
        return f"{match.group(1)}:{match.group(2)}"
    return None


def _clean_problem_message(message: str | None, details: str | None) -> str:
    parts = [part.strip() for part in (message, details) if part and part.strip()]
    if not parts:
        return ""

    if len(parts) == 1:
        return parts[0]

    if parts[1].startswith(parts[0]):
        return parts[1]

    return "\n".join(parts)


def _summary_from_output(output: str) -> str:
    lines = [line.strip() for line in output.splitlines() if line.strip()]
    return "\n".join(lines[-20:])


def _failures_from_pytest_summary(output: str) -> list[TestFailure]:
    failures: list[TestFailure] = []
    for line in output.splitlines():
        stripped = line.strip()
        if not stripped.startswith(("FAILED ", "ERROR ")):
            continue

        kind, _, rest = stripped.partition(" ")
        test_name, _, message = rest.partition(" - ")
        failures.append(
            TestFailure(
                name=test_name,
                message=(message or kind).strip()[:2000],
                endpoint=_endpoint_from_text(message or test_name),
                location=_location_from_text(message or test_name),
            )
        )
    return failures
