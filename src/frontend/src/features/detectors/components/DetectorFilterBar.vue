<script setup lang="ts">
import { ref, watch, computed, onUnmounted } from 'vue';

const props = defineProps<{
  scope: string;
  adminStatus: string;
  healthStatus: string;
  search: string;
}>();

const emit = defineEmits<{
  (e: 'update:scope', value: string): void;
  (e: 'update:adminStatus', value: string): void;
  (e: 'update:healthStatus', value: string): void;
  (e: 'update:search', value: string): void;
  (e: 'reset'): void;
}>();

const scopeOptions = [
  { label: 'All Scopes', value: 'All' },
  { label: 'Data Quality', value: 'DataQuality' },
  { label: 'Business Logic', value: 'BusinessLogic' },
  { label: 'Infrastructure', value: 'Infrastructure' },
  { label: 'Process Flow', value: 'ProcessFlow' },
  { label: 'Data Integrity', value: 'DataIntegrity' },
  { label: 'Process Integrity', value: 'ProcessIntegrity' },
  { label: 'Data Sync', value: 'DataSync' },
];

const healthOptions = [
  { label: 'All', value: 'All' },
  { label: 'Healthy', value: 'Healthy' },
  { label: 'Failing', value: 'Failing' },
  { label: 'Silent', value: 'Silent' },
];

const adminOptions = [
  { label: 'All', value: 'All' },
  { label: 'Active', value: 'Active' },
  { label: 'Paused', value: 'Paused' },
  { label: 'Retired', value: 'Retired' },
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
  }, 250);
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
    (props.scope && props.scope !== 'All') ||
    (props.adminStatus && props.adminStatus !== 'All') ||
    (props.healthStatus && props.healthStatus !== 'All') ||
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
  <div class="bg-white border border-slate-200 rounded-lg p-3 sm:p-4 shadow-2xs space-y-3">
    <!-- Top Row: Instant Search & Scope Selector -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-3">
      <!-- Search Input -->
      <div class="relative flex-1">
        <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-xs"></i>
        <input
          type="text"
          :value="localSearch"
          @input="onSearchInput"
          placeholder="Instant search by detector name, target module, description..."
          class="w-full pl-9 pr-9 py-2 rounded-md border border-slate-300 text-xs text-slate-800 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500 bg-white"
        />
        <button
          v-if="localSearch"
          type="button"
          @click="clearSearch"
          class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 cursor-pointer p-0.5"
          title="Clear search"
        >
          <i class="pi pi-times text-xs"></i>
        </button>
      </div>

      <!-- Scope Selector -->
      <div class="flex items-center gap-2 self-start md:self-auto shrink-0">
        <label for="scope-selector" class="text-xs font-medium text-slate-500 whitespace-nowrap">
          Scope:
        </label>
        <select
          id="scope-selector"
          :value="scope"
          @change="emit('update:scope', ($event.target as HTMLSelectElement).value)"
          class="py-2 pl-2.5 pr-8 rounded-md border border-slate-300 text-xs text-slate-800 bg-white focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500 cursor-pointer"
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
        class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium text-slate-600 bg-slate-100 hover:bg-slate-200 hover:text-slate-900 transition-colors cursor-pointer self-start md:self-auto"
        title="Reset all filters"
      >
        <i class="pi pi-filter-slash text-xs"></i>
        <span>Reset Filters</span>
      </button>
    </div>

    <!-- Bottom Row: Filter Pills (HealthStatus & AdminStatus) -->
    <div class="pt-2 border-t border-slate-100 flex flex-col lg:flex-row lg:items-center justify-between gap-3">
      <!-- HealthStatus Pills -->
      <div class="flex flex-wrap items-center gap-1.5">
        <span class="text-xs font-medium text-slate-500 mr-1 shrink-0">Health:</span>
        <button
          v-for="opt in healthOptions"
          :key="'health-' + opt.value"
          type="button"
          @click="emit('update:healthStatus', opt.value)"
          class="px-2.5 py-1 rounded-full text-xs font-medium transition-colors cursor-pointer border"
          :class="[
            healthStatus === opt.value
              ? (opt.value === 'Healthy'
                  ? 'bg-emerald-600 text-white border-emerald-600 shadow-2xs'
                  : opt.value === 'Failing'
                    ? 'bg-rose-600 text-white border-rose-600 shadow-2xs'
                    : opt.value === 'Silent'
                      ? 'bg-amber-600 text-white border-amber-600 shadow-2xs'
                      : 'bg-slate-900 text-white border-slate-900 shadow-2xs')
              : 'bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100 hover:text-slate-900'
          ]"
        >
          {{ opt.label }}
        </button>
      </div>

      <!-- AdminStatus Pills -->
      <div class="flex flex-wrap items-center gap-1.5">
        <span class="text-xs font-medium text-slate-500 mr-1 shrink-0">Lifecycle:</span>
        <button
          v-for="opt in adminOptions"
          :key="'admin-' + opt.value"
          type="button"
          @click="emit('update:adminStatus', opt.value)"
          class="px-2.5 py-1 rounded-full text-xs font-medium transition-colors cursor-pointer border"
          :class="[
            adminStatus === opt.value
              ? 'bg-slate-900 text-white border-slate-900 shadow-2xs'
              : 'bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100 hover:text-slate-900'
          ]"
        >
          {{ opt.label }}
        </button>
      </div>
    </div>
  </div>
</template>
