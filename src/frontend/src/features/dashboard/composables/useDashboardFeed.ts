import { useQuery, keepPreviousData } from '@tanstack/vue-query';
import { type Ref, unref, watch } from 'vue';
import http from '@/shared/api/http';
import type { GetDashboardFeedResponse } from '@/shared/types/api.generated';
import { activeIssuesCount } from '@/shared/composables/useAppMetrics';

export interface DashboardFilterState {
  status: string;
  severity: string;
  scope: string;
  search: string;
}

/**
 * TanStack Query composable for live S-01 Issue Dashboard Feed.
 * - Polls GET /api/issues/dashboard-feed every 10 seconds.
 * - Uses keepPreviousData to prevent UI flashing and scroll jumps during background polling.
 * - Reactive to filter state changes.
 * - Syncs global open + reopened counters to App Shell metrics.
 */
export function useDashboardFeed(filters: Ref<DashboardFilterState>) {
  const query = useQuery<GetDashboardFeedResponse, Error>({
    queryKey: ['dashboard-feed', filters],
    queryFn: async () => {
      const current = unref(filters);
      const params: Record<string, string> = {};

      if (current.status && current.status !== 'All') {
        params.status = current.status;
      }
      if (current.severity && current.severity !== 'All') {
        params.severity = current.severity;
      }
      if (current.scope && current.scope !== 'All') {
        params.scope = current.scope;
      }
      if (current.search && current.search.trim().length > 0) {
        params.search = current.search.trim();
      }

      const response = await http.get<GetDashboardFeedResponse>('/issues/dashboard-feed', {
        params,
      });
      return response.data;
    },
    refetchInterval: 10000,
    placeholderData: keepPreviousData,
  });

  watch(
    () => query.data.value?.summary,
    (summary) => {
      if (summary) {
        activeIssuesCount.value = (summary.openCount || 0) + (summary.reopenedCount || 0);
      }
    },
    { immediate: true }
  );

  return query;
}
