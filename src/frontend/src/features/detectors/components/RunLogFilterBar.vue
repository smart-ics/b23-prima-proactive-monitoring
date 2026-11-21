<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(
  defineProps<{
    startDate: string;
    endDate: string;
    outcome: string;
    triggerSource: string;
    totalCount?: number;
    isLoading?: boolean;
  }>(),
  {
    totalCount: undefined,
    isLoading: false,
  }
);

const emit = defineEmits<{
  (e: 'update:startDate', val: string): void;
  (e: 'update:endDate', val: string): void;
  (e: 'update:outcome', val: string): void;
  (e: 'update:triggerSource', val: string): void;
  (e: 'reset'): void;
}>();

const outcomeOptions = [
  { label: 'All Outcomes', value: 'All', badgeClass: 'text-slate-700' },
  { label: 'Passed', value: 'Passed', badgeClass: 'text-emerald-700' },
  { label: 'Finding Discovered', value: 'FindingDiscovered', badgeClass: 'text-amber-700' },
  { label: 'Execution Failed', value: 'ExecutionFailed', badgeClass: 'text-rose-700' },
];

const triggerOptions = [
  { label: 'All Sources', value: 'All' },
  { label: 'Scheduled', value: 'Scheduled' },
  { label: 'Manual', value: 'Manual' },
  { label: 'Automated Re-Run', value: 'AutomatedReRun' },
];

function getUtcDaysAgoString(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().split('T')[0];
}

function getUtcTodayString(): string {
  return new Date().toISOString().split('T')[0];
}

function applyPreset(days: number) {
  emit('update:startDate', getUtcDaysAgoString(days));
  emit('update:endDate', getUtcTodayString());
}

const isDefaultRange = computed(() => {
  const defaultStart = getUtcDaysAgoString(7);
  const defaultEnd = getUtcTodayString();
  return props.startDate === defaultStart && props.endDate === defaultEnd;
});

const isFiltered = computed(() => {
  return (
    props.outcome !== 'All' ||
    props.triggerSource !== 'All' ||
    !isDefaultRange.value
  );
});

