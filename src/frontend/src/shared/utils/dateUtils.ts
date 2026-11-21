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

export type DatePreset = 'Today' | 'This Week' | 'This Month' | 'Last Month' | 'Custom';

export interface UtcDateRange {
  fromUtc: string; // ISO 8601 UTC
  toUtc: string;   // ISO 8601 UTC
}

const WIB_TIMEZONE = 'Asia/Jakarta';
const WIB_OFFSET_MS = 7 * 3600 * 1000;
const DAY_MS = 86_400_000;

function wibStartOfDayUtc(year: number, monthIndex: number, day: number): Date {
  return new Date(Date.UTC(year, monthIndex, day, 0, 0, 0, 0) - WIB_OFFSET_MS);
}

function wibEndOfDayUtc(year: number, monthIndex: number, day: number): Date {
  return new Date(Date.UTC(year, monthIndex, day, 23, 59, 59, 999) - WIB_OFFSET_MS);
}

/**
 * Translates Indonesian hospital operational calendar presets (Asia/Jakarta, WIB, UTC+7)
 * or a custom date range into exact UTC ISO 8601 start and end boundary strings.
 */
export function getWibDateRange(
  preset: DatePreset,
  customRange?: [Date | null, Date | null],
): UtcDateRange {
  const now = new Date();
  const wibFormatter = new Intl.DateTimeFormat('en-CA', {
    timeZone: WIB_TIMEZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  });
  const todayWibStr = wibFormatter.format(now); // "YYYY-MM-DD"
  const [year, month, day] = todayWibStr.split('-').map(Number);

  let startUtc: Date;
  let endUtc: Date;

  switch (preset) {
    case 'This Week': {
      // Monday of current week WIB to end of today WIB
      const todayUtc = new Date(Date.UTC(year, month - 1, day));
      const dayOfWeek = todayUtc.getUTCDay(); // 0 is Sun, 1 is Mon
      const diffToMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
      const mondayUtc = new Date(todayUtc.getTime() - diffToMonday * DAY_MS);
      startUtc = wibStartOfDayUtc(
        mondayUtc.getUTCFullYear(),
        mondayUtc.getUTCMonth(),
        mondayUtc.getUTCDate(),
      );
      endUtc = wibEndOfDayUtc(year, month - 1, day);
      break;
    }
    case 'This Month': {
      // 1st day of month 00:00:00.000 WIB to end of today WIB
      startUtc = wibStartOfDayUtc(year, month - 1, 1);
      endUtc = wibEndOfDayUtc(year, month - 1, day);
      break;
    }
    case 'Last Month': {
      // 1st day of previous month 00:00:00.000 WIB to last day of previous month 23:59:59.999 WIB
      const lastMonthYear = month === 1 ? year - 1 : year;
      const lastMonthIndex = month === 1 ? 11 : month - 2;
      const lastDayOfLastMonth = new Date(Date.UTC(lastMonthYear, lastMonthIndex + 1, 0)).getUTCDate();
      startUtc = wibStartOfDayUtc(lastMonthYear, lastMonthIndex, 1);
      endUtc = wibEndOfDayUtc(lastMonthYear, lastMonthIndex, lastDayOfLastMonth);
      break;
    }
    case 'Custom': {
      const customStart = customRange?.[0];
      const customEnd = customRange?.[1];
      if (
        customStart &&
        customEnd &&
        !Number.isNaN(customStart.getTime()) &&
        !Number.isNaN(customEnd.getTime())
      ) {
        startUtc = new Date(customStart.getTime());
        startUtc.setHours(0, 0, 0, 0);
        endUtc = new Date(customEnd.getTime());
        endUtc.setHours(23, 59, 59, 999);
      } else {
        startUtc = wibStartOfDayUtc(year, month - 1, day);
        endUtc = wibEndOfDayUtc(year, month - 1, day);
      }
      break;
    }
    case 'Today':
    default: {
      // 00:00:00.000 WIB to 23:59:59.999 WIB
      startUtc = wibStartOfDayUtc(year, month - 1, day);
      endUtc = wibEndOfDayUtc(year, month - 1, day);
      break;
    }
  }

  return {
    fromUtc: startUtc.toISOString(),
    toUtc: endUtc.toISOString(),
  };
}
