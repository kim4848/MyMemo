import { api } from '../api/client';
import { useToastStore } from '../stores/toast';

/**
 * Request browser notification permission.
 * Safe to call multiple times — no-ops if already granted or denied.
 */
export async function requestNotificationPermission(): Promise<NotificationPermission> {
  if (!('Notification' in window)) return 'denied';
  if (Notification.permission !== 'default') return Notification.permission;
  return Notification.requestPermission();
}

/**
 * Notify the user that a memo is ready.
 * Always shows an in-app toast. Also shows a browser notification if permitted.
 */
function notifyMemoReady(
  sessionTitle: string | null,
  sessionId: string,
  onNavigate?: (path: string) => void,
): void {
  const message = sessionTitle
    ? `"${sessionTitle}" memo is ready`
    : 'Your memo is ready';

  // In-app toast — always works
  useToastStore.getState().add(message, `/sessions/${sessionId}`);

  // Browser notification — best-effort
  if ('Notification' in window && Notification.permission === 'granted') {
    const notification = new Notification('MyMemo', {
      body: message,
      tag: `memo-ready-${sessionId}`,
    });

    notification.onclick = () => {
      window.focus();
      onNavigate?.(`/sessions/${sessionId}`);
      notification.close();
    };
  }
}

// ── Global session watch list ──────────────────────────────────────────

interface WatchedSession {
  id: string;
  title: string | null;
}

const watchedSessions = new Map<string, WatchedSession>();

/**
 * Register a session for notification polling.
 * Call after finalize — automatically starts the poller.
 */
export function watchSessionForNotification(id: string, title: string | null): void {
  watchedSessions.set(id, { id, title });
  ensurePolling();
}

/**
 * Remove a session from the watch list and fire the notification.
 * Called by SessionDetailPage when its own polling finds the memo first.
 */
export function unwatchSession(id: string): void {
  const session = watchedSessions.get(id);
  if (session) {
    watchedSessions.delete(id);
    notifyMemoReady(session.title, id, navigateFn);
  }
}

// ── Poller ─────────────────────────────────────────────────────────────

const POLL_INTERVAL_MS = 5_000;
let pollTimer: ReturnType<typeof setInterval> | null = null;
let navigateFn: ((path: string) => void) | undefined;

async function pollOnce(): Promise<void> {
  for (const [id, session] of watchedSessions) {
    try {
      const memo = await api.memos.get(id);
      if (memo) {
        watchedSessions.delete(id);
        notifyMemoReady(session.title, id, navigateFn);
      }
    } catch {
      // Memo not ready yet — keep watching
    }
  }

  if (watchedSessions.size === 0) {
    stopNotificationPoller();
  }
}

/** Start the internal poller if sessions are waiting and it's not already running. */
function ensurePolling(): void {
  if (pollTimer) return;
  if (watchedSessions.size === 0) return;
  pollTimer = setInterval(pollOnce, POLL_INTERVAL_MS);
}

/**
 * Set the navigate function used for notification click handling.
 * Call from Layout on mount.
 */
export function setNotificationNavigate(navigate: (path: string) => void): void {
  navigateFn = navigate;
}

/**
 * Stop the global notification poller.
 */
export function stopNotificationPoller(): void {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = null;
  }
}
