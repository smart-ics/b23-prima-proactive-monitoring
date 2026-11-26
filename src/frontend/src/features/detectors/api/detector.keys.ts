import { unref, type MaybeRef } from 'vue';

export interface DetectorListQueryParams {
  scope?: string;
  adminStatus?: string;
  healthStatus?: string;
  search?: string;
  [key: string]: unknown;
}

export interface InspectionRunsQueryParams {
  startDateUtc?: string;
  endDateUtc?: string;
  outcome?: string;
  triggerSource?: string;
  limit?: number;
  offset?: number;
  [key: string]: unknown;
}

/**
 * TanStack Query key factory for the Detectors feature slice.
 * Provides hierarchical, type-safe query keys for caching and selective cache invalidation.
 */
export const detectorKeys = {
  all: ['detectors'] as const,
  lists: () => [...detectorKeys.all, 'list'] as const,
  list: (params?: MaybeRef<DetectorListQueryParams | undefined>) =>
    params !== undefined
      ? ([...detectorKeys.lists(), unref(params)] as const)
      : detectorKeys.lists(),
  summaries: () => [...detectorKeys.all, 'summary'] as const,
  summary: () => [...detectorKeys.summaries()] as const,
  details: () => [...detectorKeys.all, 'detail'] as const,
  detail: (id: MaybeRef<string>) => [...detectorKeys.details(), unref(id)] as const,
  runsAll: () => [...detectorKeys.all, 'runs'] as const,
  runs: (id: MaybeRef<string>, params?: MaybeRef<InspectionRunsQueryParams | undefined>) =>
    params !== undefined
      ? ([...detectorKeys.runsAll(), unref(id), unref(params)] as const)
      : ([...detectorKeys.runsAll(), unref(id)] as const),
};

/**
 * TanStack Query key factory for detector enrollment tokens (Stage 5 Handshake).
 */
export const enrollmentTokenKeys = {
  all: ['enrollment-tokens'] as const,
  pending: () => [...enrollmentTokenKeys.all, 'pending'] as const,
};


