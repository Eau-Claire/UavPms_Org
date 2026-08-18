// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Operations Service – Monitor / Dashboard endpoints
// ============================================================

test.describe('Operations Service – Monitor', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/monitor/summary – should return dashboard summary', async ({ request }) => {
    const res = await request.get('/api/v1/monitor/summary', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
  });

  test('GET /api/v1/monitor/recent-defects – should return recent defects (paginated)', async ({ request }) => {
    const res = await request.get('/api/v1/monitor/recent-defects', {
      params: { page: 1, pageSize: 5 },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/monitor/defects-statistics – should return defect statistics', async ({ request }) => {
    const res = await request.get('/api/v1/monitor/defects-statistics', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/monitor/mission-status – should return mission status overview', async ({ request }) => {
    const res = await request.get('/api/v1/monitor/mission-status', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/monitor/inspections – should return inspections with filters', async ({ request }) => {
    const res = await request.get('/api/v1/monitor/inspections', {
      params: { page: 1, pageSize: 5 },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/monitor/inspections – should filter by isDefect', async ({ request }) => {
    const res = await request.get('/api/v1/monitor/inspections', {
      params: { page: 1, pageSize: 5, isDefect: true },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/monitor/inspections – should filter by date range', async ({ request }) => {
    const res = await request.get('/api/v1/monitor/inspections', {
      params: {
        page: 1,
        pageSize: 5,
        fromDate: '2024-01-01T00:00:00Z',
        toDate: '2026-12-31T23:59:59Z',
      },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/monitor/alerts – should return alerts', async ({ request }) => {
    const res = await request.get('/api/v1/monitor/alerts', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/monitor/summary – should return 401 without token', async ({ request }) => {
    const res = await request.get('/api/v1/monitor/summary');
    expect(res.status()).toBe(401);
  });
});

// ============================================================
// Operations Service – Audit Log endpoints
// ============================================================

test.describe('Operations Service – Audit Logs', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/audit-logs – should list audit logs (paginated)', async ({ request }) => {
    const res = await request.get('/api/v1/audit-logs', {
      params: { page: 1, pageSize: 5 },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/audit-logs – should filter by tableName', async ({ request }) => {
    const res = await request.get('/api/v1/audit-logs', {
      params: { page: 1, pageSize: 5, tableName: 'Mission' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/audit-logs – should filter by actionType', async ({ request }) => {
    const res = await request.get('/api/v1/audit-logs', {
      params: { page: 1, pageSize: 5, actionType: 'Create' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/audit-logs – should support search', async ({ request }) => {
    const res = await request.get('/api/v1/audit-logs', {
      params: { page: 1, pageSize: 5, search: 'admin' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/audit-logs – should return 401 without token', async ({ request }) => {
    const res = await request.get('/api/v1/audit-logs');
    expect(res.status()).toBe(401);
  });
});
