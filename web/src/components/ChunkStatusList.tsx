import type { LocalChunk } from '../stores/recorder';

const statusConfig: Record<string, { icon: string; color: string }> = {
  pending: { icon: '...', color: 'text-gray-600' },
  uploading: { icon: '>>', color: 'text-blue-400' },
  uploaded: { icon: '\u2713', color: 'text-accent' },
  failed: { icon: '!', color: 'text-red-400' },
};

interface Props {
  chunks: LocalChunk[];
}

export default function ChunkStatusList({ chunks }: Props) {
  if (chunks.length === 0) return null;

  return (
    <div className="rounded-xl border border-navy-700 bg-navy-800 p-5">
      <h3 className="mb-3 text-sm font-semibold uppercase tracking-wider text-gray-500">Chunks</h3>
      <div className="space-y-2">
        {chunks.map((chunk) => {
          const cfg = statusConfig[chunk.status] ?? statusConfig.pending;
          return (
            <div key={chunk.chunkIndex} className="flex items-center gap-3 text-sm">
              <span className={`w-5 text-center font-mono ${cfg.color}`}>{cfg.icon}</span>
              <span className="text-gray-300">Chunk {chunk.chunkIndex + 1}</span>
              <span className="text-gray-600">{chunk.status}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
