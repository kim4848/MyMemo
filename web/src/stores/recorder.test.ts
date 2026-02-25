import { describe, test, expect, vi, beforeEach } from 'vitest';
import { useRecorderStore } from './recorder';

vi.mock('../api/client', () => ({
  api: {
    sessions: { create: vi.fn() },
    chunks: { upload: vi.fn() },
    memos: { finalize: vi.fn() },
  },
}));

vi.mock('../services/audio', () => ({
  AudioCaptureService: vi.fn().mockImplementation(function () {
    return {
      getStream: vi.fn().mockResolvedValue({
        getTracks: () => [],
        getAudioTracks: () => [],
      }),
      stop: vi.fn(),
    };
  }),
}));

vi.mock('../services/chunk-cache', () => ({
  ChunkCache: vi.fn().mockImplementation(function () {
    return {
      store: vi.fn(),
      markUploaded: vi.fn(),
      getPending: vi.fn().mockResolvedValue([]),
      clearSession: vi.fn(),
    };
  }),
}));

// Mock MediaRecorder
const mockMediaRecorder = {
  start: vi.fn(),
  stop: vi.fn(),
  ondataavailable: null as ((e: { data: Blob }) => void) | null,
  onstop: null as (() => void) | null,
  state: 'inactive',
};
vi.stubGlobal(
  'MediaRecorder',
  vi.fn().mockImplementation(function () {
    return mockMediaRecorder;
  }),
);

import { api } from '../api/client';

beforeEach(() => {
  useRecorderStore.setState({
    status: 'idle',
    sessionId: null,
    chunks: [],
    elapsedMs: 0,
    audioSource: 'microphone',
    outputMode: 'full',
  });
  vi.clearAllMocks();
});

describe('recorder store', () => {
  test('initial state is idle', () => {
    const state = useRecorderStore.getState();
    expect(state.status).toBe('idle');
    expect(state.sessionId).toBeNull();
    expect(state.chunks).toEqual([]);
  });

  test('setAudioSource updates audio source', () => {
    useRecorderStore.getState().setAudioSource('both');
    expect(useRecorderStore.getState().audioSource).toBe('both');
  });

  test('setOutputMode updates output mode', () => {
    useRecorderStore.getState().setOutputMode('summary');
    expect(useRecorderStore.getState().outputMode).toBe('summary');
  });

  test('startRecording creates session and changes status', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue({
      id: 'new-sess',
      userId: 'u1',
      title: null,
      status: 'recording',
      outputMode: 'full',
      audioSource: 'microphone',
      startedAt: '2026-01-01T00:00:00',
      endedAt: null,
      createdAt: '2026-01-01T00:00:00',
      updatedAt: '2026-01-01T00:00:00',
    });

    await useRecorderStore.getState().startRecording();

    expect(api.sessions.create).toHaveBeenCalledWith({
      outputMode: 'full',
      audioSource: 'microphone',
    });
    expect(useRecorderStore.getState().status).toBe('recording');
    expect(useRecorderStore.getState().sessionId).toBe('new-sess');
  });

  test('addChunk appends chunk to list', () => {
    useRecorderStore.getState().addChunk({
      chunkIndex: 0,
      status: 'pending',
    });

    expect(useRecorderStore.getState().chunks).toHaveLength(1);
    expect(useRecorderStore.getState().chunks[0].chunkIndex).toBe(0);
  });

  test('updateChunkStatus changes specific chunk status', () => {
    useRecorderStore.getState().addChunk({ chunkIndex: 0, status: 'pending' });
    useRecorderStore.getState().addChunk({ chunkIndex: 1, status: 'pending' });

    useRecorderStore.getState().updateChunkStatus(0, 'uploaded');

    const chunks = useRecorderStore.getState().chunks;
    expect(chunks[0].status).toBe('uploaded');
    expect(chunks[1].status).toBe('pending');
  });

  test('reset clears all state', () => {
    useRecorderStore.setState({
      status: 'recording',
      sessionId: 'sess1',
      chunks: [{ chunkIndex: 0, status: 'uploaded' }],
      elapsedMs: 60000,
    });

    useRecorderStore.getState().reset();

    const state = useRecorderStore.getState();
    expect(state.status).toBe('idle');
    expect(state.sessionId).toBeNull();
    expect(state.chunks).toEqual([]);
    expect(state.elapsedMs).toBe(0);
  });
});
