import type { Memo } from '../types';

interface Props {
  memo: Memo | null;
  isProcessing: boolean;
}

export default function MemoViewer({ memo, isProcessing }: Props) {
  if (isProcessing && !memo) {
    return (
      <div className="rounded-xl border border-blue-500/20 bg-blue-500/10 p-5">
        <div className="flex items-center gap-3">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-gray-600 border-t-blue-400" />
          <p className="text-blue-400">Generating memo... This may take a moment.</p>
        </div>
      </div>
    );
  }

  if (!memo) return null;

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <h2 className="text-lg font-semibold text-white">Memo</h2>
        <span className="rounded-full bg-accent/10 px-2.5 py-0.5 text-xs font-medium text-accent">
          {memo.outputMode === 'full' ? 'Full Transcript' : 'Summary'}
        </span>
      </div>
      <div className="rounded-xl border border-navy-700 bg-navy-800 p-6">
        <div className="whitespace-pre-wrap text-sm leading-relaxed text-gray-300">{memo.content}</div>
      </div>
      <p className="text-xs text-gray-600">
        Model: {memo.modelUsed} &middot; Tokens: {memo.promptTokens ?? 0} + {memo.completionTokens ?? 0}
      </p>
    </div>
  );
}
