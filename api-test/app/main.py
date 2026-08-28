from __future__ import annotations

import asyncio
import os
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException
from fastapi.responses import HTMLResponse

from app.runner import RegressionRunner
from app.storage import ResultStore

REPORTS_DIR = Path(os.getenv("REPORTS_DIR", "/app/reports"))
store = ResultStore(REPORTS_DIR / "results.sqlite3")
runner = RegressionRunner(store)


@asynccontextmanager
async def lifespan(app: FastAPI):
    if os.getenv("AUTO_RUN_ON_START", "true").lower() in {"1", "true", "yes"}:
        asyncio.create_task(_auto_run_once())
    yield


app = FastAPI(title="UAV PMS API Regression Tester", version="1.0.0", lifespan=lifespan)


async def _auto_run_once() -> None:
    await runner.wait_for_backend()
    try:
        await runner.run(reason="startup")
    except RuntimeError:
        pass


@app.get("/api/health")
async def health() -> dict:
    return {
        "status": "healthy",
        "running": runner.is_running,
        "currentRunId": runner.current_run_id,
        "targetApi": os.getenv("TARGET_API", "http://gateway:8080"),
    }


@app.get("/api/results")
async def latest_result() -> dict:
    latest = store.latest()
    return latest.model_dump(mode="json") if latest else {"status": "NO_RUNS"}


@app.get("/api/history")
async def history(limit: int = 25) -> list[dict]:
    return [item.model_dump(mode="json") for item in store.history(limit=limit)]


@app.post("/api/run", status_code=202)
async def run_tests() -> dict:
    if runner.is_running:
        raise HTTPException(status_code=409, detail="test run already in progress")

    async def run_background() -> None:
        await runner.run(reason="manual")

    asyncio.create_task(run_background())
    return {"status": "accepted", "message": "test run started"}


@app.get("/", response_class=HTMLResponse)
async def dashboard() -> str:
    return """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>API Regression Test Dashboard</title>
  <style>
    body { margin: 0; font-family: Arial, sans-serif; background: #f6f8fb; color: #172033; }
    main { max-width: 1080px; margin: 0 auto; padding: 28px; }
    header { display: flex; justify-content: space-between; gap: 16px; align-items: center; }
    button { border: 0; background: #1665d8; color: white; padding: 10px 14px; border-radius: 6px; cursor: pointer; }
    button:disabled { background: #8aa9d6; cursor: wait; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; margin: 18px 0; }
    .card { background: white; border: 1px solid #dde4f0; border-radius: 8px; padding: 16px; }
    .label { color: #5b6b82; font-size: 12px; text-transform: uppercase; }
    .value { font-size: 24px; font-weight: 700; margin-top: 4px; }
    .pass { color: #16803c; }
    .fail { color: #c0342b; }
    pre { white-space: pre-wrap; background: #101828; color: #e7edf7; padding: 14px; border-radius: 8px; overflow: auto; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 10px; border-bottom: 1px solid #e1e7f0; text-align: left; }
  </style>
</head>
<body>
<main>
  <header>
    <div>
      <h1>API Regression Test Dashboard</h1>
      <p id="target">Loading...</p>
    </div>
    <button id="run">Run Tests</button>
  </header>
  <section class="grid">
    <div class="card"><div class="label">Status</div><div id="status" class="value">-</div></div>
    <div class="card"><div class="label">Passed</div><div id="passed" class="value">-</div></div>
    <div class="card"><div class="label">Failed</div><div id="failed" class="value">-</div></div>
    <div class="card"><div class="label">Duration</div><div id="duration" class="value">-</div></div>
  </section>
  <section class="card">
    <h2>Failed Tests</h2>
    <pre id="failures">No failures yet.</pre>
  </section>
  <section class="card" style="margin-top: 16px;">
    <h2>Recent History</h2>
    <table><thead><tr><th>Started</th><th>Status</th><th>Passed</th><th>Failed</th><th>Skipped</th></tr></thead><tbody id="history"></tbody></table>
  </section>
</main>
<script>
const runButton = document.getElementById('run');
async function refresh() {
  const health = await fetch('/api/health').then(r => r.json());
  const latest = await fetch('/api/results').then(r => r.json());
  const history = await fetch('/api/history').then(r => r.json());
  runButton.disabled = health.running;
  document.getElementById('target').textContent = `Backend: ${health.targetApi}`;
  document.getElementById('status').textContent = latest.status || 'NO_RUNS';
  document.getElementById('status').className = `value ${latest.status === 'PASS' ? 'pass' : latest.status === 'FAIL' ? 'fail' : ''}`;
  document.getElementById('passed').textContent = latest.passed ?? '-';
  document.getElementById('failed').textContent = latest.failed ?? '-';
  document.getElementById('duration').textContent = latest.duration_seconds ? `${latest.duration_seconds.toFixed(1)}s` : '-';
  document.getElementById('failures').textContent = latest.failures?.length
    ? latest.failures.map(f => `${f.endpoint || f.name}\\n${f.message}`).join('\\n\\n')
    : 'No failures yet.';
  document.getElementById('history').innerHTML = history.map(r =>
    `<tr><td>${r.started_at}</td><td>${r.status}</td><td>${r.passed}</td><td>${r.failed + r.errors}</td><td>${r.skipped}</td></tr>`
  ).join('');
}
runButton.addEventListener('click', async () => {
  runButton.disabled = true;
  await fetch('/api/run', { method: 'POST' });
  setTimeout(refresh, 1000);
});
refresh();
setInterval(refresh, 5000);
</script>
</body>
</html>
"""


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=int(os.getenv("TESTER_PORT", "8081")))
