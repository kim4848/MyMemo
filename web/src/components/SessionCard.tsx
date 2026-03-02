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
  const style = statusStyles[session.status] ?? { bg: '', text: '' };

  return (
    <div className="group flex items-center justify-between rounded-xl border border-navy-700 bg-navy-800 p-4 transition-colors hover:border-navy-600">
      <Link to={`/sessions/${session.id}`} className="flex-1">
        <div className="flex items-center gap-3">
          <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${style.bg} ${style.text}`}>
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
      <button
        onClick={() => onDelete(session.id)}
        className="ml-4 text-sm text-gray-600 opacity-0 transition-opacity hover:text-red-400 group-hover:opacity-100"
      >
        Delete
      </button>
    </div>
  );
}
