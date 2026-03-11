import { create } from 'zustand';

export interface Toast {
  id: string;
  message: string;
  href?: string;
}

interface ToastState {
  toasts: Toast[];
  add: (message: string, href?: string) => void;
  dismiss: (id: string) => void;
}

let nextId = 0;

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  add: (message, href) => {
    const id = String(++nextId);
    set((s) => ({ toasts: [...s.toasts, { id, message, href }] }));
    setTimeout(() => {
      set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) }));
    }, 8_000);
  },
  dismiss: (id) =>
    set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}));
