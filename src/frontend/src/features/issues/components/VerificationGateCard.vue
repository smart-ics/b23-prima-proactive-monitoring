<script setup lang="ts">
import { computed } from 'vue';
import { Lock, Unlock, CheckCircle2, XCircle, Clock, ShieldCheck } from 'lucide-vue-next';
import type { IssueDetailDto, RemediationRecordDto, VerificationRecordDto } from '@/shared/types/api.generated';

const props = defineProps<{
  issueDetail?: IssueDetailDto;
  status?: string;
  latestRemediation?: RemediationRecordDto | null;
  latestVerification?: VerificationRecordDto | null;
}>();

const currentStatus = computed(() => {
  if (props.status) return props.status;
  if (props.issueDetail) {
    const raw = props.issueDetail.issue ?? props.issueDetail;
    return raw.status ?? props.issueDetail.status ?? 'Open';
  }
  return 'Open';
});

const resolvedRemediation = computed<RemediationRecordDto | null>(() => {
  if (props.latestRemediation !== undefined) return props.latestRemediation;
  const rems = props.issueDetail?.remediations ?? props.issueDetail?.remediationRecords ?? [];
  if (rems.length === 0) return null;
  const sorted = [...rems].sort(
    (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
  );
  const active = sorted.find((r) => !r.isSuperseded);
  return active ?? sorted[0];
});

const resolvedVerification = computed<VerificationRecordDto | null>(() => {
  if (props.latestVerification !== undefined) return props.latestVerification;
  const vers = props.issueDetail?.verifications ?? props.issueDetail?.verificationRecords ?? [];
  if (vers.length === 0) return null;
  const sorted = [...vers].sort(
    (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
  );
  const active = sorted.find((v) => !v.isSuperseded);
  return active ?? sorted[0];
});

// Step 1: Investigation Started (checked when status is Investigating, Resolved, or Dismissed)
const isStep1Complete = computed(() => {
  return ['Investigating', 'Resolved', 'Dismissed'].includes(currentStatus.value);
});

// Step 2: Remediation Recorded (checked when latestRemediation != null)
const isStep2Complete = computed(() => {
  return resolvedRemediation.value !== null && resolvedRemediation.value !== undefined;
});

// Step 3: Verification Passed Clean (checked when latestVerification?.cleanPassed == true && !latestVerification.isSuperseded)
const isStep3Complete = computed(() => {
  const v = resolvedVerification.value;
  return Boolean(v && v.cleanPassed === true && !v.isSuperseded);
});

// Specific failure condition for Step 3
const isVerificationFailed = computed(() => {
  const v = resolvedVerification.value;
  return Boolean(v && v.cleanPassed === false && !v.isSuperseded);
});

const isResolved = computed(() => currentStatus.value === 'Resolved');
const isGateUnlocked = computed(() => isStep1Complete.value && isStep2Complete.value && isStep3Complete.value);

function formatCategoryLabel(cat?: string): string {
  if (!cat) return '';
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
</script>

<template>
  <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-sm text-slate-100 flex flex-col gap-4">
    <!-- Card Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <ShieldCheck class="w-5 h-5 text-indigo-400" />
        <h3 class="font-semibold text-slate-100 text-sm tracking-wide uppercase">Verification Gate</h3>
      </div>
      <span class="text-xs px-2 py-0.5 rounded font-mono font-medium bg-slate-800 text-slate-400 border border-slate-700/60">
        BR-PRIMA-004
      </span>
    </div>

    <!-- Visual Lock / Unlock Indicator Banner -->
    <div
      v-if="isResolved"
      class="bg-emerald-600 text-white rounded-lg p-3.5 flex items-start gap-3 shadow-md"
    >
      <CheckCircle2 class="w-5 h-5 shrink-0 mt-0.5 text-white" />
      <div>
        <div class="font-semibold text-sm">Verification Gate Satisfied</div>
        <div class="text-xs text-emerald-100 mt-0.5">
          All governance invariants met. Issue was verified clean and formally resolved.
        </div>
      </div>
    </div>

    <div
      v-else-if="isGateUnlocked"
      class="bg-emerald-600 text-white rounded-lg p-3.5 flex items-start gap-3 shadow-md animate-pulse"
    >
      <Unlock class="w-5 h-5 shrink-0 mt-0.5 text-white" />
      <div>
        <div class="font-semibold text-sm">Verification Gate Unlocked</div>
        <div class="text-xs text-emerald-100 mt-0.5">
          Clean verification passed without recurrence. Issue is eligible to be marked Resolved.
        </div>
      </div>
    </div>

    <div
      v-else-if="isVerificationFailed"
      class="bg-rose-950/70 border border-rose-700/70 text-rose-200 rounded-lg p-3.5 flex items-start gap-3 shadow-sm"
    >
      <XCircle class="w-5 h-5 shrink-0 mt-0.5 text-rose-400" />
      <div>
        <div class="font-semibold text-sm text-rose-300">Verification Gate Blocked (Failed)</div>
        <div class="text-xs text-rose-200/90 mt-0.5">
          Latest verification run detected anomaly still present. Remediation must be re-attempted.
        </div>
      </div>
    </div>

    <div
      v-else
      class="bg-amber-950/60 border border-amber-600/50 text-amber-200 rounded-lg p-3.5 flex items-start gap-3 shadow-sm"
    >
      <Lock class="w-5 h-5 shrink-0 mt-0.5 text-amber-400" />
      <div>
        <div class="font-semibold text-sm text-amber-300">Verification Gate Locked</div>
        <div class="text-xs text-amber-200/90 mt-0.5">
          Resolution blocked until remediation is recorded and verified clean without subsequent recurrence.
        </div>
      </div>
    </div>

    <!-- 3-Step Checklist -->
    <div class="space-y-3 pt-1">
      <!-- Step 1 -->
      <div
        class="flex items-start gap-3 p-3 rounded-lg border transition-colors"
        :class="isStep1Complete ? 'bg-slate-800/60 border-slate-700/60' : 'bg-slate-900/50 border-slate-800'"
      >
        <div class="mt-0.5">
          <CheckCircle2 v-if="isStep1Complete" class="w-4 h-4 text-emerald-400" />
          <Clock v-else class="w-4 h-4 text-slate-500" />
        </div>
        <div class="flex-1 min-w-0">
          <div class="flex items-center justify-between">
            <span
              class="text-xs font-semibold"
              :class="isStep1Complete ? 'text-slate-200' : 'text-slate-400'"
            >
              Step 1: Investigation Started
            </span>
            <span
              class="text-[10px] uppercase font-mono px-1.5 py-0.5 rounded"
              :class="isStep1Complete ? 'bg-emerald-500/20 text-emerald-300' : 'bg-slate-800 text-slate-500'"
            >
              {{ isStep1Complete ? 'Complete' : 'Pending' }}
            </span>
          </div>
          <p class="text-xs text-slate-400 mt-0.5">
            {{ isStep1Complete ? `Status transitioned to ${currentStatus}` : 'Officer must take or start investigation' }}
          </p>
        </div>
      </div>

      <!-- Step 2 -->
      <div
        class="flex items-start gap-3 p-3 rounded-lg border transition-colors"
        :class="isStep2Complete ? 'bg-slate-800/60 border-slate-700/60' : 'bg-slate-900/50 border-slate-800'"
      >
        <div class="mt-0.5">
          <CheckCircle2 v-if="isStep2Complete" class="w-4 h-4 text-emerald-400" />
          <Clock v-else class="w-4 h-4 text-slate-500" />
        </div>
        <div class="flex-1 min-w-0">
          <div class="flex items-center justify-between">
            <span
              class="text-xs font-semibold"
              :class="isStep2Complete ? 'text-slate-200' : 'text-slate-400'"
            >
              Step 2: Remediation Recorded
            </span>
            <span
              class="text-[10px] uppercase font-mono px-1.5 py-0.5 rounded"
              :class="isStep2Complete ? 'bg-emerald-500/20 text-emerald-300' : 'bg-slate-800 text-slate-500'"
            >
              {{ isStep2Complete ? 'Complete' : 'Pending' }}
            </span>
          </div>
          <p class="text-xs text-slate-400 mt-0.5">
            <template v-if="resolvedRemediation">
              <span class="text-indigo-300 font-medium">{{ formatCategoryLabel(resolvedRemediation.actionCategory) }}</span>
              <span class="text-slate-500"> by {{ resolvedRemediation.performedBy }}</span>
            </template>
            <template v-else>
              Awaiting corrective SOP or database fix record
            </template>
          </p>
        </div>
      </div>

      <!-- Step 3 -->
      <div
        class="flex items-start gap-3 p-3 rounded-lg border transition-colors"
        :class="[
          isStep3Complete
            ? 'bg-slate-800/60 border-slate-700/60'
            : isVerificationFailed
              ? 'bg-rose-950/30 border-rose-800/50'
              : 'bg-slate-900/50 border-slate-800'
        ]"
      >
        <div class="mt-0.5">
          <CheckCircle2 v-if="isStep3Complete" class="w-4 h-4 text-emerald-400" />
          <XCircle v-else-if="isVerificationFailed" class="w-4 h-4 text-rose-400" />
          <Clock v-else class="w-4 h-4 text-slate-500" />
        </div>
        <div class="flex-1 min-w-0">
          <div class="flex items-center justify-between">
            <span
              class="text-xs font-semibold"
              :class="[
                isStep3Complete
                  ? 'text-slate-200'
                  : isVerificationFailed
                    ? 'text-rose-300'
                    : 'text-slate-400'
              ]"
            >
              Step 3: Verification Passed Clean
            </span>
            <span
              class="text-[10px] uppercase font-mono px-1.5 py-0.5 rounded"
              :class="[
                isStep3Complete
                  ? 'bg-emerald-500/20 text-emerald-300'
                  : isVerificationFailed
                    ? 'bg-rose-500/20 text-rose-300'
                    : 'bg-slate-800 text-slate-500'
              ]"
            >
              {{ isStep3Complete ? 'Passed' : isVerificationFailed ? 'Failed' : 'Pending' }}
            </span>
          </div>
          <p class="text-xs mt-0.5" :class="isVerificationFailed ? 'text-rose-300/80' : 'text-slate-400'">
            <template v-if="isStep3Complete">
              Verified clean by {{ resolvedVerification?.verifiedBy }}
            </template>
            <template v-else-if="isVerificationFailed">
              Anomaly still present. Re-remediation required.
            </template>
            <template v-else-if="resolvedVerification?.isSuperseded">
              Prior verification superseded by telemetry recurrence
            </template>
            <template v-else>
              Requires clean verification after remediation
            </template>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
