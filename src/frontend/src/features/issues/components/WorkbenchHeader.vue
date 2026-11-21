<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRouter } from 'vue-router';
import { ChevronLeft, Copy, Check, Lock, CheckCircle2 } from 'lucide-vue-next';
import type { IssueDetailDto } from '@/shared/types/api.generated';

const props = defineProps<{
  issueDetail: IssueDetailDto;
  isActionLoading?: boolean;
}>();

const emit = defineEmits<{
  (e: 'take-it'): void;
  (e: 'start-investigating'): void;
  (e: 'open-assign-modal'): void;
  (e: 'open-dismiss-modal'): void;
  (e: 'open-remediation-modal'): void;
  (e: 'open-verification-modal'): void;
  (e: 'resolve'): void;
  (e: 'focus-note-composer'): void;
}>();

const router = useRouter();
const isCopied = ref(false);

function navigateBack() {
  if (window.history.length > 1) {
    router.back();
  } else {
    router.push('/');
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
const detectorName = computed(() => issue.value.detectorName ?? props.issueDetail.detectorName);
const correlationKey = computed(() => issue.value.correlationKey ?? props.issueDetail.correlationKey);

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
      return 'bg-rose-500/20 text-rose-300 border border-rose-500/40';
    case 'High':
      return 'bg-orange-500/20 text-orange-300 border border-orange-500/40';
    case 'Medium':
      return 'bg-amber-500/20 text-amber-300 border border-amber-500/40';
    case 'Low':
      return 'bg-sky-500/20 text-sky-300 border border-sky-500/40';
    default:
      return 'bg-slate-700/50 text-slate-300 border border-slate-600/50';
  }
}

function getStatusBadgeClass(st: string): string {
  switch (st) {
    case 'Open':
      return 'bg-rose-500/20 text-rose-300 border border-rose-500/40';
    case 'Acknowledged':
      return 'bg-amber-500/20 text-amber-300 border border-amber-500/40';
    case 'Investigating':
      return 'bg-indigo-500/20 text-indigo-300 border border-indigo-500/40';
    case 'Resolved':
      return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40';
    case 'Dismissed':
      return 'bg-slate-700/60 text-slate-400 border border-slate-600/50';
    case 'Reopened':
      return 'bg-purple-500/20 text-purple-300 border border-purple-500/40';
    default:
      return 'bg-slate-700/50 text-slate-300 border border-slate-600/50';
  }
}
</script>

