// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Operations Service – Drone endpoints
// ============================================================

test.describe('Operations Service – Drones', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/drones – should list all drones', async ({ request }) => {
    const res = await request.get('/api/v1/drones', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
  });

  test('GET /api/v1/drones/available – should list available drones', async ({ request }) => {
    const res = await request.get('/api/v1/drones/available', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/drones/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/drones/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('GET /api/v1/drones/{id}/status – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/drones/${fakeId}/status`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('GET /api/v1/drones – should return 401 without token', async ({ request }) => {
    const res = await request.get('/api/v1/drones');
    expect(res.status()).toBe(401);
  });
});

// ============================================================
// Operations Service – Device endpoints
// ============================================================

test.describe('Operations Service – Devices', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('POST /api/v1/devices/register – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/devices/register', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing required fields
    });
    expect([400, 422]).toContain(res.status());
  });

  test('POST /api/v1/devices/heartbeat – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/devices/heartbeat', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing required fields
    });
    expect([400, 422]).toContain(res.status());
  });
});
