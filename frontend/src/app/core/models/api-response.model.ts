export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: string | null;
  meta: ApiMeta | null;
}

export interface ApiMeta {
  total: number;
  page: number;
  pageSize: number;
}
