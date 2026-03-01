import { describe, test, expect, vi, beforeEach, afterEach } from 'vitest';
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

// Mock MediaRecorder with state tracking
const mockMediaRecorder = {
  start: vi.fn(),
  stop: vi.fn(),
  ondataavailable: null as ((e: { data: Blob }) => void) | null,
  onstop: null as (() => void) | null,
  state: 'inactive' as string,
};
vi.stubGlobal(
  'MediaRecorder',
  vi.fn().mockImplementation(function () {
    return mockMediaRecorder;
  }),
);

import { api } from '../api/client';

const sessionStub = {
  id: 'sess-1',
  userId: 'u1',
  title: null,
  status: 'recording' as const,
  outputMode: 'full' as const,
  audioSource: 'microphone' as const,
  startedAt: '2026-01-01T00:00:00',
  endedAt: null,
  createdAt: '2026-01-01T00:00:00',
  updatedAt: '2026-01-01T00:00:00',
};

beforeEach(() => {
  vi.useFakeTimers();
  useRecorderStore.setState({
    status: 'idle',
    sessionId: null,
    chunks: [],
    elapsedMs: 0,
    audioSource: 'microphone',
    outputMode: 'full',
  });
  vi.clearAllMocks();
  mockMediaRecorder.state = 'inactive';
  mockMediaRecorder.ondataavailable = null;

  // Make start/stop toggle the state like a real MediaRecorder
  mockMediaRecorder.start.mockImplementation(() => {
    mockMediaRecorder.state = 'recording';
  });
  mockMediaRecorder.stop.mockImplementation(() => {
    mockMediaRecorder.state = 'inactive';
    // Fire ondataavailable with a valid blob, like a real MediaRecorder does on stop
    if (mockMediaRecorder.ondataavailable) {
      mockMediaRecorder.ondataavailable({ data: new Blob(['audio'], { type: 'audio/webm' }) });
    }
  });
});

