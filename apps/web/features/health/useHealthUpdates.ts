"use client";

import { useQueryClient } from "@tanstack/react-query";
import * as signalR from "@microsoft/signalr";
import { useEffect } from "react";
import { TOKEN_KEY } from "@/lib/api";

/**
 * Subscribes to `health.updated` on /hubs/health and refreshes health queries.
 * Silently no-ops when the hub is unreachable — polling freshness isn't critical.
 */
export function useHealthUpdates() {
  const qc = useQueryClient();
  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL ?? ""}/hubs/health`, {
        accessTokenFactory: () => localStorage.getItem(TOKEN_KEY) ?? "",
      })
      .withAutomaticReconnect()
      .build();

    conn.on("health.updated", () => {
      qc.invalidateQueries({ queryKey: ["health"] });
      qc.invalidateQueries({ queryKey: ["environments"] });
    });
    conn.start().catch(() => { /* hub down — page still works via refetch */ });
    return () => {
      conn.stop().catch(() => {});
    };
  }, [qc]);
}
