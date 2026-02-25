import type { Memo } from '../types';

interface Props {
  memo: Memo | null;
  isProcessing: boolean;
}

export default function MemoViewer({ memo, isProcessing }: Props) {
  if (isProcessing && !memo) {
    return (
      <div className="rounded-lg border border-blue-200 bg-blue-50 p-4">
        <p className="text-blue-700">Generating memo... This may take a moment.</p>
      </div>
    );
  }

  if (!memo) return null;

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <h2 className="text-lg font-semibold text-gray-900">Memo</h2>
        <span className="text-xs text-gray-400">
          {memo.outputMode === 'full' ? 'Full Transcript' : 'Summary'}
        </span>
      </div>
      <div className="prose max-w-none rounded-lg border bg-white p-4">
        <div className="whitespace-pre-wrap">{memo.content}</div>
      </div>
      <p className="text-xs text-gray-400">
        Model: {memo.modelUsed} | Tokens: {memo.promptTokens ?? 0} + {memo.completionTokens ?? 0}
      </p>
    </div>
  );
}
