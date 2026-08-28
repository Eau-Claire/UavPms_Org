from __future__ import annotations

import json
import sqlite3
from datetime import datetime
from pathlib import Path
from typing import Iterable

from app.models import TestRun


class ResultStore:
    def __init__(self, db_path: str | Path) -> None:
        self.db_path = Path(db_path)
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        self._init()

    def _connect(self) -> sqlite3.Connection:
        conn = sqlite3.connect(self.db_path)
        conn.row_factory = sqlite3.Row
        return conn

    def _init(self) -> None:
        with self._connect() as conn:
            conn.execute(
                """
                CREATE TABLE IF NOT EXISTS test_runs (
                    id TEXT PRIMARY KEY,
                    payload TEXT NOT NULL,
                    started_at TEXT NOT NULL
                )
                """
            )

    def save(self, run: TestRun) -> None:
        payload = run.model_dump_json()
        with self._connect() as conn:
            conn.execute(
                """
                INSERT OR REPLACE INTO test_runs (id, payload, started_at)
                VALUES (?, ?, ?)
                """,
                (run.id, payload, run.started_at.isoformat()),
            )

    def latest(self) -> TestRun | None:
        rows = self._rows("ORDER BY started_at DESC LIMIT 1")
        return next(iter(rows), None)

    def history(self, limit: int = 25) -> list[TestRun]:
        return list(self._rows("ORDER BY started_at DESC LIMIT ?", (limit,)))

    def _rows(self, suffix: str, params: tuple = ()) -> Iterable[TestRun]:
        with self._connect() as conn:
            for row in conn.execute(f"SELECT payload FROM test_runs {suffix}", params):
                data = json.loads(row["payload"])
                data["started_at"] = _parse_datetime(data["started_at"])
                if data.get("ended_at"):
                    data["ended_at"] = _parse_datetime(data["ended_at"])
                yield TestRun(**data)


def _parse_datetime(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))
