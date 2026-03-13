import { create } from 'zustand';
import { api } from '../api/client';
import type { SessionWithTags, Tag, CreateSessionRequest } from '../types';

interface SessionsState {
  sessions: SessionWithTags[];
  tags: Tag[];
  loading: boolean;
  error: string | null;
  selectedTagIds: string[];
  fetchSessions: () => Promise<void>;
  fetchTags: () => Promise<void>;
  createSession: (req: CreateSessionRequest) => Promise<SessionWithTags>;
  deleteSession: (id: string) => Promise<void>;
  createTag: (name: string, color?: string) => Promise<Tag>;
  deleteTag: (id: string) => Promise<void>;
  updateTag: (id: string, name: string, color?: string) => Promise<void>;
  addTagToSession: (sessionId: string, tagId: string) => Promise<void>;
  removeTagFromSession: (sessionId: string, tagId: string) => Promise<void>;
  setSelectedTagIds: (ids: string[]) => void;
  toggleTagFilter: (id: string) => void;
}

export const useSessionsStore = create<SessionsState>((set, get) => ({
  sessions: [],
  tags: [],
  loading: false,
  error: null,
  selectedTagIds: [],

  fetchSessions: async () => {
    set({ loading: true, error: null });
    try {
      const sessions = await api.sessions.list();
      set({ sessions, loading: false });
    } catch (e) {
      set({ error: (e as Error).message, loading: false });
    }
  },

  fetchTags: async () => {
    try {
      const tags = await api.tags.list();
      set({ tags });
    } catch {
      // Tags are non-critical, silently fail
    }
  },

  createSession: async (req) => {
    const session = await api.sessions.create(req);
    const sessionWithTags = { ...session, tags: [] };
    set({ sessions: [sessionWithTags, ...get().sessions] });
    return sessionWithTags;
  },

  deleteSession: async (id) => {
    await api.sessions.delete(id);
    set({ sessions: get().sessions.filter((s) => s.id !== id) });
  },

  createTag: async (name, color) => {
    const tag = await api.tags.create(name, color);
    set({ tags: [...get().tags, tag].sort((a, b) => a.name.localeCompare(b.name)) });
    return tag;
  },

  deleteTag: async (id) => {
    await api.tags.delete(id);
    set({
      tags: get().tags.filter((t) => t.id !== id),
      selectedTagIds: get().selectedTagIds.filter((tid) => tid !== id),
      sessions: get().sessions.map((s) => ({
        ...s,
        tags: s.tags.filter((t) => t.id !== id),
      })),
    });
  },

  updateTag: async (id, name, color) => {
    await api.tags.update(id, name, color);
    const updatedTag = { ...get().tags.find((t) => t.id === id)!, name, color: color ?? null };
    set({
      tags: get().tags.map((t) => (t.id === id ? updatedTag : t)),
      sessions: get().sessions.map((s) => ({
        ...s,
        tags: s.tags.map((t) => (t.id === id ? updatedTag : t)),
      })),
    });
  },

  addTagToSession: async (sessionId, tagId) => {
    await api.tags.addToSession(sessionId, tagId);
    const tag = get().tags.find((t) => t.id === tagId);
    if (!tag) return;
    set({
      sessions: get().sessions.map((s) =>
        s.id === sessionId ? { ...s, tags: [...s.tags, tag].sort((a, b) => a.name.localeCompare(b.name)) } : s,
      ),
    });
  },

  removeTagFromSession: async (sessionId, tagId) => {
    await api.tags.removeFromSession(sessionId, tagId);
    set({
      sessions: get().sessions.map((s) =>
        s.id === sessionId ? { ...s, tags: s.tags.filter((t) => t.id !== tagId) } : s,
      ),
    });
  },

  setSelectedTagIds: (ids) => set({ selectedTagIds: ids }),

  toggleTagFilter: (id) => {
    const current = get().selectedTagIds;
    set({
      selectedTagIds: current.includes(id)
        ? current.filter((tid) => tid !== id)
        : [...current, id],
    });
  },
}));
