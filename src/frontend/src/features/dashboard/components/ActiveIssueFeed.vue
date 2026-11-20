<script setup lang="ts">
import type { IssueFeedItemDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

defineProps<{
  items: IssueFeedItemDto[];
  isPending: boolean;
  isFetching?: boolean;
  isFiltered: boolean;
}>();

const emit = defineEmits<{
  (e: 'reset-filters'): void;
}>();

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

function getSeverityBorderClass(severity: string): string {
  switch (severity) {
    case 'Critical':
      return 'border-l-4 border-l-rose-600';
    case 'High':
      return 'border-l-4 border-l-orange-500';
    case 'Medium':
      return 'border-l-4 border-l-amber-400';
    case 'Low':
      return 'border-l-4 border-l-sky-400';
    default:
      return 'border-l-4 border-l-slate-300';
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
      return 'bg-blue-50 text-blue-700 border-blue-200';
    case 'ProcessIntegrity':
      return 'bg-violet-50 text-violet-700 border-violet-200';
    case 'DataSync':
      return 'bg-cyan-50 text-cyan-700 border-cyan-200';
    case 'Infrastructure':
      return 'bg-slate-100 text-slate-700 border-slate-300';
    default:
      return 'bg-slate-50 text-slate-600 border-slate-200';
  }
}

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'Open':
      return 'bg-rose-50 text-rose-700 border-rose-200';
    case 'Acknowledged':
      return 'bg-sky-50 text-sky-700 border-sky-200';
    case 'Investigating':
      return 'bg-indigo-50 text-indigo-700 border-indigo-200';
    case 'Reopened':
      return 'bg-purple-50 text-purple-700 border-purple-300 font-semibold';
    default:
      return 'bg-slate-100 text-slate-700 border-slate-200';
  }
}

