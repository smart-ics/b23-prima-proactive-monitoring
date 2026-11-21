<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRouter } from 'vue-router';
import {
  useDetectorListQuery,
  useDetectorSummaryQuery,
} from '../api/useDetectorQueries';
import {
  HealthSummaryBar,
  DetectorFilterBar,
  DetectorTable,
  DetectorCard,
} from '../components';
import type { DetectorListQueryParams } from '../api/detector.keys';

const router = useRouter();

// Filter State
const filters = ref<DetectorListQueryParams>({
  scope: 'All',
  adminStatus: 'All',
  healthStatus: 'All',
  search: '',
});

// View mode: 'table' | 'cards'
const viewMode = ref<'table' | 'cards'>('table');

// TanStack Queries (15s polling built into useDetectorQueries)
const {
  data: detectors,
  isPending: isListPending,
  isFetching: isListFetching,
  isError: isListError,
  error: listError,
  refetch: refetchList,
} = useDetectorListQuery(filters);

const {
  data: summary,
  isPending: isSummaryPending,
  isFetching: isSummaryFetching,
  refetch: refetchSummary,
} = useDetectorSummaryQuery();

const isRefreshing = computed(() => isListFetching.value || isSummaryFetching.value);

// Compute active filter string for HealthSummaryBar
const activeHealthChip = computed<string | null>(() => {
  if (filters.value.adminStatus === 'Paused') {
    return 'Paused';
  }
  if (
    filters.value.healthStatus &&
    ['Healthy', 'Failing', 'Silent'].includes(filters.value.healthStatus)
  ) {
    return filters.value.healthStatus;
  }
  return null;
});

// Handle chip clicks from HealthSummaryBar
function handleHealthChipFilter(chip: 'Healthy' | 'Failing' | 'Silent' | 'Paused' | null) {
  if (!chip) {
    // Clear health & admin filters
    filters.value.healthStatus = 'All';
    if (filters.value.adminStatus === 'Paused') {
      filters.value.adminStatus = 'All';
    }
  } else if (chip === 'Paused') {
    filters.value.adminStatus = 'Paused';
    filters.value.healthStatus = 'All';
  } else {
    filters.value.healthStatus = chip;
    if (filters.value.adminStatus === 'Paused') {
      filters.value.adminStatus = 'All';
    }
  }
}

// Reset all filters
function handleResetFilters() {
  filters.value = {
    scope: 'All',
    adminStatus: 'All',
    healthStatus: 'All',
    search: '',
  };
}

// Navigation actions
function handleRegisterClick() {
  router.push('/detectors/new');
}

function handleSelectDetector(id: string) {
  router.push(`/detectors/${id}`);
}

function handleManualRefresh() {
  refetchList();
  refetchSummary();
}
</script>

