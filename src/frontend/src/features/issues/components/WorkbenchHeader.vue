<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ChevronLeft, Copy, Check, Lock, CheckCircle2 } from 'lucide-vue-next';
import type { IssueDetailDto } from '@/shared/types/api.generated';
import { useIssueDetectorDetailQuery } from '@/features/issues/api/useIssueDetailQueries';

const props = defineProps<{
  issueDetail: IssueDetailDto;
  isActionLoading?: boolean;
  hasWebhook?: boolean;
  isTriggeringVerification?: boolean;
}>();

const emit = defineEmits<{
  (e: 'take-it'): void;
  (e: 'start-investigating'): void;
  (e: 'open-assign-modal'): void;
  (e: 'open-dismiss-modal'): void;
  (e: 'open-remediation-modal'): void;
  (e: 'open-verification-modal'): void;
  (e: 'trigger-verification'): void;
  (e: 'resolve'): void;
  (e: 'focus-note-composer'): void;
}>();

const route = useRoute();
const router = useRouter();
const isCopied = ref(false);

const isFromHistory = computed(() => route.query.from === 'history');
const backLabel = computed(() => (isFromHistory.value ? '← Back to History' : 'Back to Dashboard'));

function navigateBack() {
  if (window.history.state?.back) {
    router.back();
  } else {
    router.push(isFromHistory.value ? '/history' : '/');
  }
}

async function copyCorrelationKey() {
  if (!props.issueDetail.correlationKey) return;
  try {
    await navigator.clipboard.writeText(props.issueDetail.correlationKey);
    isCopied.value = true;
    setTimeout(() => {
      isCopied.value = false;
    }, 2000);
  } catch (err) {
    console.error('Failed to copy correlation key', err);
  }
}

// Unpack header properties
const issue = computed(() => props.issueDetail.issue ?? props.issueDetail);
const status = computed(() => issue.value.status ?? props.issueDetail.status);
const currentSeverity = computed(() => issue.value.currentSeverity ?? props.issueDetail.currentSeverity);
const title = computed(() => issue.value.title ?? props.issueDetail.title);
const detectorId = computed(() => issue.value.detectorId ?? props.issueDetail.detectorId);
const detectorName = computed(() => issue.value.detectorName ?? props.issueDetail.detectorName);
const correlationKey = computed(() => issue.value.correlationKey ?? props.issueDetail.correlationKey);

const detectorQuery = useIssueDetectorDetailQuery(detectorId);
const resolvedHasWebhook = computed(() => {
  if (typeof props.hasWebhook === 'boolean') {
    return props.hasWebhook;
  }
  return Boolean(detectorQuery.data.value?.webhookUrl && detectorQuery.data.value.webhookUrl.trim().length > 0);
});

// Remediations & Verifications state inspection
const remediations = computed(() => props.issueDetail.remediations ?? props.issueDetail.remediationRecords ?? []);
const verifications = computed(() => props.issueDetail.verifications ?? props.issueDetail.verificationRecords ?? []);

// Latest non-superseded records or latest by array order
const latestRemediation = computed(() => {
  if (!remediations.value || remediations.value.length === 0) return null;
  // Look for active (non-superseded) first, or fall back to last item
  const active = remediations.value.filter((r) => !r.isSuperseded);
  return active.length > 0 ? active[active.length - 1] : remediations.value[remediations.value.length - 1];
});

const latestVerification = computed(() => {
  if (!verifications.value || verifications.value.length === 0) return null;
  // Look for active (non-superseded) first, or fall back to last item
  const active = verifications.value.filter((v) => !v.isSuperseded);
  return active.length > 0 ? active[active.length - 1] : verifications.value[verifications.value.length - 1];
});

function getSeverityBadgeClass(sev: string): string {
  switch (sev) {
    case 'Critical':
      return 'bg-rose-50 text-rose-700 border border-rose-200';
    case 'High':
      return 'bg-orange-50 text-orange-700 border border-orange-200';
    case 'Medium':
      return 'bg-amber-50 text-amber-800 border border-amber-200';
    case 'Low':
      return 'bg-sky-50 text-sky-700 border border-sky-200';
    default:
      return 'bg-slate-100 text-slate-700 border border-slate-200';
  }
}

function getStatusBadgeClass(st: string): string {
  switch (st) {
    case 'Open':
      return 'bg-rose-50 text-rose-700 border border-rose-200';
    case 'Acknowledged':
      return 'bg-sky-50 text-sky-700 border border-sky-200';
    case 'Investigating':
      return 'bg-indigo-50 text-indigo-700 border border-indigo-200';
    case 'Resolved':
      return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
    case 'Dismissed':
      return 'bg-slate-100 text-slate-700 border border-slate-200';
    case 'Reopened':
      return 'bg-purple-50 text-purple-700 border border-purple-200';
    default:
      return 'bg-slate-100 text-slate-700 border border-slate-200';
  }
}
</script>

