"use client";

import { useRef } from "react";

// keyword tables mirror SqlGuard's tiers — display hints only, backend decides
const READ: Record<string, true> = {
  select: true, with: true, values: true, table: true, show: true,
  explain: true, describe: true,
};
const WRITE: Record<string, true> = {
  insert: true, update: true, delete: true, merge: true, call: true,
  execute: true, exec: true,
};
const DANGER: Record<string, true> = {
  drop: true, truncate: true, alter: true, create: true, grant: true,
  revoke: true, deny: true, backup: true, restore: true, attach: true,
  detach: true, copy: true, do: true, vacuum: true, set: true, begin: true,
  commit: true, rollback: true,
};
const CLAUSE: Record<string, true> = {
  from: true, where: true, join: true, inner: true, left: true, right: true,
  outer: true, cross: true, on: true, group: true, by: true, order: true,
  having: true, limit: true, offset: true, as: true, and: true, or: true,
  not: true, null: true, in: true, is: true, between: true, like: true,
  returning: true, case: true, when: true, then: true, else: true, end: true,
  union: true, all: true, distinct: true, into: true, using: true,
};
function esc(s: string) {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

export function highlightSql(sql: string): string {
  let out = "";
  let i = 0;
  const n = sql.length;
  while (i < n) {
    const c = sql[i];
    if (c === "-" && sql[i + 1] === "-") {
      const e = sql.indexOf("\n", i);
      const end = e < 0 ? n : e;
      out += `<span class="text-muted italic">${esc(sql.slice(i, end))}</span>`;
      i = end;
    } else if (c === "/" && sql[i + 1] === "*") {
      const e = sql.indexOf("*/", i + 2);
      const end = e < 0 ? n : e + 2;
      out += `<span class="text-muted italic">${esc(sql.slice(i, end))}</span>`;
      i = end;
    } else if (c === "'" || c === '"') {
      let j = i + 1;
      while (j < n) {
        if (sql[j] === "\\") { j += 2; continue; }
        if (sql[j] === c) {
          if (sql[j + 1] === c) { j += 2; continue; }
          j++;
          break;
        }
        j++;
      }
      out += `<span class="text-ok">${esc(sql.slice(i, j))}</span>`;
      i = j;
    } else if (/[$]/.test(c)) {
      const m = /^(\$[A-Za-z0-9_]*\$)/.exec(sql.slice(i));
      if (m) {
        const tag = m[1];
        const close = sql.indexOf(tag, i + tag.length);
        const end = close < 0 ? n : close + tag.length;
        out += `<span class="text-ok">${esc(sql.slice(i, end))}</span>`;
        i = end;
      } else {
        out += esc(c);
        i++;
      }
    } else if (/[A-Za-z0-9_]/.test(c)) {
      const m = /^[A-Za-z0-9_]+/.exec(sql.slice(i))!;
      const w = m[0];
      const low = w.toLowerCase();
      if (DANGER[low]) out += `<span class="text-err font-semibold">${esc(w)}</span>`;
      else if (WRITE[low]) out += `<span class="text-warn font-semibold">${esc(w)}</span>`;
      else if (READ[low] || CLAUSE[low]) out += `<span class="text-accent">${esc(w)}</span>`;
      else out += esc(w);
      i += w.length;
    } else {
      out += esc(c);
      i++;
    }
  }
  return out + "\n"; // trailing newline keeps height in sync
}

export function SqlEditor({
  value,
  onChange,
  onRun,
}: {
  value: string;
  onChange: (v: string) => void;
  onRun: () => void;
}) {
  const pre = useRef<HTMLPreElement>(null);

  const shared =
    "font-mono text-xs leading-5 whitespace-pre-wrap break-words p-3 m-0";

  return (
    <div className="relative min-h-40 overflow-hidden rounded border border-border bg-bg">
      <pre
        ref={pre}
        aria-hidden
        className={`${shared} pointer-events-none absolute inset-0 overflow-hidden text-transparent`}
      >
        <span dangerouslySetInnerHTML={{ __html: highlightSql(value) }} />
      </pre>
      <textarea
        value={value}
        spellCheck={false}
        onChange={(e) => onChange(e.target.value)}
        onScroll={(e) => {
          if (pre.current) {
            pre.current.scrollTop = e.currentTarget.scrollTop;
            pre.current.scrollLeft = e.currentTarget.scrollLeft;
          }
        }}
        onKeyDown={(e) => {
          if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            onRun();
          }
        }}
        placeholder="SELECT …  (Ctrl+Enter to run)"
        className={`${shared} relative h-40 w-full resize-y bg-transparent caret-fg outline-none`}
        style={{ color: "transparent", WebkitTextFillColor: "transparent" }}
      />
    </div>
  );
}
