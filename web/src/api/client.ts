import type {
  Session,
  SessionWithTags,
  SessionDetail,
  Chunk,
  Memo,
  Infographic,
  Tag,
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

const REQUEST_TIMEOUT_MS = 30_000;
const MAX_RETRIES = 3;
const RETRY_BASE_MS = 1_000;

function isRetryable(error: unknown): boolean {
  if (error instanceof ApiError) {
    return error.status === 502 || error.status === 503 || error.status === 504;
  }
  return error instanceof TypeError || error instanceof DOMException;
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

  let lastError: unknown;
  for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
    if (attempt > 0) {
      await new Promise(r => setTimeout(r, RETRY_BASE_MS * Math.pow(2, attempt - 1)));
    }

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

    try {
      const res = await fetch(`${API_BASE}${path}`, {
        ...fetchOptions,
        headers,
        signal: fetchOptions.signal ?? controller.signal,
      });

      clearTimeout(timeout);

      if (!res.ok) {
        const err = new ApiError(res.status, await res.text());
        if (attempt < MAX_RETRIES && isRetryable(err)) {
          lastError = err;
          continue;
        }
        throw err;
      }

      if (noContent || res.status === 204) return undefined as T;
      return res.json();
    } catch (error) {
      clearTimeout(timeout);
      lastError = error;
      if (attempt < MAX_RETRIES && isRetryable(error)) continue;
      throw error;
    }
  }

  throw lastError;
}

export const api = {
  sessions: {
    create: (body: CreateSessionRequest) =>
      request<Session>('/api/sessions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      }),
    list: () => request<SessionWithTags[]>('/api/sessions'),
    get: (id: string) => request<SessionDetail>(`/api/sessions/${id}`),
    delete: (id: string) =>
      request<void>(`/api/sessions/${id}`, { method: 'DELETE' }),
    renameSpeaker: (sessionId: string, oldName: string, newName: string) =>
      request<{ replaced: boolean }>(`/api/sessions/${sessionId}/rename-speaker`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ oldName, newName }),
      }),
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
    updateContent: (sessionId: string, content: string) =>
      request<Memo>(`/api/sessions/${sessionId}/memo`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content }),
      }),
  },
  tags: {
    list: () => request<Tag[]>('/api/tags'),
    create: (name: string, color?: string) =>
      request<Tag>('/api/tags', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, color }),
      }),
    update: (id: string, name: string, color?: string) =>
      request<void>(`/api/tags/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, color }),
      }),
    delete: (id: string) =>
      request<void>(`/api/tags/${id}`, { method: 'DELETE', noContent: true }),
    addToSession: (sessionId: string, tagId: string) =>
      request<void>(`/api/sessions/${sessionId}/tags/${tagId}`, {
        method: 'POST',
        noContent: true,
      }),
    removeFromSession: (sessionId: string, tagId: string) =>
      request<void>(`/api/sessions/${sessionId}/tags/${tagId}`, {
        method: 'DELETE',
        noContent: true,
      }),
  },
  infographics: {
    generate: (sessionId: string) =>
      request<void>(`/api/sessions/${sessionId}/infographic`, {
        method: 'POST',
        noContent: true,
      }),
    get: (sessionId: string) =>
      request<Infographic>(`/api/sessions/${sessionId}/infographic`),
    delete: (sessionId: string) =>
      request<void>(`/api/sessions/${sessionId}/infographic`, {
        method: 'DELETE',
        noContent: true,
      }),
  },
};
