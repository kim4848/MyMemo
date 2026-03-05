export type OutputMode = 'full' | 'summary' | 'product-planning';

export const outputModeLabels: Record<OutputMode, string> = {
  full: 'Full Transcript',
  summary: 'Summary',
  'product-planning': 'Product Planning',
};
export type AudioSource = 'microphone' | 'system' | 'both';
export type SessionStatus = 'recording' | 'processing' | 'completed' | 'failed';
export type ChunkStatus = 'uploaded' | 'queued' | 'transcribing' | 'transcribed' | 'failed';

export interface Session {
  id: string;
  userId: string;
  title: string | null;
  status: SessionStatus;
  outputMode: OutputMode;
  audioSource: AudioSource;
  context: string | null;
  startedAt: string;
  endedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface Chunk {
  id: string;
  sessionId: string;
  chunkIndex: number;
  blobPath: string;
  durationSec: number | null;
  status: ChunkStatus;
  errorMessage: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SessionDetail {
  session: Session;
  chunks: Chunk[];
  transcriptionDurations: Record<string, number>;
}

export interface Memo {
  id: string;
  sessionId: string;
  outputMode: OutputMode;
  content: string;
  modelUsed: string;
  promptTokens: number | null;
  completionTokens: number | null;
  generationDurationMs: number | null;
  createdAt: string;
}

export interface CreateSessionRequest {
  outputMode: OutputMode;
  audioSource: AudioSource;
  context?: string;
}
