"use client";

import { useState } from "react";
import { apiFetch } from "@/lib/api";
import type { DatabaseSchema } from "./types";

/** conn → schemas → tables/views tree; click a table to insert a SELECT into the editor */
export function SchemaTree({
  connectionId,
  onInsert,
}: {
  connectionId: string;
  onInsert: (sql: string) => void;
}) {
  const [schema, setSchema] = useState<DatabaseSchema | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [openSchemas, setOpenSchemas] = useState<Record<string, boolean>>({});
  const [openTables, setOpenTables] = useState<Record<string, boolean>>({});

  async function load() {
    setLoading(true);
    setError(null);
    try {
      setSchema(await apiFetch<DatabaseSchema>(
        `/api/databases/connections/${connectionId}/schema`));
    } catch (e) {
      setError(e instanceof Error ? e.message : "schema load failed");
    } finally {
      setLoading(false);
    }
  }

  if (!schema)
    return (
      <div>
        <button
          onClick={load}
          disabled={loading}
          className="rounded border border-border px-2 py-1 text-xs text-muted hover:text-fg disabled:opacity-40"
        >
          {loading ? "Loading…" : "Load schema"}
        </button>
        {error ? <div className="mt-1 text-xs text-err">{error}</div> : null}
      </div>
    );

  return (
    <div className="text-xs">
      <button
        onClick={load}
        className="mb-1 rounded border border-border px-2 py-0.5 text-muted hover:text-fg"
      >
        ↻
      </button>
      {schema.schemas.map((s) => (
        <div key={s.name}>
          <button
            onClick={() =>
              setOpenSchemas((o) => ({ ...o, [s.name]: !o[s.name] }))}
            className="w-full text-left font-medium text-fg hover:text-accent"
          >
            {openSchemas[s.name] ? "▾" : "▸"} {s.name}
          </button>
          {openSchemas[s.name] ? (
            <div className="ml-3">
              {s.tables.map((t) => {
                const key = `${s.name}.${t.name}`;
                return (
                  <div key={key}>
                    <button
                      onClick={() =>
                        setOpenTables((o) => ({ ...o, [key]: !o[key] }))}
                      onDoubleClick={() =>
                        onInsert(
                          `SELECT * FROM "${s.name}"."${t.name}" LIMIT 100`)}
                      className="w-full text-left text-muted hover:text-fg"
                      title="double-click to insert SELECT"
                    >
                      {openTables[key] ? "▾" : "▸"} {t.name}
                    </button>
                    {openTables[key] ? (
                      <div className="ml-3 text-muted">
                        {t.columns.map((c) => (
                          <div key={c.name} className="flex justify-between">
                            <span className={c.isPk ? "text-warn" : ""}>
                              {c.name}
                            </span>
                            <span className="opacity-60">{c.type}</span>
                          </div>
                        ))}
                        {t.indexes.map((ix) => (
                          <div key={ix.name} className="opacity-70">
                            ⚷ {ix.name} ({ix.columns.join(",")})
                          </div>
                        ))}
                      </div>
                    ) : null}
                  </div>
                );
              })}
              {s.views.map((v) => (
                <button
                  key={v}
                  onClick={() =>
                    onInsert(`SELECT * FROM "${s.name}"."${v}" LIMIT 100`)}
                  className="block w-full text-left text-muted/70 italic hover:text-fg"
                >
                  ◇ {v}
                </button>
              ))}
              {s.procedures.map((p) => (
                <div key={p.name} className="ml-0 text-muted/60">ƒ {p.name}</div>
              ))}
            </div>
          ) : null}
        </div>
      ))}
      {schema.schemas.length === 0 ? (
        <div className="text-muted">no user schemas</div>
      ) : null}
    </div>
  );
}
