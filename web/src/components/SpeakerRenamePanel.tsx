import { useState, useMemo } from 'react';
import { api } from '../api/client';

interface SpeakerRenamePanelProps {
  sessionId: string;
  transcriptionTexts: Array<{ chunkId: string; rawText: string }>;
  onRenamed: () => void;
}

function extractSpeakerLabels(texts: Array<{ rawText: string }>): string[] {
  const labels = new Set<string>();
  for (const { rawText } of texts) {
    const matches = rawText.matchAll(/Speaker \d+/g);
    for (const match of matches) {
      labels.add(match[0]);
    }
  }
  // Sort descending by number length then value to process longer labels first
  return [...labels].sort((a, b) => {
    const numA = parseInt(a.replace('Speaker ', ''), 10);
    const numB = parseInt(b.replace('Speaker ', ''), 10);
    return numB - numA;
  });
}

export default function SpeakerRenamePanel({ sessionId, transcriptionTexts, onRenamed }: SpeakerRenamePanelProps) {
  const speakers = useMemo(() => extractSpeakerLabels(transcriptionTexts), [transcriptionTexts]);
  const [names, setNames] = useState<Record<string, string>>(() =>
    Object.fromEntries(speakers.map((s) => [s, s])),
  );
  const [open, setOpen] = useState(true);
  const [saving, setSaving] = useState(false);

  if (speakers.length === 0) return null;

  const hasChanges = speakers.some((s) => names[s] && names[s] !== s);

  async function handleSave() {
    setSaving(true);
    try {
      // Process speakers with higher numbers first to avoid partial matches
      // e.g. "Speaker 10:" before "Speaker 1:"
      for (const speaker of speakers) {
        const newName = names[speaker];
        if (newName && newName !== speaker) {
          await api.sessions.renameSpeaker(sessionId, `${speaker}:`, `${newName}:`);
        }
      }
      onRenamed();
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="rounded-xl border border-border bg-bg-card p-4 shadow-sm sm:p-5">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between"
      >
        <h2 className="text-xs font-semibold uppercase tracking-wider text-text-muted">Talere</h2>
        <svg
          className={`h-4 w-4 text-text-muted transition-transform duration-200 ${open ? 'rotate-180' : ''}`}
          fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor"
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
        </svg>
      </button>
      {open && (
        <div className="mt-3 space-y-2">
          {speakers.map((speaker) => (
            <div key={speaker} className="flex items-center gap-3">
              <span className="min-w-[100px] text-sm text-text-secondary">{speaker}</span>
              <input
                type="text"
                value={names[speaker] ?? speaker}
                onChange={(e) => setNames((prev) => ({ ...prev, [speaker]: e.target.value }))}
                className="flex-1 rounded-lg border border-border bg-bg-input px-3 py-1.5 text-sm text-text-primary outline-none focus:border-accent focus:ring-2 focus:ring-accent/20"
              />
            </div>
          ))}
          <button
            disabled={!hasChanges || saving}
            onClick={handleSave}
            className="mt-2 rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-accent-hover disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {saving ? 'Omdøber...' : 'Omdøb'}
          </button>
        </div>
      )}
    </div>
  );
}

export { extractSpeakerLabels };
