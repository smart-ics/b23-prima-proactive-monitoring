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
      return 'bg-blue-50 text-blue-700 border-blue-300';
    case 'ServiceRestart':
      return 'bg-amber-50 text-amber-700 border-amber-300';
    case 'ConfigurationAdjustment':
      return 'bg-purple-50 text-purple-700 border-purple-300';
    case 'CodeRemediationRequested':
      return 'bg-cyan-50 text-cyan-700 border-cyan-300';
    default:
      return 'bg-indigo-50 text-indigo-700 border-indigo-300';
  }
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-sm text-slate-900 flex flex-col gap-4">
    <!-- Card Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <Wrench class="w-5 h-5 text-indigo-600" />
        <h3 class="font-semibold text-slate-900 text-sm tracking-wide uppercase">Remediation History</h3>
      </div>
      <span
        v-if="allRemediations.length > 0"
        class="text-xs px-2 py-0.5 rounded font-mono font-medium bg-slate-100 text-slate-700 border border-slate-300"
      >
        {{ allRemediations.length }} {{ allRemediations.length === 1 ? 'action' : 'actions' }}
      </span>
    </div>

    <!-- Empty State -->
    <div
      v-if="allRemediations.length === 0"
      class="py-6 text-center text-slate-500 border border-dashed border-slate-300 rounded-lg text-xs"
    >
      <Clock class="w-6 h-6 mx-auto mb-2 text-slate-400" />
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
            ? 'bg-indigo-50 border-indigo-300'
            : 'bg-white border-slate-200'
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
              class="inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded font-semibold bg-emerald-50 text-emerald-700 border border-emerald-300"
            >
              <CheckCircle2 class="w-3 h-3 text-emerald-600" />
              Active
            </span>
            <span
              v-else-if="r.isSuperseded"
              class="text-[10px] px-1.5 py-0.5 rounded font-medium bg-slate-100 text-slate-600 border border-slate-300"
              title="Superseded by later remediation or recurrence"
            >
              Superseded
            </span>
          </div>

          <!-- Timestamp -->
          <div
            class="text-xs text-slate-500"
            :title="formatWibDateTime(r.timestampUtc, true)"
          >
            {{ formatRelativeTime(r.timestampUtc) }}
            <span class="text-slate-400">({{ formatWibDateTime(r.timestampUtc, true) }})</span>
          </div>
        </div>

        <!-- Performed By -->
        <div class="text-xs text-slate-700">
          <span class="text-slate-500">Performed by:</span>
          <span class="font-medium text-slate-900 ml-1.5">{{ r.performedBy }}</span>
        </div>

        <!-- Summary Description -->
        <div class="text-xs text-slate-900 bg-slate-50 p-2.5 rounded-md border border-slate-200 whitespace-pre-wrap leading-relaxed font-sans">
          {{ r.summaryDescription }}
        </div>
      </div>
    </div>
  </div>
</template>
