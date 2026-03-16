import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { SessionWithTags } from '../types';
import { formatDateTime, formatDuration } from '../lib/format';
import SessionTagPicker from './SessionTagPicker';

const statusStyles: Record<string, { bg: string; text: string }> = {
  recording: { bg: 'bg-warning-light', text: 'text-warning' },
  processing: { bg: 'bg-accent-light', text: 'text-accent' },
  completed: { bg: 'bg-success-light', text: 'text-success' },
  failed: { bg: 'bg-danger-light', text: 'text-danger' },
};

interface Props {
  session: SessionWithTags;
  onDelete: (id: string) => void;
}

export default function SessionCard({ session, onDelete }: Props) {
  const [confirming, setConfirming] = useState(false);
  const style = statusStyles[session.status] ?? { bg: '', text: '' };

  return (
    <div className="group flex items-start justify-between gap-3 rounded-xl border border-border bg-bg-card p-4 shadow-sm transition-shadow hover:shadow-md sm:items-center">
      <Link to={`/sessions/${session.id}`} className="min-w-0 flex-1">
        <div className="flex flex-col gap-1.5">
          {session.title && (
            <span className="truncate text-sm font-medium text-text-primary">{session.title}</span>
          )}
          <div className="flex flex-col gap-1.5 sm:flex-row sm:flex-wrap sm:items-center sm:gap-3">
            <span className={`self-start rounded-full px-2.5 py-0.5 text-xs font-medium ${style.bg} ${style.text}`}>
              {session.status}
            </span>
            <span className="text-sm text-text-secondary">
              {formatDateTime(session.createdAt)}
            </span>
            <span className="text-sm text-text-muted">
              {formatDuration(session.startedAt, session.endedAt)}
            </span>
            <span className="text-xs text-text-muted">
              {session.outputMode} &middot; {session.audioSource}
            </span>
          </div>
          <SessionTagPicker sessionId={session.id} sessionTags={session.tags} />
        </div>
      </Link>
      {confirming ? (
        <div className="flex shrink-0 items-center gap-2">
          <span className="text-sm text-text-secondary">Are you sure?</span>
          <button
            onClick={() => onDelete(session.id)}
            className="rounded bg-danger-light px-2 py-0.5 text-sm text-danger hover:bg-danger/10"
          >
            Yes
          </button>
          <button
            onClick={() => setConfirming(false)}
            className="rounded bg-bg-hover px-2 py-0.5 text-sm text-text-secondary hover:bg-border"
          >
            No
          </button>
        </div>
      ) : (
        <button
          onClick={() => setConfirming(true)}
          className="shrink-0 text-sm text-text-muted transition-opacity hover:text-danger md:opacity-0 md:group-hover:opacity-100"
        >
          Delete
        </button>
      )}
    </div>
  );
}
