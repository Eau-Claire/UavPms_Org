// @ts-check
const { test, expect } = require('@playwright/test');

/**
 * Helper: login and return the JWT bearer token.
 * 1. Checks if UAV_TEST_TOKEN env var is set directly.
 * 2. Attempts login with email/password.
 * 3. If login returns OTP required and UAV_TEST_OTP is provided, verifies OTP to get token.
 */
async function getAuthToken(request) {
  if (process.env.UAV_TEST_TOKEN) {
    return process.env.UAV_TEST_TOKEN;
  }

  const email = process.env.UAV_TEST_EMAIL || 'phamhoangminhchau1973@gmail.com';
  const password = process.env.UAV_TEST_PASSWORD || '123';
  const deviceTrustToken = process.env.UAV_DEVICE_TRUST_TOKEN || '';

  const headers = {};
  if (deviceTrustToken) {
    headers['X-Device-Trust-Token'] = deviceTrustToken;
  }

  const res = await request.post('/api/v1/auth/login', {
    data: { email, password },
    headers,
  });

  if (res.ok()) {
    const body = await res.json();
    const token = body.token || body.accessToken || body.data?.token || body.data?.accessToken || body.data?.authResult?.accessToken;
    if (token) return token;

    // If OTP required and OTP is provided in env
    if (body.message === 'OTP required' && process.env.UAV_TEST_OTP) {
      const verifyRes = await request.post('/api/v1/auth/otp/verify', {
        data: {
          email,
          otp: process.env.UAV_TEST_OTP,
          purpose: 'Login',
        },
      });
      if (verifyRes.ok()) {
        const verifyBody = await verifyRes.json();
        return verifyBody.data?.authResult?.accessToken || verifyBody.data?.accessToken || '';
      }
    }
  }

  return '';
}

module.exports = { getAuthToken };
