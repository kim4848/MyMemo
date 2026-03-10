import { describe, test, expect, vi, beforeEach } from 'vitest';
import { api, setTokenProvider, ApiError } from './client';

const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

const jsonHeaders = { get: (name: string) => name === 'content-type' ? 'application/json' : null };

beforeEach(() => {
  mockFetch.mockReset();
  setTokenProvider(() => Promise.resolve('test-token'));
});

describe('api.sessions', () => {
  test('create sends POST with body and returns session', async () => {
    const session = { id: 'abc', status: 'recording' };
    mockFetch.mockResolvedValue({
      ok: true,
      status: 201,
      headers: jsonHeaders,
      json: () => Promise.resolve(session),
    });

    const result = await api.sessions.create({
      outputMode: 'full',
      audioSource: 'microphone',
    });

    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/sessions'),
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
          'Content-Type': 'application/json',
        }),
      }),
    );
    expect(result).toEqual(session);
  });

  test('list sends GET and returns sessions array', async () => {
    const sessions = [{ id: '1' }, { id: '2' }];
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: jsonHeaders,
      json: () => Promise.resolve(sessions),
    });

    const result = await api.sessions.list();
    expect(result).toEqual(sessions);
  });

  test('get sends GET with id and returns session detail', async () => {
    const detail = { session: { id: '1' }, chunks: [] };
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: jsonHeaders,
      json: () => Promise.resolve(detail),
    });

    const result = await api.sessions.get('abc');
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/sessions/abc'),
      expect.any(Object),
    );
    expect(result).toEqual(detail);
  });

  test('delete sends DELETE and returns void', async () => {
    mockFetch.mockResolvedValue({ ok: true, status: 204 });

    await api.sessions.delete('abc');
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/sessions/abc'),
      expect.objectContaining({ method: 'DELETE' }),
    );
  });
});

describe('api.chunks', () => {
  test('upload sends multipart form data', async () => {
    const chunk = { id: 'c1', chunkIndex: 0, status: 'queued' };
    mockFetch.mockResolvedValue({
      ok: true,
      status: 202,
      headers: jsonHeaders,
      json: () => Promise.resolve(chunk),
    });

    const blob = new Blob(['audio'], { type: 'audio/webm' });
    const result = await api.chunks.upload('sess1', blob, 0);

    const [url, options] = mockFetch.mock.calls[0];
    expect(url).toContain('/api/sessions/sess1/chunks');
    expect(options.method).toBe('POST');
    expect(options.body).toBeInstanceOf(FormData);
    expect(result).toEqual(chunk);
  });
});

describe('api.memos', () => {
  test('finalize sends POST', async () => {
    mockFetch.mockResolvedValue({ ok: true, status: 202 });

    await api.memos.finalize('sess1');
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/sessions/sess1/finalize'),
      expect.objectContaining({ method: 'POST' }),
    );
  });

  test('get returns memo', async () => {
    const memo = { id: 'm1', content: 'Hello' };
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: jsonHeaders,
      json: () => Promise.resolve(memo),
    });

    const result = await api.memos.get('sess1');
    expect(result).toEqual(memo);
  });
});

describe('error handling', () => {
  test('throws ApiError on non-ok response', async () => {
    mockFetch.mockResolvedValue({
      ok: false,
      status: 404,
      text: () => Promise.resolve('Not found'),
    });

    await expect(api.sessions.list()).rejects.toThrow(ApiError);
    await expect(api.sessions.list()).rejects.toMatchObject({
      status: 404,
    });
  });

  test('includes auth header from token provider', async () => {
    setTokenProvider(() => Promise.resolve('my-jwt'));
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: jsonHeaders,
      json: () => Promise.resolve([]),
    });

    await api.sessions.list();
    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer my-jwt',
        }),
      }),
    );
  });
});
