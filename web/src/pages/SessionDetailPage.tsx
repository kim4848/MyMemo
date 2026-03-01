import { useEffect, useState, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { api } from '../api/client';
import type { SessionDetail, Memo, ChunkStatus } from '../types';
import MemoViewer from '../components/MemoViewer';

const chunkStatusStyles: Record<ChunkStatus, string> = {
  uploaded: 'text-gray-500',
  queued: 'text-gray-500',
  transcribing: 'text-blue-400',
  transcribed: 'text-accent',
  failed: 'text-red-400',
};

const sessionStatusStyles: Record<string, string> = {
  recording: 'bg-yellow-500/10 text-yellow-400',
  processing: 'bg-blue-500/10 text-blue-400',
  completed: 'bg-accent/10 text-accent',
  failed: 'bg-red-500/10 text-red-400',
};

export default function SessionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [detail, setDetail] = useState<SessionDetail | null>(null);
  const [memo, setMemo] = useState<Memo | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval>>(undefined);

  useEffect(() => {
    if (!id) return;

    async function load() {
      try {
        const data = await api.sessions.get(id!);
        setDetail(data);

        try {
          const m = await api.memos.get(id!);
          setMemo(m);
        } catch {
          // Memo not ready yet — that's ok
        }
      } catch (e) {
        setError((e as Error).message);
      } finally {
        setLoading(false);
      }
    }

    load();
  }, [id]);

  // Poll for memo when session is processing
  useEffect(() => {
    if (!id || !detail || detail.session.status !== 'processing') return;
    if (memo) return;

    pollRef.current = setInterval(async () => {
      try {
        const m = await api.memos.get(id);
        setMemo(m);
        setDetail((prev) =>
          prev
            ? {
                ...prev,
                session: { ...prev.session, status: 'completed' },
              }
            : prev,
        );
        clearInterval(pollRef.current);
      } catch {
        // Still processing
      }
    }, 5000);

    return () => clearInterval(pollRef.current);
  }, [id, detail, memo]);

  if (loading) {
    return <div className="py-8 text-center text-gray-500">Loading...</div>;
  }

  if (error || !detail) {
    return (
      <div className="py-8 text-center text-red-400">
        {error ?? 'Session not found'}
      </div>
    );
  }

  const { session, chunks } = detail;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Link
          to="/"
          className="flex items-center gap-1 text-sm text-gray-500 transition-colors hover:text-accent"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" />
          </svg>
          Back
        </Link>
        <h1 className="text-2xl font-bold text-white">Session</h1>
        <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${sessionStatusStyles[session.status] ?? ''}`}>
          {session.status}
        </span>
      </div>

      <div className="text-sm text-gray-500">
        {session.outputMode === 'full' ? 'Full Transcript' : 'Summary'} &middot; {session.audioSource}
      </div>

      {chunks.length > 0 && (
        <div className="rounded-xl border border-navy-700 bg-navy-800 p-5">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-gray-500">Chunks</h2>
          <div className="space-y-2">
            {chunks.map((chunk) => (
              <div
                key={chunk.id}
                className="flex items-center gap-3 text-sm"
              >
                <span className={`font-medium ${chunkStatusStyles[chunk.status]}`}>
                  Chunk {chunk.chunkIndex + 1}
                </span>
                <span className="text-gray-600">{chunk.status}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      <MemoViewer
        memo={memo}
        isProcessing={session.status === 'processing'}
      />
    </div>
  );
}
