"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import { formatRelativeTime } from "@/lib/format";
import type { HistoryEntry, SavedQuery } from "./types";

/** Query history for the selected connection — click reloads the sql. */
export function QueryHistoryPanel({
  connectionId,
  onLoad,
}: {
  connectionId: string;
  onLoad: (sql: string) => void;
}) {
  const { data } = useQuery({
    queryKey: ["db-history", connectionId],
    queryFn: () =>
      apiFetch<HistoryEntry[]>(
        `/api/databases/history?connectionId=${connectionId}&limit=50`),
  });
  if (!data?.length)
    return <div className="p-2 text-xs text-muted">no history yet</div>;
  return (
    <div className="flex flex-col">
      {data.map((h) => (
        <button
          key={h.id}
          onClick={() => onLoad(h.sql)}
          className="border-b border-border/50 px-2 py-1.5 text-left hover:bg-panel"
        >
          <div className="truncate font-mono text-xs text-fg">{h.sql}</div>
          <div className="mt-0.5 text-[10px] text-muted">
            {h.success ? `${h.rowCount ?? "?"} rows` : "failed"}
            {h.durationMs != null ? ` · ${h.durationMs}ms` : ""}
            {" · "}
            {formatRelativeTime(h.executedAt)}
            {h.error ? <span className="text-err"> · {h.error}</span> : null}
          </div>
        </button>
      ))}
    </div>
  );
}

/** Saved queries — click loads into the editor, save/delete inline. */
export function SavedQueriesPanel({
  connectionId,
  sql,
  onLoad,
}: {
  connectionId: string;
  sql: string;
  onLoad: (sql: string) => void;
}) {
  const qc = useQueryClient();
  const { data } = useQuery({
    queryKey: ["db-saved", connectionId],
    queryFn: () =>
      apiFetch<SavedQuery[]>(
        `/api/databases/saved-queries?connectionId=${connectionId}`),
  });
  const invalidate = () =>
    qc.invalidateQueries({ queryKey: ["db-saved", connectionId] });
  const save = useMutation({
    mutationFn: () => {
      const name = window.prompt("Save query as:");
      if (!name?.trim()) return Promise.resolve(null);
      return apiFetch<SavedQuery>("/api/databases/saved-queries", {
        method: "POST",
        body: JSON.stringify({ connectionId, name: name.trim(), sql }),
      });
    },
    onSuccess: invalidate,
  });
  const del = useMutation({
    mutationFn: (id: string) =>
      apiFetch(`/api/databases/saved-queries/${id}`, { method: "DELETE" }),
    onSuccess: invalidate,
  });

  return (
    <div className="flex flex-col">
      <button
        onClick={() => save.mutate()}
        disabled={!sql.trim()}
        className="border-b border-border px-2 py-1.5 text-left text-xs text-accent hover:bg-panel disabled:opacity-40"
      >
        + Save current query
      </button>
      {(data ?? []).map((q) => (
        <div key={q.id}
          className="flex items-center gap-1 border-b border-border/50 px-2 py-1.5 hover:bg-panel">
          <button onClick={() => onLoad(q.sql)} className="flex-1 text-left">
            <div className="text-xs text-fg">{q.name}</div>
            <div className="truncate font-mono text-[10px] text-muted">
              {q.sql}
            </div>
          </button>
          <button
            onClick={() => del.mutate(q.id)}
            className="text-[10px] text-muted hover:text-err"
            aria-label={`delete saved query ${q.name}`}
          >
            ✕
          </button>
        </div>
      ))}
      {data && data.length === 0 ? (
        <div className="p-2 text-xs text-muted">no saved queries</div>
      ) : null}
    </div>
  );
}
