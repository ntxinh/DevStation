"use client";

import { useQuery } from "@tanstack/react-query";
import * as signalR from "@microsoft/signalr";
import { useEffect, useRef, useState } from "react";
import { apiFetch, TOKEN_KEY } from "@/lib/api";
import { formatDateTime } from "@/lib/format";

interface LogLine {
  ts: string;
  level: string;
  source: string;
  environmentId: string | null;
  message: string;
  meta: Record<string, string> | null;
}

interface Environment {
  id: string;
  name: string;
}

const LEVEL_CLASS: Record<string, string> = {
  trace: "text-muted",
  debug: "text-muted",
  info: "text-fg",
  warn: "text-warn",
  error: "text-err",
  fatal: "text-err",
};

export default function LogsPage() {
  const [source, setSource] = useState("");
  const [severity, setSeverity] = useState("");
  const [envId, setEnvId] = useState("");
  const [q, setQ] = useState("");
  const [paused, setPaused] = useState(false);
  const [live, setLive] = useState<LogLine[]>([]);
  const liveRef = useRef<LogLine[]>([]);
  const bottomRef = useRef<HTMLDivElement>(null);
  const pausedRef = useRef(false);
  pausedRef.current = paused;

  const params = new URLSearchParams();
  if (source) params.set("source", source);
  if (severity) params.set("severity", severity);
  if (envId) params.set("environmentId", envId);
  if (q) params.set("q", q);

  const { data, error, isLoading } = useQuery({
    queryKey: ["logs", source, severity, envId, q],
    queryFn: () => apiFetch<LogLine[]>(`/api/logs?${params}`),
  });

  const { data: envs } = useQuery({
    queryKey: ["environments"],
    queryFn: () => apiFetch<Environment[]>("/api/environments"),
    retry: false,
  });

  // live tail via /hubs/logs — appended client-side, honoring current filters
  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL ?? ""}/hubs/logs`, {
        accessTokenFactory: () => localStorage.getItem(TOKEN_KEY) ?? "",
      })
      .withAutomaticReconnect()
      .build();
    conn.on("log.entry", (e: LogLine) => {
      if (pausedRef.current) return;
      liveRef.current = [...liveRef.current.slice(-499), e];
      setLive(liveRef.current);
    });
    conn.start().catch(() => { /* hub down — history still loads */ });
    return () => {
      conn.stop().catch(() => {});
      liveRef.current = [];
    };
  }, []);

  const matches = (l: LogLine) =>
    (!source || l.source === source) &&
    (!severity || l.level === severity) &&
    (!envId || l.environmentId === envId) &&
    (!q || l.message.toLowerCase().includes(q.toLowerCase()));
  const download = async () => {
    // export returns text/plain — apiFetch falls back to raw text
    const text = await apiFetch<string>(`/api/logs/export?${params}`);
    const url = URL.createObjectURL(new Blob([text], { type: "text/plain" }));
    const a = document.createElement("a");
    a.href = url;
    a.download = "logs.txt";
    a.click();
    URL.revokeObjectURL(url);
  };

  // server history is newest-first; render oldest-first and let live
  // entries append at the bottom like a real tail
  const lines = [...(data ?? []), ...live.filter(matches)].toSorted(
    (a, b) => Date.parse(a.ts) - Date.parse(b.ts),
  );

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "instant" });
  }, [data, live]);
  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Logs</h1>
        <div className="flex gap-2">
          <button
            onClick={() => setPaused((p) => !p)}
            className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
          >
            {paused ? "Resume stream" : "Pause"}
          </button>
          <button
            onClick={download}
            className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
          >
            Export
          </button>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        <select
          value={source}
          onChange={(e) => setSource(e.target.value)}
          aria-label="Source"
          className="rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
        >
          <option value="">All sources</option>
          <option value="api">api</option>
          <option value="deployment">deployment</option>
          <option value="job">job</option>
          <option value="container">container</option>
        </select>
        <select
          value={severity}
          onChange={(e) => setSeverity(e.target.value)}
          aria-label="Severity"
          className="rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
        >
          <option value="">All levels</option>
          {["trace", "debug", "info", "warn", "error", "fatal"].map((l) => (
            <option key={l} value={l}>
              {l}
            </option>
          ))}
        </select>
        <select
          value={envId}
          onChange={(e) => setEnvId(e.target.value)}
          aria-label="Environment"
          className="rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
        >
          <option value="">All environments</option>
          {(envs ?? []).map((e) => (
            <option key={e.id} value={e.id}>
              {e.name}
            </option>
          ))}
        </select>
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="Search messages…"
          aria-label="Search messages"
          className="w-56 rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
        />
      </div>

      {error ? (
        <div className="text-xs text-err">{String(error)}</div>
      ) : null}
      <div className="min-h-0 flex-1 overflow-auto rounded border border-border bg-bg p-2 font-mono text-[11px] leading-relaxed">
        {isLoading ? <div className="text-muted">Loading…</div> : null}
        {lines.length === 0 && !isLoading ? (
          <div className="text-muted">No log lines.</div>
        ) : null}
        {lines.map((l, i) => (
          <div key={`${l.ts}-${i}`} className="whitespace-pre-wrap">
            <span className="text-muted">{formatDateTime(l.ts)} </span>
            <span className={LEVEL_CLASS[l.level] ?? "text-fg"}>
              {l.level.padEnd(5)}{" "}
            </span>
            <span className="text-accent">{l.source.padEnd(10)} </span>
            <span>{l.message}</span>
          </div>
        ))}
        <div ref={bottomRef} />
      </div>
    </div>
  );
}
