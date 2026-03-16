import { useEffect, useState, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { api } from '../api/client';
import type { SessionDetail, Memo, ChunkStatus, OutputMode } from '../types';
import { outputModeLabels } from '../types';
import MemoViewer from '../components/MemoViewer';
import SpeakerRenamePanel from '../components/SpeakerRenamePanel';
import { unwatchSession } from '../services/notifications';

const chunkStatusColors: Record<ChunkStatus, string> = {
  uploaded: 'bg-text-muted',
  queued: 'bg-text-muted',
  transcribing: 'bg-accent',
  batch_submitted: 'bg-accent',
  transcribed: 'bg-success',
  failed: 'bg-danger',
};

const sessionStatusStyles: Record<string, string> = {
  recording: 'bg-warning-light text-warning',
  processing: 'bg-accent-light text-accent',
  completed: 'bg-success-light text-success',
  failed: 'bg-danger-light text-danger',
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
    return <div className="py-8 text-center text-text-muted">Loading...</div>;
  }

  if (error || !detail) {
    return (
      <div className="py-8 text-center text-danger">
        {error ?? 'Session not found'}
      </div>
    );
  }

  const { session, chunks } = detail;

  const chunkList = chunks.length > 0 && (
    <div className="rounded-xl border border-border bg-bg-card p-4 shadow-sm sm:p-5">
      <button
        type="button"
        onClick={() => setChunksOpen((o) => !o)}
        className="flex w-full items-center justify-between"
      >
        <h2 className="text-xs font-semibold uppercase tracking-wider text-text-muted">Chunks</h2>
        <svg
          className={`h-4 w-4 text-text-muted transition-transform duration-200 ${chunksOpen ? 'rotate-180' : ''}`}
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
              <span className="flex items-center gap-2">
                <span className={`h-2.5 w-2.5 rounded-full transition-colors duration-300 ${chunkStatusColors[chunk.status]}`} />
                <span className="font-medium text-text-primary">Chunk {chunk.chunkIndex + 1}</span>
              </span>
              <span className="transition-colors duration-300 text-text-muted">{chunk.status === 'batch_submitted' ? 'processing' : chunk.status}</span>
              {detail.transcriptionDurations[chunk.id] != null && (
                <span className="text-text-muted">
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
          className="flex items-center gap-1 text-sm text-text-muted transition-colors hover:text-accent"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" />
          </svg>
          Back
        </Link>
        <h1 className="font-[family-name:var(--font-heading)] text-xl font-semibold text-text-primary sm:text-2xl">{session.title ?? 'Session'}</h1>
        <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${sessionStatusStyles[session.status] ?? ''}`}>
          {session.status}
        </span>
      </div>

      <div className="text-sm text-text-muted">
        {outputModeLabels[session.outputMode]} &middot; {session.audioSource}
        {session.transcriptionMode === 'speech' && (
          <span className="ml-2 inline-flex items-center rounded-full bg-accent-light px-2 py-0.5 text-xs font-medium text-accent">
            Med taleridentifikation
          </span>
        )}
      </div>

      {session.context && !memo && (
        <div className="rounded-xl border border-border bg-bg-card p-4 shadow-sm">
          <button
            type="button"
            onClick={() => setContextOpen((o) => !o)}
            className="flex w-full items-center justify-between"
          >
            <h2 className="text-xs font-semibold uppercase tracking-wider text-text-muted">Kontekst</h2>
            <svg
              className={`h-4 w-4 text-text-muted transition-transform duration-200 ${contextOpen ? 'rotate-180' : ''}`}
              fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
            </svg>
          </button>
          {contextOpen && (
            <p className="mt-2 text-sm text-text-secondary whitespace-pre-wrap">{session.context}</p>
          )}
        </div>
      )}

      {session.status === 'processing' && chunks.length > 0 && (() => {
        const transcribed = chunks.filter((c) => c.status === 'transcribed').length;
        const allTranscribed = transcribed === chunks.length;
        return (
          <div className="space-y-2">
            <p className="text-sm text-text-secondary">
              {allTranscribed ? 'Generating memo...' : `Transcribing: ${transcribed} / ${chunks.length} chunks`}
            </p>
            <div className="h-2 overflow-hidden rounded-full bg-border">
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
        <div className="rounded-xl border border-danger/30 bg-danger-light p-4 sm:p-5">
          <p className="mb-3 text-sm text-danger">
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
            className="rounded-lg bg-danger px-4 py-2 text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-40 disabled:cursor-not-allowed"
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
              className="w-full rounded-lg border border-border bg-bg-input px-3 py-2.5 text-sm text-text-primary outline-none focus:border-accent focus:ring-2 focus:ring-accent/20 sm:w-auto sm:py-2"
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
              className="rounded-lg bg-accent px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-accent-hover disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Regenerate
            </button>
          </div>
          <label className="text-xs font-semibold uppercase tracking-wider text-text-muted">Context</label>
          <textarea
            value={editContext}
            onChange={(e) => setEditContext(e.target.value)}
            placeholder="Tilf&#248;j kontekst for memo-generering (f.eks. deltagere, emne, form&#229;l)"
            rows={2}
            className="w-full resize-none rounded-lg border border-border bg-bg-input px-3 py-2.5 text-sm text-text-primary placeholder:text-text-muted outline-none focus:border-accent focus:ring-2 focus:ring-accent/20"
          />
        </div>
      )}

      {session.transcriptionMode === 'speech' && detail.transcriptionTexts?.length > 0 && (
        <SpeakerRenamePanel
          sessionId={session.id}
          transcriptionTexts={detail.transcriptionTexts}
          onRenamed={async () => {
            if (!id) return;
            const [data, m] = await Promise.all([
              api.sessions.get(id),
              api.memos.get(id).catch(() => null),
            ]);
            setDetail(data);
            if (m) setMemo(m);
          }}
        />
      )}

      <MemoViewer
        memo={memo}
        isProcessing={session.status === 'processing'}
        allTranscribed={chunks.length > 0 && chunks.every((c) => c.status === 'transcribed')}
        session={session}
        onMemoUpdate={setMemo}
      />

      {memo && chunkList}
    </div>
  );
}
