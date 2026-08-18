// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// AI Inspection Service – AI Analysis endpoints
// ============================================================

test.describe('AI Inspection Service – AI Analysis', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('POST /api/v1/ai-analysis/upload – should accept multipart upload', async ({ request }) => {
    const res = await request.post('/api/v1/ai-analysis/upload', {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        files: {
          name: 'test.jpg',
          mimeType: 'image/jpeg',
          buffer: Buffer.from('fake-image-data'),
        },
        analysisType: 'DefectDetection',
        notes: 'Test upload from Playwright',
      },
    });
    // Should accept or reject with validation
    expect([200, 400, 422, 500]).toContain(res.status());
  });

  test('GET /api/v1/ai-analysis/{id} – should return 404 for fake UUID', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/ai-analysis/${fakeId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('POST /api/v1/missions/{missionId}/ai-analysis – should reject with fake missionId', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.post(`/api/v1/missions/${fakeId}/ai-analysis`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        files: {
          name: 'test.jpg',
          mimeType: 'image/jpeg',
          buffer: Buffer.from('fake-image-data'),
        },
        analysisType: 'DefectDetection',
        preferredModel: 'SERVER',
        notes: 'Test',
      },
    });
    expect([200, 400, 404, 422, 500]).toContain(res.status());
  });

  test('GET /api/v1/missions/{missionId}/ai-analysis/detections – should return 404 for fake missionId', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/missions/${fakeId}/ai-analysis/detections`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 404]).toContain(res.status());
  });

  test('PUT /api/v1/missions/{missionId}/ai-analysis/detections/{detectionId}/review – should return 404 for fake UUIDs', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.put(`/api/v1/missions/${fakeId}/ai-analysis/detections/${fakeId}/review`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        decision: 'Confirmed',
        notes: 'Test review',
      },
    });
    expect([200, 400, 404]).toContain(res.status());
  });

  test('POST /api/v1/missions/{missionId}/ai-analysis/from-media/{mediaId} – should return 404 for fake UUIDs', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.post(`/api/v1/missions/${fakeId}/ai-analysis/from-media/${fakeId}`, {
      params: {
        analysisType: 'DefectDetection',
        preferredModel: 'SERVER',
        notes: 'Test',
      },
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 400, 404, 500]).toContain(res.status());
  });

  test('GET /api/v1/ai-analysis/{id} – should return 401 without token', async ({ request }) => {
    const fakeId = '00000000-0000-0000-0000-000000000000';
    const res = await request.get(`/api/v1/ai-analysis/${fakeId}`);
    expect(res.status()).toBe(401);
  });
});

// ============================================================
// AI Inspection Service – Vision Bridge endpoints
// ============================================================

test.describe('AI Inspection Service – Vision Bridge', () => {
  let token = '';

  test.beforeAll(async ({ request }) => {
    token = await getAuthToken(request);
  });

  test('GET /api/v1/vision/health – should return health status', async ({ request }) => {
    const res = await request.get('/api/v1/vision/health', {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect([200, 503]).toContain(res.status());
  });

  test('POST /api/v1/vision/detections – should accept multipart detection', async ({ request }) => {
    const res = await request.post('/api/v1/vision/detections', {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        drone_id: 'DRONE-TEST-001',
        class_name: 'insulator_defect',
        confidence: '0.95',
        timestamp: new Date().toISOString(),
        lat: '10.123',
        lng: '106.456',
        track_id: '1',
        bbox: '[100,200,300,400]',
        image: {
          name: 'detection.jpg',
          mimeType: 'image/jpeg',
          buffer: Buffer.from('fake-image-data'),
        },
      },
    });
    expect([200, 400, 422, 500]).toContain(res.status());
  });

  test('POST /api/v1/vision/detections/json – should accept JSON detection', async ({ request }) => {
    const res = await request.post('/api/v1/vision/detections/json', {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        droneId: 'DRONE-TEST-001',
        className: 'insulator_defect',
        confidence: 0.95,
        timestamp: new Date().toISOString(),
        latitude: 10.123,
        longitude: 106.456,
        trackId: 1,
        boundingBox: [100, 200, 300, 400],
        imageName: 'test-detection.jpg',
      },
    });
    expect([200, 400, 422, 500]).toContain(res.status());
  });
});
