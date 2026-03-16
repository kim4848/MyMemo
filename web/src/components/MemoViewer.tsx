import { useState, useCallback } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import type { Memo, Session } from '../types';
import { outputModeLabels } from '../types';
import { useMicrosoftAuth } from '../hooks/useMicrosoftAuth';
import { memoToHtml } from '../lib/memoToHtml';
import { api } from '../api/client';
import OneNotePickerModal from './OneNotePickerModal';
import InfographicViewer from './InfographicViewer';

interface Props {
  memo: Memo | null;
  isProcessing: boolean;
  allTranscribed: boolean;
  session?: Session;
  onMemoUpdate?: (memo: Memo) => void;
}

function formatDuration(ms: number): string {
  return ms >= 1000 ? `${(ms / 1000).toFixed(1)}s` : `${ms}ms`;
}

export default function MemoViewer({ memo, isProcessing, allTranscribed, session, onMemoUpdate }: Props) {
  const { isAuthenticated, login } = useMicrosoftAuth();
  const [showPickerModal, setShowPickerModal] = useState(false);
  const [copied, setCopied] = useState(false);
  const [editing, setEditing] = useState(false);
  const [editContent, setEditContent] = useState('');
  const [saving, setSaving] = useState(false);

  const handleCopy = useCallback(async () => {
    if (!memo) return;
    const html = memoToHtml(memo.content);
    try {
      await navigator.clipboard.write([
        new ClipboardItem({
          'text/html': new Blob([html], { type: 'text/html' }),
          'text/plain': new Blob([memo.content], { type: 'text/plain' }),
        }),
      ]);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      await navigator.clipboard.writeText(memo.content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  }, [memo]);

  const handleEdit = useCallback(() => {
    if (!memo) return;
    setEditContent(memo.content);
    setEditing(true);
  }, [memo]);

  const handleCancel = useCallback(() => {
    setEditing(false);
    setEditContent('');
  }, []);

  const handleSave = useCallback(async () => {
    if (!memo) return;
    setSaving(true);
    try {
      const updated = await api.memos.updateContent(memo.sessionId, editContent);
      onMemoUpdate?.(updated);
      setEditing(false);
      setEditContent('');
    } finally {
      setSaving(false);
    }
  }, [memo, editContent, onMemoUpdate]);

  if (isProcessing && !memo) {
    return (
      <div className="rounded-xl border border-accent/20 bg-accent-light p-5">
        <div className="flex items-center gap-3">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-border border-t-accent" />
          <p className="text-accent">
            {allTranscribed
              ? 'Generating memo... This may take a moment.'
              : 'Transcribing audio... This may take a moment.'}
          </p>
        </div>
      </div>
    );
  }

  if (!memo) return null;

  const toolbarBtnClass = "px-3 py-2 hover:bg-bg-hover transition-colors text-text-muted hover:text-accent";

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="font-heading text-lg font-semibold text-text-primary">Memo</h2>
        <span className="rounded-full bg-accent-light px-2.5 py-0.5 text-xs font-medium text-accent">
          {outputModeLabels[memo.outputMode]}
        </span>
        {memo.updatedAt && (
          <span className="text-xs text-text-muted">Edited</span>
        )}
        <div className="ml-auto flex items-center gap-2">
          {editing ? (
            <>
              <button
                onClick={handleCancel}
                disabled={saving}
                className="rounded-lg border border-border bg-bg-card px-3 py-1 text-sm text-text-secondary transition-colors hover:bg-bg-hover hover:text-text-primary"
              >
                Cancel
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="rounded-lg bg-accent px-3 py-1 text-sm font-medium text-white transition-colors hover:bg-accent-hover disabled:opacity-50"
              >
                {saving ? 'Saving...' : 'Save'}
              </button>
            </>
          ) : (
            <div className="flex items-center gap-px rounded-lg border border-border overflow-hidden">
              <button
                onClick={handleEdit}
                title="Edit"
                className={toolbarBtnClass}
              >
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10" />
                </svg>
              </button>
              <div className="w-px h-5 bg-border" />
              <button
                onClick={handleCopy}
                title={copied ? 'Kopieret!' : 'Kopiér'}
                className={toolbarBtnClass}
              >
                {copied ? (
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                  </svg>
                ) : (
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M15.666 3.888A2.25 2.25 0 0 0 13.5 2.25h-3a2.25 2.25 0 0 0-2.166 1.638m7.332 0c.055.194.084.4.084.612v0a.75.75 0 0 1-.75.75H9.334a.75.75 0 0 1-.75-.75v0c0-.212.03-.418.084-.612m7.332 0c.646.049 1.288.11 1.927.184 1.1.128 1.907 1.077 1.907 2.185V19.5a2.25 2.25 0 0 1-2.25 2.25H6.75A2.25 2.25 0 0 1 4.5 19.5V6.257c0-1.108.806-2.057 1.907-2.185a48.208 48.208 0 0 1 1.927-.184" />
                  </svg>
                )}
              </button>
              <div className="w-px h-5 bg-border" />
              <button
                onClick={async () => {
                  if (!isAuthenticated) await login();
                  setShowPickerModal(true);
                }}
                title="Send to OneNote"
                className={toolbarBtnClass}
              >
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M16.5 3.75V16.5L12 14.25 7.5 16.5V3.75m9 0H18A2.25 2.25 0 0 1 20.25 6v12A2.25 2.25 0 0 1 18 20.25H6A2.25 2.25 0 0 1 3.75 18V6A2.25 2.25 0 0 1 6 3.75h1.5m9 0h-9" />
                </svg>
              </button>
              <div className="w-px h-5 bg-border" />
              <div className={toolbarBtnClass}>
                <InfographicViewer sessionId={memo.sessionId} />
              </div>
            </div>
          )}
        </div>
      </div>
      {showPickerModal && session && (
        <OneNotePickerModal
          memo={memo}
          session={session}
          onClose={() => setShowPickerModal(false)}
        />
      )}
      {editing ? (
        <textarea
          value={editContent}
          onChange={(e) => setEditContent(e.target.value)}
          className="w-full rounded-xl border border-border bg-bg-card p-4 font-mono text-sm text-text-primary focus:border-accent focus:ring-2 focus:ring-accent/20 outline-none sm:p-6"
          rows={Math.max(20, editContent.split('\n').length + 2)}
        />
      ) : (
        <div className="prose prose-slate dark:prose-invert prose-sm max-w-none rounded-xl border border-border border-t-4 border-t-accent bg-bg-card p-6 shadow-sm prose-headings:font-heading prose-headings:text-text-primary prose-p:text-text-secondary prose-strong:text-text-primary prose-li:text-text-secondary prose-a:text-accent prose-code:bg-accent-light prose-code:text-accent prose-code:rounded prose-code:px-1 prose-th:text-text-secondary prose-td:text-text-secondary sm:p-8">
          <ReactMarkdown remarkPlugins={[remarkGfm]}>{memo.content}</ReactMarkdown>
        </div>
      )}
      <div className="rounded-lg bg-bg-hover px-4 py-2.5 text-xs text-text-muted">
        Model: {memo.modelUsed} &middot; Tokens: {memo.promptTokens ?? 0} + {memo.completionTokens ?? 0}
        {memo.generationDurationMs != null && (
          <> &middot; Generation: {formatDuration(memo.generationDurationMs)}</>
        )}
        {session?.endedAt && memo.createdAt && (() => {
          const totalMs = new Date(memo.createdAt).getTime() - new Date(session.endedAt).getTime();
          return totalMs > 0 ? <> &middot; Total processing: {formatDuration(totalMs)}</> : null;
        })()}
      </div>
    </div>
  );
}
