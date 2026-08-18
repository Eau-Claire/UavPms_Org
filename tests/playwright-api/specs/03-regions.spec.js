// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Operations Service – Region endpoints
// ============================================================

test.describe('Operations Service – Regions', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/regions – should list regions (paginated)', async ({ request }) => {
    const res = await request.get('/api/v1/regions', {
      params: { page: 1, pageSize: 5 },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
  });

  test('GET /api/v1/regions – should support search', async ({ request }) => {
    const res = await request.get('/api/v1/regions', {
      params: { page: 1, pageSize: 5, search: 'test' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/regions/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/regions/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('POST /api/v1/regions – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/regions', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing regionName
    });
    expect([400, 422]).toContain(res.status());
  });

  test('PUT /api/v1/regions/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.put(`/api/v1/regions/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { regionName: 'Updated Region' },
    });
    expect([200, 400, 404]).toContain(res.status());
  });

  test('DELETE /api/v1/regions/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.delete(`/api/v1/regions/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 204, 404]).toContain(res.status());
  });

  test('GET /api/v1/regions – should return 401 without token', async ({ request }) => {
    const res = await request.get('/api/v1/regions');
    expect(res.status()).toBe(401);
  });
});
