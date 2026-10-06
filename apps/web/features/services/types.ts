export interface Service {
  id: string;
  name: string;
  description: string | null;
  type: string | null;
  url: string | null;
  containerId: string | null;
  version: string | null;
  status: string;
  environmentId: string | null;
  environmentName: string | null;
  updatedAt: string;
}

/** url or host:port → the host:port part for display. */
export function serviceHostPort(url: string | null): string {
  if (!url) return "—";
  const m = url.match(/^https?:\/\/([^/]+)/);
  return m ? m[1]! : url;
}
