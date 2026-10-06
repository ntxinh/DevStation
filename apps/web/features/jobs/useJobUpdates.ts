"use client";

import { useQueryClient } from "@tanstack/react-query";
import * as signalR from "@microsoft/signalr";
import { useEffect } from "react";
import { TOKEN_KEY } from "@/lib/api";

/**
 * Subscribes to `job.updated` on /hubs/jobs and refreshes job queries.
 * Silently no-ops when the hub is unreachable — the page refetches anyway.
 */
export function useJobUpdates() {
  const qc = useQueryClient();
  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL ?? ""}/hubs/jobs`, {
        accessTokenFactory: () => localStorage.getItem(TOKEN_KEY) ?? "",
      })
      .withAutomaticReconnect()
      .build();

    conn.on("job.updated", () => {
      qc.invalidateQueries({ queryKey: ["jobs"] });
    });
    conn.start().catch(() => { /* hub down — page still works via refetch */ });
    return () => {
      conn.stop().catch(() => {});
    };
  }, [qc]);
}
