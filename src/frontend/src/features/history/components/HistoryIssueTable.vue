<script setup lang="ts">
import { useRouter } from 'vue-router';
import DataTable, { type DataTableRowClickEvent } from 'primevue/datatable';
import Column from 'primevue/column';
import type { HistoricalIssueItemDto } from '../types/history.types';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

withDefaults(
  defineProps<{
    items: HistoricalIssueItemDto[];
    totalReturned?: number;
    clampedLimit?: number;
    isLoading?: boolean;
    isFetching?: boolean;
    isFiltered?: boolean;
  }>(),
  {
    totalReturned: 0,
    clampedLimit: 500,
    isLoading: false,
    isFetching: false,
    isFiltered: false,
  }
);

const emit = defineEmits<{
  (e: 'reset-filters'): void;
}>();

const router = useRouter();

function navigateToIssue(id: string) {
  router.push({ path: `/issues/${id}`, query: { from: 'history' } });
}

function onRowClick(event: DataTableRowClickEvent) {
  const item = event.data as HistoricalIssueItemDto | undefined;
  if (item?.id) {
    navigateToIssue(item.id);
  }
}

function getSeverityBadgeClass(severity: string): string {
  switch (severity) {
    case 'Critical':
      return 'bg-rose-50 text-rose-700 border-rose-200 font-semibold';
    case 'High':
      return 'bg-orange-50 text-orange-700 border-orange-200 font-semibold';
    case 'Medium':
      return 'bg-amber-50 text-amber-800 border-amber-200 font-medium';
    case 'Low':
      return 'bg-sky-50 text-sky-700 border-sky-200 font-medium';
    default:
      return 'bg-slate-100 text-slate-700 border-slate-200';
  }
}

function getScopeLabel(scope: string): string {
  switch (scope) {
    case 'DataIntegrity':
      return 'Data Integrity';
    case 'ProcessIntegrity':
      return 'Process Integrity';
    case 'DataSync':
      return 'Data Sync';
    case 'Infrastructure':
      return 'Infrastructure';
    default:
      return scope;
  }
}

function getScopeBadgeClass(scope: string): string {
  switch (scope) {
    case 'DataIntegrity':
      return 'bg-indigo-50 text-indigo-700 border-indigo-200';
    case 'ProcessIntegrity':
      return 'bg-purple-50 text-purple-700 border-purple-200';
    case 'DataSync':
      return 'bg-cyan-50 text-cyan-700 border-cyan-200';
    case 'Infrastructure':
      return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    default:
      return 'bg-slate-50 text-slate-600 border-slate-200';
  }
}

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'Resolved':
      return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    case 'Dismissed':
      return 'bg-slate-100 text-slate-600 border-slate-300';
    case 'Reopened':
      return 'bg-purple-50 text-purple-700 border-purple-300 font-semibold';
    case 'Open':
      return 'bg-rose-50 text-rose-700 border-rose-200';
    case 'Acknowledged':
      return 'bg-sky-50 text-sky-700 border-sky-200';
    case 'Investigating':
      return 'bg-indigo-50 text-indigo-700 border-indigo-200';
    default:
      return 'bg-slate-100 text-slate-700 border-slate-200';
  }
}

function getStatusIcon(status: string): string {
  switch (status) {
    case 'Resolved':
      return 'pi pi-check-circle';
    case 'Dismissed':
      return 'pi pi-ban';
    case 'Reopened':
      return 'pi pi-replay';
    case 'Open':
      return 'pi pi-exclamation-circle';
    case 'Acknowledged':
      return 'pi pi-eye';
    case 'Investigating':
      return 'pi pi-search';
    default:
      return 'pi pi-circle';
  }
}

interface LifecycleDateInfo {
  label: string;
  isoDate: string;
}

function getLifecycleDate(item: HistoricalIssueItemDto): LifecycleDateInfo {
  if (item.status === 'Resolved' && item.resolvedAtUtc) {
    return { label: 'Resolved', isoDate: item.resolvedAtUtc };
  }
  if (item.status === 'Dismissed' && item.dismissedAtUtc) {
    return { label: 'Dismissed', isoDate: item.dismissedAtUtc };
  }
  if (item.status === 'Reopened' && item.reopenedAtUtc) {
    return { label: 'Reopened', isoDate: item.reopenedAtUtc };
  }
  return { label: 'Last seen', isoDate: item.lastOccurrenceAtUtc };
}

