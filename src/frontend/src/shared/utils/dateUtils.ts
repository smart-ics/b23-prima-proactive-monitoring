/**
 * Temporal formatting utilities conforming to Asia/Jakarta (WIB, UTC+7).
 */

/**
 * Formats an ISO UTC date string into Asia/Jakarta WIB representation:
 * Example: "20 Nov 2026, 13:21:05"
 */
export function formatWibDateTime(isoDateString?: string | null, includeZone = false): string {
  if (!isoDateString) return '-';
  try {
    const date = new Date(isoDateString);
    if (isNaN(date.getTime())) return '-';

    const dateFormatted = new Intl.DateTimeFormat('en-GB', {
      timeZone: 'Asia/Jakarta',
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    }).format(date);

    const timeFormatted = new Intl.DateTimeFormat('en-GB', {
      timeZone: 'Asia/Jakarta',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    }).format(date);

    return includeZone ? `${dateFormatted}, ${timeFormatted} WIB` : `${dateFormatted}, ${timeFormatted}`;
  } catch {
    return isoDateString;
  }
}

/**
 * Computes human-scannable relative time from now:
 * Examples: "Just now", "3m ago", "2h ago", "2d ago"
 */
export function formatRelativeTime(isoDateString?: string | null): string {
  if (!isoDateString) return '-';
  try {
    const date = new Date(isoDateString);
    if (isNaN(date.getTime())) return '-';

    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    if (diffMs < 0) return 'Just now';

    const diffSec = Math.floor(diffMs / 1000);
    if (diffSec < 45) return 'Just now';

    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) return `${diffMin}m ago`;

    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 24) return `${diffHours}h ago`;

    const diffDays = Math.floor(diffHours / 24);
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 30) return `${diffDays}d ago`;

    return formatWibDateTime(isoDateString, false);
  } catch {
    return isoDateString;
  }
}
