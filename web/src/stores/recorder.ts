import { create } from 'zustand';
import { api } from '../api/client';
import { AudioCaptureService } from '../services/audio';
import { ChunkCache } from '../services/chunk-cache';
import type { AudioSource, OutputMode } from '../types';

export type LocalChunkStatus =
  | 'pending'
  | 'uploading'
  | 'uploaded'
  | 'failed';

export interface LocalChunk {
  chunkIndex: number;
  status: LocalChunkStatus;
}

type RecorderStatus = 'idle' | 'recording' | 'stopped' | 'finalizing';

interface RecorderState {
  status: RecorderStatus;
  sessionId: string | null;
  chunks: LocalChunk[];
  elapsedMs: number;
  audioSource: AudioSource;
  outputMode: OutputMode;
  context: string;

  setAudioSource: (source: AudioSource) => void;
  setOutputMode: (mode: OutputMode) => void;
  setContext: (context: string) => void;
  startRecording: () => Promise<void>;
  stopRecording: () => void;
  finalize: () => Promise<void>;
  addChunk: (chunk: LocalChunk) => void;
  updateChunkStatus: (index: number, status: LocalChunkStatus) => void;
  setElapsedMs: (ms: number) => void;
  reset: () => void;
}

const CHUNK_INTERVAL_MS = 3 * 60 * 1000; // 3 minutes

let audioService: AudioCaptureService | null = null;

export function getAudioService(): AudioCaptureService | null {
  return audioService;
}
let mediaRecorder: MediaRecorder | null = null;
let timerInterval: ReturnType<typeof setInterval> | null = null;
let chunkInterval: ReturnType<typeof setInterval> | null = null;
let startTime: number | null = null;
const chunkCache = new ChunkCache();

export const useRecorderStore = create<RecorderState>((set, get) => ({
  status: 'idle',
  sessionId: null,
  chunks: [],
  elapsedMs: 0,
  audioSource: 'microphone',
  outputMode: 'full',
  context: '',

  setAudioSource: (audioSource) => set({ audioSource }),
  setOutputMode: (outputMode) => set({ outputMode }),
  setContext: (context) => set({ context }),

  startRecording: async () => {
    const { audioSource, outputMode, context } = get();

    const session = await api.sessions.create({ outputMode, audioSource, ...(context ? { context } : {}) });
    set({ sessionId: session.id, status: 'recording', chunks: [] });

    audioService = new AudioCaptureService();
    const stream = await audioService.getStream(audioSource);

    mediaRecorder = new MediaRecorder(stream, {
      mimeType: 'audio/webm;codecs=opus',
    });

    let chunkIndex = 0;

    mediaRecorder.ondataavailable = async (e) => {
      if (e.data.size === 0) return;

      const idx = chunkIndex++;
      get().addChunk({ chunkIndex: idx, status: 'pending' });

      await chunkCache.store(session.id, idx, e.data);
      get().updateChunkStatus(idx, 'uploading');

      try {
        await api.chunks.upload(session.id, e.data, idx);
        await chunkCache.markUploaded(session.id, idx);
        get().updateChunkStatus(idx, 'uploaded');
      } catch {
        get().updateChunkStatus(idx, 'failed');
      }
    };

    // Start without timeslice — each start() produces a standalone file
    // with its own WebM header. We stop/restart every CHUNK_INTERVAL_MS
    // so Whisper receives valid files for every chunk.
    mediaRecorder.start();

    chunkInterval = setInterval(() => {
      if (mediaRecorder?.state === 'recording') {
        mediaRecorder.stop();   // fires ondataavailable with complete file
        mediaRecorder.start();  // new recording = new WebM header
      }
    }, CHUNK_INTERVAL_MS);

    startTime = Date.now();
    timerInterval = setInterval(() => {
      if (startTime) {
        set({ elapsedMs: Date.now() - startTime });
      }
    }, 1000);
  },

  stopRecording: () => {
    if (chunkInterval) clearInterval(chunkInterval);
    chunkInterval = null;
    mediaRecorder?.stop();
    audioService?.stop();
    if (timerInterval) clearInterval(timerInterval);
    timerInterval = null;
    set({ status: 'stopped' });
  },

  finalize: async () => {
    const { sessionId, chunks } = get();
    if (!sessionId) return;

    const incomplete = chunks.filter(
      (c) => c.status === 'pending' || c.status === 'uploading',
    );
    if (incomplete.length > 0) {
      throw new Error('Cannot finalize: chunks still uploading');
    }

    const failed = chunks.filter((c) => c.status === 'failed');
    if (failed.length > 0) {
      throw new Error('Cannot finalize: some chunks failed to upload');
    }

    set({ status: 'finalizing' });
    try {
      await api.memos.finalize(sessionId);
    } catch (err) {
      set({ status: 'stopped' });
      throw err;
    }
  },

  addChunk: (chunk) =>
    set((state) => ({ chunks: [...state.chunks, chunk] })),

  updateChunkStatus: (index, status) =>
    set((state) => ({
      chunks: state.chunks.map((c) =>
        c.chunkIndex === index ? { ...c, status } : c,
      ),
    })),

  setElapsedMs: (ms) => set({ elapsedMs: ms }),

  reset: () => {
    if (chunkInterval) clearInterval(chunkInterval);
    chunkInterval = null;
    mediaRecorder?.stop();
    audioService?.stop();
    if (timerInterval) clearInterval(timerInterval);
    timerInterval = null;
    startTime = null;
    set({
      status: 'idle',
      sessionId: null,
      chunks: [],
      elapsedMs: 0,
    });
  },
}));
