// @ts-check
const { defineConfig } = require('@playwright/test');

/**
 * Playwright configuration for API testing on UAV PMS Swagger.
 * No browsers needed – only the built-in APIRequestContext is used.
 */
module.exports = defineConfig({
  testDir: './specs',
  timeout: 60_000,
  retries: 0,
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
  ],

  use: {
    baseURL: 'https://uavpms.ddns.net',
    extraHTTPHeaders: {
      'Accept': 'application/json',
    },
  },

  projects: [
    {
      name: 'api-tests',
      testDir: './specs',
    },
  ],
});
