<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import { AlertTriangle, RefreshCw, ArrowLeft } from 'lucide-vue-next';

import {
  useIssueDetailQuery,
  useTakeItMutation,
  useStartInvestigationMutation,
  useResolveIssueMutation,
  useAddNoteMutation,
} from '@/features/issues/api/useIssueDetailQueries';
import { useIssueDetail } from '@/features/issues/composables/useIssueDetail';

import {
  WorkbenchHeader,
  LatestEvidenceCard,
  OccurrencesTimeline,
  InvestigationNotesStream,
  VerificationGateCard,
  VerificationRecordCard,
  RemediationHistoryCard,
  DismissalRecordCard,
  OwnershipTimestampsCard,
  ActivityLogAccordion,
} from '@/features/issues/components';

import {
  DismissIssueModal,
  RecordRemediationModal,
  ManualVerificationModal,
  AssignOfficerModal,
} from '@/features/issues/components/modals';

const route = useRoute();
const router = useRouter();
const toast = useToast();

// Extract route param :id
const issueId = computed(() => {
  const raw = route.params.id;
  return typeof raw === 'string' ? raw : (raw?.[0] ?? '');
});

// Primary data query with 10s background polling
const {
  data: issueDetail,
  isPending,
  isError,
  error,
  refetch,
} = useIssueDetailQuery(issueId);

// Local UI state composable (modals, note draft, expanded occurrences)
const {
  isDismissModalOpen,
  isRemediationModalOpen,
  isVerificationModalOpen,
  isAssignModalOpen,
  openDismissModal,
  openRemediationModal,
  openVerificationModal,
  openAssignModal,
  noteDraftText,
  resetNoteDraft,
  expandedOccurrenceIds,
  toggleOccurrenceExpansion,
} = useIssueDetail();

// Component template ref for focusing notes composer
const notesStreamRef = ref<InstanceType<typeof InvestigationNotesStream> | null>(null);

// Mutations
const takeItMutation = useTakeItMutation();
const startInvestigationMutation = useStartInvestigationMutation();
const resolveMutation = useResolveIssueMutation();
const addNoteMutation = useAddNoteMutation();

const isActionLoading = computed(() => {
  return (
    takeItMutation.isPending.value ||
    startInvestigationMutation.isPending.value ||
    resolveMutation.isPending.value
  );
});

// Derived state
const currentStatus = computed(() => {
  return issueDetail.value?.issue?.status ?? issueDetail.value?.status ?? 'Open';
});

const isClosed = computed(() => {
  return currentStatus.value === 'Resolved' || currentStatus.value === 'Dismissed';
});

const isReassign = computed(() => {
  return Boolean(issueDetail.value?.currentAssignee);
});

// Sort occurrences to find latest
const latestOccurrence = computed(() => {
  const occs = issueDetail.value?.occurrences;
  if (!occs || occs.length === 0) return null;
  return [...occs].sort((a, b) => {
    if (b.occurrenceNumber !== a.occurrenceNumber) {
      return b.occurrenceNumber - a.occurrenceNumber;
    }
    return new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime();
  })[0];
});

