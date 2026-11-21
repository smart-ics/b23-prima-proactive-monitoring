import { toValue, type MaybeRefOrGetter } from 'vue';
import type { HistoryFilterState } from '../types/history.types';

/**
 * Filter payload accepted by `historyKeys.issues(...)`.
 * Supports both raw `HistoryFilterState` and normalized query-parameter objects.
 */
export interface HistoryIssuesKeyFilters extends Partial<HistoryFilterState> {
  fromUtc?: string;
  toUtc?: string;
}

function serializeDateBoundary(value: Date | null | undefined): string | null {
  if (!value || Number.isNaN(value.getTime())) {
    return null;
  }
  return value.toISOString();
}

function serializeHistoryIssueFilters(raw: HistoryIssuesKeyFilters): Record<string, unknown> {
  const serialized: Record<string, unknown> = {};

  if (raw.fromUtc !== undefined) serialized.fromUtc = raw.fromUtc;
  if (raw.toUtc !== undefined) serialized.toUtc = raw.toUtc;
  if (raw.preset !== undefined) serialized.preset = raw.preset;
  if (raw.customRange !== undefined) {
    serialized.customRange = [
      serializeDateBoundary(raw.customRange[0]),
      serializeDateBoundary(raw.customRange[1]),
    ];
  }
  if (raw.status !== undefined) serialized.status = raw.status;
  if (raw.scope !== undefined) serialized.scope = raw.scope;
  if (raw.detectorId !== undefined) serialized.detectorId = raw.detectorId;
  if (raw.severity !== undefined) serialized.severity = raw.severity;
  if (raw.search !== undefined) serialized.search = raw.search;
  if (raw.limit !== undefined) serialized.limit = raw.limit;

  return serialized;
}

/**
 * TanStack Query key factory for the History feature slice.
 * Provides hierarchical, type-safe, JSON-deterministic query keys for caching and invalidation.
 */
export const historyKeys = {
  all: ['history'] as const,
  detectors: () => [...historyKeys.all, 'detectors'] as const,
  kpis: () => [...historyKeys.all, 'kpi'] as const,
  kpi: (fromUtc: MaybeRefOrGetter<string>, toUtc: MaybeRefOrGetter<string>) =>
    [...historyKeys.kpis(), toValue(fromUtc), toValue(toUtc)] as const,
  issuesList: () => [...historyKeys.all, 'issues'] as const,
  issues: (filters?: MaybeRefOrGetter<HistoryIssuesKeyFilters | undefined>) => {
    const resolved = toValue(filters);
    return resolved !== undefined
      ? ([...historyKeys.issuesList(), serializeHistoryIssueFilters(resolved)] as const)
      : historyKeys.issuesList();
  },
};
