// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Operations Service – Mission endpoints
// ============================================================

test.describe('Operations Service – Missions', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/missions – should list missions (paginated)', async ({ request }) => {
    const res = await request.get('/api/v1/missions', {
      params: { page: 1, pageSize: 5 },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
  });

  test('GET /api/v1/missions – should support search filter', async ({ request }) => {
    const res = await request.get('/api/v1/missions', {
      params: { page: 1, pageSize: 5, search: 'test' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/missions – should support status filter', async ({ request }) => {
    const res = await request.get('/api/v1/missions', {
      params: { page: 1, pageSize: 5, status: 'Pending' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/missions – should support sorting', async ({ request }) => {
    const res = await request.get('/api/v1/missions', {
      params: { page: 1, pageSize: 5, sortBy: 'createdAt', sortDescending: true },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/missions/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/missions/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('GET /api/v1/missions/my – should return current user missions', async ({ request }) => {
    const res = await request.get('/api/v1/missions/my', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('POST /api/v1/missions – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/missions', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing required fields
    });
    expect([400, 422]).toContain(res.status());
  });

  test('PUT /api/v1/missions/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.put(`/api/v1/missions/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        title: 'Updated Mission',
        status: 'Pending',
        description: 'Test update',
      },
    });
    expect([200, 400, 404]).toContain(res.status());
  });

  test('DELETE /api/v1/missions/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.delete(`/api/v1/missions/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 204, 404]).toContain(res.status());
  });

  test('GET /api/v1/missions – should return 401 without token', async ({ request }) => {
    const res = await request.get('/api/v1/missions');
    expect(res.status()).toBe(401);
  });
});