<template>
  <div class="space-y-4">
    <!-- Top Page Header Bar -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
      <div>
        <h1 class="text-lg font-semibold text-slate-900 leading-snug">Detector Portfolio</h1>
        <p class="text-xs text-slate-500">
          Proactive telemetry probes, cadence monitoring, and reliability pass rates (S-03)
        </p>
      </div>

      <!-- Header Actions: Live Polling Indicator, Refresh Button, Register CTA -->
      <div class="flex items-center gap-2 self-start sm:self-auto">
        <!-- Live 15s Polling Badge -->
        <div class="inline-flex items-center gap-2 px-2.5 py-1 rounded-md text-xs font-mono bg-white border border-slate-200 text-slate-700 shadow-2xs">
          <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
          <span>Live · 15s</span>
          <i
            class="pi pi-sync text-[10px] text-slate-400"
            :class="{ 'animate-spin text-slate-600': isRefreshing }"
            title="Active 15-second background polling"
          ></i>
        </div>

        <!-- Manual Refresh Button -->
        <button
          type="button"
          @click="handleManualRefresh"
          :disabled="isRefreshing"
          class="p-1.5 rounded-md bg-white border border-slate-200 text-slate-600 hover:text-slate-900 hover:bg-slate-50 disabled:opacity-50 transition-colors cursor-pointer"
          title="Refresh portfolio now"
        >
          <i class="pi pi-refresh text-xs" :class="{ 'animate-spin': isRefreshing }"></i>
        </button>

        <!-- View Mode Switcher -->
        <div class="hidden sm:inline-flex items-center p-0.5 rounded-md bg-slate-100 border border-slate-200">
          <button
            type="button"
            @click="viewMode = 'table'"
            class="px-2 py-1 rounded text-xs font-medium transition-colors cursor-pointer"
            :class="[
              viewMode === 'table'
                ? 'bg-white text-slate-900 shadow-2xs font-semibold'
                : 'text-slate-500 hover:text-slate-800'
            ]"
            title="Table View"
          >
            <i class="pi pi-bars text-xs"></i>
          </button>
          <button
            type="button"
            @click="viewMode = 'cards'"
            class="px-2 py-1 rounded text-xs font-medium transition-colors cursor-pointer"
            :class="[
              viewMode === 'cards'
                ? 'bg-white text-slate-900 shadow-2xs font-semibold'
                : 'text-slate-500 hover:text-slate-800'
            ]"
            title="Grid Cards View"
          >
            <i class="pi pi-th-large text-xs"></i>
          </button>
        </div>

        <!-- + Register Detector CTA Button -->
        <button
          type="button"
          @click="handleRegisterClick"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-slate-900 hover:bg-slate-800 text-white text-xs font-medium transition-colors shadow-2xs cursor-pointer"
        >
          <i class="pi pi-plus text-xs"></i>
          <span>Register Detector</span>
        </button>
      </div>
    </div>

    <!-- 4-State Health Summary Bar -->
    <HealthSummaryBar
      :summary="summary"
      :active-filter="activeHealthChip"
      :is-loading="isSummaryPending"
      @filter-change="handleHealthChipFilter"
    />

    <!-- Multi-Attribute Filter Bar -->
    <DetectorFilterBar
      :scope="filters.scope ?? 'All'"
      :admin-status="filters.adminStatus ?? 'All'"
      :health-status="filters.healthStatus ?? 'All'"
      :search="filters.search ?? ''"
      @update:scope="filters.scope = $event"
      @update:admin-status="filters.adminStatus = $event"
      @update:health-status="filters.healthStatus = $event"
      @update:search="filters.search = $event"
      @reset="handleResetFilters"
    />

    <!-- Error State -->
    <div
      v-if="isListError"
      class="bg-rose-50 border border-rose-200 rounded-lg p-4 text-rose-800 text-xs flex items-center justify-between"
    >
      <div class="flex items-center gap-2">
        <i class="pi pi-exclamation-triangle text-rose-600 text-base"></i>
        <span>
          Failed to load detector portfolio: {{ listError?.message || 'Network error' }}
        </span>
      </div>
      <button
        type="button"
        @click="() => refetchList()"
        class="px-2.5 py-1 rounded bg-rose-600 text-white font-medium hover:bg-rose-700 transition-colors cursor-pointer"
      >
        Retry
      </button>
    </div>

    <!-- Data Display: Table or Cards View -->
    <div v-else>
      <!-- Table View -->
      <div v-if="viewMode === 'table'" class="hidden md:block">
        <DetectorTable
          :items="detectors ?? []"
          :is-loading="isListPending"
          @select="handleSelectDetector"
        />
      </div>

      <!-- Grid Cards View (Default on mobile/tablet or when toggled) -->
      <div
        :class="[
          viewMode === 'cards' ? 'block' : 'block md:hidden'
        ]"
      >
        <!-- Loading skeletons -->
        <div
          v-if="isListPending"
          class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3"
        >
          <div
            v-for="n in 6"
            :key="n"
            class="bg-white border border-slate-200 rounded-lg p-4 h-48 animate-pulse"
          >
            <div class="h-4 bg-slate-200 rounded w-1/3 mb-3"></div>
            <div class="h-5 bg-slate-200 rounded w-3/4 mb-2"></div>
            <div class="h-3 bg-slate-200 rounded w-full mb-4"></div>
            <div class="h-8 bg-slate-100 rounded w-full"></div>
          </div>
        </div>

        <!-- Empty State for Cards -->
        <div
          v-else-if="!detectors || detectors.length === 0"
          class="bg-white border border-slate-200 rounded-lg p-12 text-center text-slate-500 shadow-2xs"
        >
          <i class="pi pi-shield text-3xl text-slate-300 mb-2 block"></i>
          <p class="text-sm font-medium text-slate-700">No detectors found</p>
          <p class="text-xs text-slate-400 mt-1">
            Try adjusting your filters, or register a new detector probe.
          </p>
          <button
            type="button"
            @click="handleResetFilters"
            class="mt-3 px-3 py-1.5 rounded-md bg-slate-100 text-slate-700 hover:bg-slate-200 text-xs font-medium cursor-pointer"
          >
            Reset Filters
          </button>
        </div>

        <!-- Render Detector Cards -->
        <div
          v-else
          class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3"
        >
          <DetectorCard
            v-for="item in detectors"
            :key="item.id"
            :detector="item"
            @select="handleSelectDetector"
          />
        </div>
      </div>
    </div>
  </div>
</template>
