<script setup lang="ts">
import { computed } from 'vue';
import Skeleton from 'primevue/skeleton';
import type { ReliabilityKpiResponse } from '../types/history.types';

const props = withDefaults(
  defineProps<{
    kpi?: ReliabilityKpiResponse | null;
    isLoading?: boolean;
  }>(),
  {
    kpi: null,
    isLoading: false,
  }
);

interface KpiTileConfig {
  key: string;
  label: string;
  value: string;
  subtitle: string;
  tooltip: string;
  dotClass: string;
  valueClass: string;
  cardClass: string;
  badgeText?: string;
}

function formatHours(hours: number | null | undefined): string {
  if (hours === null || hours === undefined || !Number.isFinite(hours)) {
    return '—';
  }
  return `${hours.toFixed(1)} h`;
}

const tiles = computed<KpiTileConfig[]>(() => {
  const totalIssues = props.kpi?.totalIssuesCreated ?? 0;
  const resolutionRate = props.kpi?.resolutionRatePercentage ?? 0;
  const isResolutionHealthy = resolutionRate >= 90;

  const passRate = props.kpi?.detectorPassRatePercentage ?? 100;
  const isPassRateHealthy = passRate >= 98;

  const reopenedCount = props.kpi?.reopenedIssuesCount ?? 0;
  const hasReopened = reopenedCount > 0;

  return [
    {
      key: 'total-issues',
      label: 'Total Issues',
      value: totalIssues.toLocaleString('en-US'),
      subtitle: 'New in period',
      tooltip: 'Total issues discovered and created within the selected WIB date range.',
      dotClass: 'bg-slate-600',
      valueClass: 'text-slate-900',
      cardClass: 'bg-white border-slate-200',
    },
    {
      key: 'resolution-rate',
      label: 'Resolution Rate',
      value: `${resolutionRate.toFixed(1)}%`,
      subtitle: '% resolved of closed',
      tooltip:
        'Percentage of closed issues that were Resolved vs. Dismissed (Resolved / (Resolved + Dismissed)). Target: >= 90%.',
      dotClass: isResolutionHealthy ? 'bg-emerald-600' : 'bg-amber-500',
      valueClass: isResolutionHealthy ? 'text-emerald-700' : 'text-amber-700',
      cardClass: isResolutionHealthy
        ? 'bg-emerald-50/30 border-emerald-200/80'
        : 'bg-amber-50/30 border-amber-200/80',
    },
    {
      key: 'mtta',
      label: 'Avg. Time to Ack',
      value: formatHours(props.kpi?.avgTimeToAcknowledgeHours),
      subtitle: 'MTTA (hours)',
      tooltip:
        'Average elapsed hours from issue creation to officer acknowledgement for issues acknowledged in this period.',
      dotClass: 'bg-blue-600',
      valueClass: 'text-slate-900',
      cardClass: 'bg-white border-slate-200',
    },
    {
      key: 'mttr',
      label: 'Avg. Time to Resolve',
      value: formatHours(props.kpi?.avgTimeToResolveHours),
      subtitle: 'MTTR (hours)',
      tooltip:
        'Average elapsed hours from creation (or last reopening) to resolution for issues resolved in this period.',
      dotClass: 'bg-indigo-600',
      valueClass: 'text-slate-900',
      cardClass: 'bg-white border-slate-200',
    },
    {
      key: 'detector-pass-rate',
      label: 'Detector Pass Rate',
      value: `${passRate.toFixed(1)}%`,
      subtitle: '% of runs passed',
      tooltip:
        'Percentage of detector inspection runs that passed cleanly without findings or execution failures. Target: >= 98%.',
      dotClass: isPassRateHealthy ? 'bg-emerald-600' : 'bg-rose-600',
      valueClass: isPassRateHealthy ? 'text-emerald-700' : 'text-rose-700',
      cardClass: isPassRateHealthy
        ? 'bg-emerald-50/30 border-emerald-200/80'
        : 'bg-rose-50/40 border-rose-200/90',
    },
    {
      key: 'reopened-issues',
      label: 'Reopened Issues',
      value: reopenedCount.toLocaleString('en-US'),
      subtitle: 'Recurred in period',
      tooltip:
        'Issues that recurred after being marked Resolved or Dismissed within the selected period (BR-PRIMA-002).',
      dotClass: hasReopened ? 'bg-purple-600' : 'bg-slate-400',
      valueClass: hasReopened ? 'text-purple-700' : 'text-slate-900',
      cardClass: hasReopened
        ? 'bg-purple-50/40 border-purple-200/90'
        : 'bg-white border-slate-200',
      badgeText: hasReopened ? 'Recurred' : undefined,
    },
  ];
});
</script>

<template>
  <section aria-label="Historical Reliability KPI Metrics">
    <div
      v-if="isLoading"
      class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3"
      aria-busy="true"
    >
      <div
        v-for="idx in 6"
        :key="idx"
        class="p-3.5 rounded-lg border border-slate-200 bg-white flex flex-col justify-between gap-2.5 shadow-2xs"
      >
        <Skeleton width="70%" height="0.75rem" />
        <Skeleton width="55%" height="1.75rem" />
        <Skeleton width="60%" height="0.65rem" />
      </div>
    </div>

    <div
      v-else
      class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3"
    >
      <div
        v-for="tile in tiles"
        :key="tile.key"
        v-tooltip.bottom="tile.tooltip"
        class="p-3.5 rounded-lg border flex flex-col justify-between shadow-2xs transition-colors"
        :class="tile.cardClass"
      >
        <div class="flex items-center justify-between gap-1.5">
          <span class="text-[11px] font-semibold uppercase tracking-wider text-slate-600 flex items-center gap-1.5 min-w-0">
            <span class="w-2 h-2 rounded-full shrink-0" :class="tile.dotClass"></span>
            <span class="truncate">{{ tile.label }}</span>
          </span>
          <span
            v-if="tile.badgeText"
            class="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-purple-100 text-purple-800 border border-purple-200 shrink-0"
          >
            {{ tile.badgeText }}
          </span>
        </div>

        <div
          class="mt-2 font-mono text-2xl font-bold tabular-nums tracking-tight"
          :class="tile.valueClass"
        >
          {{ tile.value }}
        </div>

        <div class="text-[11px] text-slate-500 mt-0.5 truncate">
          {{ tile.subtitle }}
        </div>
      </div>
    </div>
  </section>
</template>
