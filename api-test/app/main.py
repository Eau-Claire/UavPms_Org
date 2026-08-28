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
        "authApi": os.getenv("AUTH_API") or os.getenv("TARGET_API", "http://gateway:8080"),
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
    :root { color-scheme: light; --ink:#172033; --muted:#667085; --line:#d9e2ef; --panel:#fff; --bg:#f4f7fb; --blue:#155eef; --green:#087443; --red:#b42318; --amber:#b54708; }
    * { box-sizing: border-box; }
    body { margin: 0; font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Arial, sans-serif; background: var(--bg); color: var(--ink); }
    main { max-width: 1180px; margin: 0 auto; padding: 24px; }
    header { display: flex; justify-content: space-between; gap: 16px; align-items: flex-start; margin-bottom: 18px; }
    h1 { margin: 0; font-size: 28px; line-height: 1.15; }
    h2 { margin: 0 0 12px; font-size: 17px; }
    p { margin: 6px 0 0; color: var(--muted); }
    button, select, input { font: inherit; }
    button { border: 0; background: var(--blue); color: white; min-height: 40px; padding: 0 14px; border-radius: 6px; cursor: pointer; font-weight: 700; }
    button:disabled { background: #98a2b3; cursor: wait; }
    input, select { min-height: 38px; border: 1px solid var(--line); border-radius: 6px; padding: 0 10px; background: white; color: var(--ink); }
    .tabs { display: inline-flex; border: 1px solid var(--line); border-radius: 8px; overflow: hidden; background: var(--panel); margin-bottom: 16px; }
    .tab { background: transparent; color: var(--muted); border-radius: 0; border-right: 1px solid var(--line); }
    .tab:last-child { border-right: 0; }
    .tab.active { background: #eaf1ff; color: #113b8f; }
    .toolbar { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; justify-content: space-between; margin: 12px 0; }
    .filters { display: flex; flex-wrap: wrap; gap: 10px; }
    .grid { display: grid; grid-template-columns: repeat(5, minmax(140px, 1fr)); gap: 12px; margin: 16px 0; }
    .card { background: var(--panel); border: 1px solid var(--line); border-radius: 8px; padding: 16px; }
    .label { color: var(--muted); font-size: 12px; text-transform: uppercase; font-weight: 700; letter-spacing: .02em; }
    .value { font-size: 25px; font-weight: 800; margin-top: 4px; overflow-wrap: anywhere; }
    .pass { color: var(--green); }
    .fail { color: var(--red); }
    .running { color: var(--amber); }
    .muted { color: var(--muted); }
    .section { display: none; }
    .section.active { display: block; }
    .case-list { display: grid; gap: 10px; }
    .case { border: 1px solid var(--line); border-left: 4px solid #98a2b3; background: #fff; border-radius: 8px; padding: 12px; }
    .case.case-pass { border-left-color: var(--green); background: #f6fef9; }
    .case.case-fail, .case.case-error { border-color: #f0b8b1; border-left-color: var(--red); background: #fff7f6; }
    .case.case-skip { border-left-color: #98a2b3; background: #f9fafb; }
    .case-title { font-weight: 800; margin-bottom: 8px; overflow-wrap: anywhere; }
    .case-meta { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 8px; }
    .case-chip { display: inline-block; color: #344054; background: #eef4ff; border-radius: 999px; padding: 3px 8px; font-size: 12px; font-weight: 700; }
    .case-chip.endpoint { color: #7a271a; background: #fee4e2; }
    .case-chip.pass { color: var(--green); background: #dcfae6; }
    .case-chip.fail, .case-chip.error { color: var(--red); background: #fee4e2; }
    .case-message { white-space: pre-wrap; font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: 13px; line-height: 1.45; color: #3b1b16; overflow-wrap: anywhere; }
    .empty { color: var(--muted); padding: 18px; border: 1px dashed var(--line); border-radius: 8px; background: #fbfcff; }
    table { width: 100%; border-collapse: collapse; background: white; }
    th, td { padding: 11px 10px; border-bottom: 1px solid #e6edf6; text-align: left; vertical-align: top; }
    th { color: var(--muted); font-size: 12px; text-transform: uppercase; }
    tr { cursor: pointer; }
    tr:hover td { background: #f8fbff; }
    .pill { display: inline-flex; align-items: center; border-radius: 999px; padding: 3px 8px; font-size: 12px; font-weight: 800; }
    .pill.pass { background: #dcfae6; }
    .pill.fail { background: #fee4e2; }
    .pill.error { background: #fef0c7; color: var(--amber); }
    .detail-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    @media (max-width: 820px) { main { padding: 16px; } header { flex-direction: column; } .grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } .detail-grid { grid-template-columns: 1fr; } }
  </style>
</head>
<body>
<main>
  <header>
    <div>
      <h1>API Regression Test Dashboard</h1>
      <p id="target">Loading backend target...</p>
      <p id="auth-target">Loading auth target...</p>
    </div>
    <button id="run">Run Tests</button>
  </header>

  <nav class="tabs" aria-label="Dashboard sections">
    <button class="tab active" data-tab="current">Current</button>
    <button class="tab" data-tab="history-tab">History</button>
  </nav>

  <section id="current" class="section active">
    <div class="toolbar">
      <div class="filters">
        <select id="case-status">
          <option value="all">All results</option>
          <option value="PASS">Passed</option>
          <option value="FAIL">Failed</option>
          <option value="ERROR">Errors</option>
          <option value="SKIP">Skipped</option>
        </select>
        <select id="case-module">
          <option value="all">All modules</option>
          <option value="asset">Asset</option>
          <option value="mission">Mission</option>
          <option value="inspection">Inspection</option>
          <option value="auth">Auth</option>
        </select>
        <input id="case-search" type="search" placeholder="Search test, endpoint, message">
      </div>
      <span id="run-state" class="muted">Idle</span>
    </div>

  <section class="grid">
    <div class="card"><div class="label">Status</div><div id="status" class="value">-</div></div>
    <div class="card"><div class="label">Passed</div><div id="passed" class="value">-</div></div>
    <div class="card"><div class="label">Failed</div><div id="failed" class="value">-</div></div>
    <div class="card"><div class="label">Skipped</div><div id="skipped" class="value">-</div></div>
    <div class="card"><div class="label">Duration</div><div id="duration" class="value">-</div></div>
  </section>

    <section class="detail-grid">
      <div class="card"><div class="label">Run ID</div><div id="run-id" class="muted">-</div></div>
      <div class="card"><div class="label">Last Run</div><div id="last-run" class="muted">-</div></div>
    </section>

    <section class="card" style="margin-top: 16px;">
      <h2>Test Cases</h2>
      <div id="cases" class="case-list"><div class="empty">No test cases yet.</div></div>
    </section>
  </section>

  <section id="history-tab" class="section">
    <div class="toolbar">
      <div class="filters">
        <select id="history-status">
          <option value="all">All statuses</option>
          <option value="PASS">PASS</option>
          <option value="FAIL">FAIL</option>
          <option value="ERROR">ERROR</option>
        </select>
        <input id="history-search" type="search" placeholder="Search run id or message">
      </div>
    </div>
    <section class="card">
    <h2>Recent History</h2>
      <table>
        <thead><tr><th>Started</th><th>Status</th><th>Passed</th><th>Failed</th><th>Skipped</th><th>Duration</th><th>Run</th></tr></thead>
        <tbody id="history"></tbody>
      </table>
    </section>
    <section class="card" style="margin-top: 16px;">
      <h2>Run Details</h2>
      <div id="history-details" class="empty">Select a run to inspect its test cases.</div>
    </section>
  </section>
</main>
<script>
const runButton = document.getElementById('run');
let latestRun = null;
let historyRuns = [];

document.querySelectorAll('.tab').forEach(tab => {
  tab.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach(item => item.classList.remove('active'));
    document.querySelectorAll('.section').forEach(item => item.classList.remove('active'));
    tab.classList.add('active');
    document.getElementById(tab.dataset.tab).classList.add('active');
  });
});

['case-status', 'case-module', 'case-search'].forEach(id => document.getElementById(id).addEventListener('input', renderCases));
['history-status', 'history-search'].forEach(id => document.getElementById(id).addEventListener('input', renderHistory));

async function refresh() {
  const health = await fetch('/api/health').then(r => r.json());
  const latest = await fetch('/api/results').then(r => r.json());
  const history = await fetch('/api/history').then(r => r.json());
  latestRun = latest;
  historyRuns = history;
  runButton.disabled = health.running;
  document.getElementById('target').textContent = `Backend: ${health.targetApi}`;
  document.getElementById('auth-target').textContent = `Auth: ${health.authApi}`;
  document.getElementById('run-state').textContent = health.running ? `Running ${health.currentRunId || ''}` : 'Idle';
  document.getElementById('run-state').className = health.running ? 'running' : 'muted';
  document.getElementById('status').textContent = latest.status || 'NO_RUNS';
  document.getElementById('status').className = `value ${latest.status === 'PASS' ? 'pass' : latest.status === 'FAIL' ? 'fail' : ''}`;
  document.getElementById('passed').textContent = latest.passed ?? '-';
  document.getElementById('failed').textContent = (latest.failed ?? 0) + (latest.errors ?? 0);
  document.getElementById('skipped').textContent = latest.skipped ?? '-';
  document.getElementById('duration').textContent = latest.duration_seconds ? `${latest.duration_seconds.toFixed(1)}s` : '-';
  document.getElementById('run-id').textContent = latest.id || '-';
  document.getElementById('last-run').textContent = latest.started_at ? formatTime(latest.started_at) : '-';
  renderCases();
  renderHistory();
}

function renderCases(run = latestRun) {
  const el = document.getElementById('cases');
  const source = run?.cases?.length ? run.cases : failuresAsCases(run);
  if (!source.length) {
    el.innerHTML = '<div class="empty">No test cases yet.</div>';
    return;
  }
  const status = document.getElementById('case-status').value;
  const module = document.getElementById('case-module').value;
  const query = document.getElementById('case-search').value.toLowerCase();
  const cases = source.filter(f => {
    const text = `${f.name || ''} ${f.endpoint || ''} ${f.message || ''}`.toLowerCase();
    return (status === 'all' || f.status === status)
      && (module === 'all' || text.includes(module))
      && (!query || text.includes(query));
  });
  el.innerHTML = cases.length ? cases.map(caseCard).join('') : '<div class="empty">No test cases match the current filters.</div>';
}

function caseCard(f) {
  const status = f.status || 'FAIL';
  const endpoint = f.endpoint ? `<span class="case-chip endpoint">${escapeHtml(f.endpoint)}</span>` : '';
  const location = f.location ? `<span class="case-chip">${escapeHtml(f.location)}</span>` : '';
  const duration = Number.isFinite(f.duration_seconds) ? `<span class="case-chip">${f.duration_seconds.toFixed(2)}s</span>` : '';
  const meta = `<div class="case-meta"><span class="case-chip ${status.toLowerCase()}">${status}</span>${endpoint}${location}${duration}</div>`;
  const message = f.message ? `<div class="case-message">${escapeHtml(cleanMessage(f.message))}</div>` : '';
  return `<article class="case case-${status.toLowerCase()}">
    <div class="case-title">${escapeHtml(f.name || 'Unknown test')}</div>
    ${meta}
    ${message}
  </article>`;
}

function failuresAsCases(run) {
  return (run?.failures || []).map(f => ({...f, status: 'FAIL'}));
}

function renderHistory() {
  const status = document.getElementById('history-status').value;
  const query = document.getElementById('history-search').value.toLowerCase();
  const rows = historyRuns.filter(run => {
    const text = `${run.id || ''} ${run.message || ''} ${run.status || ''}`.toLowerCase();
    return (status === 'all' || run.status === status) && (!query || text.includes(query));
  });
  document.getElementById('history').innerHTML = rows.length ? rows.map(r =>
    `<tr onclick="showRun('${r.id}')">
      <td>${formatTime(r.started_at)}</td>
      <td><span class="pill ${(r.status || '').toLowerCase()}">${r.status}</span></td>
      <td>${r.passed}</td>
      <td>${(r.failed || 0) + (r.errors || 0)}</td>
      <td>${r.skipped}</td>
      <td>${r.duration_seconds ? r.duration_seconds.toFixed(1) + 's' : '-'}</td>
      <td><span class="muted">${shortId(r.id)}</span></td>
    </tr>`
  ).join('') : '<tr><td colspan="7" class="muted">No runs match the current filters.</td></tr>';
}

function showRun(id) {
  const run = historyRuns.find(item => item.id === id);
  if (!run) return;
  document.getElementById('history-details').className = '';
  document.getElementById('history-details').innerHTML = `
    <div class="grid">
      <div><div class="label">Status</div><div class="value ${run.status === 'PASS' ? 'pass' : 'fail'}">${run.status}</div></div>
      <div><div class="label">Passed</div><div class="value">${run.passed}</div></div>
      <div><div class="label">Failed</div><div class="value">${(run.failed || 0) + (run.errors || 0)}</div></div>
      <div><div class="label">Duration</div><div class="value">${run.duration_seconds ? run.duration_seconds.toFixed(1) + 's' : '-'}</div></div>
      <div><div class="label">Run ID</div><div class="muted">${run.id}</div></div>
    </div>
    <div class="case-list">${(run.cases?.length ? run.cases : failuresAsCases(run)).map(caseCard).join('') || '<div class="empty">This run has no captured test case details.</div>'}</div>
  `;
}

function cleanMessage(value) {
  return value.replaceAll('\\\\n', '\\n').trim();
}

function formatTime(value) {
  if (!value) return '-';
  return new Date(value).toLocaleString();
}

function shortId(value) {
  return value ? value.slice(0, 8) : '-';
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
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
