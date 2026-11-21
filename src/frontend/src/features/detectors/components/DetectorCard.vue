<script setup lang="ts">
import type { DetectorListItemDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';
import {
  formatCadence,
  getPassRateBadgeClass,
  getDetectorStatusBadge,
} from '../composables/useDetectorForm';

const props = defineProps<{
  detector: DetectorListItemDto;
}>();

const emit = defineEmits<{
  (e: 'select', id: string): void;
}>();

function handleClick() {
  emit('select', props.detector.id);
}
</script>

<template>
  <div
    @click="handleClick"
    class="bg-white border border-slate-200 rounded-lg p-4 shadow-2xs hover:shadow-md hover:border-slate-300 transition-all cursor-pointer flex flex-col justify-between group"
  >
    <!-- Top Row: Status Badge & Scope Badge -->
    <div class="flex items-center justify-between gap-2 mb-2.5">
      <!-- Status Badge -->
      <span
        class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium border"
        :class="getDetectorStatusBadge(detector.adminStatus, detector.healthStatus).badgeClass"
      >
        <span
          class="w-2 h-2 rounded-full shrink-0"
          :class="getDetectorStatusBadge(detector.adminStatus, detector.healthStatus).dotClass"
        ></span>
        <span>{{ getDetectorStatusBadge(detector.adminStatus, detector.healthStatus).label }}</span>
      </span>

      <!-- Scope Badge -->
      <span class="text-[11px] font-medium px-2 py-0.5 rounded bg-slate-100 text-slate-600 border border-slate-200">
        {{ detector.scope }}
      </span>
    </div>

    <!-- Title & Target Module -->
    <div class="mb-3">
      <div class="flex items-start justify-between gap-2">
        <h3 class="text-sm font-semibold text-slate-900 group-hover:text-primary-600 transition-colors leading-snug line-clamp-1">
          {{ detector.name }}
        </h3>
      </div>
      <p class="text-xs text-slate-500 line-clamp-2 mt-1 leading-relaxed">
        {{ detector.description }}
      </p>
      <div class="mt-2 flex items-center gap-1.5">
        <span class="text-[11px] text-slate-400">Target Module:</span>
        <span class="font-mono text-[11px] font-semibold text-slate-700 bg-slate-100 px-1.5 py-0.2 rounded border border-slate-200">
          {{ detector.targetModule }}
        </span>
      </div>
    </div>

    <!-- Metrics Grid -->
    <div class="grid grid-cols-2 gap-2 py-2.5 my-2 border-y border-slate-100 text-xs">
      <!-- Cadence -->
      <div>
        <span class="text-[11px] text-slate-400 block">Cadence</span>
        <span class="font-mono font-medium text-slate-700">
          {{ formatCadence(detector.expectedCadenceSeconds) }}
        </span>
      </div>

      <!-- Last Heartbeat -->
      <div>
        <span class="text-[11px] text-slate-400 block">Last Heartbeat</span>
        <span
          class="text-slate-700 font-medium truncate block"
          :title="formatWibDateTime(detector.lastHealthyHeartbeatUtc || detector.lastRunAtUtc, true)"
        >
          {{ formatRelativeTime(detector.lastHealthyHeartbeatUtc || detector.lastRunAtUtc) }}
        </span>
      </div>

      <!-- 30-Day Pass Rate -->
      <div>
        <span class="text-[11px] text-slate-400 block">30d Pass Rate</span>
        <span
          class="inline-block px-1.5 py-0.2 rounded text-[11px] font-mono font-semibold border mt-0.5"
          :class="getPassRateBadgeClass(detector.passRate30d, detector.totalRuns30d)"
        >
          {{ Number(detector.passRate30d ?? 100).toFixed(1) }}%
          <span class="text-[10px] opacity-75">({{ detector.totalRuns30d }} runs)</span>
        </span>
      </div>

      <!-- Active Issues -->
      <div>
        <span class="text-[11px] text-slate-400 block">Active Issues</span>
        <span
          class="inline-block px-2 py-0.2 rounded text-[11px] font-mono font-bold mt-0.5"
          :class="[
            detector.activeIssuesCount > 0
              ? 'bg-rose-100 text-rose-700 border border-rose-200'
              : 'bg-slate-100 text-slate-600 border border-slate-200'
          ]"
        >
          {{ detector.activeIssuesCount }}
        </span>
      </div>
    </div>

    <!-- Card Action Footer -->
    <div class="flex items-center justify-between pt-1">
      <span class="text-[11px] text-slate-400 font-mono">
        {{ detector.webhookUrl ? 'Webhook probe' : 'Internal probe' }}
      </span>
      <button
        type="button"
        @click.stop="handleClick"
        class="inline-flex items-center gap-1 text-xs font-medium text-primary-600 group-hover:text-primary-700 cursor-pointer"
      >
        <span>View</span>
        <i class="pi pi-arrow-right text-[10px]"></i>
      </button>
    </div>
  </div>
</template>
