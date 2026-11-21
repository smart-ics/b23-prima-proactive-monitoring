import {
  computed,
  getCurrentScope,
  onScopeDispose,
  ref,
  unref,
  watch,
  type Ref,
} from 'vue';
import { keepPreviousData, useQuery } from '@tanstack/vue-query';
import http from '@/shared/api/http';
import type {
  DetectorListItemDto,
  HistoricalIssuesResponse,
  ReliabilityKpiResponse,
} from '@/shared/types/api.generated';
import type { HistoryFilterState } from '../types/history.types';
import { historyKeys } from './history.keys';

const SEARCH_DEBOUNCE_MS = 300;
const HISTORY_STALE_TIME_MS = 30_000;

export interface HistoricalIssuesQueryParams {
  fromUtc: string;
  toUtc: string;
  status?: string;
  scope?: string;
  detectorId?: string;
  severity?: string;
  search?: string;
  limit?: number;
}

export function isValidUtcDateRange(
  fromUtc: string | null | undefined,
  toUtc: string | null | undefined
): boolean {
  const from = fromUtc?.trim();
  const to = toUtc?.trim();
  if (!from || !to) {
    return false;
  }

  const fromMs = Date.parse(from);
  const toMs = Date.parse(to);
  return !Number.isNaN(fromMs) && !Number.isNaN(toMs) && fromMs <= toMs;
}

function normalizeOptionalFilter(value: string | null | undefined): string | undefined {
  const trimmed = value?.trim();
  if (!trimmed || trimmed.toLowerCase() === 'all') {
    return undefined;
  }
  return trimmed;
}

export function buildHistoricalIssuesParams(
  filters: HistoryFilterState,
  fromUtc: string,
  toUtc: string,
  debouncedSearch: string
): HistoricalIssuesQueryParams {
  const params: HistoricalIssuesQueryParams = {
    fromUtc: (fromUtc ?? '').trim(),
    toUtc: (toUtc ?? '').trim(),
  };

  const status = normalizeOptionalFilter(filters.status);
  if (status) {
    params.status = status;
  }

  const scope = normalizeOptionalFilter(filters.scope);
  if (scope) {
    params.scope = scope;
  }

  const detectorId = filters.detectorId?.trim();
  if (detectorId) {
    params.detectorId = detectorId;
  }

  const severity = normalizeOptionalFilter(filters.severity);
  if (severity) {
    params.severity = severity;
  }

  const search = debouncedSearch.trim();
  if (search) {
    params.search = search;
  }

  if (typeof filters.limit === 'number' && Number.isFinite(filters.limit) && filters.limit > 0) {
    params.limit = Math.trunc(filters.limit);
  }

  return params;
}

/**
 * Query composable for S-07 Historical Reliability KPI metrics & Scope/Severity breakdowns.
 * - Fetches GET /api/history/reliability-kpi
 * - Conditional enabled flag guards against empty or inverted date ranges
 * - 30-second staleTime with keepPreviousData to avoid UI flashes when switching presets
 */
export function useReliabilityKpiQuery(fromUtc: Ref<string>, toUtc: Ref<string>) {
  const normalizedFromUtc = computed(() => (unref(fromUtc) ?? '').trim());
  const normalizedToUtc = computed(() => (unref(toUtc) ?? '').trim());
  const isRangeEnabled = computed(() =>
    isValidUtcDateRange(normalizedFromUtc.value, normalizedToUtc.value)
  );

  return useQuery<ReliabilityKpiResponse, Error>({
    queryKey: computed(() => historyKeys.kpi(normalizedFromUtc.value, normalizedToUtc.value)),
    queryFn: async () => {
      const response = await http.get<ReliabilityKpiResponse>('/history/reliability-kpi', {
        params: {
          fromUtc: normalizedFromUtc.value,
          toUtc: normalizedToUtc.value,
        },
      });
      return response.data;
    },
    enabled: isRangeEnabled,
    staleTime: HISTORY_STALE_TIME_MS,
    placeholderData: keepPreviousData,
  });
}

/**
 * Query composable for S-07 Historical Issue Ledger.
 * - Fetches GET /api/history/issues with debounced search (300ms) and active filter parameters
 * - Conditional enabled flag guards against empty or inverted date ranges
 * - 30-second staleTime with keepPreviousData for smooth table updates
 */
export function useHistoricalIssuesQuery(
  filters: Ref<HistoryFilterState>,
  fromUtc: Ref<string>,
  toUtc: Ref<string>
) {
  const normalizedFromUtc = computed(() => (unref(fromUtc) ?? '').trim());
  const normalizedToUtc = computed(() => (unref(toUtc) ?? '').trim());
  const isRangeEnabled = computed(() =>
    isValidUtcDateRange(normalizedFromUtc.value, normalizedToUtc.value)
  );

  const debouncedSearch = ref((unref(filters).search ?? '').trim());
  let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;

  watch(
    () => unref(filters).search,
    (nextSearch) => {
      const trimmed = (nextSearch ?? '').trim();
      if (searchDebounceTimer !== null) {
        clearTimeout(searchDebounceTimer);
        searchDebounceTimer = null;
      }

      if (trimmed === debouncedSearch.value) {
        return;
      }

      searchDebounceTimer = setTimeout(() => {
        debouncedSearch.value = trimmed;
        searchDebounceTimer = null;
      }, SEARCH_DEBOUNCE_MS);
    }
  );

  if (getCurrentScope()) {
    onScopeDispose(() => {
      if (searchDebounceTimer !== null) {
        clearTimeout(searchDebounceTimer);
        searchDebounceTimer = null;
      }
    });
  }

  const activeIssueParams = computed<HistoricalIssuesQueryParams>(() =>
    buildHistoricalIssuesParams(
      unref(filters),
      normalizedFromUtc.value,
      normalizedToUtc.value,
      debouncedSearch.value
    )
  );

  return useQuery<HistoricalIssuesResponse, Error>({
    queryKey: computed(() => historyKeys.issues(activeIssueParams.value)),
    queryFn: async () => {
      const response = await http.get<HistoricalIssuesResponse>('/history/issues', {
        params: activeIssueParams.value,
      });
      return response.data;
    },
    enabled: isRangeEnabled,
    staleTime: HISTORY_STALE_TIME_MS,
    placeholderData: keepPreviousData,
  });
}

/**
 * Query composable for populating the Detector filter dropdown on S-07
 * without introducing cross-slice imports from features/detectors.
 */
export function useHistoryDetectorsQuery() {
  return useQuery<DetectorListItemDto[], Error>({
    queryKey: historyKeys.detectors(),
    queryFn: async () => {
      const response = await http.get<DetectorListItemDto[]>('/detectors');
      return response.data;
    },
    staleTime: 60_000,
  });
}
