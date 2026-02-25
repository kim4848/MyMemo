import { useEffect, useState, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { api } from '../api/client';
import type { SessionDetail, Memo, ChunkStatus } from '../types';
import MemoViewer from '../components/MemoViewer';

const chunkStatusStyles: Record<ChunkStatus, string> = {
  uploaded: 'text-gray-400',
  queued: 'text-gray-400',
  transcribing: 'text-blue-500',
  transcribed: 'text-green-600',
  failed: 'text-red-500',
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
      <div className="py-8 text-center text-red-500">
        {error ?? 'Session not found'}
      </div>
    );
  }

  const { session, chunks } = detail;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/" className="text-sm text-gray-500 hover:text-gray-700">
          Back
        </Link>
        <h1 className="text-lg font-semibold text-gray-900">
          Session
        </h1>
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium">
          {session.status}
        </span>
      </div>

      <div className="text-sm text-gray-500">
        {session.outputMode === 'full' ? 'Full Transcript' : 'Summary'} | {session.audioSource}
      </div>

      {chunks.length > 0 && (
        <div className="space-y-1">
          <h2 className="text-sm font-medium text-gray-700">Chunks</h2>
          {chunks.map((chunk) => (
            <div
              key={chunk.id}
              className="flex items-center gap-2 text-sm"
            >
              <span className={chunkStatusStyles[chunk.status]}>
                Chunk {chunk.chunkIndex + 1}
              </span>
              <span className="text-gray-400">{chunk.status}</span>
            </div>
          ))}
        </div>
      )}

      <MemoViewer
        memo={memo}
        isProcessing={session.status === 'processing'}
      />
    </div>
  );
}
