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
                  : 'border-navy-600 text-gray-400 hover:border-navy-500'
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
          className="rounded-full border border-dashed border-navy-600 px-3 py-1 text-xs text-gray-500 hover:border-navy-500 hover:text-gray-400"
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
            className="rounded-lg border border-navy-600 bg-navy-900 px-3 py-1.5 text-sm text-white placeholder-gray-500 focus:border-accent focus:outline-none"
            autoFocus
          />
          <div className="flex gap-1">
            {TAG_COLORS.map((c) => (
              <button
                key={c}
                onClick={() => setNewColor(c)}
                className={`h-5 w-5 rounded-full border-2 ${
                  newColor === c ? 'border-white' : 'border-transparent'
                }`}
                style={{ backgroundColor: c }}
              />
            ))}
          </div>
          <button
            onClick={handleCreate}
            className="rounded-lg bg-accent px-3 py-1.5 text-xs font-medium text-white hover:bg-accent/80"
          >
            Add
          </button>
          <button
            onClick={() => setShowCreate(false)}
            className="text-xs text-gray-500 hover:text-gray-400"
          >
            Cancel
          </button>
        </div>
      )}
    </div>
  );
}
