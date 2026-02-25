import { Link } from 'react-router-dom';
import type { Session } from '../types';
import { formatDateTime, formatDuration } from '../lib/format';

const statusStyles: Record<string, string> = {
  recording: 'bg-yellow-100 text-yellow-800',
  processing: 'bg-blue-100 text-blue-800',
  completed: 'bg-green-100 text-green-800',
  failed: 'bg-red-100 text-red-800',
};

interface Props {
  session: Session;
  onDelete: (id: string) => void;
}

export default function SessionCard({ session, onDelete }: Props) {
  return (
    <div className="flex items-center justify-between rounded-lg border bg-white p-4">
      <Link to={`/sessions/${session.id}`} className="flex-1">
        <div className="flex items-center gap-3">
          <span
            className={`rounded-full px-2 py-0.5 text-xs font-medium ${statusStyles[session.status] ?? ''}`}
          >
            {session.status}
          </span>
          <span className="text-sm text-gray-500">
            {formatDateTime(session.createdAt)}
          </span>
          <span className="text-sm text-gray-400">
            {formatDuration(session.startedAt, session.endedAt)}
          </span>
          <span className="text-xs text-gray-400">
            {session.outputMode} &middot; {session.audioSource}
          </span>
        </div>
      </Link>
      <button
        onClick={() => onDelete(session.id)}
        className="ml-4 text-sm text-red-500 hover:text-red-700"
      >
        Delete
      </button>
    </div>
  );
}