function getStatusIcon(status: string): string {
  switch (status) {
    case 'Open':
      return 'pi pi-exclamation-circle';
    case 'Acknowledged':
      return 'pi pi-eye';
    case 'Investigating':
      return 'pi pi-search';
    case 'Reopened':
      return 'pi pi-refresh';
    default:
      return 'pi pi-info-circle';
  }
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-lg shadow-2xs overflow-hidden">
    <!-- Cold Load Skeleton State -->
    <div v-if="isPending" class="divide-y divide-slate-100 animate-pulse">
      <div v-for="i in 5" :key="i" class="p-4 flex items-center justify-between gap-4">
        <div class="flex items-center gap-3 flex-1 min-w-0">
          <div class="w-16 h-6 bg-slate-200 rounded"></div>
          <div class="space-y-1.5 flex-1">
            <div class="w-2/3 h-4 bg-slate-200 rounded"></div>
            <div class="w-1/3 h-3 bg-slate-200 rounded"></div>
          </div>
        </div>
        <div class="flex items-center gap-4 shrink-0">
          <div class="w-20 h-5 bg-slate-200 rounded-full"></div>
          <div class="w-16 h-5 bg-slate-200 rounded-md"></div>
          <div class="w-10 h-5 bg-slate-200 rounded-full"></div>
          <div class="w-28 h-4 bg-slate-200 rounded"></div>
        </div>
      </div>
    </div>

    <!-- Active Feed Table / List View -->
    <div v-else-if="items.length > 0" class="overflow-x-auto">
      <table class="w-full text-left border-collapse">
        <thead>
          <tr class="bg-slate-50/80 border-b border-slate-200 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
            <th class="py-2.5 pl-4 pr-3 w-28">Severity</th>
            <th class="py-2.5 px-3">Issue Title & Correlation Key</th>
            <th class="py-2.5 px-3 w-36">Scope</th>
            <th class="py-2.5 px-3 w-32">Status</th>
            <th class="py-2.5 px-3 w-20 text-center">Count</th>
            <th class="py-2.5 px-3 w-48">Recency (WIB)</th>
            <th class="py-2.5 pr-4 pl-3 w-52">Detector</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100 text-xs">
          <tr
            v-for="item in items"
            :key="item.id"
            class="hover:bg-slate-50/70 transition-colors cursor-default select-text"
            :class="getSeverityBorderClass(item.currentSeverity)"
          >
            <!-- 1. Severity Badge -->
            <td class="py-3 pl-4 pr-3 align-top">
              <div class="flex items-center gap-1 flex-wrap">
                <span
                  class="inline-flex items-center px-2 py-0.5 rounded text-[11px] border"
                  :class="getSeverityBadgeClass(item.currentSeverity)"
                >
                  {{ item.currentSeverity }}
                </span>
                <span
                  v-if="item.escalationLevel > 0"
                  v-tooltip.top="'Severity auto-escalated (SLA timeout or ≥5 occurrences)'"
                  class="inline-flex items-center text-[10px] font-bold px-1 py-0.2 rounded bg-rose-100 text-rose-800 border border-rose-300"
                >
                  ↑ +{{ item.escalationLevel }}
                </span>
              </div>
            </td>

            <!-- 2. Issue Title & CorrelationKey -->
            <td class="py-3 px-3 align-top min-w-[280px]">
              <div class="font-medium text-slate-900 text-sm leading-snug line-clamp-2">
                {{ item.title }}
              </div>
              <div class="font-mono text-[11px] text-slate-500 mt-1 flex items-center gap-1.5 break-all">
                <i class="pi pi-key text-[10px] text-slate-400"></i>
                <span>{{ item.correlationKey }}</span>
              </div>
            </td>

            <!-- 3. Scope Pill -->
            <td class="py-3 px-3 align-top">
              <span
                class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium border"
                :class="getScopeBadgeClass(item.scope)"
              >
                {{ getScopeLabel(item.scope) }}
              </span>
            </td>

            <!-- 4. Status Badge -->
            <td class="py-3 px-3 align-top">
              <span
                class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[11px] font-medium border"
                :class="getStatusBadgeClass(item.status)"
              >
                <i :class="getStatusIcon(item.status)" class="text-[10px]"></i>
                {{ item.status }}
              </span>
              <div v-if="item.currentAssignee" class="text-[10px] text-slate-500 mt-1 flex items-center gap-1">
                <i class="pi pi-user text-[9px]"></i>
                <span class="truncate max-w-[100px]">{{ item.currentAssignee }}</span>
              </div>
              <div v-else class="text-[10px] text-slate-400 italic mt-1">
                Unassigned
              </div>
            </td>

            <!-- 5. Occurrence Count Pill -->
            <td class="py-3 px-3 align-top text-center">
              <span
                class="inline-flex items-center justify-center font-mono text-[11px] px-2 py-0.5 rounded-full border"
                :class="[
                  item.occurrenceCount >= 5
                    ? 'bg-amber-100 text-amber-800 border-amber-300 font-bold'
                    : 'bg-slate-100 text-slate-700 border-slate-200 font-medium'
                ]"
                :title="`${item.occurrenceCount} occurrences recorded`"
              >
                x{{ item.occurrenceCount }}
              </span>
            </td>

            <!-- 6. Recency (Asia/Jakarta WIB dd MMM yyyy, HH:mm:ss) -->
            <td class="py-3 px-3 align-top">
              <div
                class="font-mono text-xs text-slate-800 leading-tight"
                v-tooltip.top="formatWibDateTime(item.lastOccurrenceAtUtc, true)"
              >
                {{ formatWibDateTime(item.lastOccurrenceAtUtc) }}
              </div>
              <div class="text-[11px] text-slate-500 mt-0.5 flex items-center gap-1">
                <i class="pi pi-clock text-[10px] text-slate-400"></i>
                <span>{{ formatRelativeTime(item.lastOccurrenceAtUtc) }}</span>
              </div>
            </td>

            <!-- 7. Detector Name -->
            <td class="py-3 pr-4 pl-3 align-top">
              <div class="font-medium text-slate-700 leading-tight">
                {{ item.detectorName }}
              </div>
              <div class="text-[10px] font-mono text-slate-400 mt-0.5">
                {{ item.detectorId.slice(0, 8) }}...
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Empty State: Filtered -->
    <div
      v-else-if="isFiltered"
      class="p-12 text-center flex flex-col items-center justify-center space-y-3"
    >
      <div class="w-12 h-12 rounded-full bg-slate-100 text-slate-500 flex items-center justify-center text-lg">
        <i class="pi pi-filter-slash"></i>
      </div>
      <div>
        <p class="font-medium text-slate-800 text-sm">No issues match the selected filters.</p>
        <p class="text-xs text-slate-500 mt-1">Try widening your filter criteria or clear the search query.</p>
      </div>
      <button
        type="button"
        @click="emit('reset-filters')"
        class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium bg-slate-900 hover:bg-slate-800 text-white transition-colors cursor-pointer"
      >
        <i class="pi pi-refresh text-xs"></i>
        <span>Reset Filters</span>
      </button>
    </div>

    <!-- Empty State: All-Clean State -->
    <div
      v-else
      class="p-12 text-center flex flex-col items-center justify-center space-y-3"
    >
      <div class="w-12 h-12 rounded-full bg-emerald-50 text-emerald-600 border border-emerald-200 flex items-center justify-center text-xl">
        <i class="pi pi-check-circle"></i>
      </div>
      <div>
        <p class="font-semibold text-slate-900 text-base">All clear</p>
        <p class="text-xs text-slate-600 mt-1 max-w-md">
          No active issues detected. All hospital inspection routines running normally.
        </p>
      </div>
      <span class="inline-flex items-center gap-1.5 text-[11px] font-medium text-emerald-700 bg-emerald-50 px-2.5 py-1 rounded-full border border-emerald-200">
        <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
        Zero Active Exceptions
      </span>
    </div>
  </div>
</template>
