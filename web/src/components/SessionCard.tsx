import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { Session } from '../types';
import { formatDateTime, formatDuration } from '../lib/format';

const statusStyles: Record<string, { bg: string; text: string }> = {
  recording: { bg: 'bg-yellow-500/10', text: 'text-yellow-400' },
  processing: { bg: 'bg-blue-500/10', text: 'text-blue-400' },
  completed: { bg: 'bg-accent/10', text: 'text-accent' },
  failed: { bg: 'bg-red-500/10', text: 'text-red-400' },
};

interface Props {
  session: Session;
  onDelete: (id: string) => void;
}

export default function SessionCard({ session, onDelete }: Props) {
  const [confirming, setConfirming] = useState(false);
  const style = statusStyles[session.status] ?? { bg: '', text: '' };

  return (
    <div className="group flex items-start justify-between gap-3 rounded-xl border border-navy-700 bg-navy-800 p-4 transition-colors hover:border-navy-600 sm:items-center">
      <Link to={`/sessions/${session.id}`} className="min-w-0 flex-1">
        <div className="flex flex-col gap-1.5 sm:flex-row sm:flex-wrap sm:items-center sm:gap-3">
          <span className={`self-start rounded-full px-2.5 py-0.5 text-xs font-medium ${style.bg} ${style.text}`}>
            {session.status}
          </span>
          <span className="text-sm text-gray-300">
            {formatDateTime(session.createdAt)}
          </span>
          <span className="text-sm text-gray-500">
            {formatDuration(session.startedAt, session.endedAt)}
          </span>
          <span className="text-xs text-gray-600">
            {session.outputMode} &middot; {session.audioSource}
          </span>
        </div>
      </Link>
      {confirming ? (
        <div className="flex shrink-0 items-center gap-2">
          <span className="text-sm text-gray-400">Are you sure?</span>
          <button
            onClick={() => onDelete(session.id)}
            className="rounded bg-red-500/20 px-2 py-0.5 text-sm text-red-400 hover:bg-red-500/30"
          >
            Yes
          </button>
          <button
            onClick={() => setConfirming(false)}
            className="rounded bg-navy-700 px-2 py-0.5 text-sm text-gray-400 hover:bg-navy-600"
          >
            No
          </button>
        </div>
      ) : (
        <button
          onClick={() => setConfirming(true)}
          className="shrink-0 text-sm text-gray-600 transition-opacity hover:text-red-400 md:opacity-0 md:group-hover:opacity-100"
        >
          Delete
        </button>
      )}
    </div>
  );
}
