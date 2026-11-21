<script setup lang="ts">
import type { DetectorSummaryDto } from '@/shared/types/api.generated';

const props = defineProps<{
  summary?: DetectorSummaryDto | null;
  activeFilter?: string | null;
  isLoading?: boolean;
}>();

const emit = defineEmits<{
  (e: 'filter-change', filter: 'Healthy' | 'Failing' | 'Silent' | 'Paused' | null): void;
}>();

function handleChipClick(filterKey: 'Healthy' | 'Failing' | 'Silent' | 'Paused') {
  if (props.activeFilter === filterKey) {
    emit('filter-change', null);
  } else {
    emit('filter-change', filterKey);
  }
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-lg p-3 sm:p-4 shadow-2xs">
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-3">
      <div class="flex items-center gap-2">
        <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Health Overview</span>
        <span
          v-if="summary"
          class="font-mono text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-700 border border-slate-200 font-medium"
        >
          {{ summary.totalCount }} total {{ summary.totalCount === 1 ? 'detector' : 'detectors' }}
        </span>
      </div>

      <div
        v-if="props.activeFilter"
        class="flex items-center gap-2 self-start sm:self-auto"
      >
        <span class="text-xs text-slate-500">
          Filtered by: <strong class="text-slate-800">{{ props.activeFilter }}</strong>
        </span>
        <button
          type="button"
          @click="emit('filter-change', null)"
          class="text-xs text-primary-600 hover:text-primary-800 font-medium hover:underline cursor-pointer"
        >
          Clear filter
        </button>
      </div>
    </div>

    <!-- 4 Interactive Count Chips -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-2.5">
      <!-- 1. Healthy Chip -->
      <button
        type="button"
        @click="handleChipClick('Healthy')"
        :disabled="isLoading"
        class="flex items-center justify-between p-3 rounded-lg border text-left transition-all cursor-pointer select-none"
        :class="[
          activeFilter === 'Healthy'
            ? 'bg-emerald-50 border-emerald-500 ring-2 ring-emerald-500/20 shadow-xs'
            : 'bg-slate-50/50 border-slate-200 hover:bg-emerald-50/40 hover:border-emerald-300'
        ]"
      >
        <div class="flex items-center gap-2.5 min-w-0">
          <span class="w-3 h-3 rounded-full bg-emerald-500 shrink-0"></span>
          <div class="truncate">
            <span class="text-xs font-medium text-slate-600 block">Healthy</span>
            <span class="text-lg font-bold text-slate-900 leading-tight">
              {{ summary?.healthyCount ?? 0 }}
            </span>
          </div>
        </div>
        <i
          v-if="activeFilter === 'Healthy'"
          class="pi pi-check text-emerald-600 text-xs shrink-0"
        ></i>
      </button>

      <!-- 2. Failing Chip -->
      <button
        type="button"
        @click="handleChipClick('Failing')"
        :disabled="isLoading"
        class="flex items-center justify-between p-3 rounded-lg border text-left transition-all cursor-pointer select-none"
        :class="[
          activeFilter === 'Failing'
            ? 'bg-rose-50 border-rose-500 ring-2 ring-rose-500/20 shadow-xs'
            : (summary && summary.failingCount > 0)
              ? 'bg-rose-50/40 border-rose-200 hover:bg-rose-50 hover:border-rose-300'
              : 'bg-slate-50/50 border-slate-200 hover:bg-rose-50/40 hover:border-rose-300'
        ]"
      >
        <div class="flex items-center gap-2.5 min-w-0">
          <span
            class="w-3 h-3 rounded-full bg-rose-500 shrink-0"
            :class="{ 'animate-pulse': summary && summary.failingCount > 0 }"
          ></span>
          <div class="truncate">
            <span class="text-xs font-medium text-slate-600 block">Failing</span>
            <span
              class="text-lg font-bold leading-tight"
              :class="summary && summary.failingCount > 0 ? 'text-rose-700' : 'text-slate-900'"
            >
              {{ summary?.failingCount ?? 0 }}
            </span>
          </div>
        </div>
        <i
          v-if="activeFilter === 'Failing'"
          class="pi pi-check text-rose-600 text-xs shrink-0"
        ></i>
      </button>

      <!-- 3. Silent Chip -->
      <button
        type="button"
        @click="handleChipClick('Silent')"
        :disabled="isLoading"
        class="flex items-center justify-between p-3 rounded-lg border text-left transition-all cursor-pointer select-none"
        :class="[
          activeFilter === 'Silent'
            ? 'bg-amber-50 border-amber-500 ring-2 ring-amber-500/20 shadow-xs'
            : (summary && summary.silentCount > 0)
              ? 'bg-amber-50/40 border-amber-200 hover:bg-amber-50 hover:border-amber-300'
              : 'bg-slate-50/50 border-slate-200 hover:bg-amber-50/40 hover:border-amber-300'
        ]"
      >
        <div class="flex items-center gap-2.5 min-w-0">
          <span
            class="w-3 h-3 rounded-full bg-amber-500 shrink-0"
            :class="{ 'animate-pulse': summary && summary.silentCount > 0 }"
          ></span>
          <div class="truncate">
            <span class="text-xs font-medium text-slate-600 block">Silent</span>
            <span
              class="text-lg font-bold leading-tight"
              :class="summary && summary.silentCount > 0 ? 'text-amber-700' : 'text-slate-900'"
            >
              {{ summary?.silentCount ?? 0 }}
            </span>
          </div>
        </div>
        <i
          v-if="activeFilter === 'Silent'"
          class="pi pi-check text-amber-600 text-xs shrink-0"
        ></i>
      </button>

      <!-- 4. Paused Chip -->
      <button
        type="button"
        @click="handleChipClick('Paused')"
        :disabled="isLoading"
        class="flex items-center justify-between p-3 rounded-lg border text-left transition-all cursor-pointer select-none"
        :class="[
          activeFilter === 'Paused'
            ? 'bg-slate-100 border-slate-500 ring-2 ring-slate-500/20 shadow-xs'
            : 'bg-slate-50/50 border-slate-200 hover:bg-slate-100 hover:border-slate-300'
        ]"
      >
        <div class="flex items-center gap-2.5 min-w-0">
          <span class="w-3 h-3 rounded-full bg-slate-400 shrink-0"></span>
          <div class="truncate">
            <span class="text-xs font-medium text-slate-600 block">Paused</span>
            <span class="text-lg font-bold text-slate-900 leading-tight">
              {{ summary?.pausedCount ?? 0 }}
            </span>
          </div>
        </div>
        <i
          v-if="activeFilter === 'Paused'"
          class="pi pi-check text-slate-600 text-xs shrink-0"
        ></i>
      </button>
    </div>
  </div>
</template>
