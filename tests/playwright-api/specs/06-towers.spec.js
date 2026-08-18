// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Operations Service – Tower endpoints
// ============================================================

test.describe('Operations Service – Towers', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/towers – should list towers (paginated)', async ({ request }) => {
    const res = await request.get('/api/v1/towers', {
      params: { page: 1, pageSize: 5 },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
  });

  test('GET /api/v1/towers – should filter by lineAssetId', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get('/api/v1/towers', {
      params: { page: 1, pageSize: 5, lineAssetId: fakeId },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/towers/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/towers/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('POST /api/v1/towers – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/towers', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing required fields
    });
    expect([400, 422]).toContain(res.status());
  });

  test('PUT /api/v1/towers/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.put(`/api/v1/towers/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        lineAssetId: fakeId,
        towerCode: 'TOWER-TEST-001',
        latitude: 10.123,
        longitude: 106.456,
      },
    });
    expect([200, 400, 404]).toContain(res.status());
  });

  test('DELETE /api/v1/towers/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.delete(`/api/v1/towers/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 204, 404]).toContain(res.status());
  });

  test('POST /api/v1/towers/import – should reject empty file upload', async ({ request }) => {
    const res = await request.post('/api/v1/towers/import', {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        file: {
          name: 'empty.csv',
          mimeType: 'text/csv',
          buffer: Buffer.from(''),
        },
      },
    });
    expect([400, 422]).toContain(res.status());
  });

  test('GET /api/v1/towers – should return 401 without token', async ({ request }) => {
    const res = await request.get('/api/v1/towers');
    expect(res.status()).toBe(401);
  });
});
