import { describe, test, expect, vi, beforeEach } from 'vitest';
import { useSessionsStore } from './sessions';

vi.mock('../api/client', () => ({
  api: {
    sessions: {
      list: vi.fn(),
      create: vi.fn(),
      delete: vi.fn(),
    },
  },
}));

import { api } from '../api/client';

const mockSession = {
  id: 's1',
  userId: 'u1',
  title: null,
  status: 'recording' as const,
  outputMode: 'full' as const,
  audioSource: 'microphone' as const,
  context: null,
  startedAt: '2026-01-01T00:00:00',
  endedAt: null,
  createdAt: '2026-01-01T00:00:00',
  updatedAt: '2026-01-01T00:00:00',
};

beforeEach(() => {
  useSessionsStore.setState({
    sessions: [],
    loading: false,
    error: null,
  });
  vi.clearAllMocks();
});

describe('fetchSessions', () => {
  test('sets loading true, fetches, then sets sessions', async () => {
    vi.mocked(api.sessions.list).mockResolvedValue([mockSession]);

    const promise = useSessionsStore.getState().fetchSessions();
    expect(useSessionsStore.getState().loading).toBe(true);

    await promise;
    expect(useSessionsStore.getState().loading).toBe(false);
    expect(useSessionsStore.getState().sessions).toEqual([mockSession]);
    expect(useSessionsStore.getState().error).toBeNull();
  });

  test('sets error on failure', async () => {
    vi.mocked(api.sessions.list).mockRejectedValue(new Error('fail'));

    await useSessionsStore.getState().fetchSessions();
    expect(useSessionsStore.getState().loading).toBe(false);
    expect(useSessionsStore.getState().error).toBe('fail');
    expect(useSessionsStore.getState().sessions).toEqual([]);
  });
});

describe('createSession', () => {
  test('creates session and adds to store', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue(mockSession);

    const result = await useSessionsStore.getState().createSession({
      outputMode: 'full',
      audioSource: 'microphone',
    });

    expect(result).toEqual(mockSession);
    expect(useSessionsStore.getState().sessions).toContainEqual(mockSession);
  });
});

describe('deleteSession', () => {
  test('removes session from store', async () => {
    useSessionsStore.setState({ sessions: [mockSession] });
    vi.mocked(api.sessions.delete).mockResolvedValue(undefined);

    await useSessionsStore.getState().deleteSession('s1');
    expect(useSessionsStore.getState().sessions).toEqual([]);
  });
});
