export interface ApiErrorBody {
  code: string;
  message: string;
}

export interface ApiEnvelope<T> {
  data: T;
  error: ApiErrorBody | null;
}
