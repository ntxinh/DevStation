"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { ErrorState } from "@/components/ErrorState";
import type { AppEnvironment, CompareResult } from "@/features/environments/types";
import { apiFetch } from "@/lib/api";
import { formatDateTime } from "@/lib/format";

const SELECT =
  "rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent";

function ComparePage() {
  const params = useSearchParams();
  const router = useRouter();
  const a = params.get("a") ?? "";
  const b = params.get("b") ?? "";

  const envs = useQuery({
    queryKey: ["environments"],
    queryFn: () => apiFetch<AppEnvironment[]>("/api/environments"),
  });
  const diff = useQuery({
    queryKey: ["environments", "compare", a, b],
    queryFn: () =>
      apiFetch<CompareResult>(`/api/environments/compare?a=${a}&b=${b}`),
    enabled: a !== "" && b !== "" && a !== b,
  });

  const setParam = (k: string, v: string) => {
    const next = new URLSearchParams(params.toString());
    if (v) next.set(k, v); else next.delete(k);
    router.replace(`/environments/compare?${next.toString()}`);
  };

  const c = diff.data;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-3">
        <Link href="/environments" className="text-xs text-muted hover:text-fg">← Environments</Link>
        <h1 className="text-lg font-semibold">Compare environments</h1>
      </div>
      <div className="flex items-center gap-2">
        <select value={a} onChange={(e) => setParam("a", e.target.value)} className={SELECT} aria-label="Environment A">
          <option value="">Select A…</option>
          {(envs.data ?? []).map((e) => (
            <option key={e.id} value={e.id}>{e.name}</option>
          ))}
        </select>
        <span className="text-xs text-muted">vs</span>
        <select value={b} onChange={(e) => setParam("b", e.target.value)} className={SELECT} aria-label="Environment B">
          <option value="">Select B…</option>
          {(envs.data ?? []).map((e) => (
            <option key={e.id} value={e.id}>{e.name}</option>
          ))}
        </select>
      </div>
      {a !== "" && a === b ? (
        <div className="text-xs text-muted">Pick two different environments.</div>
      ) : null}
      {diff.error ? <ErrorState error={diff.error} /> : null}
      {c ? (
        <div className="flex flex-col gap-3">
          <div className="rounded-lg border border-border bg-panel p-3 text-xs">
            <div className="grid grid-cols-[7rem_1fr_1fr] gap-x-3 gap-y-1.5">
              <span className="text-muted" />
              <span className="font-medium">{envs.data?.find((e) => e.id === a)?.name ?? "A"}</span>
              <span className="font-medium">{envs.data?.find((e) => e.id === b)?.name ?? "B"}</span>
              <span className="text-muted">Version</span>
              <span className="font-mono">{c.version.a ?? "—"}</span>
              <span className="font-mono">{c.version.b ?? "—"}</span>
              <span className="text-muted">Commit</span>
              <span className="font-mono">{c.commit.a?.slice(0, 12) ?? "—"}</span>
              <span className="font-mono">{c.commit.b?.slice(0, 12) ?? "—"}</span>
              <span className="text-muted">Deployed</span>
              <span>{formatDateTime(c.deployedAt.a)}</span>
              <span>{formatDateTime(c.deployedAt.b)}</span>
            </div>
          </div>

          <div className="rounded-lg border border-border bg-panel p-3 text-xs">
            <h2 className="text-xs font-medium text-muted">Services</h2>
            {c.services.length === 0 ? <p className="mt-2 text-muted">None</p> : (
              <table className="mt-2 w-full border-collapse">
                <tbody>
                  {c.services.map((s) => (
                    <tr key={s.name} className="border-b border-border/50 last:border-0">
                      <td className="py-1 pr-3 font-mono">{s.name}</td>
                      <td className={`py-1 pr-3 ${s.inA ? "" : "text-err"}`}>{s.inA ? "✓" : "missing"}</td>
                      <td className={`py-1 ${s.inB ? "" : "text-err"}`}>{s.inB ? "✓" : "missing"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          <div className="rounded-lg border border-border bg-panel p-3 text-xs">
            <h2 className="text-xs font-medium text-muted">Variables</h2>
            {c.variables.length === 0 ? <p className="mt-2 text-muted">None</p> : (
              <table className="mt-2 w-full border-collapse">
                <tbody>
                  {c.variables.map((v) => (
                    <tr key={v.key} className="border-b border-border/50 last:border-0">
                      <td className="py-1 pr-3 font-mono">{v.key}</td>
                      <td className="py-1 pr-3 text-muted">{v.masked ? "secret" : "plain"}</td>
                      <td className={`py-1 ${v.differs ? "text-warn" : "text-muted"}`}>
                        {v.differs ? "differs" : "same"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}

export default function Page() {
  return (
    <Suspense fallback={<div className="text-xs text-muted">Loading…</div>}>
      <ComparePage />
    </Suspense>
  );
}
