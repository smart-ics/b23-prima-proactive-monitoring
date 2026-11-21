import { computed, unref, type MaybeRef } from 'vue';
import {
  useQuery,
  useMutation,
  useQueryClient,
  keepPreviousData,
} from '@tanstack/vue-query';
import http from '@/shared/api/http';
import type {
  DetectorListItemDto,
  DetectorSummaryDto,
  DetectorDetailDto,
  InspectionRunLogResponse,
  InspectionRunResultDto,
  RegisterDetectorRequest,
  UpdateDetectorRequest,
  ToggleDetectorStatusRequest,
} from '@/shared/types/api.generated';
import {
  detectorKeys,
  type DetectorListQueryParams,
  type InspectionRunsQueryParams,
} from './detector.keys';

export interface UpdateDetectorMutationVariables {
  id?: string;
  data: UpdateDetectorRequest;
}

export interface ToggleDetectorStatusMutationVariables {
  id?: string;
  action: 'Pause' | 'Resume' | 'Retire' | string;
}

/**
 * Query composable for S-03 Detector Portfolio List.
 * - 15-second background polling
 * - 7.5-second stale time
 * - keepPreviousData to prevent UI flashes during polling
 */
export function useDetectorListQuery(params?: MaybeRef<DetectorListQueryParams | undefined>) {
  return useQuery<DetectorListItemDto[], Error>({
    queryKey: computed(() => detectorKeys.list(params)),
    queryFn: async () => {
      const p = unref(params);
      const queryParams: Record<string, unknown> = {};

      if (p) {
        for (const [key, val] of Object.entries(p)) {
          if (val !== undefined && val !== null && val !== '' && val !== 'All') {
            const paramKey = key.charAt(0).toUpperCase() + key.slice(1);
            queryParams[paramKey] = typeof val === 'string' ? val.trim() : val;
          }
        }
      }

      const response = await http.get<DetectorListItemDto[]>('/detectors', {
        params: queryParams,
      });
      return response.data;
    },
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
    staleTime: 7_500,
    placeholderData: keepPreviousData,
  });
}

/**
 * Query composable for Detector Portfolio Summary Counts (S-03 & App Shell sidebar).
 * - 15-second background polling
 * - 7.5-second stale time
 */
export function useDetectorSummaryQuery() {
  return useQuery<DetectorSummaryDto, Error>({
    queryKey: detectorKeys.summary(),
    queryFn: async () => {
      const response = await http.get<DetectorSummaryDto>('/detectors/summary');
      return response.data;
    },
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
    staleTime: 7_500,
    placeholderData: keepPreviousData,
  });
}

/**
 * Composite query composable for S-04 Detector Detail View.
 * - 15-second background polling
 * - 7.5-second stale time
 * - keepPreviousData preserves expanded sub-elements during polling
 */
export function useDetectorDetailQuery(id: MaybeRef<string>) {
  return useQuery<DetectorDetailDto, Error>({
    queryKey: computed(() => detectorKeys.detail(id)),
    queryFn: async () => {
      const detectorId = unref(id);
      const response = await http.get<DetectorDetailDto>(`/detectors/${detectorId}`);
      return response.data;
    },
    enabled: computed(() => !!unref(id)),
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
    staleTime: 7_500,
    placeholderData: keepPreviousData,
  });
}

/**
 * Query composable for S-06 Historical Inspection Run Log.
 * - 10-second stale time
 * - keepPreviousData preserves pagination and open trace blocks
 */
export function useInspectionRunsQuery(
  id: MaybeRef<string>,
  params?: MaybeRef<InspectionRunsQueryParams | undefined>
) {
  return useQuery<InspectionRunLogResponse, Error>({
    queryKey: computed(() => detectorKeys.runs(id, params)),
    queryFn: async () => {
      const detectorId = unref(id);
      const p = unref(params);
      const queryParams: Record<string, unknown> = {};

      if (p) {
        for (const [key, val] of Object.entries(p)) {
          if (val !== undefined && val !== null && val !== '' && val !== 'All') {
            const paramKey = key.charAt(0).toUpperCase() + key.slice(1);
            queryParams[paramKey] = typeof val === 'string' ? val.trim() : val;
          }
        }
      }

      const response = await http.get<InspectionRunLogResponse>(`/detectors/${detectorId}/runs`, {
        params: queryParams,
      });
      return response.data;
    },
    enabled: computed(() => !!unref(id)),
    staleTime: 10_000,
    placeholderData: keepPreviousData,
  });
}

/**
 * Mutation composable for registering a new detector (S-05).
 * Selectively invalidates detector portfolio list and summary caches.
 */