<template>
  <header
    class="sticky top-0 z-20 backdrop-blur bg-slate-900/90 border-b border-slate-800 px-6 py-4 transition-all"
  >
    <div class="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
      <!-- Left: Back Navigation, Title, Badges, Copy Key -->
      <div class="flex items-start gap-3 min-w-0">
        <button
          type="button"
          @click="navigateBack"
          class="mt-1 p-2 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition-colors shrink-0 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          title="Back to Dashboard"
          aria-label="Back to Dashboard"
        >
          <ChevronLeft class="w-5 h-5" />
        </button>

        <div class="min-w-0 flex-1">
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

            <!-- Detector Badge with Tooltip & No active router link (OQ-006) -->
            <span
              v-tooltip.bottom="'Detector portfolio detail available in Stage 3'"
              class="px-2.5 py-0.5 text-xs font-medium rounded-md bg-slate-800 text-slate-300 border border-slate-700 cursor-help inline-flex items-center gap-1 hover:border-slate-600 transition-colors"
            >
              <i class="pi pi-compass text-[11px] text-slate-400"></i>
              {{ detectorName }}
            </span>

            <!-- Copyable Correlation Key Button -->
            <button
              v-if="correlationKey"
              type="button"
              @click="copyCorrelationKey"
              class="inline-flex items-center gap-1.5 px-2 py-0.5 text-xs font-mono text-slate-400 hover:text-slate-200 bg-slate-800/80 hover:bg-slate-700/80 border border-slate-700 rounded-md transition-colors focus:outline-none focus:ring-1 focus:ring-indigo-500"
              :title="`Copy Correlation Key: ${correlationKey}`"
            >
              <Check v-if="isCopied" class="w-3.5 h-3.5 text-emerald-400" />
              <Copy v-else class="w-3.5 h-3.5 text-slate-400" />
              <span class="truncate max-w-[150px] sm:max-w-[220px]">{{ correlationKey }}</span>
              <span v-if="isCopied" class="text-emerald-400 text-[10px] font-sans">Copied!</span>
            </button>
          </div>

          <!-- Main Issue Title -->
          <h1 class="text-xl font-bold text-white tracking-tight truncate" :title="title">
            {{ title }}
          </h1>
        </div>
      </div>

      <!-- Right: Context-Sensitive Action Bar -->
      <div class="flex items-center flex-wrap gap-2.5 shrink-0">
        <!-- 1. Open State -->
        <template v-if="status === 'Open'">
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('take-it')"
            class="px-4 py-2 text-sm font-semibold rounded-lg bg-slate-100 hover:bg-white text-slate-900 shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-slate-900 focus:ring-white disabled:opacity-50"
          >
            <i class="pi pi-bolt text-xs"></i>
            Take It
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-assign-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50"
          >
            <i class="pi pi-user-plus text-xs text-slate-400"></i>
            Assign to…
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-dismiss-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-rose-950/40 text-slate-300 hover:text-rose-300 border border-slate-700 hover:border-rose-800/60 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
          >
            <i class="pi pi-ban text-xs text-rose-400"></i>
            Dismiss
          </button>
        </template>

        <!-- 2. Reopened State -->
        <template v-else-if="status === 'Reopened'">
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('take-it')"
            class="px-4 py-2 text-sm font-semibold rounded-lg bg-purple-600 hover:bg-purple-500 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-slate-900 focus:ring-purple-400 disabled:opacity-50"
          >
            <i class="pi pi-bolt text-xs"></i>
            Take It
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-assign-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50"
          >
            <i class="pi pi-user-plus text-xs text-slate-400"></i>
            Assign to…
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-dismiss-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-rose-950/40 text-slate-300 hover:text-rose-300 border border-slate-700 hover:border-rose-800/60 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
          >
            <i class="pi pi-ban text-xs text-rose-400"></i>
            Dismiss
          </button>
        </template>

        <!-- 3. Acknowledged State -->
        <template v-else-if="status === 'Acknowledged'">
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('start-investigating')"
            class="px-4 py-2 text-sm font-semibold rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-slate-900 focus:ring-indigo-500 disabled:opacity-50"
          >
            <i class="pi pi-search text-xs"></i>
            Start Investigating
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-assign-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50"
          >
            <i class="pi pi-user-edit text-xs text-slate-400"></i>
            Reassign
          </button>
          <button
            type="button"
            :disabled="isActionLoading"
            @click="emit('open-dismiss-modal')"
            class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-rose-950/40 text-slate-300 hover:text-rose-300 border border-slate-700 hover:border-rose-800/60 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
          >
            <i class="pi pi-ban text-xs text-rose-400"></i>
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
              class="px-4 py-2 text-sm font-semibold rounded-lg bg-slate-100 hover:bg-white text-slate-900 shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-slate-900 focus:ring-white disabled:opacity-50"
            >
              <i class="pi pi-wrench text-xs"></i>
              Record Remediation
            </button>
            <!-- Disabled Mark Resolved -->
            <button
              type="button"
              disabled
              v-tooltip.bottom="'🔒 Remediation & verification required'"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800/60 text-slate-500 border border-slate-800 cursor-not-allowed flex items-center gap-1.5"
            >
              <Lock class="w-3.5 h-3.5 text-slate-500" />
              Mark Resolved
            </button>
            <!-- Secondary buttons -->
            <button
              type="button"
              @click="emit('focus-note-composer')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <i class="pi pi-comment text-xs text-slate-400"></i>
              Add Note
            </button>
            <button
              type="button"
              :disabled="isActionLoading"
              @click="emit('open-dismiss-modal')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-rose-950/40 text-slate-300 hover:text-rose-300 border border-slate-700 hover:border-rose-800/60 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
            >
              <i class="pi pi-ban text-xs text-rose-400"></i>
              Dismiss
            </button>
          </template>

          <!-- Case B: Investigating with remediation, without verification -->
          <template v-else-if="latestRemediation && !latestVerification">
            <!-- Manual Verification promoted per OQ-001 -->
            <button
              type="button"
              :disabled="isActionLoading"
              @click="emit('open-verification-modal')"
              class="px-4 py-2 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-slate-900 focus:ring-emerald-400 disabled:opacity-50"
            >
              <i class="pi pi-verified text-xs"></i>
              Manual Verification
            </button>
            <!-- Disabled Mark Resolved -->
            <button
              type="button"
              disabled
              v-tooltip.bottom="'🔒 Clean verification required'"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800/60 text-slate-500 border border-slate-800 cursor-not-allowed flex items-center gap-1.5"
            >
              <Lock class="w-3.5 h-3.5 text-slate-500" />
              Mark Resolved
            </button>
            <!-- Secondary buttons -->
            <button
              type="button"
              @click="emit('focus-note-composer')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <i class="pi pi-comment text-xs text-slate-400"></i>
              Add Note
            </button>
            <button
              type="button"
              :disabled="isActionLoading"
              @click="emit('open-dismiss-modal')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-rose-950/40 text-slate-300 hover:text-rose-300 border border-slate-700 hover:border-rose-800/60 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-rose-500 disabled:opacity-50"
            >
              <i class="pi pi-ban text-xs text-rose-400"></i>
              Dismiss
            </button>
          </template>

          <!-- Case C: Investigating with failed verification (CleanPassed == false) -->
          <template v-else-if="latestVerification && !latestVerification.cleanPassed">
            <!-- Retry Remediation Primary -->
            <button
              type="button"
              :disabled="isActionLoading"
              @click="emit('open-remediation-modal')"
              class="px-4 py-2 text-sm font-semibold rounded-lg bg-slate-100 hover:bg-white text-slate-900 shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-slate-900 focus:ring-white disabled:opacity-50"
            >
              <i class="pi pi-refresh text-xs"></i>
              Record Remediation (Retry)
            </button>
            <!-- Secondary Manual Verification -->
            <button
              type="button"
              :disabled="isActionLoading"
              @click="emit('open-verification-modal')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50"
            >
              <i class="pi pi-verified text-xs text-emerald-400"></i>
              Manual Verification
            </button>
            <!-- Disabled Mark Resolved -->
            <button
              type="button"
              disabled
              v-tooltip.bottom="'🔒 Verification failed. Retry remediation & clean verification required'"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800/60 text-slate-500 border border-slate-800 cursor-not-allowed flex items-center gap-1.5"
            >
              <Lock class="w-3.5 h-3.5 text-slate-500" />
              Mark Resolved
            </button>
            <!-- Secondary Add Note -->
            <button
              type="button"
              @click="emit('focus-note-composer')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <i class="pi pi-comment text-xs text-slate-400"></i>
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
              class="px-4 py-2 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm transition-colors flex items-center gap-2 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-slate-900 focus:ring-emerald-400 disabled:opacity-50"
            >
              <CheckCircle2 class="w-4 h-4 text-white" />
              Mark Resolved
            </button>
            <!-- Secondary Add Note -->
            <button
              type="button"
              @click="emit('focus-note-composer')"
              class="px-3.5 py-2 text-sm font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition-colors flex items-center gap-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <i class="pi pi-comment text-xs text-slate-400"></i>
              Add Note
            </button>
          </template>
        </template>

        <!-- 5. Resolved or Dismissed (Read-Only) -->
        <template v-else-if="status === 'Resolved' || status === 'Dismissed'">
          <div
            class="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-slate-800 text-slate-400 border border-slate-700 flex items-center gap-1.5"
          >
            <i :class="status === 'Resolved' ? 'pi pi-check text-emerald-400' : 'pi pi-ban text-slate-400'"></i>
            Issue is {{ status }} (Read-Only)
          </div>
        </template>
      </div>
    </div>
  </header>
</template>
