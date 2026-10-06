"use client";

import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import * as signalR from "@microsoft/signalr";
import { LuBell, LuCheckCheck } from "react-icons/lu";
import { apiFetch, TOKEN_KEY } from "@/lib/api";

interface NotificationRow {
  id: string;
  type: string;
  title: string;
  message: string | null;
  link: string | null;
  read: boolean;
  severity: string | null;
  createdAt: string;
}

const SEVERITY_DOT: Record<string, string> = {
  error: "bg-err",
  warn: "bg-warn",
  info: "bg-accent",
};

/** Unread-count badge + dropdown; live updates via `notification.new`
 * on /hubs/notifications invalidate the query (refetch does the work). */
export function NotificationsBell() {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const { data } = useQuery({
    queryKey: ["notifications"],
    queryFn: () => apiFetch<NotificationRow[]>("/api/notifications"),
    refetchInterval: 60_000,
  });
  const unread = (data ?? []).filter((n) => !n.read).length;

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL ?? ""}/hubs/notifications`, {
        accessTokenFactory: () => localStorage.getItem(TOKEN_KEY) ?? "",
      })
      .withAutomaticReconnect()
      .build();
    conn.on("notification.new", () =>
      qc.invalidateQueries({ queryKey: ["notifications"] }));
    conn.start().catch(() => { /* hub down — badge still polls */ });
    return () => { conn.stop().catch(() => {}); };
  }, [qc]);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen((o) => !o)}
        aria-label={`Notifications${unread ? `, ${unread} unread` : ""}`}
        className="relative text-muted hover:text-fg"
      >
        <LuBell className="h-4 w-4" aria-hidden />
        {unread > 0 ? (
          <span className="absolute -right-1.5 -top-1.5 flex h-3.5 min-w-3.5 items-center justify-center rounded-full bg-err px-0.5 text-[9px] font-semibold text-white">
            {unread > 99 ? "99+" : unread}
          </span>
        ) : null}
      </button>
      {open ? (
        <div className="absolute right-0 top-7 z-40 w-80 rounded-lg border border-border bg-panel text-xs shadow-lg">
          <div className="flex items-center justify-between border-b border-border px-3 py-2">
            <span className="font-medium">Notifications</span>
            <button
              onClick={() =>
                apiFetch("/api/notifications/read-all", { method: "POST" })
                  .then(() => qc.invalidateQueries({ queryKey: ["notifications"] }))}
              className="flex items-center gap-1 text-muted hover:text-fg"
            >
              <LuCheckCheck className="h-3.5 w-3.5" aria-hidden /> Mark all read
            </button>
          </div>
          <div className="max-h-80 overflow-auto">
            {(data ?? []).length === 0 ? (
              <div className="px-3 py-4 text-muted">No notifications.</div>
            ) : (
              (data ?? []).map((n) => (
                <button
                  key={n.id}
                  onClick={() => {
                    if (!n.read)
                      apiFetch(`/api/notifications/${n.id}/read`, { method: "POST" })
                        .then(() => qc.invalidateQueries({ queryKey: ["notifications"] }));
                  }}
                  className={`flex w-full items-start gap-2 border-b border-border/50 px-3 py-2 text-left last:border-0 hover:bg-bg ${n.read ? "opacity-50" : ""}`}
                >
                  <span className={`mt-1 h-1.5 w-1.5 shrink-0 rounded-full ${SEVERITY_DOT[n.severity ?? "info"] ?? "bg-accent"}`} />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium text-fg">{n.title}</span>
                    {n.message ? (
                      <span className="block truncate text-muted">{n.message}</span>
                    ) : null}
                    <span className="block text-[10px] text-muted">
                      {new Date(n.createdAt).toLocaleString()}
                    </span>
                  </span>
                </button>
              ))
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}