/**
 * Computes MTTR duration matching DEC-002:
 * COALESCE(ReopenedAtUtc, CreatedAtUtc) -> ResolvedAtUtc.
 */
function formatMttrDuration(item: HistoricalIssueItemDto): string {
  if (!item.resolvedAtUtc) {
    return '—';
  }
  const startIso = item.reopenedAtUtc ?? item.createdAtUtc;
  const startMs = Date.parse(startIso);
  const endMs = Date.parse(item.resolvedAtUtc);
  if (Number.isNaN(startMs) || Number.isNaN(endMs) || endMs < startMs) {
    return '—';
  }

  const diffMinutes = Math.round((endMs - startMs) / 60_000);
  if (diffMinutes < 1) {
    return '< 1m';
  }
  if (diffMinutes < 60) {
    return `${diffMinutes}m`;
  }
  const diffHours = (endMs - startMs) / 3_600_000;
  return `${diffHours.toFixed(1)} h`;
}
</script>

<template>
  <section
    class="bg-white border border-slate-200 rounded-lg shadow-2xs overflow-hidden"
    aria-label="Historical Issue Audit Ledger"
  >
    <!-- Ledger Summary Strip -->
    <div class="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 border-b border-slate-200 bg-slate-50/70">
      <div class="flex items-center gap-2 text-xs text-slate-600">
        <i class="pi pi-history text-slate-400 text-xs" aria-hidden="true"></i>
        <span class="font-semibold text-slate-800">Historical Issue Ledger</span>
        <span class="font-mono text-[11px] px-2 py-0.5 rounded-full bg-white border border-slate-200 text-slate-700 tabular-nums">
          {{ totalReturned.toLocaleString('en-US') }} {{ totalReturned === 1 ? 'issue' : 'issues' }}
        </span>
        <span
          v-if="totalReturned >= clampedLimit && totalReturned > 0"
          class="text-[11px] text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded font-medium"
        >
          Showing first {{ clampedLimit.toLocaleString('en-US') }} rows — refine filters to narrow
        </span>
      </div>

      <div class="flex items-center gap-2 text-[11px] text-slate-500">
        <span v-if="isFetching" class="inline-flex items-center gap-1 text-slate-600 font-medium">
          <i class="pi pi-spin pi-spinner text-[10px]" aria-hidden="true"></i>
          Updating...
        </span>
        <span class="hidden sm:inline">Click any row to inspect read-only detail (S-02)</span>
      </div>
    </div>

    <!-- Virtualized DataTable (Zero Pagination per PRIMA-UI-UX-GUIDELINE §10) -->
    <DataTable
      :value="items"
      :loading="isLoading"
      scrollable
      scrollHeight="580px"
      :virtualScrollerOptions="{ itemSize: 64 }"
      rowHover
      dataKey="id"
      @row-click="onRowClick"
      class="text-xs cursor-pointer"
      tableClass="min-w-full divide-y divide-slate-200"
    >
      <!-- Column 1: Correlation Key / Title -->
      <Column header="Issue & Correlation Key" style="min-width: 280px">
        <template #body="{ data }">
          <div class="py-1 min-w-0">
            <div class="font-semibold text-slate-900 text-xs sm:text-sm truncate" :title="data.title">
              {{ data.title }}
            </div>
            <div class="flex items-center gap-2 mt-0.5 min-w-0">
              <span
                class="font-mono text-[11px] text-slate-600 bg-slate-100 px-1.5 py-0.2 rounded border border-slate-200 truncate max-w-[200px]"
                :title="data.correlationKey"
              >
                {{ data.correlationKey }}
              </span>
              <span class="text-[11px] text-slate-500 truncate">
                &middot; {{ data.detectorName }}
              </span>
            </div>
          </div>
        </template>
      </Column>

      <!-- Column 2: Scope -->
      <Column header="Scope" style="width: 145px; min-width: 135px">
        <template #body="{ data }">
          <span
            class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border whitespace-nowrap"
            :class="getScopeBadgeClass(data.scope)"
          >
            {{ getScopeLabel(data.scope) }}
          </span>
        </template>
      </Column>

      <!-- Column 3: Severity -->
      <Column header="Severity" style="width: 105px; min-width: 95px">
        <template #body="{ data }">
          <span
            class="inline-flex items-center px-2 py-0.5 rounded text-[11px] border whitespace-nowrap"
            :class="getSeverityBadgeClass(data.currentSeverity)"
          >
            {{ data.currentSeverity }}
          </span>
        </template>
      </Column>

      <!-- Column 4: Status -->
      <Column header="Status" style="width: 130px; min-width: 120px">
        <template #body="{ data }">
          <span
            class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-medium border whitespace-nowrap"
            :class="getStatusBadgeClass(data.status)"
          >
            <i :class="getStatusIcon(data.status)" class="text-[10px]" aria-hidden="true"></i>
            <span>{{ data.status }}</span>
          </span>
        </template>
      </Column>

      <!-- Column 5: Occurrences -->
      <Column header="Occurrences" style="width: 105px; min-width: 95px">
        <template #body="{ data }">
          <span class="font-mono text-xs font-semibold text-slate-800 tabular-nums">
            {{ Number(data.occurrenceCount ?? 0).toLocaleString('en-US') }}&times;
          </span>
        </template>
      </Column>

      <!-- Column 6: Created At / Lifecycle Date -->
      <Column header="Created / Lifecycle (WIB)" style="width: 210px; min-width: 195px">
        <template #body="{ data }">
          <div class="leading-tight space-y-0.5">
            <div class="text-[11px] text-slate-700 font-medium truncate" :title="formatWibDateTime(data.createdAtUtc, true)">
              <span class="text-slate-400">Created:</span>
              {{ formatWibDateTime(data.createdAtUtc, false) }}
            </div>
            <div
              class="text-[11px] text-slate-500 truncate"
              :title="`${getLifecycleDate(data).label}: ${formatWibDateTime(getLifecycleDate(data).isoDate, true)}`"
            >
              <span class="text-slate-400">{{ getLifecycleDate(data).label }}:</span>
              {{ formatWibDateTime(getLifecycleDate(data).isoDate, false) }}
              <span class="text-slate-400">({{ formatRelativeTime(getLifecycleDate(data).isoDate) }})</span>
            </div>
          </div>
        </template>
      </Column>

      <!-- Column 7: MTTR Duration -->
      <Column header="MTTR" style="width: 90px; min-width: 80px">
        <template #body="{ data }">
          <span
            class="font-mono text-xs tabular-nums"
            :class="data.resolvedAtUtc ? 'font-semibold text-slate-800' : 'text-slate-400'"
            :title="data.resolvedAtUtc ? 'Time to resolve (from creation or latest reopening)' : 'Not resolved'"
          >
            {{ formatMttrDuration(data) }}
          </span>
        </template>
      </Column>

      <!-- Column 8: Action Link -->
      <Column header="Action" style="width: 95px; min-width: 85px" headerClass="text-right" bodyClass="text-right">
        <template #body="{ data }">
          <button
            type="button"
            @click.stop="navigateToIssue(data.id)"
            class="inline-flex items-center gap-1 px-2.5 py-1 rounded text-xs font-medium text-slate-700 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 transition-colors cursor-pointer"
            title="Inspect issue details"
          >
            <span>Inspect</span>
            <i class="pi pi-arrow-right text-[10px]" aria-hidden="true"></i>
          </button>
        </template>
      </Column>

      <!-- Empty State -->
      <template #empty>
        <div class="py-14 px-4 text-center text-slate-500">
          <div class="w-12 h-12 rounded-full bg-slate-100 border border-slate-200 flex items-center justify-center mx-auto mb-3">
            <i class="pi pi-inbox text-xl text-slate-400" aria-hidden="true"></i>
          </div>
          <p class="text-sm font-semibold text-slate-700">
            No historical issues found for the selected period
          </p>
          <p class="text-xs text-slate-400 mt-1 max-w-md mx-auto">
            {{
              isFiltered
                ? 'No issues match your active status, scope, detector, severity, or search filters.'
                : 'Try expanding the date range preset (e.g. This Week or This Month) to inspect earlier operational periods.'
            }}
          </p>
          <button
            v-if="isFiltered"
            type="button"
            @click="emit('reset-filters')"
            class="mt-4 inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium bg-slate-900 text-white hover:bg-slate-800 transition-colors cursor-pointer"
          >
            <i class="pi pi-filter-slash text-[11px]" aria-hidden="true"></i>
            <span>Reset Filters</span>
          </button>
        </div>
      </template>
    </DataTable>
  </section>
</template>
