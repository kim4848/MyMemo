import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { SessionWithTags } from '../types';
import { formatRelativeTime, formatDuration } from '../lib/format';
import SessionTagPicker from './SessionTagPicker';
import DropdownMenu from './DropdownMenu';

const statusStyles: Record<string, { bg: string; text: string }> = {
  recording: { bg: 'bg-warning-light', text: 'text-warning' },
  processing: { bg: 'bg-accent-light', text: 'text-accent' },
  completed: { bg: 'bg-success-light', text: 'text-success' },
  failed: { bg: 'bg-danger-light', text: 'text-danger' },
};

const statusBarColors: Record<string, string> = {
  recording: 'bg-warning',
  processing: 'bg-accent',
  completed: 'bg-success',
  failed: 'bg-danger',
};

interface Props {
  session: SessionWithTags;
  onDelete: (id: string) => void;
}

export default function SessionCard({ session, onDelete }: Props) {
  const [confirming, setConfirming] = useState(false);
  const style = statusStyles[session.status] ?? { bg: '', text: '' };
  const barColor = statusBarColors[session.status] ?? 'bg-border';

  return (
    <div className="group relative flex items-start gap-3 rounded-xl border border-border bg-bg-card p-4 shadow-sm transition-shadow hover:shadow-md overflow-hidden">
      {/* Left status bar */}
      <div className={`absolute left-0 top-0 bottom-0 w-1 rounded-l-xl ${barColor}`} />

      {/* Document icon */}
      <div className="ml-3 mt-0.5 flex-shrink-0">
        <svg className="h-8 w-8 text-text-muted/60" fill="none" viewBox="0 0 32 32" strokeWidth={1}>
          <rect x="6" y="4" width="20" height="24" rx="3" stroke="currentColor" strokeWidth="1.5" />
          <path d="M11 11h10M11 15h10M11 19h6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
      </div>

      {/* Content */}
      <Link to={`/sessions/${session.id}`} className="min-w-0 flex-1">
        <h3 className="text-base font-medium text-text-primary truncate">
          {session.title ?? 'Untitled Session'}
        </h3>
        <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
          <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${style.bg} ${style.text}`}>
            {session.status}
          </span>
          <span className="text-text-secondary">{formatRelativeTime(session.createdAt)}</span>
          <span className="text-text-muted">{formatDuration(session.startedAt, session.endedAt)}</span>
        </div>
        <SessionTagPicker sessionId={session.id} sessionTags={session.tags} className="mt-1.5" />
      </Link>

      {/* Desktop: 3-dot menu */}
      <div className="flex-shrink-0 self-start opacity-0 group-hover:opacity-100 transition-opacity hidden md:block">
        <DropdownMenu
          items={[
            {
              label: 'Delete',
              variant: 'danger',
              icon: (
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0" />
                </svg>
              ),
              onClick: () => setConfirming(true),
            },
          ]}
        />
      </div>

      {/* Mobile: simple delete button */}
      {!confirming && (
        <button
          onClick={(e) => { e.preventDefault(); setConfirming(true); }}
          className="md:hidden shrink-0 text-sm text-text-muted hover:text-danger"
        >
          Delete
        </button>
      )}

      {/* Delete confirmation bar */}
      {confirming && (
        <div className="flex shrink-0 items-center gap-2">
          <span className="text-sm text-text-secondary">Delete?</span>
          <button
            onClick={(e) => { e.preventDefault(); onDelete(session.id); }}
            className="rounded bg-danger-light px-2 py-0.5 text-sm text-danger hover:bg-danger/10"
          >
            Yes
          </button>
          <button
            onClick={(e) => { e.preventDefault(); setConfirming(false); }}
            className="rounded bg-bg-hover px-2 py-0.5 text-sm text-text-secondary hover:bg-border"
          >
            No
          </button>
        </div>
      )}
    </div>
  );
}
