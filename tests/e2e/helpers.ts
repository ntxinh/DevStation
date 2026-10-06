import type { APIRequestContext, Page } from "@playwright/test";

export const TOKEN = process.env.DEVSTATION_TOKEN ?? "dev-token-change-me";
export const API_URL = process.env.E2E_API_URL ?? "http://localhost:5080";

/** localStorage key used by apps/web/lib/api.ts */
export const TOKEN_KEY = "devstation.token";

/** Seed the bearer token so TokenGate lets the app through. */
export async function seedToken(page: Page) {
  await page.addInitScript(
    ([key, token]) => window.localStorage.setItem(key, token),
    [TOKEN_KEY, TOKEN],
  );
}

async function api<T>(
  request: APIRequestContext,
  method: "post",
  path: string,
  data: unknown,
): Promise<T> {
  const res = await request[method](`${API_URL}${path}`, {
    headers: { Authorization: `Bearer ${TOKEN}` },
    data,
  });
  if (!res.ok()) {
    throw new Error(
      `${method.toUpperCase()} ${path} -> ${res.status()}: ${await res.text()}`,
    );
  }
  const body = await res.json();
  return (body?.data ?? body) as T;
}

export function seedProject(request: APIRequestContext, name: string) {
  return api<{ id: string }>(request, "post", "/api/projects", {
    name,
    description: "e2e seeded",
    repositoryUrl: null,
    localPath: null,
    defaultBranch: "main",
    technology: "test",
    tags: ["e2e"],
  });
}

export function seedDbConnection(
  request: APIRequestContext,
  name: string,
  host = process.env.E2E_DB_HOST ?? "localhost",
  port = Number(process.env.E2E_DB_PORT ?? 55432),
) {
  return api<{ id: string }>(request, "post", "/api/databases/connections", {
    name,
    kind: "postgresql",
    host,
    port,
    database: "devstation",
    username: "devstation",
    password: "devstation",
    environmentId: null,
    readOnly: true,
    maxRows: 100,
    timeoutSeconds: 10,
  });
}
