import type { LocalChunk } from '../stores/recorder';

const statusIcons: Record<string, string> = {
  pending: '...',
  uploading: '>>',
  uploaded: 'OK',
  failed: '!!',
};

const statusStyles: Record<string, string> = {
  pending: 'text-gray-400',
  uploading: 'text-blue-500',
  uploaded: 'text-green-600',
  failed: 'text-red-500',
};

interface Props {
  chunks: LocalChunk[];
}

export default function ChunkStatusList({ chunks }: Props) {
  if (chunks.length === 0) return null;

  return (
    <div className="space-y-1">
      <h3 className="text-sm font-medium text-gray-700">Chunks</h3>
      {chunks.map((chunk) => (
        <div key={chunk.chunkIndex} className="flex items-center gap-2 text-sm">
          <span className={statusStyles[chunk.status]}>
            [{statusIcons[chunk.status]}]
          </span>
          <span>Chunk {chunk.chunkIndex + 1}</span>
          <span className="text-gray-400">{chunk.status}</span>
        </div>
      ))}
    </div>
  );
}
