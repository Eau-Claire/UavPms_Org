from __future__ import annotations

from datetime import datetime
from typing import Any

from pydantic import BaseModel, Field


class TestFailure(BaseModel):
    name: str
    message: str = ""
    endpoint: str | None = None
    location: str | None = None


class TestCase(BaseModel):
    name: str
    status: str
    duration_seconds: float | None = None
    message: str = ""
    endpoint: str | None = None
    location: str | None = None


class TestRun(BaseModel):
    id: str
    status: str
    started_at: datetime
    ended_at: datetime | None = None
    duration_seconds: float | None = None
    total: int = 0
    passed: int = 0
    failed: int = 0
    skipped: int = 0
    errors: int = 0
    failures: list[TestFailure] = Field(default_factory=list)
    cases: list[TestCase] = Field(default_factory=list)
    target_api: str
    backend_build_id: str | None = None
    report_path: str | None = None
    log_path: str | None = None
    exit_code: int | None = None
    message: str | None = None
    raw: dict[str, Any] = Field(default_factory=dict)
