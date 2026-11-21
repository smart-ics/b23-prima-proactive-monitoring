<script setup lang="ts">
import { ref } from 'vue';
import { RouterLink } from 'vue-router';
import {
  ChevronRight,
  ChevronDown,
  Copy,
  Check,
  CheckCircle2,
  AlertTriangle,
  XCircle,
  ExternalLink,
  Clock,
  Zap,
} from 'lucide-vue-next';
import type { RecentRunSummaryDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

interface Props {
  runs?: RecentRunSummaryDto[] | null;
  detectorId: string;
}

defineProps<Props>();

// Reactive Set to track expanded run IDs across 15s background query refetches
const expandedRunIds = ref<Set<string>>(new Set());

// Transient clipboard copied indicator for run failure reasons
const copiedRunId = ref<string | null>(null);

function toggleRow(runId: string): void {
  const next = new Set(expandedRunIds.value);
  if (next.has(runId)) {
    next.delete(runId);
  } else {
    next.add(runId);
  }
  expandedRunIds.value = next;
}

function isExpanded(runId: string): boolean {
  return expandedRunIds.value.has(runId);
}

async function copyFailureReason(runId: string, reason?: string | null): Promise<void> {
  if (!reason) return;
  try {
    await navigator.clipboard.writeText(reason);
    copiedRunId.value = runId;
    setTimeout(() => {
      if (copiedRunId.value === runId) {
        copiedRunId.value = null;
      }
    }, 2000);
  } catch (err) {
    console.error('Failed to copy failure reason to clipboard:', err);
  }
}

function formatDuration(ms: number): string {
  if (!ms || ms < 0) return '0 ms';
  if (ms < 1000) return `${ms} ms`;
  return `${(ms / 1000).toFixed(2)} s`;
}

function getOutcomeBadgeClass(outcome: string): string {
  switch (outcome) {
    case 'Passed':
      return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    case 'FindingDiscovered':
      return 'bg-amber-50 text-amber-700 border-amber-200';
    case 'ExecutionFailed':
      return 'bg-rose-50 text-rose-700 border-rose-200';
    default:
      return 'bg-slate-100 text-slate-700 border-slate-200';
  }
}

function getTriggerBadgeClass(trigger: string): string {
  switch (trigger) {
    case 'Scheduled':
      return 'bg-slate-100 text-slate-700 border-slate-200';
    case 'Manual':
      return 'bg-blue-50 text-blue-700 border-blue-200';
    case 'Verification':
      return 'bg-purple-50 text-purple-700 border-purple-200';
    default:
      return 'bg-slate-50 text-slate-600 border-slate-200';
  }
}
</script>

<template>
  <div class="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
    <!-- Section Header -->
    <div class="px-5 py-4 border-b border-slate-200 flex flex-wrap items-center justify-between gap-3">
      <div>
        <h3 class="text-sm font-semibold text-slate-900 uppercase tracking-wider flex items-center gap-2">
          <Clock class="w-4 h-4 text-slate-500" />
          Recent Inspection Runs
          <span class="text-xs font-normal text-slate-500 lowercase">(last 10 runs)</span>
        </h3>
        <p class="text-xs text-slate-500 mt-0.5">
          Inline row details remain expanded through 15-second background polling cycles.
        </p>
      </div>

      <RouterLink
        :to="`/detectors/${detectorId}/runs`"
        class="inline-flex items-center gap-1.5 text-xs font-medium text-slate-700 hover:text-slate-900 hover:bg-slate-100 px-3 py-1.5 rounded-lg border border-slate-200 transition-colors"
      >
        <span>View Full Run Log</span>
        <ExternalLink class="w-3.5 h-3.5" />
      </RouterLink>
    </div>

    <!-- Empty State -->
    <div
      v-if="!runs || runs.length === 0"
      class="p-8 text-center"
    >
      <Clock class="w-8 h-8 text-slate-400 mx-auto mb-2" />
      <p class="text-sm font-medium text-slate-700">No inspection runs recorded yet</p>
      <p class="text-xs text-slate-500 mt-1">
        Runs will populate here as probes report heartbeats or manual runs are dispatched.
      </p>
    </div>

    <!-- Runs Table -->
    <div v-else class="overflow-x-auto">
      <table class="w-full text-left border-collapse">
        <thead>
          <tr class="bg-slate-50/80 text-[11px] font-semibold text-slate-600 uppercase tracking-wider border-b border-slate-200">
            <th class="py-3 px-3 w-10 text-center"></th>
            <th class="py-3 px-4">Execution Time</th>
            <th class="py-3 px-4">Trigger Source</th>
            <th class="py-3 px-4">Outcome</th>
            <th class="py-3 px-4 text-right">Duration</th>
            <th class="py-3 px-4 text-right">Findings</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100 text-xs text-slate-800">
          <template v-for="run in runs" :key="run.id">
            <!-- Summary Row -->
            <tr
              class="hover:bg-slate-50/70 transition-colors cursor-pointer select-none"
              :class="{ 'bg-slate-50/50': isExpanded(run.id) }"
              @click="toggleRow(run.id)"
            >
              <td class="py-3 px-3 text-center text-slate-400">
                <component
                  :is="isExpanded(run.id) ? ChevronDown : ChevronRight"
                  class="w-4 h-4 mx-auto text-slate-500 transition-transform"
                />
              </td>

              <!-- Execution Time -->
              <td class="py-3 px-4">
                <div class="font-medium text-slate-900" :title="formatWibDateTime(run.executionTimestampUtc, true)">
                  {{ formatWibDateTime(run.executionTimestampUtc, false) }}
                </div>
                <div class="text-[11px] text-slate-500">
                  {{ formatRelativeTime(run.executionTimestampUtc) }}
                </div>
              </td>

              <!-- Trigger Source -->
              <td class="py-3 px-4">
                <span
                  class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium border"
                  :class="getTriggerBadgeClass(run.triggerSource)"
                >
                  <Zap v-if="run.triggerSource === 'Manual'" class="w-3 h-3 text-blue-600" />
                  {{ run.triggerSource }}
                </span>
              </td>

              <!-- Outcome -->
              <td class="py-3 px-4">
                <span
                  class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-semibold border"
                  :class="getOutcomeBadgeClass(run.outcome)"
                >
                  <CheckCircle2 v-if="run.outcome === 'Passed'" class="w-3.5 h-3.5 text-emerald-600" />
                  <AlertTriangle v-else-if="run.outcome === 'FindingDiscovered'" class="w-3.5 h-3.5 text-amber-600" />
                  <XCircle v-else class="w-3.5 h-3.5 text-rose-600" />
                  {{ run.outcome }}
                </span>
              </td>

              <!-- Duration -->
              <td class="py-3 px-4 text-right font-mono text-slate-700">
                {{ formatDuration(run.durationMs) }}
              </td>

              <!-- Findings Count -->
              <td class="py-3 px-4 text-right">
                <span
                  v-if="run.findingsCount > 0"
                  class="inline-flex items-center px-2 py-0.5 rounded font-semibold text-[11px] bg-amber-100 text-amber-800"
                >
                  {{ run.findingsCount }} {{ run.findingsCount === 1 ? 'finding' : 'findings' }}
                </span>
                <span
                  v-else
                  class="text-slate-400 font-normal"
                >
                  0
                </span>
              </td>
            </tr>

            <!-- Expanded Detail Row -->
            <tr v-if="isExpanded(run.id)" class="bg-slate-50/90 border-t border-b border-slate-200">
              <td colspan="6" class="px-5 py-4">
                <div class="space-y-3">
                  <!-- Run Identity and Metadata Strip -->
                  <div class="flex flex-wrap items-center justify-between text-xs text-slate-600 pb-2 border-b border-slate-200">
                    <div class="flex items-center gap-2">
                      <span class="font-medium text-slate-700">Run ID:</span>
                      <code class="font-mono text-[11px] bg-white px-1.5 py-0.5 rounded border border-slate-200 text-slate-800 select-all">
                        {{ run.id }}
                      </code>
                    </div>
                    <div class="flex items-center gap-4 text-[11px]">
                      <span>Timestamp (WIB): <strong class="text-slate-800">{{ formatWibDateTime(run.executionTimestampUtc, true) }}</strong></span>
                      <span>Duration: <strong class="text-slate-800">{{ run.durationMs }} ms</strong></span>
                    </div>
                  </div>

                  <!-- Failure Reason Monospace Block with Copy Button -->
                  <div v-if="run.outcome === 'ExecutionFailed' || run.failureReason" class="space-y-1.5">
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-semibold text-rose-700 flex items-center gap-1.5">
                        <XCircle class="w-3.5 h-3.5" />
                        Execution Failure Diagnostics
                      </span>
                      <button
                        type="button"
                        class="inline-flex items-center gap-1 text-[11px] font-medium px-2 py-1 rounded bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 shadow-xs transition-colors cursor-pointer"
                        @click.stop="copyFailureReason(run.id, run.failureReason)"
                      >
                        <component :is="copiedRunId === run.id ? Check : Copy" class="w-3 h-3 text-slate-600" />
                        <span>{{ copiedRunId === run.id ? 'Copied!' : 'Copy Reason' }}</span>
                      </button>
                    </div>

                    <div class="relative rounded-lg bg-slate-900 text-slate-100 p-3.5 font-mono text-xs overflow-x-auto leading-relaxed border border-slate-800 shadow-inner">
                      {{ run.failureReason || 'No detailed error message was returned by probe webhook.' }}
                    </div>
                  </div>

                  <!-- Finding Discovered Banner -->
                  <div
                    v-else-if="run.outcome === 'FindingDiscovered' || run.findingsCount > 0"
                    class="bg-amber-50 rounded-lg p-3 border border-amber-200 flex items-start gap-2.5 text-xs text-amber-900"
                  >
                    <AlertTriangle class="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
                    <div>
                      <p class="font-semibold">Inspection run yielded {{ run.findingsCount }} finding(s).</p>
                      <p class="text-amber-800 text-[11px] mt-0.5">
                        Findings are correlated into active issues. Refer to the Active Issues section below or open the full run log for individual finding telemetry.
                      </p>
                    </div>
                  </div>

                  <!-- Passed Clean State Banner -->
                  <div
                    v-else
                    class="bg-emerald-50 rounded-lg p-3 border border-emerald-200 flex items-center gap-2 text-xs text-emerald-900"
                  >
                    <CheckCircle2 class="w-4 h-4 text-emerald-600 shrink-0" />
                    <span>Clean inspection run. No anomalies, variances, or operational exceptions detected.</span>
                  </div>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
  </div>
</template>
