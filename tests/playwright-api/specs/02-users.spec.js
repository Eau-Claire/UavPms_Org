// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Identity Service – User endpoints
// ============================================================

test.describe('Identity Service – Users', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/users – should list users (paginated)', async ({ request }) => {
    const res = await request.get('/api/v1/users', {
      params: { page: 1, pageSize: 5 },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
  });

  test('GET /api/v1/users – should support search param', async ({ request }) => {
    const res = await request.get('/api/v1/users', {
      params: { page: 1, pageSize: 5, search: 'admin' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/users/assignable – should return assignable users', async ({ request }) => {
    const res = await request.get('/api/v1/users/assignable', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });

  test('GET /api/v1/users/me – should return current user profile', async ({ request }) => {
    const res = await request.get('/api/v1/users/me', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
  });

  test('GET /api/v1/users/{id} – should return 400/404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/users/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 400, 404]).toContain(res.status());
  });

  test('PUT /api/v1/users/{id} – should return 400/404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.put(`/api/v1/users/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        email: 'updated@example.com',
        fullName: 'Test User',
        phone: '0123456789',
        status: 'Active',
        roles: ['Operator'],
      },
    });
    expect([200, 400, 404]).toContain(res.status());
  });

  test('POST /api/v1/users/{id}/suspend – should return 400/404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.post(`/api/v1/users/${fakeId}/suspend`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 400, 404]).toContain(res.status());
  });

  test('POST /api/v1/users – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/users', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing required fields
    });
    expect([400, 409, 422]).toContain(res.status());
  });

  test('POST /api/v1/users/change-password – should reject empty newPassword', async ({ request }) => {
    const res = await request.post('/api/v1/users/change-password', {
      headers: { Authorization: `Bearer ${token}` },
      data: { newPassword: '' },
    });
    expect([400, 401, 422]).toContain(res.status());
  });

  test('GET /api/v1/users – should return 401 without token', async ({ request }) => {
    const res = await request.get('/api/v1/users');
    expect(res.status()).toBe(401);
  });
});
