<script setup lang="ts">
import { computed } from 'vue';
import { ShieldCheck, AlertTriangle, CheckCircle2, XCircle, Clock, History } from 'lucide-vue-next';
import type { IssueDetailDto, VerificationRecordDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

const props = defineProps<{
  issueDetail?: IssueDetailDto;
  verifications?: VerificationRecordDto[];
}>();

const allVerifications = computed<VerificationRecordDto[]>(() => {
  if (props.verifications) {
    return [...props.verifications].sort(
      (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
    );
  }
  const fromIssue = props.issueDetail?.verifications ?? props.issueDetail?.verificationRecords ?? [];
  return [...fromIssue].sort(
    (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
  );
});

const latestVerification = computed<VerificationRecordDto | null>(() => {
  if (allVerifications.value.length === 0) return null;
  return allVerifications.value[0];
});

const priorVerifications = computed<VerificationRecordDto[]>(() => {
  if (allVerifications.value.length <= 1) return [];
  return allVerifications.value.slice(1);
});

const isLatestFailed = computed(() => {
  return latestVerification.value !== null && !latestVerification.value.cleanPassed;
});

function formatMethodLabel(method?: string): string {
  if (!method) return 'Manual Verification';
  switch (method) {
    case 'ManualVerification':
      return 'Manual Verification';
    case 'AutomatedSupervisor':
      return 'Automated Supervisor';
    case 'TelemetryRecurrence':
      return 'Telemetry Recurrence';
    case 'AutomatedReRun':
      return 'Automated Re-Run';
    default:
      return method;
  }
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-sm text-slate-900 flex flex-col gap-4">
    <!-- Card Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <ShieldCheck class="w-5 h-5 text-indigo-600" />
        <h3 class="font-semibold text-slate-900 text-sm tracking-wide uppercase">Verification Records</h3>
      </div>
      <span
        v-if="allVerifications.length > 0"
        class="text-xs px-2 py-0.5 rounded font-mono font-medium bg-slate-100 text-slate-700 border border-slate-300"
      >
        {{ allVerifications.length }} {{ allVerifications.length === 1 ? 'attempt' : 'attempts' }}
      </span>
    </div>

    <!-- Rose Warning Banner if Latest Verification Failed -->
    <div
      v-if="isLatestFailed"
      class="bg-rose-50 border border-rose-200 text-rose-800 rounded-lg p-3.5 flex items-start gap-3 shadow-sm"
    >
      <AlertTriangle class="w-5 h-5 shrink-0 mt-0.5 text-rose-600" />
      <div>
        <div class="font-semibold text-sm text-rose-700">Latest Verification Failed</div>
        <div class="text-xs text-rose-600 mt-0.5">
          The anomaly is still present in the system. The verification gate remains locked until corrective remediation is reapplied and confirmed clean.
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div
      v-if="allVerifications.length === 0"
      class="py-6 text-center text-slate-500 border border-dashed border-slate-300 rounded-lg text-xs"
    >
      <Clock class="w-6 h-6 mx-auto mb-2 text-slate-400" />
      No verification attempts recorded yet.
    </div>

    <!-- Latest Verification Display -->
    <div
      v-else-if="latestVerification"
      class="border rounded-lg p-4 space-y-3"
      :class="[
        latestVerification.cleanPassed
          ? 'bg-slate-50 border-emerald-300'
          : 'bg-rose-50 border-rose-300'
      ]"
    >
      <div class="flex items-center justify-between flex-wrap gap-2">
        <div class="flex items-center gap-2">
          <!-- Outcome Chip -->
          <span
            v-if="latestVerification.cleanPassed"
            class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-300"
          >
            <CheckCircle2 class="w-3.5 h-3.5 text-emerald-600" />
            Clean Passed
          </span>
          <span
            v-else
            class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-50 text-rose-700 border border-rose-300"
          >
            <XCircle class="w-3.5 h-3.5 text-rose-600" />
            Anomaly Still Present
          </span>

          <!-- Method Chip -->
          <span class="text-[11px] px-2 py-0.5 rounded bg-slate-100 text-slate-700 border border-slate-300 font-mono">
            {{ formatMethodLabel(latestVerification.verificationMethod) }}
          </span>

          <!-- Superseded Chip -->
          <span
            v-if="latestVerification.isSuperseded"
            class="text-[11px] px-2 py-0.5 rounded bg-amber-50 text-amber-700 border border-amber-300 font-medium"
            title="Superseded by subsequent telemetry recurrence or new remediation"
          >
            Superseded
          </span>
        </div>

        <!-- Timestamp -->
        <div
          class="text-xs text-slate-500"
          :title="formatWibDateTime(latestVerification.timestampUtc, true)"
        >
          {{ formatRelativeTime(latestVerification.timestampUtc) }}
          <span class="text-slate-400">({{ formatWibDateTime(latestVerification.timestampUtc, true) }})</span>
        </div>
      </div>

      <!-- Verified By Officer -->
      <div class="text-xs text-slate-700">
        <span class="text-slate-500">Verified by:</span>
        <span class="font-medium text-slate-900 ml-1.5">{{ latestVerification.verifiedBy }}</span>
      </div>

      <!-- Evidence Notes -->
      <div class="space-y-1">
        <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wider">Evidence / Verification Notes</span>
        <div class="text-xs text-slate-900 bg-slate-50 rounded-md p-2.5 border border-slate-200 font-mono whitespace-pre-wrap leading-relaxed">
          {{ latestVerification.evidenceNotes }}
        </div>
      </div>
    </div>

    <!-- Prior Historical Verification Attempts -->
    <div v-if="priorVerifications.length > 0" class="pt-2 border-t border-slate-200 space-y-3">
      <div class="flex items-center gap-2 text-xs font-medium text-slate-500 uppercase tracking-wider">
        <History class="w-3.5 h-3.5 text-slate-400" />
        Prior Verification History ({{ priorVerifications.length }})
      </div>

      <div class="space-y-2">
        <div
          v-for="v in priorVerifications"
          :key="v.id"
          class="p-3 rounded-lg border border-slate-200 bg-white space-y-1.5 text-xs"
        >
          <div class="flex items-center justify-between flex-wrap gap-2">
            <div class="flex items-center gap-1.5">
              <span
                v-if="v.cleanPassed"
                class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-50 text-emerald-700 border border-emerald-300"
              >
                Clean Passed
              </span>
              <span
                v-else
                class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-rose-50 text-rose-700 border border-rose-300"
              >
                Anomaly Still Present
              </span>

              <span v-if="v.isSuperseded" class="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 text-slate-600">
                Superseded
              </span>
            </div>
            <span class="text-[11px] text-slate-500" :title="formatWibDateTime(v.timestampUtc, true)">
              {{ formatRelativeTime(v.timestampUtc) }} • {{ formatWibDateTime(v.timestampUtc, true) }}
            </span>
          </div>

          <div class="text-slate-600 text-[11px]">
            <span class="text-slate-500">By</span> {{ v.verifiedBy }} •
            <span class="text-slate-500">{{ formatMethodLabel(v.verificationMethod) }}</span>
          </div>

          <div class="text-slate-900 text-xs bg-slate-50 p-2 rounded border border-slate-200 font-mono whitespace-pre-wrap">
            {{ v.evidenceNotes }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
