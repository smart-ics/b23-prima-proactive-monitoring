<script setup lang="ts">
import { ref, watch, computed, onUnmounted } from 'vue';

const props = defineProps<{
  status: string;
  severity: string;
  scope: string;
  search: string;
}>();

const emit = defineEmits<{
  (e: 'update:status', value: string): void;
  (e: 'update:severity', value: string): void;
  (e: 'update:scope', value: string): void;
  (e: 'update:search', value: string): void;
  (e: 'reset'): void;
}>();

const statusOptions = [
  { label: 'All Statuses', value: 'All' },
  { label: 'Open', value: 'Open' },
  { label: 'Acknowledged', value: 'Acknowledged' },
  { label: 'Investigating', value: 'Investigating' },
  { label: 'Reopened', value: 'Reopened' },
];

const severityOptions = [
  { label: 'All Severities', value: 'All' },
  { label: 'Critical', value: 'Critical' },
  { label: 'High', value: 'High' },
  { label: 'Medium', value: 'Medium' },
  { label: 'Low', value: 'Low' },
];

const scopeOptions = [
  { label: 'All Scopes', value: 'All' },
  { label: 'Data Integrity', value: 'DataIntegrity' },
  { label: 'Process Integrity', value: 'ProcessIntegrity' },
  { label: 'Data Sync', value: 'DataSync' },
  { label: 'Infrastructure', value: 'Infrastructure' },
];

const localSearch = ref(props.search);
let debounceTimer: ReturnType<typeof setTimeout> | null = null;

watch(
  () => props.search,
  (newVal) => {
    if (newVal !== localSearch.value) {
      localSearch.value = newVal;
    }
  }
);

function onSearchInput(event: Event) {
  const target = event.target as HTMLInputElement;
  localSearch.value = target.value;
  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }
  debounceTimer = setTimeout(() => {
    emit('update:search', localSearch.value);
  }, 300);
}

function clearSearch() {
  localSearch.value = '';
  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }
  emit('update:search', '');
}

onUnmounted(() => {
  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }
});

const isFiltered = computed(() => {
  return (
    (props.status && props.status !== 'All') ||
    (props.severity && props.severity !== 'All') ||
    (props.scope && props.scope !== 'All') ||
    Boolean(props.search && props.search.trim().length > 0)
  );
});

function handleReset() {
  localSearch.value = '';
  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }
  emit('reset');
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-lg p-3 sm:p-4 shadow-2xs">
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-3">
      <!-- Left: Filter Dropdowns Group -->
      <div class="flex flex-wrap items-center gap-2.5">
        <!-- Status Dropdown (Excludes Resolved/Dismissed) -->
        <div class="flex items-center gap-1.5">
          <label for="filter-status" class="text-xs font-semibold text-slate-500 uppercase tracking-wider hidden xl:inline">Status:</label>
          <select
            id="filter-status"
            :value="status"
            @change="emit('update:status', ($event.target as HTMLSelectElement).value)"
            class="text-xs font-medium bg-slate-50 border border-slate-300 rounded-md px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:bg-white transition-colors"
          >
            <option v-for="opt in statusOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>

        <!-- Severity Dropdown -->
        <div class="flex items-center gap-1.5">
          <label for="filter-severity" class="text-xs font-semibold text-slate-500 uppercase tracking-wider hidden xl:inline">Severity:</label>
          <select
            id="filter-severity"
            :value="severity"
            @change="emit('update:severity', ($event.target as HTMLSelectElement).value)"
            class="text-xs font-medium bg-slate-50 border border-slate-300 rounded-md px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:bg-white transition-colors"
          >
            <option v-for="opt in severityOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>

        <!-- Scope Dropdown -->
        <div class="flex items-center gap-1.5">
          <label for="filter-scope" class="text-xs font-semibold text-slate-500 uppercase tracking-wider hidden xl:inline">Scope:</label>
          <select
            id="filter-scope"
            :value="scope"
            @change="emit('update:scope', ($event.target as HTMLSelectElement).value)"
            class="text-xs font-medium bg-slate-50 border border-slate-300 rounded-md px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:bg-white transition-colors"
          >
            <option v-for="opt in scopeOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>

        <!-- Reset Filters Button -->
        <button
          v-if="isFiltered"
          type="button"
          @click="handleReset"
          class="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-md text-xs font-medium text-slate-600 hover:text-rose-700 hover:bg-rose-50 border border-slate-200 hover:border-rose-200 transition-colors cursor-pointer"
          title="Reset all filters to default"
        >
          <i class="pi pi-filter-slash text-[11px]"></i>
          <span>Reset Filters</span>
        </button>
      </div>

      <!-- Right: Debounced Search Input -->
      <div class="relative w-full md:w-72 lg:w-80">
        <div class="absolute inset-y-0 left-0 pl-2.5 flex items-center pointer-events-none text-slate-400">
          <i class="pi pi-search text-xs"></i>
        </div>
        <input
          type="text"
          :value="localSearch"
          @input="onSearchInput"
          placeholder="Search title, detector, correlation key..."
          class="w-full text-xs font-medium bg-slate-50 border border-slate-300 rounded-md pl-8 pr-7 py-1.5 text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:bg-white transition-colors"
        />
        <button
          v-if="localSearch"
          type="button"
          @click="clearSearch"
          class="absolute inset-y-0 right-0 pr-2.5 flex items-center text-slate-400 hover:text-slate-700 cursor-pointer"
          title="Clear search"
        >
          <i class="pi pi-times text-xs"></i>
        </button>
      </div>
    </div>
  </div>
</template>
