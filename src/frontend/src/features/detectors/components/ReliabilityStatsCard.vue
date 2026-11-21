<script setup lang="ts">
import { computed } from 'vue';
import {
  CheckCircle2,
  XCircle,
  Activity,
  ShieldCheck,
  AlertTriangle,
} from 'lucide-vue-next';
import type { DetectorReliabilityStatsDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

interface Props {
  stats?: DetectorReliabilityStatsDto | null;
}

const props = defineProps<Props>();

const totalRuns = computed(() => props.stats?.totalRuns30d ?? 0);
const passRate = computed(() => Number(props.stats?.passRate30d ?? 0));

const passRateDisplay = computed(() => {
  if (totalRuns.value === 0) return 'N/A';
  return `${passRate.value.toFixed(1)}%`;
});

const passRateColorClass = computed(() => {
  if (totalRuns.value === 0) {
    return 'bg-slate-100 text-slate-600 border-slate-200';
  }
  if (passRate.value >= 95) {
    return 'bg-emerald-50 text-emerald-700 border-emerald-200';
  }
  if (passRate.value >= 85) {
    return 'bg-amber-50 text-amber-700 border-amber-200';
  }
  return 'bg-rose-50 text-rose-700 border-rose-200';
});

const passRateLabel = computed(() => {
  if (totalRuns.value === 0) return 'No runs in 30d';
  if (passRate.value >= 95) return 'Optimal (≥95%)';
  if (passRate.value >= 85) return 'Degraded (85%–94%)';
  return 'Unreliable (<85%)';
});
</script>

<template>
  <div class="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
    <div class="flex items-center justify-between mb-4">
      <h3 class="text-sm font-semibold text-slate-800 uppercase tracking-wider flex items-center gap-2">
        <Activity class="w-4 h-4 text-slate-500" />
        30-Day Operational Reliability Metrics
      </h3>
      <span class="text-xs text-slate-500 font-normal">Rolling 30-day window</span>
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <!-- 1. Last Successful Run -->
      <div class="bg-slate-50 rounded-lg p-4 border border-slate-200 flex flex-col justify-between">
        <div class="flex items-center justify-between mb-2">
          <span class="text-xs font-medium text-slate-600">Last Successful Run</span>
          <CheckCircle2 class="w-4 h-4 text-emerald-600" />
        </div>
        <div>
          <div class="text-sm font-semibold text-slate-900 truncate" :title="formatWibDateTime(stats?.lastSuccessfulRunAtUtc, true)">
            {{ stats?.lastSuccessfulRunAtUtc ? formatWibDateTime(stats.lastSuccessfulRunAtUtc, false) : 'Never' }}
          </div>
          <div class="text-xs text-slate-500 mt-1">
            {{ stats?.lastSuccessfulRunAtUtc ? formatRelativeTime(stats.lastSuccessfulRunAtUtc) : 'No successful runs yet' }}
          </div>
        </div>
      </div>

      <!-- 2. Last Failure -->
      <div class="bg-slate-50 rounded-lg p-4 border border-slate-200 flex flex-col justify-between">
        <div class="flex items-center justify-between mb-2">
          <span class="text-xs font-medium text-slate-600">Last Failure</span>
          <component
            :is="stats?.lastFailureAtUtc ? XCircle : CheckCircle2"
            :class="['w-4 h-4', stats?.lastFailureAtUtc ? 'text-rose-600' : 'text-emerald-500']"
          />
        </div>
        <div>
          <div class="text-sm font-semibold text-slate-900 truncate" :title="stats?.lastFailureAtUtc ? formatWibDateTime(stats.lastFailureAtUtc, true) : 'No failures'">
            {{ stats?.lastFailureAtUtc ? formatWibDateTime(stats.lastFailureAtUtc, false) : 'None' }}
          </div>
          <div class="text-xs mt-1" :class="stats?.lastFailureAtUtc ? 'text-rose-600' : 'text-slate-500'">
            {{ stats?.lastFailureAtUtc ? formatRelativeTime(stats.lastFailureAtUtc) : 'Clean record' }}
          </div>
        </div>
      </div>

      <!-- 3. Total Runs (30d) -->
      <div class="bg-slate-50 rounded-lg p-4 border border-slate-200 flex flex-col justify-between">
        <div class="flex items-center justify-between mb-2">
          <span class="text-xs font-medium text-slate-600">Total Runs (30d)</span>
          <Activity class="w-4 h-4 text-blue-600" />
        </div>
        <div>
          <div class="text-2xl font-bold text-slate-900">
            {{ totalRuns }}
          </div>
          <div class="text-xs text-slate-500 mt-1">
            Executed executions
          </div>
        </div>
      </div>

      <!-- 4. Clean Pass Rate -->
      <div
        class="rounded-lg p-4 border flex flex-col justify-between transition-colors"
        :class="passRateColorClass"
      >
        <div class="flex items-center justify-between mb-2">
          <span class="text-xs font-semibold uppercase tracking-wider">Clean Pass Rate</span>
          <component
            :is="totalRuns === 0 ? ShieldCheck : passRate >= 85 ? ShieldCheck : AlertTriangle"
            class="w-4 h-4"
          />
        </div>
        <div>
          <div class="text-2xl font-black">
            {{ passRateDisplay }}
          </div>
          <div class="text-xs font-medium mt-1">
            {{ passRateLabel }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
