export interface DbConnection {
  id: string;
  name: string;
  kind: string;
  environmentId: string | null;
  environmentType: string | null;
  host: string;
  port: number;
  database: string;
  username: string;
  hasPassword: boolean;
  readOnly: boolean;
  maxRows: number;
  timeoutSeconds: number;
}

export interface ColumnInfo {
  name: string;
  type: string;
  nullable: boolean;
  isPk: boolean;
}

export interface IndexInfo {
  name: string;
  columns: string[];
  unique: boolean;
}

export interface TableInfo {
  name: string;
  columns: ColumnInfo[];
  indexes: IndexInfo[];
}

export interface ProcedureInfo {
  name: string;
}

export interface SchemaInfo {
  name: string;
  tables: TableInfo[];
  views: string[];
  procedures: ProcedureInfo[];
}

export interface DatabaseSchema {
  schemas: SchemaInfo[];
}

export interface ResultColumn {
  name: string;
  dataType: string;
}

export type Cell = string | number | boolean | null;

export interface QueryResult {
  columns: ResultColumn[];
  rows: Cell[][];
  rowCount: number;
  durationMs: number;
  truncated: boolean;
}

export interface ExecuteResponse {
  result: QueryResult;
  tier: "Read" | "Write" | "Dangerous";
  multiStatement: boolean;
}

export interface HistoryEntry {
  id: string;
  connectionId: string | null;
  sql: string;
  rowCount: number | null;
  durationMs: number | null;
  success: boolean;
  error: string | null;
  executedAt: string;
}

export interface SavedQuery {
  id: string;
  connectionId: string | null;
  name: string;
  sql: string;
  createdAt: string;
  updatedAt: string;
}
