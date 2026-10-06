export interface ContainerInfo {
  id: string;
  name: string;
  image: string;
  state: string;
  status: string;
  ports: string[];
}

export interface ContainerMount {
  type: string;
  source: string;
  destination: string;
  readWrite: boolean;
}

export interface ContainerNetwork {
  name: string;
  ipAddress: string | null;
}

export interface ContainerInspect {
  id: string;
  name: string;
  image: string;
  state: string;
  exitCode: number | null;
  startedAt: string | null;
  createdAt: string | null;
  env: string[];
  mounts: ContainerMount[];
  networks: ContainerNetwork[];
}

export interface ContainerStats {
  id: string;
  name: string;
  cpuPercent: string | null;
  memUsage: string | null;
  memPercent: string | null;
  netIo: string | null;
  blockIo: string | null;
  pids: string | null;
}
