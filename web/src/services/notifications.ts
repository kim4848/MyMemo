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
 * Returns the Notification instance, or null if not permitted.
 */
export function notifyMemoReady(
  sessionTitle: string | null,
  sessionId: string,
): Notification | null {
  if (!('Notification' in window)) return null;
  if (Notification.permission !== 'granted') return null;

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
    notification.close();
  };

  return notification;
}
