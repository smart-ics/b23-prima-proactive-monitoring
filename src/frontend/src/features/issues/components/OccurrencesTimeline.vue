<script setup lang="ts">
import { computed } from 'vue';
import { History, ChevronDown, ChevronRight, Copy, Check, FileText } from 'lucide-vue-next';
import { ref } from 'vue';
import type { OccurrenceItemDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

const props = defineProps<{
  occurrences: OccurrenceItemDto[];
  expandedOccurrenceIds: Set<string>;
}>();

const emit = defineEmits<{
  (e: 'toggle-occurrence', id: string): void;
}>();

// Sort occurrences newest-first (#N, #N-1, ...)
const sortedOccurrences = computed(() => {
  if (!props.occurrences) return [];
  return [...props.occurrences].sort((a, b) => {
    // Primary: descending occurrenceNumber
    if (b.occurrenceNumber !== a.occurrenceNumber) {
      return b.occurrenceNumber - a.occurrenceNumber;
    }
    // Secondary: descending timestampUtc
    return new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime();
  });
});

const copiedTraceId = ref<string | null>(null);

async function copyTrace(id: string, trace?: string | null) {
  if (!trace) return;
  try {
    await navigator.clipboard.writeText(trace);
    copiedTraceId.value = id;
    setTimeout(() => {
      if (copiedTraceId.value === id) {
        copiedTraceId.value = null;
      }
    }, 2000);
  } catch (err) {
    console.error('Failed to copy occurrence trace', err);
  }
}

const idrFormatter = new Intl.NumberFormat('id-ID', {
  style: 'currency',
  currency: 'IDR',
  maximumFractionDigits: 2,
});

function formatIdr(value?: number | null): string {
  if (value === undefined || value === null) return '-';
  return idrFormatter.format(value);
}
</script>

<template>
  <div class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm space-y-4">
    <!-- Header -->
    <div class="flex items-center justify-between pb-3 border-b border-slate-200">
      <div class="flex items-center gap-2">
        <History class="w-4 h-4 text-indigo-600" />
        <h2 class="text-sm font-semibold text-slate-900">Occurrences Timeline</h2>
      </div>
      <span class="text-xs font-medium text-slate-500">
        {{ occurrences.length }} total {{ occurrences.length === 1 ? 'occurrence' : 'occurrences' }}
      </span>
    </div>

    <!-- Empty State -->
    <div v-if="!occurrences || occurrences.length === 0" class="py-6 text-center text-sm text-slate-500">
      No occurrences logged.
    </div>

    <!-- Occurrences List (Sorted Newest-First) -->
    <div v-else class="space-y-2.5">
      <div
        v-for="occ in sortedOccurrences"
        :key="occ.id"
        class="border border-slate-200 rounded-lg bg-white overflow-hidden transition-colors"
      >
        <!-- Occurrence Row Accordion Header -->
        <button
          type="button"
          @click="emit('toggle-occurrence', occ.id)"
          class="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-slate-50 transition-colors focus:outline-none focus:ring-1 focus:ring-indigo-500"
        >
          <div class="flex items-center gap-3 min-w-0">
            <span
              class="flex items-center justify-center w-6 h-6 rounded bg-slate-100 text-slate-700 font-mono text-xs font-bold border border-slate-300 shrink-0"
            >
              #{{ occ.occurrenceNumber }}
            </span>
            <div class="min-w-0">
              <span class="text-sm font-medium text-slate-900 block truncate">
                {{ occ.module }} <span v-if="occ.sourceTable" class="text-slate-500 font-mono text-xs">({{ occ.sourceTable }})</span>
              </span>
              <span class="text-xs text-slate-500">
                {{ formatRelativeTime(occ.timestampUtc) }} ·
                <span class="text-slate-400">{{ formatWibDateTime(occ.timestampUtc, true) }}</span>
              </span>
            </div>
          </div>

          <div class="flex items-center gap-2.5 shrink-0 ml-2">
            <span
              v-if="occ.varianceDelta && occ.varianceDelta !== 0"
              class="text-xs font-mono font-semibold text-rose-600 hidden sm:inline"
            >
              {{ formatIdr(occ.varianceDelta) }}
            </span>
            <component
              :is="expandedOccurrenceIds.has(occ.id) ? ChevronDown : ChevronRight"
              class="w-4 h-4 text-slate-500"
            />
          </div>
        </button>

        <!-- Inline Collapsible Evidence Detail -->
        <div
          v-if="expandedOccurrenceIds.has(occ.id)"
          class="px-4 pb-4 pt-2 border-t border-slate-200 bg-slate-50 space-y-3"
        >
          <!-- Meta details -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs">
            <div class="space-y-0.5">
              <span class="text-slate-500 uppercase tracking-wider text-[10px]">Record Keys</span>
              <p class="font-mono text-slate-900 truncate" :title="occ.recordKeys ?? undefined">
                {{ occ.recordKeys || '-' }}
              </p>
            </div>
            <div class="space-y-0.5">
              <span class="text-slate-500 uppercase tracking-wider text-[10px]">Inspection Run ID</span>
              <p class="font-mono text-slate-900 truncate" :title="occ.inspectionRunId">
                {{ occ.inspectionRunId }}
              </p>
            </div>
          </div>

          <!-- Financial breakdown if present -->
          <div
            v-if="occ.debitTotal !== null || occ.creditTotal !== null || occ.varianceDelta !== null"
            class="p-2.5 rounded bg-white border border-slate-200 grid grid-cols-3 gap-2 text-xs"
          >
            <div>
              <span class="text-[10px] text-slate-500 block">Debit</span>
              <span class="font-mono text-slate-900">{{ formatIdr(occ.debitTotal) }}</span>
            </div>
            <div>
              <span class="text-[10px] text-slate-500 block">Credit</span>
              <span class="font-mono text-slate-900">{{ formatIdr(occ.creditTotal) }}</span>
            </div>
            <div>
              <span class="text-[10px] text-slate-500 block">Variance</span>
              <span
                :class="[
                  'font-mono font-semibold',
                  occ.varianceDelta && occ.varianceDelta !== 0 ? 'text-rose-600' : 'text-emerald-600',
                ]"
              >
                {{ formatIdr(occ.varianceDelta) }}
              </span>
            </div>
          </div>

          <!-- Diagnostic trace snippet with copy -->
          <div v-if="occ.traceString" class="space-y-1.5 pt-1">
            <div class="flex items-center justify-between">
              <span class="text-[10px] font-medium text-slate-500 uppercase tracking-wider flex items-center gap-1">
                <FileText class="w-3 h-3 text-slate-500" />
                Occurrence Trace
              </span>
              <button
                type="button"
                @click="copyTrace(occ.id, occ.traceString)"
                class="inline-flex items-center gap-1 px-2 py-0.5 text-[11px] font-medium text-slate-600 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded transition-colors focus:outline-none"
              >
                <Check v-if="copiedTraceId === occ.id" class="w-3 h-3 text-emerald-600" />
                <Copy v-else class="w-3 h-3 text-slate-500" />
                <span>{{ copiedTraceId === occ.id ? 'Copied' : 'Copy' }}</span>
              </button>
            </div>
            <div
              class="p-2.5 rounded bg-slate-50 border border-slate-200 font-mono text-xs text-slate-900 max-h-36 overflow-y-auto whitespace-pre-wrap break-all"
            >
              {{ occ.traceString }}
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
