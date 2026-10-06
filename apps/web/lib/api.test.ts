import { it, expect, vi } from "vitest";
import { apiFetch, ApiError } from "./api";

function sentHeaders(): Record<string, string> {
  const init = vi.mocked(fetch).mock.calls[0][1] as RequestInit;
  return init.headers as Record<string, string>;
}

it("unwraps data and sets auth header", async () => {
  localStorage.setItem("devstation.token", "t");
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => new Response(JSON.stringify({ data: { id: "1" }, error: null }), { status: 200 })),
  );
  const r = await apiFetch<{ id: string }>("/api/x");
  expect(r.id).toBe("1");
  expect(sentHeaders().Authorization).toBe("Bearer t");
});

it("throws ApiError on error envelope", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => new Response(JSON.stringify({ data: null, error: { code: "X", message: "bad" } }), { status: 400 })),
  );
  await expect(apiFetch("/api/x")).rejects.toMatchObject({ code: "X" });
});

it("tolerates non-JSON body on 401", async () => {
  vi.stubGlobal("fetch", vi.fn(async () => new Response("", { status: 401 })));
  await expect(apiFetch("/api/x")).rejects.toMatchObject({ code: "HTTP_401", status: 401 });
});

it("sets content-type on bodies", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => new Response(JSON.stringify({ data: 1, error: null }), { status: 200 })),
  );
  await apiFetch("/api/x", { method: "POST", body: JSON.stringify({ a: 1 }) });
  expect(sentHeaders()["Content-Type"]).toBe("application/json");
});

it("is an ApiError instance", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => new Response(JSON.stringify({ data: null, error: { code: "X", message: "bad" } }), { status: 400 })),
  );
  await expect(apiFetch("/api/x")).rejects.toBeInstanceOf(ApiError);
});
