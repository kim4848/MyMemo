import { useEffect, useState, useRef } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../api/client';
import type { SessionDetail, Memo, ChunkStatus, OutputMode } from '../types';
import { outputModeLabels } from '../types';
import MemoViewer from '../components/MemoViewer';
import SpeakerRenamePanel from '../components/SpeakerRenamePanel';
import PageHeader from '../components/PageHeader';
import Skeleton from '../components/Skeleton';
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
  const [regenerateOpen, setRegenerateOpen] = useState(false);
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
    return (
      <div className="animate-[fadeInUp_0.3s_ease-out] space-y-6">
        <Skeleton className="h-8 w-1/3" />
        <Skeleton className="h-4 w-1/2" />
        <Skeleton className="h-3 w-full rounded-full" />
        <div className="rounded-xl border border-border bg-bg-card p-4 shadow-sm space-y-3">
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-3 w-1/2" />
        </div>
        <div className="rounded-xl border border-border bg-bg-card p-4 shadow-sm space-y-3">
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="h-3 w-1/3" />
        </div>
      </div>
    );
  }

  if (error || !detail) {
    return (
      <div className="py-8 text-center text-danger">
        {error ?? 'Session not found'}
      </div>
    );
  }

  const { session, chunks } = detail;

  const subtitleParts = [
    outputModeLabels[session.outputMode],
    session.audioSource,
  ];
  const subtitleText = subtitleParts.join(' · ');

  const chunkList = chunks.length > 0 && (
    <div className="rounded-xl border border-border bg-bg-card p-4 shadow-sm sm:p-5">
      <button
        type="button"
        onClick={() => setChunksOpen((o) => !o)}
        className="flex w-full items-center justify-between"
      >
        <div className="flex items-center gap-2">
          <svg className="h-4 w-4 text-text-muted" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6A2.25 2.25 0 0 1 6 3.75h2.25A2.25 2.25 0 0 1 10.5 6v2.25a2.25 2.25 0 0 1-2.25 2.25H6a2.25 2.25 0 0 1-2.25-2.25V6ZM3.75 15.75A2.25 2.25 0 0 1 6 13.5h2.25a2.25 2.25 0 0 1 2.25 2.25V18a2.25 2.25 0 0 1-2.25 2.25H6A2.25 2.25 0 0 1 3.75 18v-2.25ZM13.5 6a2.25 2.25 0 0 1 2.25-2.25H18A2.25 2.25 0 0 1 20.25 6v2.25A2.25 2.25 0 0 1 18 10.5h-2.25a2.25 2.25 0 0 1-2.25-2.25V6ZM13.5 15.75a2.25 2.25 0 0 1 2.25-2.25H18a2.25 2.25 0 0 1 2.25 2.25V18A2.25 2.25 0 0 1 18 20.25h-2.25a2.25 2.25 0 0 1-2.25-2.25v-2.25Z" />
          </svg>
          <h2 className="text-xs font-semibold uppercase tracking-wider text-text-muted">Chunks</h2>
        </div>
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
    <div className="space-y-4 animate-[fadeInUp_0.3s_ease-out]">
      <PageHeader
        breadcrumb={[{ label: 'Sessions', to: '/' }]}
        title={session.title ?? 'Session'}
        subtitle={
          subtitleText + (session.transcriptionMode === 'speech' ? ' · Med taleridentifikation' : '')
        }
        actions={
          <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${sessionStatusStyles[session.status] ?? ''}`}>
            {session.status}
          </span>
        }
      />

      {session.status === 'processing' && chunks.length > 0 && (() => {
        const transcribed = chunks.filter((c) => c.status === 'transcribed').length;
        const pct = Math.round((transcribed / chunks.length) * 100);
        const allTranscribed = transcribed === chunks.length;
        return (
          <div className="rounded-xl border border-accent/20 bg-accent-light/30 p-5 space-y-3">
            <div className="flex items-center justify-between">
              <p className="text-sm text-text-secondary">
                {allTranscribed ? 'Generating memo...' : `Transcribing: ${transcribed} / ${chunks.length} chunks`}
              </p>
              <span className="text-sm font-medium text-accent">{pct}%</span>
            </div>
            <div className="h-3 overflow-hidden rounded-full bg-border">
              <div
                className="h-full rounded-full bg-accent transition-all duration-500 relative overflow-hidden"
                style={{ width: `${pct}%` }}
              >
                <div className="absolute inset-0 animate-[shimmer_1.5s_linear_infinite] bg-gradient-to-r from-transparent via-white/20 to-transparent bg-[length:200%_100%]" />
              </div>
            </div>
          </div>
        );
      })()}

      {session.context && !memo && (
        <div className="rounded-xl border border-border bg-bg-card p-4 shadow-sm">
          <button
            type="button"
            onClick={() => setContextOpen((o) => !o)}
            className="flex w-full items-center justify-between"
          >
            <div className="flex items-center gap-2">
              <svg className="h-4 w-4 text-text-muted" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
              </svg>
              <h2 className="text-xs font-semibold uppercase tracking-wider text-text-muted">Kontekst</h2>
            </div>
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
        <div className="rounded-xl border border-border bg-bg-card p-4 shadow-sm">
          <button
            type="button"
            onClick={() => setRegenerateOpen((o) => !o)}
            className="flex w-full items-center justify-between"
          >
            <div className="flex items-center gap-2">
              <svg className="h-4 w-4 text-text-muted" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0 3.181 3.183a8.25 8.25 0 0 0 13.803-3.7M4.031 9.865a8.25 8.25 0 0 1 13.803-3.7l3.181 3.182M21.015 4.356v4.992" />
              </svg>
              <h2 className="text-xs font-semibold uppercase tracking-wider text-text-muted">Regenerate Memo</h2>
            </div>
            <svg
              className={`h-4 w-4 text-text-muted transition-transform duration-200 ${regenerateOpen ? 'rotate-180' : ''}`}
              fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
            </svg>
          </button>
          {regenerateOpen && (
          <div className="mt-3 space-y-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="flex-1 min-w-[200px]">
              <label className="text-xs font-semibold uppercase tracking-wider text-text-muted">Output mode</label>
              <select
                aria-label="Output mode"
                value={selectedMode}
                onChange={(e) => setSelectedMode(e.target.value as OutputMode)}
                className="mt-1 w-full rounded-lg border border-border bg-bg-input px-3 py-2.5 text-sm text-text-primary outline-none focus:border-accent focus:ring-2 focus:ring-accent/20"
              >
                {Object.entries(outputModeLabels).map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </div>
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
          <div>
            <label className="text-xs font-semibold uppercase tracking-wider text-text-muted">Context</label>
            <textarea
              value={editContext}
              onChange={(e) => setEditContext(e.target.value)}
              placeholder="Tilf&#248;j kontekst for memo-generering (f.eks. deltagere, emne, form&#229;l)"
              rows={2}
              className="mt-1 w-full resize-none rounded-lg border border-border bg-bg-input px-3 py-2.5 text-sm text-text-primary placeholder:text-text-muted outline-none focus:border-accent focus:ring-2 focus:ring-accent/20"
            />
          </div>
          </div>
          )}
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
