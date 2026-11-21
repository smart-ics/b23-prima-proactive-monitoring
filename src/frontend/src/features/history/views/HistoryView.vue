<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { formatWibDateTime, getWibDateRange, type DatePreset } from '@/shared/utils/dateUtils';
import {
  createDefaultHistoryFilterState,
  DEFAULT_HISTORY_LIMIT,
  type HistoryFilterState,
} from '../types/history.types';
import {
  useHistoricalIssuesQuery,
  useHistoryDetectorsQuery,
  useReliabilityKpiQuery,
} from '../api/useHistoryQueries';
import DateRangePresetBar from '../components/DateRangePresetBar.vue';
import ReliabilityKpiGrid from '../components/ReliabilityKpiGrid.vue';
import ScopeSeverityBreakdown from '../components/ScopeSeverityBreakdown.vue';
import HistoryIssueFilters from '../components/HistoryIssueFilters.vue';
import HistoryIssueTable from '../components/HistoryIssueTable.vue';

const VALID_PRESETS: readonly DatePreset[] = [
  'Today',
  'This Week',
  'This Month',
  'Last Month',
  'Custom',
];

const VALID_STATUSES: readonly string[] = [
  'All',
  'Resolved',
  'Dismissed',
  'Reopened',
  'Open',
  'Acknowledged',
  'Investigating',
];

function parsePresetParam(raw: unknown): DatePreset {
  if (typeof raw === 'string' && VALID_PRESETS.includes(raw as DatePreset)) {
    return raw as DatePreset;
  }
  return 'Today';
}

function parseStatusParam(raw: unknown): string {
  if (typeof raw === 'string') {
    const matched = VALID_STATUSES.find((s) => s.toLowerCase() === raw.trim().toLowerCase());
    if (matched) {
      return matched;
    }
  }
  return 'All';
}

const route = useRoute();
const router = useRouter();

const filters = ref<HistoryFilterState>(
  createDefaultHistoryFilterState({
    preset: parsePresetParam(route.query.preset),
    status: parseStatusParam(route.query.status),
  })
);

watch(
  () => [route.query.preset, route.query.status],
  ([nextPresetRaw, nextStatusRaw]) => {
    const nextPreset = parsePresetParam(nextPresetRaw);
    const nextStatus = parseStatusParam(nextStatusRaw);
    if (nextPreset !== filters.value.preset) {
      filters.value.preset = nextPreset;
    }
    if (nextStatus !== filters.value.status) {
      filters.value.status = nextStatus;
    }
  }
);

function syncRouteQuery(preset: DatePreset, status: string) {
  if (route.query.preset === preset && route.query.status === status) {
    return;
  }
  router.replace({
    query: {
      ...route.query,
      preset,
      status,
    },
  });
}

const utcDateRange = computed(() =>
  getWibDateRange(filters.value.preset, filters.value.customRange)
);

const fromUtc = computed(() => utcDateRange.value.fromUtc);
const toUtc = computed(() => utcDateRange.value.toUtc);

const kpiQuery = useReliabilityKpiQuery(fromUtc, toUtc);
const issuesQuery = useHistoricalIssuesQuery(filters, fromUtc, toUtc);
const detectorsQuery = useHistoryDetectorsQuery();

const kpiData = computed(() => kpiQuery.data.value ?? null);
const issueItems = computed(() => issuesQuery.data.value?.items ?? []);
const totalReturned = computed(() => issuesQuery.data.value?.totalReturned ?? 0);
const clampedLimit = computed(() => issuesQuery.data.value?.clampedLimit ?? DEFAULT_HISTORY_LIMIT);
const detectors = computed(() => detectorsQuery.data.value ?? []);

const isAnyFetching = computed(
  () => kpiQuery.isFetching.value || issuesQuery.isFetching.value
);

const hasError = computed(() => kpiQuery.isError.value || issuesQuery.isError.value);

const errorMessage = computed(() => {
  return (
    kpiQuery.error.value?.message ||
    issuesQuery.error.value?.message ||
    'Unable to load historical reliability data.'
  );
});

const dateRangeSubtitle = computed(() => {
  return `${formatWibDateTime(fromUtc.value, false)} — ${formatWibDateTime(toUtc.value, true)}`;
});

const isIssueFiltered = computed(() => {
  return (
    filters.value.status !== 'All' ||
    filters.value.scope !== 'All' ||
    Boolean(filters.value.detectorId) ||
    filters.value.severity !== 'All' ||
    Boolean(filters.value.search && filters.value.search.trim().length > 0)
  );
});

