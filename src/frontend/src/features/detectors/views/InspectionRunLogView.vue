<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRoute, useRouter, RouterLink } from 'vue-router';
import {
  useDetectorDetailQuery,
  useInspectionRunsQuery,
} from '@/features/detectors/api/useDetectorQueries';
import type { InspectionRunsQueryParams } from '@/features/detectors/api/detector.keys';
import type { InspectionRunDetailDto } from '@/shared/types/api.generated';
import RunLogFilterBar from '@/features/detectors/components/RunLogFilterBar.vue';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

const route = useRoute();
const router = useRouter();

const detectorId = computed(() => route.params.id as string);

// Date Helpers (Default 7 Days)
function getUtcDaysAgoString(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().split('T')[0];
}

function getUtcTodayString(): string {
  return new Date().toISOString().split('T')[0];
}

// Filter State
const startDate = ref<string>(getUtcDaysAgoString(7));
const endDate = ref<string>(getUtcTodayString());
const outcome = ref<string>('All');
const triggerSource = ref<string>('All');

// Computed query parameters for backend query
const queryParams = computed<InspectionRunsQueryParams>(() => {
  const p: InspectionRunsQueryParams = {};
  if (startDate.value) {
    p.startDateUtc = `${startDate.value}T00:00:00Z`;
  }
  if (endDate.value) {
    p.endDateUtc = `${endDate.value}T23:59:59.999Z`;
  }
  if (outcome.value && outcome.value !== 'All') {
    p.outcome = outcome.value;
  }
  if (triggerSource.value && triggerSource.value !== 'All') {
    p.triggerSource = triggerSource.value;
  }
  return p;
});

// Detector Metadata Query (for Header & Breadcrumb Context)
const {
  data: detector,
  isLoading: isDetectorLoading,
} = useDetectorDetailQuery(detectorId);

// Inspection Runs Query
const {
  data: runLogData,
  isLoading: isRunsLoading,
  isFetching: isRunsFetching,
  isError: isRunsError,
  error: runsError,
  refetch: refetchRuns,
} = useInspectionRunsQuery(detectorId, queryParams);

const runs = computed<InspectionRunDetailDto[]>(() => {
  return runLogData.value?.runs ?? [];
});

const totalCount = computed<number>(() => {
  return runLogData.value?.totalCount ?? runs.value.length;
});

// Row Expansion State Preserved Across Query Refetches via reactive Set<string>
const expandedRowIds = ref<Set<string>>(new Set());

function toggleRow(id: string) {
  const next = new Set(expandedRowIds.value);
  if (next.has(id)) {
    next.delete(id);
  } else {
    next.add(id);
  }
  expandedRowIds.value = next;
}

function isRowExpanded(id: string): boolean {
  return expandedRowIds.value.has(id);
}

function expandAll() {
  const next = new Set<string>();
  for (const r of runs.value) {
    next.add(r.id);
  }
  expandedRowIds.value = next;
}

function collapseAll() {
  expandedRowIds.value = new Set();
}

// Copy to Clipboard for Failure Traces
const copiedTraceId = ref<string | null>(null);

async function copyFailureTrace(runId: string, traceText: string) {
  try {
    await navigator.clipboard.writeText(traceText);
    copiedTraceId.value = runId;
    setTimeout(() => {
      if (copiedTraceId.value === runId) {
        copiedTraceId.value = null;
      }
    }, 2000);
  } catch (err) {
    console.error('Failed to copy failure trace', err);
  }
}

// Format Helpers
function formatDuration(ms: number): string {
  if (ms < 1000) {
    return `${ms} ms`;
  }
  return `${(ms / 1000).toFixed(2)} s`;
}

function getOutcomeBadgeClass(val: string): string {
  switch (val) {
    case 'Passed':
      return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    case 'FindingDiscovered':
      return 'bg-amber-50 text-amber-800 border-amber-200';
    case 'ExecutionFailed':
      return 'bg-rose-50 text-rose-700 border-rose-200';
    default:
      return 'bg-slate-100 text-slate-700 border-slate-200';
  }
}

function getOutcomeLabel(val: string): string {
  switch (val) {
    case 'Passed':
      return 'Clean Pass';
    case 'FindingDiscovered':
      return 'Finding Discovered';
    case 'ExecutionFailed':
      return 'Execution Failed';
    default:
      return val;
  }
}

