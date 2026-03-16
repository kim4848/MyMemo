import { useState } from 'react';
import { useSessionsStore } from '../stores/sessions';

const TAG_COLORS = [
  '#6366f1', '#8b5cf6', '#ec4899', '#f43f5e',
  '#f97316', '#eab308', '#22c55e', '#14b8a6',
  '#06b6d4', '#3b82f6',
];

export default function TagFilter() {
  const { tags, selectedTagIds, toggleTagFilter, createTag, deleteTag } =
    useSessionsStore();
  const [showCreate, setShowCreate] = useState(false);
  const [newName, setNewName] = useState('');
  const [newColor, setNewColor] = useState(TAG_COLORS[0]);

  const handleCreate = async () => {
    const name = newName.trim();
    if (!name) return;
    await createTag(name, newColor);
    setNewName('');
    setShowCreate(false);
  };

  return (
    <div className="mb-4">
      <div className="flex flex-wrap items-center gap-2">
        {tags.map((tag) => {
          const active = selectedTagIds.includes(tag.id);
          return (
            <button
              key={tag.id}
              onClick={() => toggleTagFilter(tag.id)}
              className={`group flex items-center gap-1 rounded-full border px-3 py-1 text-xs font-medium transition-colors ${
                active
                  ? 'border-transparent text-white'
                  : 'border-border text-text-secondary hover:border-border-strong'
              }`}
              style={active ? { backgroundColor: tag.color ?? '#6366f1' } : undefined}
            >
              <span
                className="inline-block h-2 w-2 rounded-full"
                style={{ backgroundColor: tag.color ?? '#6366f1' }}
              />
              {tag.name}
              <span
                role="button"
                tabIndex={0}
                onClick={(e) => {
                  e.stopPropagation();
                  deleteTag(tag.id);
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') { e.stopPropagation(); deleteTag(tag.id); }
                }}
                className="ml-0.5 hidden text-current opacity-60 hover:opacity-100 group-hover:inline"
              >
                &times;
              </span>
            </button>
          );
        })}
        <button
          onClick={() => setShowCreate(!showCreate)}
          className="rounded-full border border-dashed border-border-strong px-3 py-1 text-xs text-text-muted hover:border-accent hover:text-accent"
        >
          + Tag
        </button>
      </div>

      {showCreate && (
        <div className="mt-2 flex items-center gap-2">
          <input
            type="text"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
            placeholder="Tag name..."
            className="rounded-lg border border-border bg-bg-input px-3 py-1.5 text-sm text-text-primary placeholder:text-text-muted focus:border-accent focus:ring-2 focus:ring-accent/20 outline-none"
            autoFocus
          />
          <div className="flex gap-1">
            {TAG_COLORS.map((c) => (
              <button
                key={c}
                onClick={() => setNewColor(c)}
                className={`h-5 w-5 rounded-full border-2 ${
                  newColor === c ? 'border-accent' : 'border-transparent'
                }`}
                style={{ backgroundColor: c }}
              />
            ))}
          </div>
          <button
            onClick={handleCreate}
            className="rounded-lg bg-accent px-3 py-1.5 text-xs font-medium text-white hover:bg-accent-hover"
          >
            Add
          </button>
          <button
            onClick={() => setShowCreate(false)}
            className="text-xs text-text-muted hover:text-text-secondary"
          >
            Cancel
          </button>
        </div>
      )}
    </div>
  );
}
