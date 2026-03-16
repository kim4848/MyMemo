import { create } from 'zustand';

export type ToastType = 'success' | 'info' | 'warning' | 'error';

export interface Toast {
  id: string;
  message: string;
  href?: string;
  type: ToastType;
}

interface ToastState {
  toasts: Toast[];
  add: (message: string, href?: string, type?: ToastType) => void;
  dismiss: (id: string) => void;
}

let nextId = 0;

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  add: (message, href, type = 'success') => {
    const id = String(++nextId);
    set((s) => ({ toasts: [...s.toasts, { id, message, href, type }] }));

    const dismiss = () =>
      set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) }));

    // If tab is hidden, wait until visible before starting auto-dismiss timer
    if (document.visibilityState === 'hidden') {
      const onVisible = () => {
        if (document.visibilityState === 'visible') {
          document.removeEventListener('visibilitychange', onVisible);
          setTimeout(dismiss, 8_000);
        }
      };
      document.addEventListener('visibilitychange', onVisible);
    } else {
      setTimeout(dismiss, 8_000);
    }
  },
  dismiss: (id) =>
    set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}));