// Latest non-superseded remediation
const latestRemediation = computed(() => {
  const rems = issueDetail.value?.remediations ?? issueDetail.value?.remediationRecords ?? [];
  if (rems.length === 0) return null;
  const sorted = [...rems].sort(
    (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
  );
  return sorted.find((r) => !r.isSuperseded) ?? sorted[0];
});

// Latest non-superseded verification
const latestVerification = computed(() => {
  const vers = issueDetail.value?.verifications ?? issueDetail.value?.verificationRecords ?? [];
  if (vers.length === 0) return null;
  const sorted = [...vers].sort(
    (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
  );
  return sorted.find((v) => !v.isSuperseded) ?? sorted[0];
});

// Action handlers
async function handleTakeIt() {
  if (!issueId.value) return;
  try {
    await takeItMutation.mutateAsync(issueId.value);
    toast.add({
      severity: 'success',
      summary: 'Assigned',
      detail: 'You have taken ownership of this issue (moved to Investigating).',
      life: 3000,
    });
  } catch (err: unknown) {
    toast.add({
      severity: 'error',
      summary: 'Action Failed',
      detail: err instanceof Error ? err.message : 'Failed to take ownership.',
      life: 5000,
    });
  }
}

async function handleStartInvestigating() {
  if (!issueId.value) return;
  try {
    await startInvestigationMutation.mutateAsync(issueId.value);
    toast.add({
      severity: 'success',
      summary: 'Investigation Started',
      detail: 'Issue status updated to Investigating.',
      life: 3000,
    });
  } catch (err: unknown) {
    toast.add({
      severity: 'error',
      summary: 'Action Failed',
      detail: err instanceof Error ? err.message : 'Failed to start investigation.',
      life: 5000,
    });
  }
}

async function handleResolve() {
  if (!issueId.value) return;
  try {
    await resolveMutation.mutateAsync(issueId.value);
    toast.add({
      severity: 'success',
      summary: 'Issue Resolved',
      detail: 'Issue has passed verification gate and is marked Resolved.',
      life: 3000,
    });
  } catch (err: unknown) {
    toast.add({
      severity: 'error',
      summary: 'Resolution Blocked',
      detail: err instanceof Error ? err.message : 'Cannot resolve issue: clean verification required.',
      life: 5000,
    });
  }
}

async function handleAddNote() {
  if (!issueId.value || !noteDraftText.value.trim()) return;
  try {
    await addNoteMutation.mutateAsync({
      issueId: issueId.value,
      noteText: noteDraftText.value.trim(),
    });
    resetNoteDraft();
    toast.add({
      severity: 'success',
      summary: 'Note Added',
      detail: 'Investigation note appended to timeline.',
      life: 3000,
    });
  } catch (err: unknown) {
    toast.add({
      severity: 'error',
      summary: 'Action Failed',
      detail: err instanceof Error ? err.message : 'Failed to add investigation note.',
      life: 5000,
    });
  }
}

function handleFocusNoteComposer() {
  notesStreamRef.value?.focus();
}
</script>

<template>
  <div class="space-y-6">
    <!-- Cold Load Skeleton State -->
    <div v-if="isPending" class="space-y-6 animate-pulse">
      <!-- Header Skeleton -->
      <div class="h-28 bg-slate-900 border border-slate-800 rounded-lg p-6 flex flex-col justify-between">
        <div class="flex items-center gap-3">
          <div class="w-8 h-8 bg-slate-800 rounded-lg"></div>
          <div class="w-20 h-5 bg-slate-800 rounded"></div>
          <div class="w-24 h-5 bg-slate-800 rounded"></div>
          <div class="w-48 h-5 bg-slate-800 rounded"></div>
        </div>
        <div class="w-1/2 h-6 bg-slate-800 rounded mt-3"></div>
      </div>

      <!-- 2-Column Grid Skeleton -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div class="lg:col-span-8 space-y-6">
          <div class="h-64 bg-slate-900 border border-slate-800 rounded-lg p-6"></div>
          <div class="h-48 bg-slate-900 border border-slate-800 rounded-lg p-6"></div>
          <div class="h-48 bg-slate-900 border border-slate-800 rounded-lg p-6"></div>
        </div>
        <div class="lg:col-span-4 space-y-6">
          <div class="h-56 bg-slate-900 border border-slate-800 rounded-lg p-6"></div>
          <div class="h-48 bg-slate-900 border border-slate-800 rounded-lg p-6"></div>
          <div class="h-40 bg-slate-900 border border-slate-800 rounded-lg p-6"></div>
        </div>
      </div>
    </div>

    <!-- Error Fallback State -->
    <div
      v-else-if="isError || !issueDetail"
      class="p-12 text-center flex flex-col items-center justify-center space-y-4 bg-slate-900 border border-slate-800 rounded-lg shadow-sm"
    >
      <div class="w-14 h-14 rounded-full bg-rose-500/10 text-rose-400 border border-rose-500/20 flex items-center justify-center text-2xl">
        <AlertTriangle class="w-7 h-7" />
      </div>
      <div>
        <h2 class="text-lg font-semibold text-white">Issue Not Found or Failed to Load</h2>
        <p class="text-xs text-slate-400 mt-1 max-w-md">
          {{ error?.message || 'Unable to retrieve details for this issue. It may have been purged or the connection timed out.' }}
        </p>
      </div>
      <div class="flex items-center gap-3 pt-2">
        <button
          type="button"
          @click="() => refetch()"
          class="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg text-xs font-medium bg-indigo-600 hover:bg-indigo-500 text-white transition-colors cursor-pointer"
        >
          <RefreshCw class="w-3.5 h-3.5" />
          <span>Retry</span>
        </button>
        <button
          type="button"
          @click="router.push('/')"
          class="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition-colors cursor-pointer"
        >
          <ArrowLeft class="w-3.5 h-3.5" />
          <span>Back to Dashboard</span>
        </button>
      </div>
    </div>

    <!-- Active Operational Workbench (S-02 2-Column Asymmetric Grid) -->
    <div v-else class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
      <!-- Left Column: Operational Workbench (8 Cols) -->
      <div class="lg:col-span-8 space-y-6">
        <!-- Workbench Header (Sticky with Context-Sensitive Action Bar) -->
        <WorkbenchHeader
          class="rounded-lg border border-slate-800 shadow-md"
          :issue-detail="issueDetail"
          :is-action-loading="isActionLoading"
          @take-it="handleTakeIt"
          @start-investigating="handleStartInvestigating"
          @open-assign-modal="openAssignModal"
          @open-dismiss-modal="openDismissModal"
          @open-remediation-modal="openRemediationModal"
          @open-verification-modal="openVerificationModal"
          @resolve="handleResolve"
          @focus-note-composer="handleFocusNoteComposer"
        />

        <!-- Latest Evidence Card (2x2 Metadata Well, Null-Safe Financial Strip, Trace) -->
        <LatestEvidenceCard :occurrence="latestOccurrence" />

        <!-- Occurrences Timeline (Collapsible, Newest-First) -->
        <OccurrencesTimeline
          :occurrences="issueDetail.occurrences ?? []"
          :expanded-occurrence-ids="expandedOccurrenceIds"
          @toggle-occurrence="toggleOccurrenceExpansion"
        />

        <!-- Investigation Notes Stream (Chronological, Ctrl+Enter Composer) -->
        <InvestigationNotesStream
          ref="notesStreamRef"
          :notes="issueDetail.notes ?? []"
          v-model="noteDraftText"
          :is-submitting="addNoteMutation.isPending.value"
          :is-read-only="isClosed"
          @submit="handleAddNote"
        />
      </div>

      <!-- Right Column: Governance and Audit (4 Cols) -->
      <div class="lg:col-span-4 space-y-6">
        <!-- Verification Gate Card (3-Step Checklist BR-PRIMA-004) -->
        <VerificationGateCard
          :issue-detail="issueDetail"
          :status="currentStatus"
          :latest-remediation="latestRemediation"
          :latest-verification="latestVerification"
        />

        <!-- Verification Record Card -->
        <VerificationRecordCard
          :issue-detail="issueDetail"
          :verifications="issueDetail.verifications ?? issueDetail.verificationRecords ?? []"
        />

        <!-- Remediation History Card -->
        <RemediationHistoryCard
          :issue-detail="issueDetail"
          :remediations="issueDetail.remediations ?? issueDetail.remediationRecords ?? []"
        />

        <!-- Dismissal Record Card (Rendered when status is Dismissed) -->
        <DismissalRecordCard
          v-if="currentStatus === 'Dismissed'"
          :issue-detail="issueDetail"
          :dismissal="issueDetail.dismissal"
          :status="currentStatus"
        />

        <!-- Ownership and Timestamps Card -->
        <OwnershipTimestampsCard
          :issue-detail="issueDetail"
          :assignee="issueDetail.currentAssignee"
          :created-at-utc="issueDetail.createdAtUtc"
          :acknowledged-at-utc="issueDetail.acknowledgedAtUtc"
          :last-occurrence-at-utc="issueDetail.lastOccurrenceAtUtc"
          :resolved-at-utc="issueDetail.resolvedAtUtc"
          :dismissed-at-utc="issueDetail.dismissedAtUtc"
          :reopened-at-utc="issueDetail.issue?.reopenedAtUtc"
          :status="currentStatus"
          @open-assign-modal="openAssignModal"
          @reassign="openAssignModal"
        />

        <!-- Activity Log Accordion (Audit Trail) -->
        <ActivityLogAccordion
          :issue-detail="issueDetail"
          :activity-logs="issueDetail.activityLogs ?? []"
        />
      </div>
    </div>

    <!-- Workflow Modals (M-01, M-02, M-03 & Assign Modal) -->
    <DismissIssueModal
      v-model:visible="isDismissModalOpen"
      :issue-id="issueId"
      @dismissed="() => refetch()"
    />

    <RecordRemediationModal
      v-model:visible="isRemediationModalOpen"
      :issue-id="issueId"
      @recorded="() => refetch()"
    />

    <ManualVerificationModal
      v-model:visible="isVerificationModalOpen"
      :issue-id="issueId"
      @verified="() => refetch()"
    />

    <AssignOfficerModal
      v-model:visible="isAssignModalOpen"
      :issue-id="issueId"
      :is-reassign="isReassign"
      @assigned="() => refetch()"
    />
  </div>
</template>
