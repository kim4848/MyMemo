import { useState } from 'react';
import type { LocalChunk } from '../stores/recorder';

const statusConfig: Record<string, { color: string }> = {
  pending: { color: 'bg-text-muted' },
  uploading: { color: 'bg-accent' },
  uploaded: { color: 'bg-success' },
  failed: { color: 'bg-danger' },
};

interface Props {
  chunks: LocalChunk[];
}

export default function ChunkStatusList({ chunks }: Props) {
  const [open, setOpen] = useState(false);

  if (chunks.length === 0) return null;

  return (
    <div className="rounded-xl border border-border bg-bg-card p-5 shadow-sm">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between"
      >
        <h3 className="text-xs font-semibold uppercase tracking-wider text-text-muted">Chunks</h3>
        <svg
          className={`h-4 w-4 text-text-muted transition-transform duration-200 ${open ? 'rotate-180' : ''}`}
          fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor"
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
        </svg>
      </button>
      {open && (
        <div className="mt-3 space-y-2">
          {chunks.map((chunk) => {
            const cfg = statusConfig[chunk.status] ?? statusConfig.pending;
            return (
              <div key={chunk.chunkIndex} className="flex items-center gap-3 text-sm">
                <span className={`h-2.5 w-2.5 rounded-full ${cfg.color}`} />
                <span className="text-text-primary">Chunk {chunk.chunkIndex + 1}</span>
                <span className="text-text-muted">{chunk.status}</span>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
