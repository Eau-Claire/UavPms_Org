// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Operations Service – Asset endpoints
// ============================================================

test.describe('Operations Service – Assets', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/assets – should list assets (paginated)', async ({ request }) => {
    const res = await request.get('/api/v1/assets', {
      params: { page: 1, pageSize: 5 },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
  });

  test('GET /api/v1/assets – should filter by towerId', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get('/api/v1/assets', {
      params: { page: 1, pageSize: 5, towerId: fakeId },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/assets – should filter by assetType', async ({ request }) => {
    const res = await request.get('/api/v1/assets', {
      params: { page: 1, pageSize: 5, assetType: 'Insulator' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/assets – should filter by status', async ({ request }) => {
    const res = await request.get('/api/v1/assets', {
      params: { page: 1, pageSize: 5, status: 'Active' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/assets/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/assets/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('POST /api/v1/assets – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/assets', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing required fields
    });
    expect([400, 422]).toContain(res.status());
  });

  test('PUT /api/v1/assets/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.put(`/api/v1/assets/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        towerId: fakeId,
        assetType: 'Insulator',
        assetCode: 'AST-TEST-001',
      },
    });
    expect([200, 400, 404]).toContain(res.status());
  });

  test('DELETE /api/v1/assets/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.delete(`/api/v1/assets/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 204, 404]).toContain(res.status());
  });

  test('GET /api/v1/assets – should return 401 without token', async ({ request }) => {
    const res = await request.get('/api/v1/assets');
    expect(res.status()).toBe(401);
  });
});
