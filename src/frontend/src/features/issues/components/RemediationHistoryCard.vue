<script setup lang="ts">
import { computed } from 'vue';
import { Wrench, Clock, CheckCircle2 } from 'lucide-vue-next';
import type { IssueDetailDto, RemediationRecordDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

const props = defineProps<{
  issueDetail?: IssueDetailDto;
  remediations?: RemediationRecordDto[];
}>();

const allRemediations = computed<RemediationRecordDto[]>(() => {
  if (props.remediations) {
    return [...props.remediations].sort(
      (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
    );
  }
  const fromIssue = props.issueDetail?.remediations ?? props.issueDetail?.remediationRecords ?? [];
  return [...fromIssue].sort(
    (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
  );
});

function formatCategoryLabel(cat?: string): string {
  if (!cat) return 'Remediation';
  switch (cat) {
    case 'DataCorrection':
      return 'Data Correction';
    case 'ServiceRestart':
      return 'Service Restart';
    case 'ConfigurationAdjustment':
      return 'Configuration Adjustment';
    case 'CodeRemediationRequested':
      return 'Code Remediation Requested';
    default:
      return cat;
  }
}

function getCategoryChipClass(cat?: string): string {
  switch (cat) {
    case 'DataCorrection':
      return 'bg-blue-500/20 text-blue-300 border-blue-500/40';
    case 'ServiceRestart':
      return 'bg-amber-500/20 text-amber-300 border-amber-500/40';
    case 'ConfigurationAdjustment':
      return 'bg-purple-500/20 text-purple-300 border-purple-500/40';
    case 'CodeRemediationRequested':
      return 'bg-cyan-500/20 text-cyan-300 border-cyan-500/40';
    default:
      return 'bg-indigo-500/20 text-indigo-300 border-indigo-500/40';
  }
}
</script>

<template>
  <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-sm text-slate-100 flex flex-col gap-4">
    <!-- Card Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <Wrench class="w-5 h-5 text-indigo-400" />
        <h3 class="font-semibold text-slate-100 text-sm tracking-wide uppercase">Remediation History</h3>
      </div>
      <span
        v-if="allRemediations.length > 0"
        class="text-xs px-2 py-0.5 rounded font-mono font-medium bg-slate-800 text-slate-400 border border-slate-700/60"
      >
        {{ allRemediations.length }} {{ allRemediations.length === 1 ? 'action' : 'actions' }}
      </span>
    </div>

    <!-- Empty State -->
    <div
      v-if="allRemediations.length === 0"
      class="py-6 text-center text-slate-500 border border-dashed border-slate-800 rounded-lg text-xs"
    >
      <Clock class="w-6 h-6 mx-auto mb-2 text-slate-600" />
      No remediation actions recorded yet.
    </div>

    <!-- Remediation Records List -->
    <div v-else class="space-y-3">
      <div
        v-for="(r, index) in allRemediations"
        :key="r.id"
        class="rounded-lg p-3.5 border transition-colors space-y-2.5"
        :class="[
          index === 0 && !r.isSuperseded
            ? 'bg-slate-800/50 border-indigo-500/40'
            : 'bg-slate-950/40 border-slate-800/80'
        ]"
      >
        <div class="flex items-center justify-between flex-wrap gap-2">
          <div class="flex items-center gap-2">
            <!-- Category Chip -->
            <span
              class="px-2.5 py-0.5 rounded-full text-xs font-medium border"
              :class="getCategoryChipClass(r.actionCategory)"
            >
              {{ formatCategoryLabel(r.actionCategory) }}
            </span>

            <!-- Latest / Superseded tag -->
            <span
              v-if="index === 0 && !r.isSuperseded"
              class="inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30"
            >
              <CheckCircle2 class="w-3 h-3 text-emerald-400" />
              Active
            </span>
            <span
              v-else-if="r.isSuperseded"
              class="text-[10px] px-1.5 py-0.5 rounded font-medium bg-slate-800 text-slate-500 border border-slate-700/60"
              title="Superseded by later remediation or recurrence"
            >
              Superseded
            </span>
          </div>

          <!-- Timestamp -->
          <div
            class="text-xs text-slate-400"
            :title="formatWibDateTime(r.timestampUtc, true)"
          >
            {{ formatRelativeTime(r.timestampUtc) }}
            <span class="text-slate-600">({{ formatWibDateTime(r.timestampUtc, true) }})</span>
          </div>
        </div>

        <!-- Performed By -->
        <div class="text-xs text-slate-300">
          <span class="text-slate-500">Performed by:</span>
          <span class="font-medium text-slate-200 ml-1.5">{{ r.performedBy }}</span>
        </div>

        <!-- Summary Description -->
        <div class="text-xs text-slate-300 bg-slate-900/80 p-2.5 rounded-md border border-slate-800/80 whitespace-pre-wrap leading-relaxed font-sans">
          {{ r.summaryDescription }}
        </div>
      </div>
    </div>
  </div>
</template>
