import { useState, useRef, useEffect } from 'react';
import { useSessionsStore } from '../stores/sessions';
import type { Tag } from '../types';

interface Props {
  sessionId: string;
  sessionTags: Tag[];
  className?: string;
}

export default function SessionTagPicker({ sessionId, sessionTags, className }: Props) {
  const { tags, addTagToSession, removeTagFromSession } = useSessionsStore();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const sessionTagIds = new Set(sessionTags.map((t) => t.id));
  const available = tags.filter((t) => !sessionTagIds.has(t.id));

  return (
    <div ref={ref} className={`relative inline-flex items-center gap-1 ${className ?? ''}`}>
      {sessionTags.map((tag) => (
        <span
          key={tag.id}
          className="group inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-[10px] font-medium text-white"
          style={{ backgroundColor: tag.color ?? '#6366f1' }}
        >
          {tag.name}
          <button
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              removeTagFromSession(sessionId, tag.id);
            }}
            className="hidden text-white/70 hover:text-white group-hover:inline"
          >
            &times;
          </button>
        </span>
      ))}
      <button
        onClick={(e) => {
          e.preventDefault();
          e.stopPropagation();
          setOpen(!open);
        }}
        className="rounded-full border border-dashed border-border px-1.5 py-0.5 text-[10px] text-text-muted hover:border-accent hover:text-accent"
      >
        +
      </button>
      {open && available.length > 0 && (
        <div className="absolute left-0 top-full z-10 mt-1 min-w-[140px] rounded-lg border border-border bg-bg-card py-1 shadow-lg">
          {available.map((tag) => (
            <button
              key={tag.id}
              onClick={(e) => {
                e.preventDefault();
                e.stopPropagation();
                addTagToSession(sessionId, tag.id);
                setOpen(false);
              }}
              className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-xs text-text-secondary hover:bg-bg-hover"
            >
              <span
                className="inline-block h-2 w-2 rounded-full"
                style={{ backgroundColor: tag.color ?? '#6366f1' }}
              />
              {tag.name}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