function getOutcomeIcon(val: string): string {
  switch (val) {
    case 'Passed':
      return 'pi pi-check-circle text-emerald-600';
    case 'FindingDiscovered':
      return 'pi pi-exclamation-triangle text-amber-600';
    case 'ExecutionFailed':
      return 'pi pi-times-circle text-rose-600';
    default:
      return 'pi pi-info-circle text-slate-500';
  }
}

function getTriggerBadgeClass(val: string): string {
  switch (val) {
    case 'Scheduled':
      return 'bg-slate-100 text-slate-700 border-slate-200';
    case 'Manual':
      return 'bg-purple-50 text-purple-700 border-purple-200';
    case 'AutomatedReRun':
      return 'bg-sky-50 text-sky-700 border-sky-200';
    default:
      return 'bg-slate-50 text-slate-600 border-slate-200';
  }
}

function getTriggerLabel(val: string): string {
  switch (val) {
    case 'Scheduled':
      return 'Scheduled';
    case 'Manual':
      return 'Manual Probe';
    case 'AutomatedReRun':
      return 'Automated Re-Run';
    default:
      return val;
  }
}

function getTriggerIcon(val: string): string {
  switch (val) {
    case 'Scheduled':
      return 'pi pi-clock text-slate-500';
    case 'Manual':
      return 'pi pi-bolt text-purple-500';
    case 'AutomatedReRun':
      return 'pi pi-sync text-sky-500';
    default:
      return 'pi pi-play text-slate-400';
  }
}

function handleResetFilters() {
  startDate.value = getUtcDaysAgoString(7);
  endDate.value = getUtcTodayString();
  outcome.value = 'All';
  triggerSource.value = 'All';
}
</script>

