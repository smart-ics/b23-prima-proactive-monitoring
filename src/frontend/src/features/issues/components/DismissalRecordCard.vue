<script setup lang="ts">
import { computed } from 'vue';
import { Ban, Quote } from 'lucide-vue-next';
import type { IssueDetailDto, DismissalRecordDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

const props = defineProps<{
  issueDetail?: IssueDetailDto;
  dismissal?: DismissalRecordDto | null;
  status?: string;
}>();

const currentStatus = computed(() => {
  if (props.status) return props.status;
  if (props.issueDetail) {
    const raw = props.issueDetail.issue ?? props.issueDetail;
    return raw.status ?? props.issueDetail.status ?? '';
  }
  return '';
});

const isDismissed = computed(() => currentStatus.value === 'Dismissed');

const resolvedDismissal = computed<DismissalRecordDto | null>(() => {
  if (props.dismissal !== undefined) return props.dismissal;
  return props.issueDetail?.dismissal ?? null;
});

function formatReasonLabel(reason?: string): string {
  if (!reason) return 'Dismissed';
  switch (reason) {
    case 'FalsePositive':
      return 'False Positive';
    case 'AcceptedOperationalException':
      return 'Accepted Operational Exception';
    case 'DuplicateManualReport':
      return 'Duplicate Manual Report';
    default:
      return reason;
  }
}

function getReasonChipClass(reason?: string): string {
  switch (reason) {
    case 'FalsePositive':
      return 'bg-purple-500/20 text-purple-300 border-purple-500/40';
    case 'AcceptedOperationalException':
      return 'bg-amber-500/20 text-amber-300 border-amber-500/40';
    case 'DuplicateManualReport':
      return 'bg-sky-500/20 text-sky-300 border-sky-500/40';
    default:
      return 'bg-slate-700/60 text-slate-300 border-slate-600/50';
  }
}
</script>

<template>
  <div
    v-if="isDismissed && resolvedDismissal"
    class="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-sm text-slate-100 flex flex-col gap-4"
  >
    <!-- Card Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <Ban class="w-5 h-5 text-rose-400" />
        <h3 class="font-semibold text-slate-100 text-sm tracking-wide uppercase">Dismissal Record</h3>
      </div>
      <span class="text-xs px-2 py-0.5 rounded font-mono font-medium bg-slate-800 text-slate-400 border border-slate-700/60">
        BR-PRIMA-006
      </span>
    </div>

    <!-- Dismissal Summary Banner -->
    <div class="bg-slate-800/60 border border-slate-700/70 rounded-lg p-3.5 space-y-3">
      <div class="flex items-center justify-between flex-wrap gap-2">
        <div class="flex items-center gap-2">
          <!-- Reason Chip -->
          <span
            class="px-2.5 py-0.5 rounded-full text-xs font-semibold border"
            :class="getReasonChipClass(resolvedDismissal.dismissalReason)"
          >
            {{ formatReasonLabel(resolvedDismissal.dismissalReason) }}
          </span>

          <span
            v-if="resolvedDismissal.isSuperseded"
            class="text-[10px] px-1.5 py-0.5 rounded bg-amber-500/10 text-amber-400 border border-amber-500/30"
          >
            Superseded
          </span>
        </div>

        <!-- Timestamp -->
        <div
          class="text-xs text-slate-400"
          :title="formatWibDateTime(resolvedDismissal.timestampUtc, true)"
        >
          {{ formatRelativeTime(resolvedDismissal.timestampUtc) }}
          <span class="text-slate-600">({{ formatWibDateTime(resolvedDismissal.timestampUtc, true) }})</span>
        </div>
      </div>

      <!-- Dismissed by Officer -->
      <div class="text-xs text-slate-300">
        <span class="text-slate-500">Dismissed by:</span>
        <span class="font-medium text-slate-200 ml-1.5">{{ resolvedDismissal.dismissedBy }}</span>
      </div>

      <!-- Justification Rationale (min 20 chars per BR-PRIMA-006) -->
      <div class="space-y-1.5">
        <div class="flex items-center gap-1.5 text-[11px] font-medium text-slate-400 uppercase tracking-wider">
          <Quote class="w-3.5 h-3.5 text-slate-500" />
          Documented Justification Rationale
        </div>
        <div class="text-xs text-slate-200 bg-slate-900/90 rounded-md p-3 border border-slate-800 italic leading-relaxed whitespace-pre-wrap">
          "{{ resolvedDismissal.justificationRationale }}"
        </div>
      </div>
    </div>
  </div>
</template>
