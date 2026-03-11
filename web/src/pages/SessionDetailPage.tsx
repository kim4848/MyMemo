import { useEffect, useState, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { api } from '../api/client';
import type { SessionDetail, Memo, ChunkStatus, OutputMode } from '../types';
import { outputModeLabels } from '../types';
import MemoViewer from '../components/MemoViewer';
import { unwatchSession } from '../services/notifications';

const chunkStatusStyles: Record<ChunkStatus, string> = {
  uploaded: 'text-gray-500',
  queued: 'text-gray-500',
  transcribing: 'text-blue-400',
  batch_submitted: 'text-blue-400',
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
  const [retrying, setRetrying] = useState(false);
  const [contextOpen, setContextOpen] = useState(true);
  const [chunksOpen, setChunksOpen] = useState(true);
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

  // Collapse context & chunks once memo is available
  useEffect(() => {
    if (memo) {
      setContextOpen(false);
      setChunksOpen(false);
    }
  }, [memo]);

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
          unwatchSession(id);
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

  const chunkList = chunks.length > 0 && (
    <div className="rounded-xl border border-navy-700 bg-navy-800 p-4 sm:p-5">
      <button
        type="button"
        onClick={() => setChunksOpen((o) => !o)}
        className="flex w-full items-center justify-between"
      >
        <h2 className="text-sm font-semibold uppercase tracking-wider text-gray-500">Chunks</h2>
        <svg
          className={`h-4 w-4 text-gray-500 transition-transform duration-200 ${chunksOpen ? 'rotate-180' : ''}`}
          fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor"
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
        </svg>
      </button>
      {chunksOpen && (
        <div className="mt-3 space-y-2">
          {chunks.map((chunk) => (
            <div
              key={chunk.id}
              className="flex flex-wrap items-center gap-2 text-sm sm:gap-3"
            >
              <span className={`font-medium transition-colors duration-300 ${chunkStatusStyles[chunk.status]}`}>
                Chunk {chunk.chunkIndex + 1}
              </span>
              <span className="transition-colors duration-300 text-gray-600">{chunk.status === 'batch_submitted' ? 'processing' : chunk.status}</span>
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
      )}
    </div>
  );

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
        <h1 className="text-xl font-bold text-white sm:text-2xl">{session.title ?? 'Session'}</h1>
        <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${sessionStatusStyles[session.status] ?? ''}`}>
          {session.status}
        </span>
      </div>

      <div className="text-sm text-gray-500">
        {outputModeLabels[session.outputMode]} &middot; {session.audioSource}
        {session.transcriptionMode === 'speech' && (
          <span className="ml-2 inline-flex items-center rounded-full bg-blue-500/10 px-2 py-0.5 text-xs font-medium text-blue-400">
            Med taleridentifikation
          </span>
        )}
      </div>

      {session.context && !memo && (
        <div className="rounded-xl border border-navy-700 bg-navy-800 p-4">
          <button
            type="button"
            onClick={() => setContextOpen((o) => !o)}
            className="flex w-full items-center justify-between"
          >
            <h2 className="text-xs font-semibold uppercase tracking-wider text-gray-500">Kontekst</h2>
            <svg
              className={`h-4 w-4 text-gray-500 transition-transform duration-200 ${contextOpen ? 'rotate-180' : ''}`}
              fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
            </svg>
          </button>
          {contextOpen && (
            <p className="mt-2 text-sm text-gray-300 whitespace-pre-wrap">{session.context}</p>
          )}
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

      {!memo && chunkList}

      {session.status === 'failed' && !memo && (
        <div className="rounded-xl border border-red-500/30 bg-red-500/5 p-4 sm:p-5">
          <p className="mb-3 text-sm text-red-400">
            Session processing failed. You can retry to regenerate the memo.
          </p>
          <button
            disabled={retrying}
            onClick={async () => {
              if (!id) return;
              setRetrying(true);
              try {
                await api.memos.regenerate(id, selectedMode, editContext || undefined);
                setMemo(null);
                setDetail((prev) =>
                  prev ? { ...prev, session: { ...prev.session, status: 'processing', outputMode: selectedMode, context: editContext || null } } : prev,
                );
              } catch {
                // Stay on failed state if retry itself fails
              } finally {
                setRetrying(false);
              }
            }}
            className="rounded-lg bg-red-500 px-4 py-2 text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {retrying ? 'Retrying...' : 'Retry'}
          </button>
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
          <label className="text-xs font-semibold uppercase tracking-wider text-gray-500">Context</label>
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

      {memo && chunkList}
    </div>
  );
}
