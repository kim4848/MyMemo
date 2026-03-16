import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useToastStore, type ToastType } from '../stores/toast';

const typeConfig: Record<ToastType, { bar: string; icon: ReactNode }> = {
  success: {
    bar: 'bg-success',
    icon: (
      <svg className="h-5 w-5 shrink-0 text-success" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
      </svg>
    ),
  },
  info: {
    bar: 'bg-accent',
    icon: (
      <svg className="h-5 w-5 shrink-0 text-accent" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="m11.25 11.25.041-.02a.75.75 0 0 1 1.063.852l-.708 2.836a.75.75 0 0 0 1.063.853l.041-.021M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9-3.75h.008v.008H12V8.25Z" />
      </svg>
    ),
  },
  warning: {
    bar: 'bg-warning',
    icon: (
      <svg className="h-5 w-5 shrink-0 text-warning" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
      </svg>
    ),
  },
  error: {
    bar: 'bg-danger',
    icon: (
      <svg className="h-5 w-5 shrink-0 text-danger" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="m9.75 9.75 4.5 4.5m0-4.5-4.5 4.5M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
      </svg>
    ),
  },
};

export default function ToastContainer() {
  const toasts = useToastStore((s) => s.toasts);
  const dismiss = useToastStore((s) => s.dismiss);
  const navigate = useNavigate();

  if (toasts.length === 0) return null;

  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2">
      {toasts.map((t) => {
        const config = typeConfig[t.type];
        return (
          <div
            key={t.id}
            className="flex items-center gap-3 rounded-lg border border-border bg-bg-card pl-1 shadow-lg animate-[slideIn_0.2s_ease-out]"
            role="alert"
          >
            <div className={`w-1 self-stretch rounded-l-lg ${config.bar}`} />
            <div className="flex items-center gap-3 px-3 py-3">
              {config.icon}
              <span className="text-sm text-text-primary">{t.message}</span>
              {t.href && (
                <button
                  onClick={() => { navigate(t.href!); dismiss(t.id); }}
                  className="ml-2 whitespace-nowrap rounded bg-accent-light px-2 py-1 text-xs font-medium text-accent hover:bg-accent/10 transition-colors"
                >
                  View
                </button>
              )}
              <button
                onClick={() => dismiss(t.id)}
                className="ml-1 text-text-muted hover:text-text-primary"
                aria-label="Dismiss"
              >
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
          </div>
        );
      })}
    </div>
  );
}
