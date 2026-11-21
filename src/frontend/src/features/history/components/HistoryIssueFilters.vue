<script setup lang="ts">
import { computed } from 'vue';
import type { DetectorListItemDto } from '@/shared/types/api.generated';

const props = withDefaults(
  defineProps<{
    status: string;
    scope: string;
    detectorId: string | null;
    severity: string;
    search: string;
    detectors?: DetectorListItemDto[];
    detectorsLoading?: boolean;
  }>(),
  {
    detectors: () => [],
    detectorsLoading: false,
  }
);

const emit = defineEmits<{
  (e: 'update:status', value: string): void;
  (e: 'update:scope', value: string): void;
  (e: 'update:detectorId', value: string | null): void;
  (e: 'update:severity', value: string): void;
  (e: 'update:search', value: string): void;
  (e: 'reset'): void;
}>();

const STATUS_OPTIONS: ReadonlyArray<{ label: string; value: string }> = [
  { label: 'All', value: 'All' },
  { label: 'Resolved', value: 'Resolved' },
  { label: 'Dismissed', value: 'Dismissed' },
  { label: 'Reopened', value: 'Reopened' },
  { label: 'Open', value: 'Open' },
  { label: 'Acknowledged', value: 'Acknowledged' },
  { label: 'Investigating', value: 'Investigating' },
];

const SCOPE_OPTIONS: ReadonlyArray<{ label: string; value: string }> = [
  { label: 'All Scopes', value: 'All' },
  { label: 'Data Integrity', value: 'DataIntegrity' },
  { label: 'Process Integrity', value: 'ProcessIntegrity' },
  { label: 'Data Sync', value: 'DataSync' },
  { label: 'Infrastructure', value: 'Infrastructure' },
];

const SEVERITY_OPTIONS: ReadonlyArray<{ label: string; value: string }> = [
  { label: 'All Severities', value: 'All' },
  { label: 'Critical', value: 'Critical' },
  { label: 'High', value: 'High' },
  { label: 'Medium', value: 'Medium' },
  { label: 'Low', value: 'Low' },
];

const sortedDetectors = computed(() =>
  [...props.detectors].sort((a, b) => a.name.localeCompare(b.name))
);

const isFiltered = computed(() => {
  return (
    (props.status && props.status !== 'All') ||
    (props.scope && props.scope !== 'All') ||
    Boolean(props.detectorId) ||
    (props.severity && props.severity !== 'All') ||
    Boolean(props.search && props.search.trim().length > 0)
  );
});

function onDetectorChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value;
  emit('update:detectorId', value ? value : null);
}

function onSearchInput(event: Event) {
  const value = (event.target as HTMLInputElement).value;
  emit('update:search', value);
}

function clearSearch() {
  emit('update:search', '');
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-lg p-3 sm:p-4 shadow-2xs space-y-3">
    <!-- Row 1: Status Filter Pills + Reset -->
    <div class="flex flex-wrap items-center justify-between gap-2.5">
      <div class="flex flex-wrap items-center gap-1.5" role="group" aria-label="Filter by issue status">
        <span class="text-[11px] font-semibold uppercase tracking-wider text-slate-500 mr-1">
          Status:
        </span>
        <button
          v-for="opt in STATUS_OPTIONS"
          :key="opt.value"
          type="button"
          :aria-pressed="status === opt.value"
          @click="emit('update:status', opt.value)"
          class="px-2.5 py-1 rounded-full text-xs font-medium border transition-colors cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-900"
          :class="[
            status === opt.value
              ? 'bg-slate-900 text-white border-slate-900 shadow-2xs'
              : 'bg-slate-50 text-slate-700 border-slate-200 hover:bg-slate-100 hover:text-slate-900',
          ]"
        >
          {{ opt.label }}
        </button>
      </div>

      <button
        type="button"
        @click="emit('reset')"
        :disabled="!isFiltered"
        class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium border transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
        :class="[
          isFiltered
            ? 'bg-white border-slate-300 text-slate-700 hover:bg-slate-50 hover:text-slate-900'
            : 'bg-slate-50 border-slate-200 text-slate-400',
        ]"
        title="Reset all issue table filters"
      >
        <i class="pi pi-filter-slash text-[11px]" aria-hidden="true"></i>
        <span>Reset Filters</span>
      </button>
    </div>

    <!-- Row 2: Scope, Detector, Severity Dropdowns + Search Input -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-12 gap-2.5 pt-2 border-t border-slate-100">
      <!-- Scope Dropdown -->
      <div class="lg:col-span-2">
        <label for="history-filter-scope" class="sr-only">Monitoring Scope</label>
        <select
          id="history-filter-scope"
          :value="scope"
          @change="emit('update:scope', ($event.target as HTMLSelectElement).value)"
          class="w-full text-xs font-medium bg-slate-50 border border-slate-300 rounded-md px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:bg-white transition-colors"
        >
          <option v-for="opt in SCOPE_OPTIONS" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </option>
        </select>
      </div>

      <!-- Detector Dropdown -->
      <div class="lg:col-span-4">
        <label for="history-filter-detector" class="sr-only">Detector</label>
        <select
          id="history-filter-detector"
          :value="detectorId ?? ''"
          :disabled="detectorsLoading && sortedDetectors.length === 0"
          @change="onDetectorChange"
          class="w-full text-xs font-medium bg-slate-50 border border-slate-300 rounded-md px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:bg-white transition-colors disabled:opacity-60"
        >
          <option value="">
            {{ detectorsLoading && sortedDetectors.length === 0 ? 'Loading detectors...' : 'All Detectors' }}
          </option>
          <option v-for="det in sortedDetectors" :key="det.id" :value="det.id">
            {{ det.name }} ({{ det.targetModule }})
          </option>
        </select>
      </div>

      <!-- Severity Dropdown -->
      <div class="lg:col-span-2">
        <label for="history-filter-severity" class="sr-only">Severity</label>
        <select
          id="history-filter-severity"
          :value="severity"
          @change="emit('update:severity', ($event.target as HTMLSelectElement).value)"
          class="w-full text-xs font-medium bg-slate-50 border border-slate-300 rounded-md px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:bg-white transition-colors"
        >
          <option v-for="opt in SEVERITY_OPTIONS" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </option>
        </select>
      </div>

      <!-- Search Input with Instant Clear -->
      <div class="lg:col-span-4 relative">
        <label for="history-filter-search" class="sr-only">Search historical issues</label>
        <i
          class="pi pi-search text-xs text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2 pointer-events-none"
          aria-hidden="true"
        ></i>
        <input
          id="history-filter-search"
          type="text"
          :value="search"
          @input="onSearchInput"
          placeholder="Search title, detector, or correlation key..."
          class="w-full text-xs bg-slate-50 border border-slate-300 rounded-md pl-8 pr-8 py-1.5 text-slate-800 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:bg-white transition-colors"
        />
        <button
          v-if="search && search.length > 0"
          type="button"
          @click="clearSearch"
          aria-label="Clear search"
          title="Clear search"
          class="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-700 p-0.5 cursor-pointer"
        >
          <i class="pi pi-times text-xs" aria-hidden="true"></i>
        </button>
      </div>
    </div>
  </div>
</template>
