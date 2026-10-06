import type { ApiEnvelope } from "./types";

export class ApiError extends Error {
  constructor(
    public code: string,
    message: string,
    public status: number,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export const TOKEN_KEY = "devstation.token";

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = typeof window === "undefined" ? null : localStorage.getItem(TOKEN_KEY);
  const headers: Record<string, string> = {
    ...(init.headers ? Object.fromEntries(new Headers(init.headers)) : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
  if (init.body && !new Headers(headers).has("Content-Type")) {
    headers["Content-Type"] = "application/json";
  }

  const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL ?? ""}${path}`, { ...init, headers });
  const text = await res.text();
  let envelope: ApiEnvelope<T> | null = null;
  try {
    envelope = text ? (JSON.parse(text) as ApiEnvelope<T>) : null;
  } catch {
    envelope = null;
  }

  if (!res.ok) {
    const err = envelope?.error ?? { code: `HTTP_${res.status}`, message: res.statusText };
    throw new ApiError(err.code, err.message, res.status);
  }
  if (envelope?.error) throw new ApiError(envelope.error.code, envelope.error.message, res.status);
  return (envelope ? envelope.data : (text as unknown)) as T;
}