<template>
  <header
    class="backdrop-blur bg-white/95 border-b border-slate-200 px-6 py-4 transition-all shadow-sm"
  >
    <div class="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
      <!-- Left: Back Navigation, Title, Badges, Copy Key -->
      <div class="flex items-start gap-3 min-w-0">
        <button
          type="button"
          @click="navigateBack"
          class="mt-1 p-2 rounded-lg text-slate-500 hover:text-slate-900 hover:bg-slate-100 transition-colors shrink-0 focus:outline-none focus:ring-2 focus:ring-indigo-500 cursor-pointer"
          :title="backLabel"
          :aria-label="backLabel"
        >
          <ChevronLeft v-if="!isFromHistory" class="w-5 h-5" />
          <span v-else class="text-xs font-medium whitespace-nowrap px-1">{{ backLabel }}</span>
        </button>

        <div class="min-w-0 flex-1 overflow-hidden">
          <div class="flex flex-wrap items-center gap-2 mb-1.5">
            <!-- Severity Badge -->
            <span
              :class="[
                'px-2 py-0.5 text-xs font-semibold rounded-md uppercase tracking-wider',
                getSeverityBadgeClass(currentSeverity),
              ]"
            >
              {{ currentSeverity }}
            </span>

            <!-- Status Badge -->
            <span
              :class="[
                'px-2 py-0.5 text-xs font-medium rounded-md uppercase tracking-wider',
                getStatusBadgeClass(status),
              ]"
            >
              {{ status }}
            </span>

            <!-- Detector Badge: Active Router Link to S-04 Detector Detail (E-2) -->
            <router-link
              v-if="detectorId"
              :to="`/detectors/${detectorId}`"
              v-tooltip.bottom="'View detector operational details (S-04)'"
              class="px-2.5 py-0.5 text-xs font-medium rounded-md bg-slate-100 hover:bg-slate-200 text-slate-900 hover:text-slate-900 border border-slate-300 hover:border-slate-400 inline-flex items-center gap-1.5 transition-colors group cursor-pointer"
            >
              <i class="pi pi-compass text-[11px] text-indigo-600 group-hover:text-indigo-500"></i>
              <span>{{ detectorName }}</span>
              <i class="pi pi-arrow-up-right text-[9px] text-slate-500 group-hover:text-slate-600"></i>
            </router-link>
            <span
              v-else
              class="px-2.5 py-0.5 text-xs font-medium rounded-md bg-slate-100 text-slate-700 border border-slate-300 inline-flex items-center gap-1"
            >
              <i class="pi pi-compass text-[11px] text-slate-500"></i>
              {{ detectorName }}
            </span>

            <!-- Copyable Correlation Key Button -->
            <button
              v-if="correlationKey"
              type="button"
              @click="copyCorrelationKey"
              class="inline-flex items-center gap-1.5 px-2 py-0.5 text-xs font-mono text-slate-600 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded-md transition-colors focus:outline-none focus:ring-1 focus:ring-indigo-500"
              :title="`Copy Correlation Key: ${correlationKey}`"
            >
              <Check v-if="isCopied" class="w-3.5 h-3.5 text-emerald-600" />
              <Copy v-else class="w-3.5 h-3.5 text-slate-500" />
              <span class="truncate max-w-[150px] sm:max-w-[220px]">{{ correlationKey }}</span>
              <span v-if="isCopied" class="text-emerald-600 text-[10px] font-sans">Copied!</span>
            </button>
          </div>

          <!-- Main Issue Title -->
          <h1 class="text-xl font-bold text-slate-900 tracking-tight truncate min-w-0" :title="title">
            {{ title }}
          </h1>
        </div>
      </div>

      <!-- Right: Context-Sensitive Action Bar -->
      <div class="flex items-center flex-wrap gap-2.5 shrink-0 max-w-xl">
        <!-- 1. Open State -->
        <template v-if="status === 'Open'">
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('take-it')"
            class="px-4 py-2 text-sm font-semibold rounded-lg bg-slate-900 hover:bg-slate-800 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-slate-900 disabled:opacity-50"
          >
            <i class="pi pi-bolt text-xs"></i>
            Take It
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-assign-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50"
          >
            <i class="pi pi-user-plus text-xs text-slate-500"></i>
            Assign to…
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-dismiss-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-rose-50 text-slate-700 hover:text-rose-700 border border-slate-300 hover:border-rose-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
          >
            <i class="pi pi-ban text-xs text-rose-600"></i>
            Dismiss
          </button>
        </template>

        <!-- 2. Reopened State -->
        <template v-else-if="status === 'Reopened'">
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('take-it')"
            class="px-4 py-2 text-sm font-semibold rounded-lg bg-purple-600 hover:bg-purple-500 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-purple-500 disabled:opacity-50"
          >
            <i class="pi pi-bolt text-xs"></i>
            Take It
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-assign-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50"
          >
            <i class="pi pi-user-plus text-xs text-slate-500"></i>
            Assign to…
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-dismiss-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-rose-50 text-slate-700 hover:text-rose-700 border border-slate-300 hover:border-rose-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
          >
            <i class="pi pi-ban text-xs text-rose-600"></i>
            Dismiss
          </button>
        </template>

        <!-- 3. Acknowledged State -->
        <template v-else-if="status === 'Acknowledged'">
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('start-investigating')"
            class="px-4 py-2 text-sm font-semibold rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-indigo-500 disabled:opacity-50"
          >
            <i class="pi pi-search text-xs"></i>
            Start Investigating
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-assign-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50"
          >
            <i class="pi pi-user-edit text-xs text-slate-500"></i>
            Reassign
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-dismiss-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-rose-50 text-slate-700 hover:text-rose-700 border border-slate-300 hover:border-rose-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
          >
            <i class="pi pi-ban text-xs text-rose-600"></i>
            Dismiss
          </button>
        </template>

        <!-- 4. Investigating State Sub-Matrices -->
        <template v-else-if="status === 'Investigating'">
          <!-- Case A: Investigating without remediation -->
          <template v-if="!latestRemediation">
            <button
              type="button"
              :disabled="isActionLoading"
              @click="emit('open-remediation-modal')"
              class="px-4 py-2 text-sm font-semibold rounded-lg bg-slate-900 hover:bg-slate-800 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-slate-900 disabled:opacity-50"
            >
              <i class="pi pi-wrench text-xs"></i>
              Record Remediation
            </button>
            <!-- Disabled Mark Resolved -->
            <button
              type="button"
              disabled
              v-tooltip.bottom="'🔒 Remediation & verification required'"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-200/60 text-slate-400 border border-slate-300 cursor-not-allowed flex items-center gap-1.5"
            >
              <Lock class="w-3.5 h-3.5 text-slate-400" />
              Mark Resolved
            </button>
            <!-- Secondary buttons -->
            <button
              type="button"
              @click="emit('focus-note-composer')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <i class="pi pi-comment text-xs text-slate-500"></i>
              Add Note
            </button>
            <button
              type="button"
              :disabled="isActionLoading"
              @click="emit('open-dismiss-modal')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-rose-50 text-slate-700 hover:text-rose-700 border border-slate-300 hover:border-rose-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
            >
              <i class="pi pi-ban text-xs text-rose-600"></i>
              Dismiss
            </button>
          </template>

          <!-- Case B: Investigating with remediation, without verification -->
          <template v-else-if="latestRemediation && !latestVerification">
            <!-- If detector has Webhook: Trigger Verification Re-run is Primary, Manual is Secondary (D-2) -->
            <template v-if="resolvedHasWebhook">
              <button
                type="button"
                :disabled="isActionLoading || isTriggeringVerification"
                @click="emit('trigger-verification')"
                class="px-4 py-2 text-sm font-semibold rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-indigo-500 disabled:opacity-50 cursor-pointer"
              >
                <i v-if="isTriggeringVerification" class="pi pi-spin pi-spinner text-xs"></i>
                <i v-else class="pi pi-play text-xs"></i>
                <span>Trigger Verification Re-run</span>
              </button>
              <button
                type="button"
                :disabled="isActionLoading || isTriggeringVerification"
                @click="emit('open-verification-modal')"
                class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50 cursor-pointer"
              >
                <i class="pi pi-verified text-xs text-emerald-600"></i>
                <span>Manual Verification</span>
              </button>
            </template>

            <!-- If no webhook configured: Manual Verification is Primary with Helper Note (D-2) -->
            <template v-else>
              <button
                type="button"
                :disabled="isActionLoading"
                @click="emit('open-verification-modal')"
                class="px-4 py-2 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-emerald-500 disabled:opacity-50 cursor-pointer"
              >
                <i class="pi pi-verified text-xs"></i>
                <span>Manual Verification</span>
              </button>
              <span
                class="text-xs text-slate-500 italic flex items-center gap-1 px-1"
                title="Configure Webhook URL on detector to enable automated re-runs"
              >
                <i class="pi pi-info-circle text-[11px] text-slate-400"></i>
                <span>No webhook configured for automated re-run</span>
              </span>
            </template>

            <!-- Disabled Mark Resolved -->
            <button
              type="button"
              disabled
              v-tooltip.bottom="'🔒 Clean verification required'"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-200/60 text-slate-400 border border-slate-300 cursor-not-allowed flex items-center gap-1.5"
            >
              <Lock class="w-3.5 h-3.5 text-slate-400" />
              Mark Resolved
            </button>
            <!-- Secondary buttons -->
            <button
              type="button"
              @click="emit('focus-note-composer')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <i class="pi pi-comment text-xs text-slate-500"></i>
              Add Note
            </button>
            <button
              type="button"
              :disabled="isActionLoading || isTriggeringVerification"
              @click="emit('open-dismiss-modal')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-rose-50 text-slate-700 hover:text-rose-700 border border-slate-300 hover:border-rose-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
            >
              <i class="pi pi-ban text-xs text-rose-600"></i>
              Dismiss
            </button>
          </template>

          <!-- Case C: Investigating with failed verification (CleanPassed == false) -->
          <template v-else-if="latestVerification && !latestVerification.cleanPassed">
            <!-- If detector has Webhook: Trigger Verification Re-run is Primary (D-2) -->
            <template v-if="resolvedHasWebhook">
              <button
                type="button"
                :disabled="isActionLoading || isTriggeringVerification"
                @click="emit('trigger-verification')"
                class="px-4 py-2 text-sm font-semibold rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-indigo-500 disabled:opacity-50 cursor-pointer"
              >
                <i v-if="isTriggeringVerification" class="pi pi-spin pi-spinner text-xs"></i>
                <i v-else class="pi pi-play text-xs"></i>
                <span>Trigger Verification Re-run</span>
              </button>
              <button
                type="button"
                :disabled="isActionLoading || isTriggeringVerification"
                @click="emit('open-verification-modal')"
                class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50 cursor-pointer"
              >
                <i class="pi pi-verified text-xs text-emerald-600"></i>
                <span>Manual Verification</span>
              </button>
            </template>

            <!-- If no webhook configured: Manual Verification is Primary with Helper Note (D-2) -->
            <template v-else>
              <button
                type="button"
                :disabled="isActionLoading"
                @click="emit('open-verification-modal')"
                class="px-4 py-2 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-emerald-500 disabled:opacity-50 cursor-pointer"
              >
                <i class="pi pi-verified text-xs"></i>
                <span>Manual Verification</span>
              </button>
              <span
                class="text-xs text-slate-500 italic flex items-center gap-1 px-1"
                title="Configure Webhook URL on detector to enable automated re-runs"
              >
                <i class="pi pi-info-circle text-[11px] text-slate-400"></i>
                <span>No webhook configured for automated re-run</span>
              </span>
            </template>

            <!-- Secondary Retry Remediation -->
            <button
              type="button"
              :disabled="isActionLoading || isTriggeringVerification"
              @click="emit('open-remediation-modal')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50 cursor-pointer"
            >
              <i class="pi pi-refresh text-xs text-amber-600"></i>
              <span>Record Remediation (Retry)</span>
            </button>
            <!-- Disabled Mark Resolved -->
            <button
              type="button"
              disabled
              v-tooltip.bottom="'🔒 Verification failed. Retry remediation & clean verification required'"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-200/60 text-slate-400 border border-slate-300 cursor-not-allowed flex items-center gap-1.5"
            >
              <Lock class="w-3.5 h-3.5 text-slate-400" />
              Mark Resolved
            </button>
            <!-- Secondary Add Note -->
            <button
              type="button"
              @click="emit('focus-note-composer')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <i class="pi pi-comment text-xs text-slate-500"></i>
              Add Note
            </button>
          </template>

          <!-- Case D: Investigating with clean verification (CleanPassed == true) -->
          <template v-else-if="latestVerification && latestVerification.cleanPassed">
            <!-- Unlocked Mark Resolved Primary per BR-PRIMA-004 -->
            <button
              type="button"
              :disabled="isActionLoading"
              @click="emit('resolve')"
              class="px-4 py-2 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-emerald-500 disabled:opacity-50"
            >
              <CheckCircle2 class="w-4 h-4 text-white" />
              Mark Resolved
            </button>
            <!-- Secondary Add Note -->
            <button
              type="button"
              @click="emit('focus-note-composer')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-white hover:bg-slate-50 text-slate-900 border border-slate-300 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <i class="pi pi-comment text-xs text-slate-500"></i>
              Add Note
            </button>
          </template>
        </template>

        <!-- 5. Resolved or Dismissed (Read-Only) -->
        <template v-else-if="status === 'Resolved' || status === 'Dismissed'">
          <div
            class="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-slate-100 text-slate-600 border border-slate-300 flex items-center gap-1.5"
          >
            <i :class="status === 'Resolved' ? 'pi pi-check text-emerald-600' : 'pi pi-ban text-slate-500'"></i>
            Issue is {{ status }} (Read-Only)
          </div>
        </template>
      </div>
    </div>
  </header>
</template>
