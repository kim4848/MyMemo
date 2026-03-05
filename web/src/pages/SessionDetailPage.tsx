import { useEffect, useState, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { api } from '../api/client';
import type { SessionDetail, Memo, ChunkStatus, OutputMode } from '../types';
import { outputModeLabels } from '../types';
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
  const [selectedMode, setSelectedMode] = useState<OutputMode>('full');
  const [editContext, setEditContext] = useState('');
  const [regenerating, setRegenerating] = useState(false);
  const pollRef = useRef<ReturnType<typeof setInterval>>(undefined);

  useEffect(() => {
    if (!id) return;

    async function load() {
      try {
        const data = await api.sessions.get(id!);
        setDetail(data);
        setSelectedMode(data.session.outputMode);
        setEditContext(data.session.context ?? '');

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

  // Poll for session detail + memo when session is processing
  useEffect(() => {
    if (!id || !detail || detail.session.status !== 'processing') return;
    if (memo) return;

    pollRef.current = setInterval(async () => {
      try {
        const [updatedDetail, memoResult] = await Promise.all([
          api.sessions.get(id),
          api.memos.get(id).catch(() => null),
        ]);

        if (memoResult) {
          setMemo(memoResult);
          setDetail({
            ...updatedDetail,
            session: { ...updatedDetail.session, status: 'completed' },
          });
          clearInterval(pollRef.current);
        } else {
          setDetail(updatedDetail);
        }
      } catch {
        // Poll failed — retry next interval
      }
    }, 5000);

    return () => clearInterval(pollRef.current);
  }, [id, detail?.session.status, memo]);

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
      <div className="flex flex-wrap items-center gap-3 sm:gap-4">
        <Link
          to="/"
          className="flex items-center gap-1 text-sm text-gray-500 transition-colors hover:text-accent"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" />
          </svg>
          Back
        </Link>
        <h1 className="text-xl font-bold text-white sm:text-2xl">Session</h1>
        <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${sessionStatusStyles[session.status] ?? ''}`}>
          {session.status}
        </span>
      </div>

      <div className="text-sm text-gray-500">
        {outputModeLabels[session.outputMode]} &middot; {session.audioSource}
      </div>

      {session.context && (
        <div className="rounded-xl border border-navy-700 bg-navy-800 p-4">
          <h2 className="mb-1 text-xs font-semibold uppercase tracking-wider text-gray-500">Kontekst</h2>
          <p className="text-sm text-gray-300 whitespace-pre-wrap">{session.context}</p>
        </div>
      )}

      {session.status === 'processing' && chunks.length > 0 && (() => {
        const transcribed = chunks.filter((c) => c.status === 'transcribed').length;
        const allTranscribed = transcribed === chunks.length;
        return (
          <div className="space-y-2">
            <p className="text-sm text-gray-400">
              {allTranscribed ? 'Generating memo...' : `Transcribing: ${transcribed} / ${chunks.length} chunks`}
            </p>
            <div className="h-2 overflow-hidden rounded-full bg-navy-700">
              <div
                className="h-full rounded-full bg-accent transition-all duration-500"
                style={{ width: `${(transcribed / chunks.length) * 100}%` }}
              />
            </div>
          </div>
        );
      })()}

      {chunks.length > 0 && (
        <div className="rounded-xl border border-navy-700 bg-navy-800 p-4 sm:p-5">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-gray-500">Chunks</h2>
          <div className="space-y-2">
            {chunks.map((chunk) => (
              <div
                key={chunk.id}
                className="flex flex-wrap items-center gap-2 text-sm sm:gap-3"
              >
                <span className={`font-medium transition-colors duration-300 ${chunkStatusStyles[chunk.status]}`}>
                  Chunk {chunk.chunkIndex + 1}
                </span>
                <span className="transition-colors duration-300 text-gray-600">{chunk.status}</span>
                {detail.transcriptionDurations[chunk.id] != null && (
                  <span className="text-gray-600">
                    ({detail.transcriptionDurations[chunk.id] >= 1000
                      ? `${(detail.transcriptionDurations[chunk.id] / 1000).toFixed(1)}s`
                      : `${detail.transcriptionDurations[chunk.id]}ms`})
                  </span>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {(session.status === 'completed' || session.status === 'failed') && memo && (
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-3">
            <select
              aria-label="Output mode"
              value={selectedMode}
              onChange={(e) => setSelectedMode(e.target.value as OutputMode)}
              className="w-full rounded-lg border border-navy-600 bg-navy-700 px-3 py-2.5 text-sm text-gray-200 outline-none focus:border-accent sm:w-auto sm:py-2"
            >
              {Object.entries(outputModeLabels).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
            <button
              disabled={regenerating}
              onClick={async () => {
                if (!id) return;
                setRegenerating(true);
                try {
                  const contextToSend = editContext !== (session.context ?? '') ? editContext || undefined : undefined;
                  await api.memos.regenerate(id, selectedMode, contextToSend);
                  setMemo(null);
                  setDetail((prev) =>
                    prev ? { ...prev, session: { ...prev.session, status: 'processing', outputMode: selectedMode, context: editContext || null } } : prev,
                  );
                } finally {
                  setRegenerating(false);
                }
              }}
              className="rounded-lg bg-accent px-4 py-2 text-sm font-medium text-navy-900 transition-opacity hover:opacity-90 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Regenerate
            </button>
          </div>
          <textarea
            value={editContext}
            onChange={(e) => setEditContext(e.target.value)}
            placeholder="Tilf&#248;j kontekst for memo-generering (f.eks. deltagere, emne, form&#229;l)"
            rows={2}
            className="w-full resize-none rounded-lg border border-navy-600 bg-navy-700 px-3 py-2 text-sm text-gray-200 placeholder-gray-500 outline-none focus:border-accent"
          />
        </div>
      )}

      <MemoViewer
        memo={memo}
        isProcessing={session.status === 'processing'}
        allTranscribed={chunks.length > 0 && chunks.every((c) => c.status === 'transcribed')}
        session={session}
      />
    </div>
  );
}
