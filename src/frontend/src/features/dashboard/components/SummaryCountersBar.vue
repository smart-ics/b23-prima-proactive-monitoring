<script setup lang="ts">
import { useRouter } from 'vue-router';
import type { DashboardSummaryDto } from '@/shared/types/api.generated';

const router = useRouter();

const props = defineProps<{
  summary?: DashboardSummaryDto;
  activeStatus: string;
  activeSeverity: string;
}>();

const emit = defineEmits<{
  (e: 'update:status', value: string): void;
  (e: 'update:severity', value: string): void;
}>();

function toggleCritical() {
  if (props.activeSeverity === 'Critical') {
    emit('update:severity', 'All');
  } else {
    emit('update:severity', 'Critical');
  }
}

function toggleHigh() {
  if (props.activeSeverity === 'High') {
    emit('update:severity', 'All');
  } else {
    emit('update:severity', 'High');
  }
}

function toggleOpen() {
  if (props.activeStatus === 'Open') {
    emit('update:status', 'All');
  } else {
    emit('update:status', 'Open');
  }
}

function toggleReopened() {
  if (props.activeStatus === 'Reopened') {
    emit('update:status', 'All');
  } else {
    emit('update:status', 'Reopened');
  }
}

function goToResolvedToday() {
  router.push({ path: '/history', query: { preset: 'Today', status: 'Resolved' } });
}
</script>

<template>
  <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3" role="region" aria-label="Summary KPI Tiles">
    <!-- 1. Critical Tile (Crimson / Red Accent) -->
    <button
      type="button"
      @click="toggleCritical"
      class="text-left p-3.5 rounded-lg border transition-all flex flex-col justify-between cursor-pointer focus:outline-none focus:ring-2 focus:ring-rose-500/50"
      :class="[
        activeSeverity === 'Critical'
          ? 'bg-rose-50/80 border-rose-400 ring-2 ring-rose-600 shadow-xs'
          : 'bg-white border-slate-200 hover:border-rose-300 hover:bg-rose-50/30'
      ]"
    >
      <div class="flex items-center justify-between">
        <span class="text-[11px] font-semibold uppercase tracking-wider text-rose-700 flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-rose-600"></span>
          Critical
        </span>
        <span
          v-if="activeSeverity === 'Critical'"
          class="text-[10px] font-medium px-1.5 py-0.2 rounded bg-rose-200 text-rose-800"
        >
          Filtered
        </span>
      </div>
      <div class="mt-2 font-mono text-2xl font-bold text-slate-900 tabular-nums">
        {{ summary?.criticalCount ?? 0 }}
      </div>
      <div class="text-[11px] text-slate-500 mt-0.5">Active critical anomalies</div>
    </button>

    <!-- 2. High Tile (Amber / Orange Accent) -->
    <button
      type="button"
      @click="toggleHigh"
      class="text-left p-3.5 rounded-lg border transition-all flex flex-col justify-between cursor-pointer focus:outline-none focus:ring-2 focus:ring-orange-500/50"
      :class="[
        activeSeverity === 'High'
          ? 'bg-orange-50/80 border-orange-400 ring-2 ring-orange-500 shadow-xs'
          : 'bg-white border-slate-200 hover:border-orange-300 hover:bg-orange-50/30'
      ]"
    >
      <div class="flex items-center justify-between">
        <span class="text-[11px] font-semibold uppercase tracking-wider text-orange-700 flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-orange-500"></span>
          High
        </span>
        <span
          v-if="activeSeverity === 'High'"
          class="text-[10px] font-medium px-1.5 py-0.2 rounded bg-orange-200 text-orange-800"
        >
          Filtered
        </span>
      </div>
      <div class="mt-2 font-mono text-2xl font-bold text-slate-900 tabular-nums">
        {{ summary?.highCount ?? 0 }}
      </div>
      <div class="text-[11px] text-slate-500 mt-0.5">High severity issues</div>
    </button>

    <!-- 3. Open Tile (Blue Accent) -->
    <button
      type="button"
      @click="toggleOpen"
      class="text-left p-3.5 rounded-lg border transition-all flex flex-col justify-between cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500/50"
      :class="[
        activeStatus === 'Open'
          ? 'bg-blue-50/80 border-blue-400 ring-2 ring-blue-600 shadow-xs'
          : 'bg-white border-slate-200 hover:border-blue-300 hover:bg-blue-50/30'
      ]"
    >
      <div class="flex items-center justify-between">
        <span class="text-[11px] font-semibold uppercase tracking-wider text-blue-700 flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-blue-600"></span>
          Open
        </span>
        <span
          v-if="activeStatus === 'Open'"
          class="text-[10px] font-medium px-1.5 py-0.2 rounded bg-blue-200 text-blue-800"
        >
          Filtered
        </span>
      </div>
      <div class="mt-2 font-mono text-2xl font-bold text-slate-900 tabular-nums">
        {{ summary?.openCount ?? 0 }}
      </div>
      <div class="text-[11px] text-slate-500 mt-0.5">Unacknowledged feed</div>
    </button>

    <!-- 4. Reopened Tile (Purple Accent) -->
    <button
      type="button"
      @click="toggleReopened"
      class="text-left p-3.5 rounded-lg border transition-all flex flex-col justify-between cursor-pointer focus:outline-none focus:ring-2 focus:ring-purple-500/50"
      :class="[
        activeStatus === 'Reopened'
          ? 'bg-purple-50/80 border-purple-400 ring-2 ring-purple-600 shadow-xs'
          : 'bg-white border-slate-200 hover:border-purple-300 hover:bg-purple-50/30'
      ]"
    >
      <div class="flex items-center justify-between">
        <span class="text-[11px] font-semibold uppercase tracking-wider text-purple-700 flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-purple-600"></span>
          Reopened
        </span>
        <span
          v-if="activeStatus === 'Reopened'"
          class="text-[10px] font-medium px-1.5 py-0.2 rounded bg-purple-200 text-purple-800"
        >
          Filtered
        </span>
      </div>
      <div class="mt-2 font-mono text-2xl font-bold text-slate-900 tabular-nums">
        {{ summary?.reopenedCount ?? 0 }}
      </div>
      <div class="text-[11px] text-slate-500 mt-0.5">Recurred anomalies (BR-PRIMA-002)</div>
    </button>

    <!-- 5. Resolved Today Tile (Green Accent - Deep Link to S-07 History & Reports) -->
    <button
      type="button"
      @click="goToResolvedToday"
      class="text-left p-3.5 rounded-lg border transition-all flex flex-col justify-between cursor-pointer focus:outline-none focus:ring-2 focus:ring-emerald-500/50 bg-white border-slate-200 hover:border-emerald-300 hover:bg-emerald-50/30"
    >
      <div class="flex items-center justify-between">
        <span class="text-[11px] font-semibold uppercase tracking-wider text-emerald-700 flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-emerald-600"></span>
          Resolved Today
        </span>
      </div>
      <div class="mt-2 font-mono text-2xl font-bold text-slate-900 tabular-nums">
        {{ summary?.resolvedTodayCount ?? 0 }}
      </div>
      <div class="text-[11px] text-slate-500 mt-0.5">Verified today (WIB)</div>
    </button>
  </div>
</template>
