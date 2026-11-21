import { ref } from 'vue';

/**
 * Composable for managing local reactive UI state within the Issue Detail feature (S-02).
 *
 * Responsibilities:
 * 1. Independent dialog visibility flags (preserves user inputs across 10s background polling).
 * 2. Uncommitted note composer draft text decoupled from server cache.
 * 3. Expanded occurrence accordion IDs tracking stable entity IDs across poll refetches.
 */
export function useIssueDetail() {
  const isDismissModalOpen = ref(false);
  const isRemediationModalOpen = ref(false);
  const isVerificationModalOpen = ref(false);
  const isAssignModalOpen = ref(false);

  const noteDraftText = ref('');
  const expandedOccurrenceIds = ref<Set<string>>(new Set());

  function openDismissModal() {
    isDismissModalOpen.value = true;
  }

  function closeDismissModal() {
    isDismissModalOpen.value = false;
  }

  function openRemediationModal() {
    isRemediationModalOpen.value = true;
  }

  function closeRemediationModal() {
    isRemediationModalOpen.value = false;
  }

  function openVerificationModal() {
    isVerificationModalOpen.value = true;
  }

  function closeVerificationModal() {
    isVerificationModalOpen.value = false;
  }

  function openAssignModal() {
    isAssignModalOpen.value = true;
  }

  function closeAssignModal() {
    isAssignModalOpen.value = false;
  }

  function toggleOccurrenceExpansion(occurrenceId: string) {
    const next = new Set(expandedOccurrenceIds.value);
    if (next.has(occurrenceId)) {
      next.delete(occurrenceId);
    } else {
      next.add(occurrenceId);
    }
    expandedOccurrenceIds.value = next;
  }

  function isOccurrenceExpanded(occurrenceId: string): boolean {
    return expandedOccurrenceIds.value.has(occurrenceId);
  }

  function expandAllOccurrences(occurrenceIds: string[]) {
    expandedOccurrenceIds.value = new Set(occurrenceIds);
  }

  function collapseAllOccurrences() {
    expandedOccurrenceIds.value = new Set();
  }

  function resetNoteDraft() {
    noteDraftText.value = '';
  }

  return {
    // Modal visibility flags
    isDismissModalOpen,
    isRemediationModalOpen,
    isVerificationModalOpen,
    isAssignModalOpen,

    // Modal action helpers
    openDismissModal,
    closeDismissModal,
    openRemediationModal,
    closeRemediationModal,
    openVerificationModal,
    closeVerificationModal,
    openAssignModal,
    closeAssignModal,

    // Note draft state
    noteDraftText,
    resetNoteDraft,

    // Occurrence expansion state
    expandedOccurrenceIds,
    toggleOccurrenceExpansion,
    isOccurrenceExpanded,
    expandAllOccurrences,
    collapseAllOccurrences,
  };
}

export type UseIssueDetailReturn = ReturnType<typeof useIssueDetail>;