afterEach(() => {
  // Clean up any running intervals/timers
  useRecorderStore.getState().reset();
  vi.useRealTimers();
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
    vi.mocked(api.sessions.create).mockResolvedValue(sessionStub);

    await useRecorderStore.getState().startRecording();

    expect(api.sessions.create).toHaveBeenCalledWith({
      outputMode: 'full',
      audioSource: 'microphone',
    });
    expect(useRecorderStore.getState().status).toBe('recording');
    expect(useRecorderStore.getState().sessionId).toBe('sess-1');
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

describe('multi-chunk recording', () => {
  test('start() is called without timeslice', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue(sessionStub);

    await useRecorderStore.getState().startRecording();

    // start() should be called with no arguments (no timeslice)
    expect(mockMediaRecorder.start).toHaveBeenCalledWith();
    expect(mockMediaRecorder.start).toHaveBeenCalledTimes(1);
  });

  test('chunk interval triggers stop then start to produce standalone files', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue(sessionStub);
    vi.mocked(api.chunks.upload).mockResolvedValue({} as never);

    await useRecorderStore.getState().startRecording();

    // Initial start() call
    expect(mockMediaRecorder.start).toHaveBeenCalledTimes(1);
    expect(mockMediaRecorder.stop).not.toHaveBeenCalled();

    // Advance to first chunk boundary (3 minutes)
    vi.advanceTimersByTime(3 * 60 * 1000);

    // stop() fires ondataavailable (chunk 0), then start() begins chunk 1
    expect(mockMediaRecorder.stop).toHaveBeenCalledTimes(1);
    expect(mockMediaRecorder.start).toHaveBeenCalledTimes(2);

    // Advance to second chunk boundary
    vi.advanceTimersByTime(3 * 60 * 1000);

    expect(mockMediaRecorder.stop).toHaveBeenCalledTimes(2);
    expect(mockMediaRecorder.start).toHaveBeenCalledTimes(3);
  });

  test('each chunk cycle uploads independently', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue(sessionStub);
    vi.mocked(api.chunks.upload).mockResolvedValue({} as never);

    await useRecorderStore.getState().startRecording();

    // First chunk boundary — stop fires ondataavailable
    vi.advanceTimersByTime(3 * 60 * 1000);
    await vi.waitFor(() => {
      expect(api.chunks.upload).toHaveBeenCalledTimes(1);
    });

    expect(vi.mocked(api.chunks.upload).mock.calls[0][2]).toBe(0); // chunkIndex 0

    // Second chunk boundary
    vi.advanceTimersByTime(3 * 60 * 1000);
    await vi.waitFor(() => {
      expect(api.chunks.upload).toHaveBeenCalledTimes(2);
    });

    expect(vi.mocked(api.chunks.upload).mock.calls[1][2]).toBe(1); // chunkIndex 1

    // Both chunks tracked in store
    const chunks = useRecorderStore.getState().chunks;
    expect(chunks).toHaveLength(2);
    expect(chunks[0].chunkIndex).toBe(0);
    expect(chunks[1].chunkIndex).toBe(1);
  });

  test('stopRecording clears chunk interval and fires final ondataavailable', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue(sessionStub);
    vi.mocked(api.chunks.upload).mockResolvedValue({} as never);

    await useRecorderStore.getState().startRecording();

    // Stop recording — this should clear the chunk interval and call stop()
    useRecorderStore.getState().stopRecording();

    expect(mockMediaRecorder.stop).toHaveBeenCalledTimes(1);
    expect(useRecorderStore.getState().status).toBe('stopped');

    // Advancing time should NOT trigger more stop/start cycles
    const stopCallCount = mockMediaRecorder.stop.mock.calls.length;
    const startCallCount = mockMediaRecorder.start.mock.calls.length;

    vi.advanceTimersByTime(5 * 60 * 1000);

    expect(mockMediaRecorder.stop).toHaveBeenCalledTimes(stopCallCount);
    expect(mockMediaRecorder.start).toHaveBeenCalledTimes(startCallCount);
  });

  test('upload failure marks chunk as failed without stopping recording', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue(sessionStub);
    vi.mocked(api.chunks.upload).mockRejectedValue(new Error('network error'));

    await useRecorderStore.getState().startRecording();

    // Trigger first chunk
    vi.advanceTimersByTime(3 * 60 * 1000);
    await vi.waitFor(() => {
      expect(api.chunks.upload).toHaveBeenCalledTimes(1);
    });

    // Chunk should be marked failed
    await vi.waitFor(() => {
      expect(useRecorderStore.getState().chunks[0].status).toBe('failed');
    });

    // Recording should still be active
    expect(useRecorderStore.getState().status).toBe('recording');
    expect(mockMediaRecorder.state).toBe('recording');
  });

  test('zero-size blobs are ignored', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue(sessionStub);

    // Override stop to fire an empty blob
    mockMediaRecorder.stop.mockImplementation(() => {
      mockMediaRecorder.state = 'inactive';
      if (mockMediaRecorder.ondataavailable) {
        mockMediaRecorder.ondataavailable({ data: new Blob([], { type: 'audio/webm' }) });
      }
    });

    await useRecorderStore.getState().startRecording();

    vi.advanceTimersByTime(5 * 60 * 1000);

    // No chunks should be added for zero-size blobs
    expect(useRecorderStore.getState().chunks).toHaveLength(0);
    expect(api.chunks.upload).not.toHaveBeenCalled();
  });

  test('reset clears chunk interval', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue(sessionStub);

    await useRecorderStore.getState().startRecording();
    useRecorderStore.getState().reset();

    const stopCallCount = mockMediaRecorder.stop.mock.calls.length;
    const startCallCount = mockMediaRecorder.start.mock.calls.length;

    vi.advanceTimersByTime(5 * 60 * 1000);

    // No more stop/start cycles after reset
    expect(mockMediaRecorder.stop).toHaveBeenCalledTimes(stopCallCount);
    expect(mockMediaRecorder.start).toHaveBeenCalledTimes(startCallCount);
  });
});

describe('finalize guards', () => {
  test('finalize throws when chunks still uploading', async () => {
    useRecorderStore.setState({
      status: 'stopped',
      sessionId: 'sess-1',
      chunks: [
        { chunkIndex: 0, status: 'uploaded' },
        { chunkIndex: 1, status: 'uploading' },
      ],
    });

    await expect(useRecorderStore.getState().finalize()).rejects.toThrow(
      'Cannot finalize: chunks still uploading',
    );
    expect(api.memos.finalize).not.toHaveBeenCalled();
  });

  test('finalize throws when chunks failed', async () => {
    useRecorderStore.setState({
      status: 'stopped',
      sessionId: 'sess-1',
      chunks: [
        { chunkIndex: 0, status: 'uploaded' },
        { chunkIndex: 1, status: 'failed' },
      ],
    });

    await expect(useRecorderStore.getState().finalize()).rejects.toThrow(
      'Cannot finalize: some chunks failed to upload',
    );
    expect(api.memos.finalize).not.toHaveBeenCalled();
  });

  test('finalize calls API when all chunks uploaded', async () => {
    vi.mocked(api.memos.finalize).mockResolvedValue({} as never);

    useRecorderStore.setState({
      status: 'stopped',
      sessionId: 'sess-1',
      chunks: [
        { chunkIndex: 0, status: 'uploaded' },
        { chunkIndex: 1, status: 'uploaded' },
      ],
    });

    await useRecorderStore.getState().finalize();

    expect(api.memos.finalize).toHaveBeenCalledWith('sess-1');
    expect(useRecorderStore.getState().status).toBe('finalizing');
  });
});
