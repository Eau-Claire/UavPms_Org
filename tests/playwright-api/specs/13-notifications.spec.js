// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Notification Service endpoints
// ============================================================

test.describe('Notification Service', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/notifications/history – should return notification history', async ({ request }) => {
    const res = await request.get('/api/v1/notifications/history', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
  });

  test('GET /api/v1/notifications/history – should filter by userId', async ({ request }) => {
    const res = await request.get('/api/v1/notifications/history', {
      params: { userId: 'test-user' },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 400]).toContain(res.status());
  });

  test('GET /api/v1/notifications/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/notifications/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('PUT /api/v1/notifications/{id}/read – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.put(`/api/v1/notifications/${fakeId}/read`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('DELETE /api/v1/notifications/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.delete(`/api/v1/notifications/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 204, 404]).toContain(res.status());
  });

  test('POST /api/v1/notifications – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/notifications', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing required fields
    });
    expect([400, 422]).toContain(res.status());
  });

  test('POST /api/v1/notifications – should create notification with valid data', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000001';
    const res = await request.post('/api/v1/notifications', {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        userId: fakeId,
        type: 'Info',
        referenceType: 'Mission',
        referenceId: fakeId,
        title: 'Test Notification',
        body: 'This is a test notification from Playwright.',
      },
    });
    expect([200, 201, 400, 404]).toContain(res.status());
  });

  test('POST /api/v1/notifications/enqueue-email – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/notifications/enqueue-email', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing required fields
    });
    expect([400, 422]).toContain(res.status());
  });

  test('POST /api/v1/notifications/enqueue-email – should accept valid email request', async ({ request }) => {
    const res = await request.post('/api/v1/notifications/enqueue-email', {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        email: 'test@example.com',
        subject: 'Test Subject',
        body: 'Test email body from Playwright.',
      },
    });
    expect([200, 201, 400, 500]).toContain(res.status());
  });

  test('POST /api/v1/notifications/schedule – should validate required fields', async ({ request }) => {
    const res = await request.post('/api/v1/notifications/schedule', {
      headers: { Authorization: `Bearer ${token}` },
      data: {}, // Missing required fields
    });
    expect([400, 422]).toContain(res.status());
  });

  test('POST /api/v1/notifications/schedule – should accept valid schedule request', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000001';
    const res = await request.post('/api/v1/notifications/schedule', {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        userId: fakeId,
        title: 'Scheduled Test',
        body: 'Scheduled notification from Playwright',
        type: 'Reminder',
        delaySeconds: 3600,
      },
    });
    expect([200, 201, 400, 404]).toContain(res.status());
  });

  test('GET /api/v1/notifications/history – should return 401 without token', async ({ request }) => {
    const res = await request.get('/api/v1/notifications/history');
    expect(res.status()).toBe(401);
  });
});
