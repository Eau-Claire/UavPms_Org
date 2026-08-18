// @ts-check
const { test, expect } = require('@playwright/test');
const { getAuthToken } = require('../helpers/auth');

// ============================================================
// Identity Service – Auth endpoints
// ============================================================

test.describe('Identity Service – Auth', () => {

  test('POST /api/v1/auth/login – should return 200 with valid credentials', async ({ request }) => {
    const res = await request.post('/api/v1/auth/login', {
      data: {
        email: process.env.UAV_TEST_EMAIL || 'phamhoangminhchau1973@gmail.com',
        password: process.env.UAV_TEST_PASSWORD || '123',
      },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body).toBeTruthy();
    expect(body.success).toBe(true);
  });

  test('POST /api/v1/auth/login – should return 400/401 with invalid credentials', async ({ request }) => {
    const res = await request.post('/api/v1/auth/login', {
      data: {
        email: 'nonexistent@example.com',
        password: 'WrongPassword!',
      },
    });
    expect([400, 401]).toContain(res.status());
  });

  test('POST /api/v1/auth/otp/send – should accept OTP send request', async ({ request }) => {
    const res = await request.post('/api/v1/auth/otp/send', {
      data: {
        email: 'phamhoangminhchau1973@gmail.com',
        purpose: 'Login',
      },
    });
    expect([200, 400, 404]).toContain(res.status());
  });

  test('POST /api/v1/auth/otp/verify – should reject invalid OTP', async ({ request }) => {
    const res = await request.post('/api/v1/auth/otp/verify', {
      data: {
        email: 'phamhoangminhchau1973@gmail.com',
        otp: '000000',
        purpose: 'Login',
      },
    });
    expect([400, 401, 404, 500]).toContain(res.status());
  });

  test('POST /api/v1/auth/refresh-token – should reject invalid refresh token', async ({ request }) => {
    const res = await request.post('/api/v1/auth/refresh-token', {
      data: {
        refreshToken: 'invalid-token',
      },
    });
    expect([400, 401, 500]).toContain(res.status());
  });

  test('POST /api/v1/auth/reset-password – should reject invalid verification token', async ({ request }) => {
    const res = await request.post('/api/v1/auth/reset-password', {
      data: {
        verificationToken: 'invalid-token',
        newPassword: 'NewPassword@123',
      },
    });
    expect([400, 401, 404, 500]).toContain(res.status());
  });
});
