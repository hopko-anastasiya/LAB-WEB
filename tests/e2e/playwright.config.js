import { readFileSync } from 'node:fs';
import { defineConfig } from '@playwright/test';

const PORT = 5199;

function connectionString() {
  const raw = readFileSync(new URL('../../src/LabWeb/appsettings.Local.json', import.meta.url), 'utf8').replace(/^﻿/, '');
  const base = JSON.parse(raw).ConnectionStrings.Default;
  return base.replace(/Database=[^;]+/i, 'Database=labweb_e2e');
}

export default defineConfig({
  testDir: './specs',
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list']],
  globalSetup: './coverage-setup.js',
  globalTeardown: './coverage-report.js',
  use: {
    baseURL: `http://localhost:${PORT}`,
    locale: 'uk-UA',
    trace: 'retain-on-failure'
  },
  webServer: {
    command: `dotnet run --project ../../src/LabWeb --no-build --urls http://localhost:${PORT}`,
    url: `http://localhost:${PORT}/`,
    timeout: 120_000,
    reuseExistingServer: false,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      ConnectionStrings__Default: connectionString(),
      Auth__Google__ClientId: '',
      Auth__Google__ClientSecret: '',
      Ai__Gemini__ApiKey: ''
    }
  }
});
