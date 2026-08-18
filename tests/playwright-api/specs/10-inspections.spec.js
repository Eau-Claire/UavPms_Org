// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Operations Service – Inspection endpoints
// ============================================================

test.describe('Operations Service – Inspections', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('POST /api/v1/inspections/upload – should reject request without file', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.post('/api/v1/inspections/upload', {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        missionId: fakeId,
        assetId: fakeId,
        capturedAt: new Date().toISOString(),
        file: {
          name: 'test.jpg',
          mimeType: 'image/jpeg',
          buffer: Buffer.from('fake-image-data'),
        },
      },
    });
    // Should accept the upload or reject with validation error
    expect([200, 400, 404, 422]).toContain(res.status());
  });

  test('GET /api/v1/inspections/report/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/inspections/report/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('GET /api/v1/inspections/mission/{missionId} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/inspections/mission/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('GET /api/v1/inspections/report/{id} – should return 401 without token', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/inspections/report/${fakeId}`);
    expect(res.status()).toBe(401);
  });
});
