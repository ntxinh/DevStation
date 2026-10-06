import { defineConfig } from "@playwright/test";

const token = process.env.DEVSTATION_TOKEN ?? "dev-token-change-me";
const apiUrl = process.env.E2E_API_URL ?? "http://localhost:5080";

export default defineConfig({
  testDir: "../../tests/e2e",
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  retries: process.env.CI ? 1 : 0,
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:3000",
  },
  // Both servers are reused if already running (e.g. `make dev` or CI compose
  // already up — reuseExistingServer probes the url and skips the command).
  webServer: [
    {
      command: "../../scripts/e2e-api.sh",
      url: `${apiUrl}/health`,
      reuseExistingServer: true,
      timeout: 240_000,
      env: {
        DEVSTATION_TOKEN: token,
        // empty string keeps the script default (dedicated pg on :55432);
        // set DEVSTATION_E2E_DB to point at another Postgres (e.g. CI service)
        ...(process.env.DEVSTATION_E2E_DB
          ? { DEVSTATION_E2E_DB: process.env.DEVSTATION_E2E_DB }
          : {}),
      },
    },
    {
      command: "pnpm dev",
      url: "http://localhost:3000",
      reuseExistingServer: true,
      timeout: 120_000,
      env: { NEXT_PUBLIC_API_URL: apiUrl },
    },
  ],
});
