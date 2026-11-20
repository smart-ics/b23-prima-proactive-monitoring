<script setup lang="ts">
import { ref, computed } from 'vue';
import { useDashboardFeed, type DashboardFilterState } from '../composables/useDashboardFeed';
import SummaryCountersBar from '../components/SummaryCountersBar.vue';
import DashboardFilterBar from '../components/DashboardFilterBar.vue';
import ActiveIssueFeed from '../components/ActiveIssueFeed.vue';

const filters = ref<DashboardFilterState>({
  status: 'All',
  severity: 'All',
  scope: 'All',
  search: '',
});

const { data, isPending, isFetching, refetch } = useDashboardFeed(filters);

const summary = computed(() => data.value?.summary);
const items = computed(() => data.value?.items ?? []);

const isFiltered = computed(() => {
  return (
    filters.value.status !== 'All' ||
    filters.value.severity !== 'All' ||
    filters.value.scope !== 'All' ||
    Boolean(filters.value.search && filters.value.search.trim().length > 0)
  );
});

function handleResetFilters() {
  filters.value = {
    status: 'All',
    severity: 'All',
    scope: 'All',
    search: '',
  };
}

function handleStatusFilter(status: string) {
  filters.value.status = status;
}

function handleSeverityFilter(severity: string) {
  filters.value.severity = severity;
}

function handleScopeFilter(scope: string) {
  filters.value.scope = scope;
}

function handleSearchFilter(search: string) {
  filters.value.search = search;
}
</script>

<template>
  <div class="space-y-4">
    <!-- Workbench Header Bar -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
      <div>
        <h1 class="text-lg font-semibold text-slate-900 leading-snug">Issue Dashboard</h1>
        <p class="text-xs text-slate-500">Live operational exception monitoring feed (S-01)</p>
      </div>

      <!-- Live Sync / Polling Status Strip -->
      <div class="flex items-center gap-2 self-start sm:self-auto">
        <div class="inline-flex items-center gap-2 px-2.5 py-1 rounded-md text-xs font-mono bg-white border border-slate-200 text-slate-700 shadow-2xs">
          <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
          <span>Live · 10s</span>
          <i
            class="pi pi-sync text-[10px] text-slate-400"
            :class="{ 'animate-spin text-slate-600': isFetching }"
            title="Active 10-second background polling"
          ></i>
        </div>

        <button
          type="button"
          @click="() => refetch()"
          :disabled="isFetching"
          class="p-1.5 rounded-md bg-white border border-slate-200 text-slate-600 hover:text-slate-900 hover:bg-slate-50 disabled:opacity-50 transition-colors cursor-pointer"
          title="Manually refresh feed now"
        >
          <i class="pi pi-refresh text-xs" :class="{ 'animate-spin': isFetching }"></i>
        </button>
      </div>
    </div>

    <!-- Conditional Recurrence Notification Strip -->
    <div
      v-if="summary && summary.reopenedCount > 0"
      class="bg-purple-50 border border-purple-200 text-purple-900 rounded-lg px-4 py-2.5 flex items-center justify-between text-xs font-medium shadow-2xs"
    >
      <div class="flex items-center gap-2">
        <i class="pi pi-info-circle text-purple-600 text-sm"></i>
        <span>
          <strong>{{ summary.reopenedCount }}</strong>
          {{ summary.reopenedCount === 1 ? 'issue has' : 'issues have' }} reopened after prior resolution (BR-PRIMA-002 auto-reopening).
        </span>
      </div>
      <button
        type="button"
        @click="handleStatusFilter('Reopened')"
        class="text-xs font-semibold text-purple-700 hover:text-purple-900 underline underline-offset-2 cursor-pointer ml-3 shrink-0"
      >
        View Reopened →
      </button>
    </div>

    <!-- 1. KPI Summary Counters Bar -->
    <SummaryCountersBar
      :summary="summary"
      :active-status="filters.status"
      :active-severity="filters.severity"
      @update:status="handleStatusFilter"
      @update:severity="handleSeverityFilter"
    />

    <!-- 2. Multi-Dimension Filter Bar -->
    <DashboardFilterBar
      :status="filters.status"
      :severity="filters.severity"
      :scope="filters.scope"
      :search="filters.search"
      @update:status="handleStatusFilter"
      @update:severity="handleSeverityFilter"
      @update:scope="handleScopeFilter"
      @update:search="handleSearchFilter"
      @reset="handleResetFilters"
    />

    <!-- 3. High-Density Active Issue Feed -->
    <ActiveIssueFeed
      :items="items"
      :is-pending="isPending"
      :is-fetching="isFetching"
      :is-filtered="isFiltered"
      @reset-filters="handleResetFilters"
    />
  </div>
</template>