function handlePresetChange(nextPreset: DatePreset) {
  filters.value.preset = nextPreset;
  syncRouteQuery(nextPreset, filters.value.status);
}

function handleCustomRangeChange(nextRange: [Date | null, Date | null]) {
  filters.value.customRange = nextRange;
}

function handleStatusChange(nextStatus: string) {
  filters.value.status = nextStatus;
  syncRouteQuery(filters.value.preset, nextStatus);
}

function handleResetIssueFilters() {
  filters.value = createDefaultHistoryFilterState({
    preset: filters.value.preset,
    customRange: filters.value.customRange,
  });
  syncRouteQuery(filters.value.preset, filters.value.status);
}

async function handleManualRefresh() {
  await Promise.all([kpiQuery.refetch(), issuesQuery.refetch()]);
}
</script>

<template>
  <div class="space-y-4">
    <!-- S-07 Read-Only Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
      <div>
        <div class="flex items-center gap-2">
          <h1 class="text-lg font-semibold text-slate-900 leading-snug">
            Historical Reliability &amp; Reports
          </h1>
          <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-slate-100 text-slate-600 border border-slate-200">
            <i class="pi pi-lock text-[10px]" aria-hidden="true"></i>
            <span>Read-Only</span>
          </span>
        </div>
        <p class="text-xs text-slate-500 mt-0.5 font-mono tabular-nums">
          Period: {{ dateRangeSubtitle }}
        </p>
      </div>

      <div class="flex items-center gap-2 self-start sm:self-auto">
        <button
          type="button"
          @click="handleManualRefresh"
          :disabled="isAnyFetching"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-white border border-slate-200 text-xs font-medium text-slate-700 hover:text-slate-900 hover:bg-slate-50 disabled:opacity-50 transition-colors cursor-pointer shadow-2xs"
          title="Refresh historical KPIs and issue ledger"
        >
          <i class="pi pi-refresh text-xs" :class="{ 'animate-spin': isAnyFetching }" aria-hidden="true"></i>
          <span>Refresh</span>
        </button>
      </div>
    </div>

    <!-- Date Range Preset Controls -->
    <DateRangePresetBar
      :preset="filters.preset"
      :custom-range="filters.customRange"
      @update:preset="handlePresetChange"
      @update:custom-range="handleCustomRangeChange"
    />

    <!-- Error Banner -->
    <div
      v-if="hasError"
      role="alert"
      class="bg-rose-50 border border-rose-200 text-rose-800 rounded-lg px-4 py-3 flex items-center justify-between gap-3 text-xs"
    >
      <div class="flex items-center gap-2">
        <i class="pi pi-exclamation-circle text-rose-600 text-sm" aria-hidden="true"></i>
        <span class="font-medium">{{ errorMessage }}</span>
      </div>
      <button
        type="button"
        @click="handleManualRefresh"
        class="px-2.5 py-1 rounded bg-white border border-rose-200 text-rose-700 hover:bg-rose-100 font-medium cursor-pointer shrink-0"
      >
        Retry
      </button>
    </div>

    <!-- 6-Tile Reliability KPI Grid -->
    <ReliabilityKpiGrid
      :kpi="kpiData"
      :is-loading="kpiQuery.isPending.value"
    />

    <!-- 2-Column Scope & Severity Breakdown (Pure CSS Proportion Bars) -->
    <ScopeSeverityBreakdown
      :scope-breakdown="kpiData?.scopeBreakdown"
      :severity-breakdown="kpiData?.severityBreakdown"
      :is-loading="kpiQuery.isPending.value"
    />

    <!-- Multi-Attribute Historical Issue Filters -->
    <HistoryIssueFilters
      :status="filters.status"
      :scope="filters.scope"
      :detector-id="filters.detectorId"
      :severity="filters.severity"
      :search="filters.search"
      :detectors="detectors"
      :detectors-loading="detectorsQuery.isPending.value"
      @update:status="handleStatusChange"
      @update:scope="(val) => (filters.scope = val)"
      @update:detector-id="(val) => (filters.detectorId = val)"
      @update:severity="(val) => (filters.severity = val)"
      @update:search="(val) => (filters.search = val)"
      @reset="handleResetIssueFilters"
    />

    <!-- Virtualized Historical Issue Audit Table (Zero Pagination) -->
    <HistoryIssueTable
      :items="issueItems"
      :total-returned="totalReturned"
      :clamped-limit="clampedLimit"
      :is-loading="issuesQuery.isPending.value"
      :is-fetching="issuesQuery.isFetching.value"
      :is-filtered="isIssueFiltered"
      @reset-filters="handleResetIssueFilters"
    />
  </div>
</template>
