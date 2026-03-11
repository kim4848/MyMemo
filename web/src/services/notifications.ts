import { api } from '../api/client';

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
 * Show a browser notification when a memo is ready.
 * Only shows when the tab is hidden (user is elsewhere).
 */
function showNotification(
  sessionTitle: string | null,
  sessionId: string,
  onNavigate?: (path: string) => void,
): void {
  if (!('Notification' in window)) return;
  if (Notification.permission !== 'granted') return;
  if (document.visibilityState === 'visible') return;

  const title = 'MyMemo';
  const body = sessionTitle
    ? `"${sessionTitle}" memo is ready`
    : 'Your memo is ready';

  const notification = new Notification(title, {
    body,
    tag: `memo-ready-${sessionId}`,
  });

  notification.onclick = () => {
    window.focus();
    onNavigate?.(`/sessions/${sessionId}`);
    notification.close();
  };
}

// ── Global session watch list ──────────────────────────────────────────

interface WatchedSession {
  id: string;
  title: string | null;
}

const watchedSessions = new Map<string, WatchedSession>();

/**
 * Register a session for notification polling.
 * Call after finalize so the global poller picks it up.
 */
export function watchSessionForNotification(id: string, title: string | null): void {
  watchedSessions.set(id, { id, title });
}

/**
 * Remove a session from the watch list (e.g. when user views it).
 */
export function unwatchSession(id: string): void {
  watchedSessions.delete(id);
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
        showNotification(session.title, id, navigateFn);
      }
    } catch {
      // Memo not ready yet — keep watching
    }
  }

  // Stop polling when nothing left to watch
  if (watchedSessions.size === 0) {
    stopNotificationPoller();
  }
}

/**
 * Start the global notification poller.
 * Call from a top-level component (Layout) — idempotent.
 */
export function startNotificationPoller(navigate?: (path: string) => void): void {
  navigateFn = navigate;
  if (pollTimer) return;
  if (watchedSessions.size === 0) return;
  pollTimer = setInterval(pollOnce, POLL_INTERVAL_MS);
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

/**
 * Returns the number of sessions currently being watched.
 */
export function watchedCount(): number {
  return watchedSessions.size;
}
