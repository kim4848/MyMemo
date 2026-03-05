import type {
  Session,
  SessionDetail,
  Chunk,
  Memo,
  CreateSessionRequest,
  OutputMode,
} from '../types';

const API_BASE = import.meta.env.VITE_API_URL ?? '';

type TokenProvider = () => Promise<string | null>;
let tokenProvider: TokenProvider = () => Promise.resolve(null);

export function setTokenProvider(provider: TokenProvider) {
  tokenProvider = provider;
}

export class ApiError extends Error {
  status: number;
  body: string;

  constructor(status: number, body: string) {
    super(`API error ${status}: ${body}`);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

interface RequestOptions extends RequestInit {
  /** When true, skip JSON parsing and return undefined (for 202/204 responses) */
  noContent?: boolean;
}

async function request<T>(
  path: string,
  options?: RequestOptions,
): Promise<T> {
  const { noContent, ...fetchOptions } = options ?? {};
  const token = await tokenProvider();
  const headers: Record<string, string> = {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(fetchOptions.headers as Record<string, string>),
  };

  const res = await fetch(`${API_BASE}${path}`, {
    ...fetchOptions,
    headers,
  });

  if (!res.ok) {
    throw new ApiError(res.status, await res.text());
  }

  if (noContent || res.status === 204) return undefined as T;
  return res.json();
}

export const api = {
  sessions: {
    create: (body: CreateSessionRequest) =>
      request<Session>('/api/sessions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      }),
    list: () => request<Session[]>('/api/sessions'),
    get: (id: string) => request<SessionDetail>(`/api/sessions/${id}`),
    delete: (id: string) =>
      request<void>(`/api/sessions/${id}`, { method: 'DELETE' }),
  },
  chunks: {
    upload: (sessionId: string, audio: Blob, chunkIndex: number) => {
      const form = new FormData();
      form.append('audio', audio);
      form.append('chunkIndex', chunkIndex.toString());
      return request<Chunk>(`/api/sessions/${sessionId}/chunks`, {
        method: 'POST',
        body: form,
      });
    },
  },
  memos: {
    finalize: (sessionId: string) =>
      request<void>(`/api/sessions/${sessionId}/finalize`, {
        method: 'POST',
        noContent: true,
      }),
    get: (sessionId: string) =>
      request<Memo>(`/api/sessions/${sessionId}/memo`),
    regenerate: (sessionId: string, outputMode: OutputMode, context?: string) =>
      request<void>(`/api/sessions/${sessionId}/regenerate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ outputMode, context }),
        noContent: true,
      }),
  },
};
