import { useState, useCallback } from 'react';
import type { Memo, Session } from '../types';
import { outputModeLabels } from '../types';
import { useMicrosoftAuth } from '../hooks/useMicrosoftAuth';
import OneNotePickerModal from './OneNotePickerModal';

interface Props {
  memo: Memo | null;
  isProcessing: boolean;
  allTranscribed: boolean;
  session?: Session;
}

function formatDuration(ms: number): string {
  return ms >= 1000 ? `${(ms / 1000).toFixed(1)}s` : `${ms}ms`;
}

function memoToHtml(content: string): string {
  const paragraphs = content
    .split('\n')
    .map((line) => `<p>${line || '&nbsp;'}</p>`)
    .join('\n');
  return paragraphs;
}

export default function MemoViewer({ memo, isProcessing, allTranscribed, session }: Props) {
  const { isAuthenticated, login } = useMicrosoftAuth();
  const [showPickerModal, setShowPickerModal] = useState(false);
  const [copied, setCopied] = useState(false);

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
  if (isProcessing && !memo) {
    return (
      <div className="rounded-xl border border-blue-500/20 bg-blue-500/10 p-5">
        <div className="flex items-center gap-3">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-gray-600 border-t-blue-400" />
          <p className="text-blue-400">
            {allTranscribed
              ? 'Generating memo... This may take a moment.'
              : 'Transcribing audio... This may take a moment.'}
          </p>
        </div>
      </div>
    );
  }

  if (!memo) return null;

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold text-white">Memo</h2>
        <span className="rounded-full bg-accent/10 px-2.5 py-0.5 text-xs font-medium text-accent">
          {outputModeLabels[memo.outputMode]}
        </span>
        <div className="ml-auto flex items-center gap-2">
          <button
            onClick={handleCopy}
            className="rounded-lg border border-navy-600 bg-navy-700 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-accent hover:text-accent sm:py-1"
          >
            {copied ? 'Kopieret!' : 'Kopiér'}
          </button>
          <button
            onClick={async () => {
              if (!isAuthenticated) await login();
              setShowPickerModal(true);
            }}
            className="rounded-lg border border-navy-600 bg-navy-700 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-accent hover:text-accent sm:py-1"
          >
            Send to OneNote
          </button>
        </div>
      </div>
      {showPickerModal && session && (
        <OneNotePickerModal
          memo={memo}
          session={session}
          onClose={() => setShowPickerModal(false)}
        />
      )}
      <div className="rounded-xl border border-navy-700 bg-navy-800 p-4 sm:p-6">
        <div className="whitespace-pre-wrap text-sm leading-relaxed text-gray-300">{memo.content}</div>
      </div>
      <p className="text-xs text-gray-600">
        Model: {memo.modelUsed} &middot; Tokens: {memo.promptTokens ?? 0} + {memo.completionTokens ?? 0}
        {memo.generationDurationMs != null && (
          <> &middot; Generation: {formatDuration(memo.generationDurationMs)}</>
        )}
        {session?.endedAt && memo.createdAt && (() => {
          const totalMs = new Date(memo.createdAt).getTime() - new Date(session.endedAt).getTime();
          return totalMs > 0 ? <> &middot; Total processing: {formatDuration(totalMs)}</> : null;
        })()}
      </p>
    </div>
  );
}
