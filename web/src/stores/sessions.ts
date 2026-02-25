import { create } from 'zustand';
import { api } from '../api/client';
import type { Session, CreateSessionRequest } from '../types';

interface SessionsState {
  sessions: Session[];
  loading: boolean;
  error: string | null;
  fetchSessions: () => Promise<void>;
  createSession: (req: CreateSessionRequest) => Promise<Session>;
  deleteSession: (id: string) => Promise<void>;
}

export const useSessionsStore = create<SessionsState>((set, get) => ({
  sessions: [],
  loading: false,
  error: null,

  fetchSessions: async () => {
    set({ loading: true, error: null });
    try {
      const sessions = await api.sessions.list();
      set({ sessions, loading: false });
    } catch (e) {
      set({ error: (e as Error).message, loading: false });
    }
  },

  createSession: async (req) => {
    const session = await api.sessions.create(req);
    set({ sessions: [session, ...get().sessions] });
    return session;
  },

  deleteSession: async (id) => {
    await api.sessions.delete(id);
    set({ sessions: get().sessions.filter((s) => s.id !== id) });
  },
}));
