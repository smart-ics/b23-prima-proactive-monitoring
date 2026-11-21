import type { DatePreset, UtcDateRange } from '@/shared/utils/dateUtils';
import type {
  ReliabilityKpiResponse,
  BreakdownItemDto,
  HistoricalIssuesResponse,
  HistoricalIssueItemDto,
  IssueStatus,
  IssueSeverity,
  MonitoringScope,
} from '@/shared/types/api.generated';

/**
 * View filter and date preset state for the S-07 History & Reports screen.
 */
export interface HistoryFilterState {
  preset: DatePreset;
  customRange: [Date | null, Date | null];
  status: string;
  scope: string;
  detectorId: string | null;
  severity: string;
  search: string;
  limit: number;
}

export const DEFAULT_HISTORY_LIMIT = 500;
export const MAX_HISTORY_LIMIT = 1000;

export function createDefaultHistoryFilterState(
  overrides?: Partial<HistoryFilterState>
): HistoryFilterState {
  return {
    preset: 'Today',
    customRange: [null, null],
    status: 'All',
    scope: 'All',
    detectorId: null,
    severity: 'All',
    search: '',
    limit: DEFAULT_HISTORY_LIMIT,
    ...overrides,
  };
}

export type {
  DatePreset,
  UtcDateRange,
  ReliabilityKpiResponse,
  BreakdownItemDto,
  HistoricalIssuesResponse,
  HistoricalIssueItemDto,
  IssueStatus,
  IssueSeverity,
  MonitoringScope,
};
