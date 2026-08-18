// @ts-check
const { test, expect } = require('@playwright/test');

// ============================================================
// Health Check endpoints across all services
// ============================================================

test.describe('Health Checks', () => {

  test('GET /health – API Gateway health', async ({ request }) => {
    const res = await request.get('/health');
    expect(res.status()).toBe(200);
  });

  test('Swagger UI – should be accessible', async ({ request }) => {
    const res = await request.get('/swagger/index.html');
    expect(res.status()).toBe(200);
  });

  test('Swagger JSON – API Gateway spec should be accessible', async ({ request }) => {
    const res = await request.get('/swagger/gateway/swagger.json');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.openapi).toBeTruthy();
    expect(body.info.title).toContain('UAV PMS');
  });

  test('Swagger JSON – Identity Service spec should be accessible', async ({ request }) => {
    const res = await request.get('/swagger/services/identity/v1/swagger.json');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.openapi).toBeTruthy();
  });

  test('Swagger JSON – Operations Service spec should be accessible', async ({ request }) => {
    const res = await request.get('/swagger/services/operations/v1/swagger.json');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.openapi).toBeTruthy();
  });

  test('Swagger JSON – AI Inspection Service spec should be accessible', async ({ request }) => {
    const res = await request.get('/swagger/services/ai-inspection/v1/swagger.json');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.openapi).toBeTruthy();
  });

  test('Swagger JSON – Notification Service spec should be accessible', async ({ request }) => {
    const res = await request.get('/swagger/services/notifications/v1/swagger.json');
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.openapi).toBeTruthy();
  });
});