export function useRegisterDetectorMutation() {
  const queryClient = useQueryClient();

  return useMutation<DetectorDetailDto, Error, RegisterDetectorRequest>({
    mutationFn: async (payload: RegisterDetectorRequest) => {
      const response = await http.post<DetectorDetailDto>('/detectors', payload);
      return response.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: detectorKeys.lists() });
      queryClient.invalidateQueries({ queryKey: detectorKeys.summaries() });
    },
  });
}

/**
 * Mutation composable for updating an existing detector configuration (S-05).
 * Selectively invalidates detail, list, and summary caches.
 */
export function useUpdateDetectorMutation(id?: MaybeRef<string>) {
  const queryClient = useQueryClient();

  return useMutation<void, Error, UpdateDetectorRequest | UpdateDetectorMutationVariables>({
    mutationFn: async (variables) => {
      let targetId = unref(id);
      let payload: UpdateDetectorRequest;

      if ('data' in variables) {
        targetId = variables.id ?? targetId;
        payload = variables.data;
      } else {
        payload = variables;
      }

      if (!targetId) {
        throw new Error('Detector ID is required for update');
      }

      await http.put(`/detectors/${targetId}`, payload);
    },
    onSuccess: (_data, variables) => {
      let targetId = unref(id);
      if (typeof variables === 'object' && 'data' in variables && variables.id) {
        targetId = variables.id;
      }

      if (targetId) {
        queryClient.invalidateQueries({ queryKey: detectorKeys.detail(targetId) });
      } else {
        queryClient.invalidateQueries({ queryKey: detectorKeys.details() });
      }
      queryClient.invalidateQueries({ queryKey: detectorKeys.lists() });
      queryClient.invalidateQueries({ queryKey: detectorKeys.summaries() });
    },
  });
}

/**
 * Mutation composable for toggling detector administrative status (Pause, Resume, Retire).
 * Selectively invalidates detail, list, and summary caches.
 */
export function useToggleDetectorStatusMutation(id?: MaybeRef<string>) {
  const queryClient = useQueryClient();

  return useMutation<void, Error, ToggleDetectorStatusRequest | ToggleDetectorStatusMutationVariables | string>({
    mutationFn: async (variables) => {
      let targetId = unref(id);
      let payload: ToggleDetectorStatusRequest;

      if (typeof variables === 'string') {
        payload = { action: variables };
      } else if ('id' in variables && variables.id) {
        targetId = variables.id;
        payload = { action: variables.action };
      } else {
        payload = { action: variables.action };
      }

      if (!targetId) {
        throw new Error('Detector ID is required to toggle status');
      }

      await http.post(`/detectors/${targetId}/toggle-status`, payload);
    },
    onSuccess: (_data, variables) => {
      let targetId = unref(id);
      if (typeof variables === 'object' && 'id' in variables && variables.id) {
        targetId = variables.id;
      }

      if (targetId) {
        queryClient.invalidateQueries({ queryKey: detectorKeys.detail(targetId) });
      } else {
        queryClient.invalidateQueries({ queryKey: detectorKeys.details() });
      }
      queryClient.invalidateQueries({ queryKey: detectorKeys.lists() });
      queryClient.invalidateQueries({ queryKey: detectorKeys.summaries() });
    },
  });
}

/**
 * Mutation composable for triggering an immediate manual inspection run (S-04 / E-7).
 * Selectively invalidates detail, runs log, and dashboard feed caches.
 */
export function useTriggerManualRunMutation(id?: MaybeRef<string>) {
  const queryClient = useQueryClient();

  return useMutation<InspectionRunResultDto, Error, string | void>({
    mutationFn: async (overrideId) => {
      const targetId =
        typeof overrideId === 'string' && overrideId.length > 0 ? overrideId : unref(id);

      if (!targetId) {
        throw new Error('Detector ID is required to trigger manual run');
      }

      const response = await http.post<InspectionRunResultDto>(
        `/detectors/${targetId}/trigger-run`
      );
      return response.data;
    },
    onSuccess: (_data, overrideId) => {
      const targetId =
        typeof overrideId === 'string' && overrideId.length > 0 ? overrideId : unref(id);

      if (targetId) {
        queryClient.invalidateQueries({ queryKey: detectorKeys.detail(targetId) });
        queryClient.invalidateQueries({ queryKey: detectorKeys.runs(targetId) });
      } else {
        queryClient.invalidateQueries({ queryKey: detectorKeys.details() });
        queryClient.invalidateQueries({ queryKey: detectorKeys.runsAll() });
      }

      queryClient.invalidateQueries({ queryKey: ['dashboard', 'feed'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-feed'] });
    },
  });
}
