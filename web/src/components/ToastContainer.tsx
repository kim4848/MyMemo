import { useNavigate } from 'react-router-dom';
import { useToastStore } from '../stores/toast';

export default function ToastContainer() {
  const toasts = useToastStore((s) => s.toasts);
  const dismiss = useToastStore((s) => s.dismiss);
  const navigate = useNavigate();

  if (toasts.length === 0) return null;

  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2">
      {toasts.map((t) => (
        <div
          key={t.id}
          className="flex items-center gap-3 rounded-lg border border-accent/30 bg-navy-800 px-4 py-3 shadow-lg animate-[slideIn_0.2s_ease-out]"
          role="alert"
        >
          <svg className="h-5 w-5 shrink-0 text-accent" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
          </svg>
          <span className="text-sm text-gray-200">{t.message}</span>
          {t.href && (
            <button
              onClick={() => { navigate(t.href!); dismiss(t.id); }}
              className="ml-2 whitespace-nowrap rounded bg-accent/20 px-2 py-1 text-xs font-medium text-accent hover:bg-accent/30 transition-colors"
            >
              View
            </button>
          )}
          <button
            onClick={() => dismiss(t.id)}
            className="ml-1 text-gray-500 hover:text-gray-300"
            aria-label="Dismiss"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      ))}
    </div>
  );
}
