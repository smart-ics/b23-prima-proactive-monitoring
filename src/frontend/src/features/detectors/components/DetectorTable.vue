<script setup lang="ts">
import DataTable, { type DataTableRowClickEvent } from 'primevue/datatable';
import Column from 'primevue/column';
import { AlertTriangle } from 'lucide-vue-next';
import type { DetectorListItemDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';
import {
  formatCadence,
  getPassRateBadgeClass,
  getDetectorStatusBadge,
} from '../composables/useDetectorForm';

defineProps<{
  items: DetectorListItemDto[];
  isLoading?: boolean;
}>();

const emit = defineEmits<{
  (e: 'select', id: string): void;
}>();

function onRowClick(event: DataTableRowClickEvent) {
  const item = event.data as DetectorListItemDto;
  if (item && item.id) {
    emit('select', item.id);
  }
}

function handleViewClick(id: string) {
  emit('select', id);
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-lg shadow-2xs overflow-hidden">
    <DataTable
      :value="items"
      :loading="isLoading"
      scrollable
      scrollHeight="620px"
      :virtualScrollerOptions="{ itemSize: 64 }"
      rowHover
      dataKey="id"
      @row-click="onRowClick"
      class="text-xs"
      tableClass="min-w-full divide-y divide-slate-200"
    >
      <!-- Column 1: Status -->
      <Column header="Status" style="width: 140px; min-width: 130px">
        <template #body="{ data }">
          <span
            class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium border"
            :class="getDetectorStatusBadge(data.adminStatus, data.healthStatus).badgeClass"
          >
            <span
              class="w-2 h-2 rounded-full shrink-0"
              :class="getDetectorStatusBadge(data.adminStatus, data.healthStatus).dotClass"
            ></span>
            <span>{{ getDetectorStatusBadge(data.adminStatus, data.healthStatus).label }}</span>
          </span>
        </template>
      </Column>

      <!-- Column 2: Name & Target Module -->
      <Column header="Detector & Target Module" style="min-width: 260px">
        <template #body="{ data }">
          <div class="py-1 min-w-0">
            <div class="flex items-center gap-2">
              <span class="font-semibold text-slate-900 hover:text-primary-600 transition-colors text-sm truncate cursor-pointer">
                {{ data.name }}
              </span>
              <span class="text-[10px] font-medium px-1.5 py-0.2 rounded bg-slate-100 text-slate-600 border border-slate-200 shrink-0">
                {{ data.scope }}
              </span>
            </div>
            <div class="flex items-center gap-2 mt-0.5">
              <span class="text-[11px] text-slate-400">Module:</span>
              <span class="font-mono text-[11px] font-semibold text-slate-700 bg-slate-50 px-1.5 py-0.2 rounded border border-slate-200">
                {{ data.targetModule }}
              </span>
              <span class="text-[11px] text-slate-400 truncate max-w-xs hidden xl:inline">
                &middot; {{ data.description }}
              </span>
            </div>
          </div>
        </template>
      </Column>

      <!-- Column 3: Cadence -->
      <Column header="Cadence" style="width: 140px; min-width: 130px">
        <template #body="{ data }">
          <div class="flex items-center gap-1.5 flex-wrap">
            <span class="font-mono text-xs text-slate-700 font-medium whitespace-nowrap">
              {{ formatCadence(data.expectedCadenceSeconds) }}
            </span>
            <span
              v-if="data.cadenceDriftWarning"
              v-tooltip.top="data.cadenceDriftWarning"
              class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium bg-amber-50 text-amber-800 border border-amber-300 shadow-2xs cursor-help"
            >
              <AlertTriangle class="w-2.5 h-2.5 text-amber-600 shrink-0" />
              <span>Drift</span>
            </span>
          </div>
        </template>
      </Column>

      <!-- Column 4: Last Heartbeat -->
      <Column header="Last Heartbeat" style="width: 190px; min-width: 180px">
        <template #body="{ data }">
          <div class="leading-tight">
            <span
              class="text-xs text-slate-800 font-medium block truncate"
              :title="formatWibDateTime(data.lastHealthyHeartbeatUtc || data.lastRunAtUtc, true)"
            >
              {{ formatWibDateTime(data.lastHealthyHeartbeatUtc || data.lastRunAtUtc, false) }}
            </span>
            <span class="text-[11px] text-slate-400">
              {{ formatRelativeTime(data.lastHealthyHeartbeatUtc || data.lastRunAtUtc) }}
            </span>
          </div>
        </template>
      </Column>

      <!-- Column 5: 30-Day Clean Pass Rate -->
      <Column header="30d Pass Rate" style="width: 160px; min-width: 150px">
        <template #body="{ data }">
          <div class="flex items-center gap-1.5">
            <span
              class="inline-block px-2 py-0.5 rounded text-xs font-mono font-semibold border"
              :class="getPassRateBadgeClass(data.passRate30d, data.totalRuns30d)"
            >
              {{ Number(data.passRate30d ?? 100).toFixed(1) }}%
            </span>
            <span class="text-[11px] text-slate-400 font-mono">
              ({{ data.totalRuns30d }} runs)
            </span>
          </div>
        </template>
      </Column>

      <!-- Column 6: Active Issues -->
      <Column header="Active Issues" style="width: 120px; min-width: 110px">
        <template #body="{ data }">
          <span
            class="inline-flex items-center justify-center min-w-[28px] px-2 py-0.5 rounded-full text-xs font-mono font-bold"
            :class="[
              data.activeIssuesCount > 0
                ? 'bg-rose-100 text-rose-700 border border-rose-200'
                : 'bg-slate-100 text-slate-500 border border-slate-200'
            ]"
          >
            {{ data.activeIssuesCount }}
          </span>
        </template>
      </Column>

      <!-- Column 7: Action -->
      <Column header="Action" style="width: 90px; min-width: 80px" headerClass="text-right" bodyClass="text-right">
        <template #body="{ data }">
          <button
            type="button"
            @click.stop="handleViewClick(data.id)"
            class="inline-flex items-center gap-1 px-2.5 py-1 rounded text-xs font-medium text-slate-700 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 transition-colors cursor-pointer"
            title="View detector details"
          >
            <span>View</span>
            <i class="pi pi-arrow-right text-[10px]"></i>
          </button>
        </template>
      </Column>

      <!-- Empty State -->
      <template #empty>
        <div class="py-12 text-center text-slate-500">
          <i class="pi pi-shield text-3xl text-slate-300 mb-2 block"></i>
          <p class="text-sm font-medium text-slate-700">No detectors match your search criteria</p>
          <p class="text-xs text-slate-400 mt-1">Try clearing or adjusting your scope, health, or search filters.</p>
        </div>
      </template>
    </DataTable>
  </div>
</template>
