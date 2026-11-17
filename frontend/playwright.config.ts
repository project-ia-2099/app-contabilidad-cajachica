import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './e2e', timeout: 45_000, fullyParallel: false,
  use: { baseURL: 'http://127.0.0.1:5175', channel: process.env.PLAYWRIGHT_CHANNEL, headless: true, launchOptions: { slowMo: Number(process.env.PLAYWRIGHT_SLOW_MO ?? 0) } },
  projects: [ { name: 'desktop', use: { viewport: { width: 1440, height: 1000 } } }, { name: 'mobile', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } } ],
  webServer: { command: 'npm run dev -- --port 5175', url: 'http://127.0.0.1:5175', env: { VITE_API_URL: 'http://127.0.0.1:5100' }, reuseExistingServer: false },
});
