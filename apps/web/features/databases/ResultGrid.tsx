import type { QueryResult } from "./types";

function renderCell(v: unknown): string {
  if (v === null || v === undefined) return "NULL";
  if (typeof v === "object") return JSON.stringify(v);
  return String(v);
}

export function ResultGrid({ result }: { result: QueryResult }) {
  if (result.columns.length === 0)
    return (
      <div className="text-xs text-muted">
        {result.rowCount >= 0
          ? `${result.rowCount} rows affected`
          : "command completed"}
      </div>
    );
  return (
    <div className="max-h-80 overflow-auto rounded border border-border">
      <table className="w-full border-collapse text-xs">
        <thead className="sticky top-0 bg-panel">
          <tr className="border-b border-border text-left text-muted">
            {result.columns.map((c, i) => (
              <th key={i} className="px-2 py-1.5 font-medium whitespace-nowrap">
                {c.name}
                <span className="ml-1 font-normal opacity-60">{c.dataType}</span>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {result.rows.map((row, ri) => (
            <tr key={ri} className="border-b border-border/50 hover:bg-panel">
              {row.map((cell, ci) => (
                <td
                  key={ci}
                  className={`max-w-64 truncate px-2 py-1 font-mono ${
                    cell === null ? "italic text-muted" : "text-fg"
                  }`}
                >
                  {renderCell(cell)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