function handleReset() {
  emit('update:startDate', getUtcDaysAgoString(7));
  emit('update:endDate', getUtcTodayString());
  emit('update:outcome', 'All');
  emit('update:triggerSource', 'All');
  emit('reset');
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-lg p-4 shadow-2xs space-y-3.5">
    <!-- Top Filter Controls Bar -->
    <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
      <!-- Date Range Controls -->
      <div class="flex flex-wrap items-center gap-2">
        <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider flex items-center gap-1.5 mr-1">
          <i class="pi pi-calendar text-slate-400 text-xs"></i>
          <span>Date Range:</span>
        </span>

        <!-- Quick Presets -->
        <div class="inline-flex rounded-md shadow-2xs p-0.5 bg-slate-100 border border-slate-200">
          <button
            type="button"
            @click="applyPreset(1)"
            class="px-2.5 py-1 text-xs font-medium rounded transition-colors"
            :class="[
              startDate === getUtcDaysAgoString(1) && endDate === getUtcTodayString()
                ? 'bg-white text-slate-900 shadow-2xs font-semibold'
                : 'text-slate-600 hover:text-slate-900'
            ]"
          >
            24h
          </button>
          <button
            type="button"
            @click="applyPreset(7)"
            class="px-2.5 py-1 text-xs font-medium rounded transition-colors"
            :class="[
              isDefaultRange
                ? 'bg-white text-slate-900 shadow-2xs font-semibold'
                : 'text-slate-600 hover:text-slate-900'
            ]"
          >
            Last 7 Days
          </button>
          <button
            type="button"
            @click="applyPreset(30)"
            class="px-2.5 py-1 text-xs font-medium rounded transition-colors"
            :class="[
              startDate === getUtcDaysAgoString(30) && endDate === getUtcTodayString()
                ? 'bg-white text-slate-900 shadow-2xs font-semibold'
                : 'text-slate-600 hover:text-slate-900'
            ]"
          >
            30 Days
          </button>
        </div>

        <!-- Custom Date Range Inputs -->
        <div class="flex items-center gap-1.5 text-xs text-slate-600">
          <input
            type="date"
            :value="startDate"
            @change="emit('update:startDate', ($event.target as HTMLInputElement).value)"
            class="bg-slate-50 border border-slate-300 rounded px-2 py-1 text-xs text-slate-800 focus:outline-none focus:ring-1 focus:ring-slate-900 focus:bg-white transition-colors"
            title="Start Date"
          />
          <span class="text-slate-400 text-xs">to</span>
          <input
            type="date"
            :value="endDate"
            @change="emit('update:endDate', ($event.target as HTMLInputElement).value)"
            class="bg-slate-50 border border-slate-300 rounded px-2 py-1 text-xs text-slate-800 focus:outline-none focus:ring-1 focus:ring-slate-900 focus:bg-white transition-colors"
            title="End Date"
          />
        </div>
      </div>

      <!-- Total Count / Active Filters Status -->
      <div class="flex items-center gap-2 self-start lg:self-center">
        <span
          v-if="totalCount !== undefined"
          class="text-xs text-slate-500 font-medium bg-slate-100 px-2.5 py-1 rounded-full border border-slate-200"
        >
          <span class="font-bold text-slate-800 font-mono">{{ totalCount }}</span>
          {{ totalCount === 1 ? ' run found' : ' runs found' }}
        </span>

        <button
          v-if="isFiltered"
          type="button"
          @click="handleReset"
          class="inline-flex items-center gap-1 text-xs font-medium text-slate-500 hover:text-slate-800 bg-slate-100 hover:bg-slate-200 px-2.5 py-1 rounded transition-colors"
        >
          <i class="pi pi-filter-slash text-[10px]"></i>
          <span>Reset Filters</span>
        </button>
      </div>
    </div>

    <!-- Filter Pills Row (Outcome + Trigger Source) -->
    <div class="pt-2 border-t border-slate-100 flex flex-col md:flex-row md:items-center gap-4">
      <!-- Outcome Filter Pills -->
      <div class="flex flex-wrap items-center gap-1.5">
        <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider mr-1">
          Outcome:
        </span>
        <button
          v-for="opt in outcomeOptions"
          :key="opt.value"
          type="button"
          @click="emit('update:outcome', opt.value)"
          class="px-2.5 py-1 rounded-full text-xs font-medium border transition-colors inline-flex items-center gap-1.5"
          :class="[
            outcome === opt.value
              ? opt.value === 'Passed'
                ? 'bg-emerald-600 text-white border-emerald-600 shadow-2xs font-semibold'
                : opt.value === 'FindingDiscovered'
                ? 'bg-amber-600 text-white border-amber-600 shadow-2xs font-semibold'
                : opt.value === 'ExecutionFailed'
                ? 'bg-rose-600 text-white border-rose-600 shadow-2xs font-semibold'
                : 'bg-slate-900 text-white border-slate-900 shadow-2xs font-semibold'
              : 'bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100 hover:text-slate-800'
          ]"
        >
          <span
            v-if="opt.value !== 'All'"
            class="w-1.5 h-1.5 rounded-full"
            :class="[
              outcome === opt.value
                ? 'bg-white'
                : opt.value === 'Passed'
                ? 'bg-emerald-500'
                : opt.value === 'FindingDiscovered'
                ? 'bg-amber-500'
                : 'bg-rose-500'
            ]"
          ></span>
          {{ opt.label }}
        </button>
      </div>

      <!-- Trigger Source Filter Pills -->
      <div class="flex flex-wrap items-center gap-1.5">
        <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider mr-1">
          Trigger:
        </span>
        <button
          v-for="opt in triggerOptions"
          :key="opt.value"
          type="button"
          @click="emit('update:triggerSource', opt.value)"
          class="px-2.5 py-1 rounded-full text-xs font-medium border transition-colors inline-flex items-center gap-1.5"
          :class="[
            triggerSource === opt.value
              ? 'bg-slate-900 text-white border-slate-900 shadow-2xs font-semibold'
              : 'bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100 hover:text-slate-800'
          ]"
        >
          <i
            v-if="opt.value === 'Scheduled'"
            class="pi pi-clock text-[10px]"
            :class="triggerSource === opt.value ? 'text-white' : 'text-slate-400'"
          ></i>
          <i
            v-else-if="opt.value === 'Manual'"
            class="pi pi-bolt text-[10px]"
            :class="triggerSource === opt.value ? 'text-white' : 'text-slate-400'"
          ></i>
          <i
            v-else-if="opt.value === 'AutomatedReRun'"
            class="pi pi-sync text-[10px]"
            :class="triggerSource === opt.value ? 'text-white' : 'text-slate-400'"
          ></i>
          {{ opt.label }}
        </button>
      </div>
    </div>
  </div>
</template>