<template>
  <div class="space-y-5 max-w-7xl mx-auto pb-12">
    <!-- Header Section -->
    <header class="space-y-2">
      <!-- Breadcrumb & Back Button -->
      <div class="flex items-center justify-between flex-wrap gap-2">
        <nav class="flex items-center gap-2 text-xs text-slate-500">
          <RouterLink
            to="/detectors"
            class="hover:text-slate-900 transition-colors font-medium flex items-center gap-1"
          >
            <i class="pi pi-server text-[11px]"></i>
            <span>Detectors</span>
          </RouterLink>
          <span class="text-slate-300">/</span>
          <RouterLink
            :to="`/detectors/${detectorId}`"
            class="hover:text-slate-900 transition-colors font-medium truncate max-w-[200px]"
            :title="detector?.name || detectorId"
          >
            {{ detector?.name || (isDetectorLoading ? 'Loading detector...' : 'Detector') }}
          </RouterLink>
          <span class="text-slate-300">/</span>
          <span class="text-slate-900 font-semibold">Inspection Run Log</span>
        </nav>

        <button
          type="button"
          @click="router.push(`/detectors/${detectorId}`)"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-slate-300 bg-white hover:bg-slate-50 text-xs font-medium text-slate-700 transition-colors shadow-2xs"
        >
          <i class="pi pi-arrow-left text-[11px]"></i>
          <span>Back to Detector</span>
        </button>
      </div>

      <!-- Main Title & Context Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-3 pt-1">
        <div>
          <div class="flex items-center gap-2.5 flex-wrap">
            <h1 class="text-xl sm:text-2xl font-bold text-slate-900 tracking-tight">
              Historical Inspection Run Log
            </h1>
            <span
              v-if="detector?.targetModule"
              class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-slate-100 text-slate-700 border border-slate-200"
            >
              {{ detector.targetModule }}
            </span>
            <span
              v-if="detector?.scope"
              class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-slate-100 text-slate-700 border border-slate-200"
            >
              {{ detector.scope }}
            </span>
          </div>
          <p class="text-xs sm:text-sm text-slate-500 mt-1">
            Historical probe execution outcomes, execution durations, failure traces, and discovered issues.
          </p>
        </div>

        <!-- Query Refresh Control -->
        <div class="flex items-center gap-2 self-start md:self-center">
          <button
            type="button"
            @click="refetchRuns()"
            :disabled="isRunsFetching"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-slate-300 bg-white hover:bg-slate-50 text-xs font-medium text-slate-700 transition-colors shadow-2xs disabled:opacity-60"
            title="Refresh Inspection Runs"
          >
            <i class="pi pi-refresh text-xs" :class="{ 'animate-spin': isRunsFetching }"></i>
            <span>{{ isRunsFetching ? 'Refreshing...' : 'Refresh' }}</span>
          </button>
        </div>
      </div>
    </header>

    <!-- Run Log Filter Bar (Component Requirement 1) -->
    <RunLogFilterBar
      v-model:startDate="startDate"
      v-model:endDate="endDate"
      v-model:outcome="outcome"
      v-model:triggerSource="triggerSource"
      :totalCount="totalCount"
      :isLoading="isRunsLoading || isRunsFetching"
      @reset="handleResetFilters"
    />

    <!-- Main Run Log Content -->
    <div class="bg-white border border-slate-200 rounded-lg shadow-2xs overflow-hidden">
      <!-- Table Header Bar / Controls -->
      <div class="px-4 py-3 bg-slate-50/60 border-b border-slate-200 flex items-center justify-between flex-wrap gap-2">
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold text-slate-700">Runs Table</span>
          <span class="text-xs text-slate-400">·</span>
          <span class="text-xs text-slate-500 font-mono">{{ totalCount }} total records</span>
        </div>

        <div v-if="runs.length > 0" class="flex items-center gap-2">
          <button
            type="button"
            @click="expandAll"
            class="text-xs text-slate-600 hover:text-slate-900 font-medium px-2 py-1 rounded hover:bg-slate-100 transition-colors"
          >
            Expand All Details
          </button>
          <span class="text-slate-300">|</span>
          <button
            type="button"
            @click="collapseAll"
            class="text-xs text-slate-600 hover:text-slate-900 font-medium px-2 py-1 rounded hover:bg-slate-100 transition-colors"
          >
            Collapse All
          </button>
        </div>
      </div>

      <!-- Loading State Skeleton -->
      <div v-if="isRunsLoading" class="p-8 space-y-4">
        <div class="flex items-center justify-center gap-3 text-slate-500 py-6">
          <i class="pi pi-spin pi-spinner text-xl text-slate-600"></i>
          <span class="text-sm font-medium">Loading historical inspection runs...</span>
        </div>
        <div v-for="i in 5" :key="i" class="h-12 bg-slate-100 rounded animate-pulse"></div>
      </div>

      <!-- Error State -->
      <div v-else-if="isRunsError" class="p-8 text-center space-y-3">
        <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-600 flex items-center justify-center mx-auto border border-rose-200">
          <i class="pi pi-exclamation-triangle text-xl"></i>
        </div>
        <h3 class="text-sm font-semibold text-slate-900">Failed to load inspection run log</h3>
        <p class="text-xs text-slate-500 max-w-md mx-auto">
          {{ runsError?.message || 'An unexpected error occurred while fetching historical inspection records.' }}
        </p>
        <button
          type="button"
          @click="refetchRuns()"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-slate-900 text-white text-xs font-medium hover:bg-slate-800 transition-colors shadow-2xs"
        >
          <i class="pi pi-refresh text-xs"></i>
          <span>Retry Query</span>
        </button>
      </div>

      <!-- Empty State -->
      <div v-else-if="runs.length === 0" class="p-12 text-center space-y-3">
        <div class="w-12 h-12 rounded-full bg-slate-100 text-slate-400 flex items-center justify-center mx-auto border border-slate-200">
          <i class="pi pi-filter-slash text-xl"></i>
        </div>
        <h3 class="text-sm font-semibold text-slate-900">No inspection runs found</h3>
        <p class="text-xs text-slate-500 max-w-md mx-auto">
          No probe runs match the selected date range and filter criteria. Try broadening your filter parameters.
        </p>
        <button
          type="button"
          @click="handleResetFilters"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-slate-300 bg-white hover:bg-slate-50 text-xs font-medium text-slate-700 transition-colors shadow-2xs"
        >
          <i class="pi pi-filter-slash text-xs"></i>
          <span>Reset Filters to Default</span>
        </button>
      </div>

      <!-- Virtualized / Structured Data Table -->
      <div v-else class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="bg-slate-50/80 border-b border-slate-200 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
              <th class="py-2.5 pl-4 pr-3 w-48">Timestamp (WIB)</th>
              <th class="py-2.5 px-3 w-40">Trigger Source</th>
              <th class="py-2.5 px-3 w-44">Outcome</th>
              <th class="py-2.5 px-3 w-32 text-right">Duration (ms)</th>
              <th class="py-2.5 px-3 w-36 text-center">Findings</th>
              <th class="py-2.5 pr-4 pl-3 w-28 text-center">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100 text-xs">
            <template v-for="run in runs" :key="run.id">
              <!-- Summary Table Row -->
              <tr
                @click="toggleRow(run.id)"
                class="transition-colors cursor-pointer select-text hover:bg-slate-50/80"
                :class="[
                  isRowExpanded(run.id) ? 'bg-slate-50/50' : '',
                  run.outcome === 'ExecutionFailed'
                    ? 'border-l-4 border-l-rose-500'
                    : run.outcome === 'FindingDiscovered'
                    ? 'border-l-4 border-l-amber-500'
                    : 'border-l-4 border-l-emerald-500'
                ]"
              >
                <!-- 1. Timestamp (WIB) -->
                <td class="py-3 pl-4 pr-3 align-middle">
                  <div class="font-medium text-slate-900 leading-snug">
                    {{ formatWibDateTime(run.executionTimestampUtc) }}
                  </div>
                  <div class="text-[11px] text-slate-400 mt-0.5">
                    {{ formatRelativeTime(run.executionTimestampUtc) }}
                  </div>
                </td>

                <!-- 2. Trigger Source -->
                <td class="py-3 px-3 align-middle">
                  <span
                    class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[11px] font-medium border"
                    :class="getTriggerBadgeClass(run.triggerSource)"
                  >
                    <i :class="getTriggerIcon(run.triggerSource)" class="text-[10px]"></i>
                    {{ getTriggerLabel(run.triggerSource) }}
                  </span>
                </td>

                <!-- 3. Outcome -->
                <td class="py-3 px-3 align-middle">
                  <span
                    class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-semibold border"
                    :class="getOutcomeBadgeClass(run.outcome)"
                  >
                    <i :class="getOutcomeIcon(run.outcome)" class="text-[10px]"></i>
                    {{ getOutcomeLabel(run.outcome) }}
                  </span>
                </td>

                <!-- 4. Duration (ms) -->
                <td class="py-3 px-3 align-middle text-right">
                  <span class="font-mono text-slate-800 font-medium">
                    {{ formatDuration(run.durationMs) }}
                  </span>
                </td>

                <!-- 5. Findings Count -->
                <td class="py-3 px-3 align-middle text-center">
                  <span
                    class="inline-flex items-center justify-center font-mono text-[11px] px-2.5 py-0.5 rounded-full border"
                    :class="[
                      run.findingsCount > 0
                        ? 'bg-amber-100 text-amber-800 border-amber-300 font-bold'
                        : 'bg-slate-100 text-slate-600 border-slate-200 font-medium'
                    ]"
                  >
                    {{ run.findingsCount }}
                    {{ run.findingsCount === 1 ? ' finding' : ' findings' }}
                  </span>
                </td>

                <!-- 6. Actions (Expand Details) -->
                <td class="py-3 pr-4 pl-3 align-middle text-center">
                  <button
                    type="button"
                    @click.stop="toggleRow(run.id)"
                    class="inline-flex items-center gap-1 px-2.5 py-1 rounded text-xs font-medium text-slate-600 hover:text-slate-900 hover:bg-slate-200/70 transition-colors"
                    :title="isRowExpanded(run.id) ? 'Collapse Details' : 'Expand Details'"
                  >
                    <span>{{ isRowExpanded(run.id) ? 'Hide' : 'Details' }}</span>
                    <i
                      class="pi text-[10px] transition-transform duration-200"
                      :class="isRowExpanded(run.id) ? 'pi-chevron-up' : 'pi-chevron-down'"
                    ></i>
                  </button>
                </td>
              </tr>

              <!-- Expandable Row Details Container -->
              <tr v-if="isRowExpanded(run.id)" class="bg-slate-900/[0.02]">
                <td colspan="6" class="p-4 sm:p-5 border-t border-b border-slate-200">
                  <div class="space-y-4 max-w-5xl mx-auto">
                    <!-- Execution Run Metadata Header -->
                    <div class="flex items-center justify-between flex-wrap gap-2 text-xs text-slate-600 pb-2 border-b border-slate-200">
                      <div class="flex items-center gap-3">
                        <span class="font-mono text-slate-500">Run ID: {{ run.id }}</span>
                        <span class="text-slate-300">·</span>
                        <span>Executed: {{ formatWibDateTime(run.executionTimestampUtc, true) }}</span>
                      </div>
                      <div class="flex items-center gap-2">
                        <span class="text-slate-500">Duration:</span>
                        <span class="font-mono font-medium text-slate-800">{{ run.durationMs }} ms</span>
                      </div>
                    </div>

                    <!-- Failure Reason / Trace Block (Dark Monospace with Copy Button) -->
                    <div v-if="run.outcome === 'ExecutionFailed' || run.failureReason" class="space-y-1.5">
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-semibold text-rose-700 uppercase tracking-wider flex items-center gap-1.5">
                          <i class="pi pi-times-circle text-xs"></i>
                          <span>Execution Failure Trace & Diagnostic Log:</span>
                        </span>

                        <button
                          v-if="run.failureReason"
                          type="button"
                          @click.stop="copyFailureTrace(run.id, run.failureReason)"
                          class="inline-flex items-center gap-1 text-[11px] font-medium px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 transition-colors shadow-2xs"
                        >
                          <i
                            :class="copiedTraceId === run.id ? 'pi pi-check text-emerald-400' : 'pi pi-copy text-slate-400'"
                            class="text-[10px]"
                          ></i>
                          <span>{{ copiedTraceId === run.id ? 'Copied!' : 'Copy Trace' }}</span>
                        </button>
                      </div>

                      <div class="relative bg-slate-950 text-slate-200 rounded-lg p-3.5 border border-slate-800 shadow-inner font-mono text-xs overflow-x-auto whitespace-pre-wrap leading-relaxed select-text">
                        {{ run.failureReason || '[No explicit failure trace was recorded for this failed probe run.]' }}
                      </div>
                    </div>

                    <!-- Discovered Issues Links (Links directly to S-02) -->
                    <div v-if="run.discoveredIssueIds && run.discoveredIssueIds.length > 0" class="space-y-2">
                      <div class="flex items-center gap-2">
                        <i class="pi pi-exclamation-triangle text-amber-600 text-xs"></i>
                        <span class="text-xs font-semibold text-slate-800 uppercase tracking-wider">
                          Discovered Issues ({{ run.discoveredIssueIds.length }} linked)
                        </span>
                        <span class="text-[11px] text-slate-400">· Click to open in Issue Detail Workbench (S-02)</span>
                      </div>

                      <div class="flex flex-wrap gap-2">
                        <RouterLink
                          v-for="issueId in run.discoveredIssueIds"
                          :key="issueId"
                          :to="`/issues/${issueId}`"
                          class="inline-flex items-center gap-2 px-3 py-2 rounded-md bg-white border border-slate-300 hover:border-slate-500 hover:bg-slate-50 text-xs font-mono text-slate-800 transition-all shadow-2xs group"
                        >
                          <i class="pi pi-ticket text-amber-600 text-xs"></i>
                          <span class="font-semibold text-slate-900 group-hover:text-blue-700 transition-colors">
                            Issue {{ issueId }}
                          </span>
                          <i class="pi pi-external-link text-[10px] text-slate-400 group-hover:text-blue-700 transition-colors"></i>
                        </RouterLink>
                      </div>
                    </div>

                    <!-- Non-issue findings note (if findingsCount > 0 but no issue IDs recorded) -->
                    <div
                      v-else-if="run.findingsCount > 0"
                      class="p-3 bg-amber-50 border border-amber-200 rounded-md text-xs text-amber-900 flex items-start gap-2"
                    >
                      <i class="pi pi-info-circle text-amber-700 mt-0.5"></i>
                      <div>
                        <span class="font-semibold">Findings Detected: </span>
                        Probe reported {{ run.findingsCount }} anomaly findings during this inspection run.
                      </div>
                    </div>

                    <!-- Clean Inspection Run Note -->
                    <div
                      v-else-if="run.outcome === 'Passed'"
                      class="p-3 bg-emerald-50/70 border border-emerald-200 rounded-md text-xs text-emerald-900 flex items-center gap-2"
                    >
                      <i class="pi pi-check-circle text-emerald-600 text-sm"></i>
                      <span>
                        Clean pass: Probe executed successfully and verified 0 data anomalies against hospital systems.
                      </span>
                    </div>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
