<script setup lang="ts">
import { ref, computed } from 'vue';
import { Copy, Check, FileCode2, Layers } from 'lucide-vue-next';
import type { OccurrenceItemDto } from '@/shared/types/api.generated';

const props = defineProps<{
  occurrence?: OccurrenceItemDto | null;
}>();

const isCopied = ref(false);

const idrFormatter = new Intl.NumberFormat('id-ID', {
  style: 'currency',
  currency: 'IDR',
  maximumFractionDigits: 2,
});

function formatIdr(value?: number | null): string {
  if (value === undefined || value === null) return '-';
  return idrFormatter.format(value);
}

// Null-safe financial display check
const hasFinancialData = computed(() => {
  if (!props.occurrence) return false;
  return (
    (props.occurrence.debitTotal !== null && props.occurrence.debitTotal !== undefined) ||
    (props.occurrence.creditTotal !== null && props.occurrence.creditTotal !== undefined) ||
    (props.occurrence.varianceDelta !== null && props.occurrence.varianceDelta !== undefined)
  );
});

async function copyTrace() {
  if (!props.occurrence?.traceString) return;
  try {
    await navigator.clipboard.writeText(props.occurrence.traceString);
    isCopied.value = true;
    setTimeout(() => {
      isCopied.value = false;
    }, 2000);
  } catch (err) {
    console.error('Failed to copy trace string', err);
  }
}
</script>

<template>
  <div class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm space-y-5">
    <!-- Card Header -->
    <div class="flex items-center justify-between pb-3 border-b border-slate-200">
      <div class="flex items-center gap-2">
        <Layers class="w-4 h-4 text-indigo-600" />
        <h2 class="text-sm font-semibold text-slate-900">Latest Evidence & Forensic Context</h2>
      </div>
      <span
        v-if="occurrence"
        class="text-xs px-2 py-0.5 rounded font-mono bg-slate-100 text-slate-700 border border-slate-300"
      >
        Occurrence #{{ occurrence.occurrenceNumber }}
      </span>
    </div>

    <!-- Empty State -->
    <div v-if="!occurrence" class="py-6 text-center text-sm text-slate-500">
      No evidence record available for this issue.
    </div>

    <template v-else>
      <!-- 2x2 Metadata Well -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3.5 bg-slate-50 p-4 rounded-lg border border-slate-200">
        <!-- Module -->
        <div class="space-y-1">
          <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Module</span>
          <p class="text-sm font-semibold text-slate-900">
            {{ occurrence.module || '-' }}
          </p>
        </div>

        <!-- Source Table -->
        <div class="space-y-1">
          <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Source Table</span>
          <p class="text-sm font-mono text-slate-900 truncate" :title="occurrence.sourceTable ?? undefined">
            {{ occurrence.sourceTable || '-' }}
          </p>
        </div>

        <!-- Record Keys -->
        <div class="space-y-1">
          <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Record Keys</span>
          <p class="text-sm font-mono text-slate-900 truncate" :title="occurrence.recordKeys ?? undefined">
            {{ occurrence.recordKeys || '-' }}
          </p>
        </div>

        <!-- Inspection Run ID -->
        <div class="space-y-1">
          <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Inspection Run ID</span>
          <p class="text-sm font-mono text-slate-900 truncate" :title="occurrence.inspectionRunId">
            {{ occurrence.inspectionRunId || '-' }}
          </p>
        </div>
      </div>

      <!-- Null-safe IDR Financial Comparison Strip -->
      <div
        v-if="hasFinancialData"
        class="p-4 rounded-lg bg-slate-50 border border-indigo-200 space-y-2.5"
      >
        <span class="text-xs font-semibold text-slate-700 uppercase tracking-wider">
          Financial Impact & Variance (IDR)
        </span>
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 pt-1">
          <!-- Debit Total -->
          <div class="bg-white p-3 rounded-md border border-slate-200">
            <span class="text-xs text-slate-500 block mb-1">Debit Total</span>
            <span class="text-sm font-mono font-medium text-slate-900">
              {{ formatIdr(occurrence.debitTotal) }}
            </span>
          </div>

          <!-- Credit Total -->
          <div class="bg-white p-3 rounded-md border border-slate-200">
            <span class="text-xs text-slate-500 block mb-1">Credit Total</span>
            <span class="text-sm font-mono font-medium text-slate-900">
              {{ formatIdr(occurrence.creditTotal) }}
            </span>
          </div>

          <!-- Variance Delta -->
          <div
            :class="[
              'p-3 rounded-md border',
              occurrence.varianceDelta && occurrence.varianceDelta !== 0
                ? 'bg-rose-50 border-rose-200'
                : 'bg-emerald-50 border-emerald-200',
            ]"
          >
            <span class="text-xs text-slate-500 block mb-1">Variance Delta</span>
            <span
              :class="[
                'text-sm font-mono font-bold',
                occurrence.varianceDelta && occurrence.varianceDelta !== 0
                  ? 'text-rose-700'
                  : 'text-emerald-700',
              ]"
            >
              {{ formatIdr(occurrence.varianceDelta) }}
            </span>
          </div>
        </div>
      </div>

      <!-- Monospace Trace Block -->
      <div class="space-y-2">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-1.5">
            <FileCode2 class="w-3.5 h-3.5 text-slate-500" />
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Diagnostic Trace</span>
          </div>
          <button
            v-if="occurrence.traceString"
            type="button"
            @click="copyTrace"
            class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium text-slate-600 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded transition-colors focus:outline-none focus:ring-1 focus:ring-indigo-500"
            title="Copy trace string"
          >
            <Check v-if="isCopied" class="w-3 h-3 text-emerald-600" />
            <Copy v-else class="w-3 h-3 text-slate-500" />
            <span>{{ isCopied ? 'Copied' : 'Copy Trace' }}</span>
          </button>
        </div>

        <div
          class="rounded-lg bg-slate-50 p-4 border border-slate-200 font-mono text-xs text-slate-700 leading-relaxed overflow-x-auto max-h-56 scrollbar-thin scrollbar-thumb-slate-300"
        >
          <pre class="whitespace-pre-wrap break-all">{{ occurrence.traceString || 'No trace diagnostic available.' }}</pre>
        </div>
      </div>
    </template>
  </div>
</template>
