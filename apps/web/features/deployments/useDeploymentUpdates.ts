"use client";

import { useQueryClient } from "@tanstack/react-query";
import * as signalR from "@microsoft/signalr";
import { useEffect } from "react";
import { TOKEN_KEY } from "@/lib/api";

/**
 * Subscribes to `deployment.updated` on /hubs/deployments and refreshes
 * deployment queries. Silently no-ops when the hub is unreachable.
 */
export function useDeploymentUpdates() {
  const qc = useQueryClient();
  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL ?? ""}/hubs/deployments`, {
        accessTokenFactory: () => localStorage.getItem(TOKEN_KEY) ?? "",
      })
      .withAutomaticReconnect()
      .build();

    conn.on("deployment.updated", () => {
      qc.invalidateQueries({ queryKey: ["deployments"] });
    });
    conn.start().catch(() => { /* hub down — page still works via refetch */ });
    return () => {
      conn.stop().catch(() => {});
    };
  }, [qc]);
}
