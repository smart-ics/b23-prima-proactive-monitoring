import { computed, unref, type MaybeRef } from 'vue';
import {
  useQuery,
  useMutation,
  useQueryClient,
  keepPreviousData,
} from '@tanstack/vue-query';
import http from '@/shared/api/http';
import type {
  IssueDetailDto,
  DetectorDetailDto,
  OfficerDto,
  NoteDto,
  RemediationActionCategory,
  DismissalReason,
  VerificationResultDto,
} from '@/shared/types/api.generated';
import { issueDetailKeys } from './issueDetail.keys';

export interface AssignMutationVariables {
  issueId: string;
  assignedOfficerId: string;
}

export interface AddNoteMutationVariables {
  issueId: string;
  noteText: string;
}

export interface RecordRemediationMutationVariables {
  issueId: string;
  actionCategory: RemediationActionCategory;
  summaryDescription: string;
}

export interface RecordVerificationMutationVariables {
  issueId: string;
  cleanPassed: boolean;
  evidenceNotes: string;
}

export interface DismissMutationVariables {
  issueId: string;
  dismissalReason: DismissalReason;
  justificationRationale: string;
}

/**
 * Primary query composable for Issue Detail View (S-02).
 * - 10-second background polling
 * - Refetches on window focus
 * - 5-second stale time
 * - keepPreviousData to prevent UI flashes during polling
 */
export function useIssueDetailQuery(id: MaybeRef<string>) {
  return useQuery<IssueDetailDto, Error>({
    queryKey: computed(() => issueDetailKeys.detail(id)),
    queryFn: async () => {
      const issueId = unref(id);
      const response = await http.get<IssueDetailDto>(`/issues/${issueId}`);
      return response.data;
    },
    enabled: computed(() => !!unref(id)),
    refetchInterval: 10_000,
    refetchOnWindowFocus: true,
    staleTime: 5_000,
    placeholderData: keepPreviousData,
  });
}

/**
 * Local detector detail lookup for S-02 webhook capability without cross-slice imports.
 */
export function useIssueDetectorDetailQuery(id: MaybeRef<string>) {
  return useQuery<DetectorDetailDto, Error>({
    queryKey: computed(() => issueDetailKeys.detectorDetail(id)),
    queryFn: async () => {
      const detectorId = unref(id);
      const response = await http.get<DetectorDetailDto>(`/detectors/${detectorId}`);
      return response.data;
    },
    enabled: computed(() => !!unref(id)),
    staleTime: 7_500,
    placeholderData: keepPreviousData,
  });
}

/**
 * Query composable for active EDP officer lookup.
 * Populates assignment and re-assignment dropdowns.
 */
export function useOfficersQuery() {
  return useQuery<OfficerDto[], Error>({
    queryKey: issueDetailKeys.officers(),
    queryFn: async () => {
      const response = await http.get<OfficerDto[]>('/officers');
      return response.data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Mutation composable for officer self-assignment ("Take It").
 * Transitions issue status to Investigating and sets CurrentAssignee to authenticated user.
 */
export function useTakeItMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (issueId: string) => {
      await http.post(`/issues/${issueId}/take-it`);
      return issueId;
    },
    onSuccess: (issueId) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(issueId) });
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'feed'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-feed'] });
    },
  });
}

/**
 * Mutation composable for delegating issue assignment to a specific officer.
 * Transitions Open/Reopened issues to Acknowledged.
 */
export function useAssignMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (vars: AssignMutationVariables) => {
      await http.post(`/issues/${vars.issueId}/assign`, {
        assignedOfficerId: vars.assignedOfficerId,
      });
      return vars;
    },
    onSuccess: (vars) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(vars.issueId) });
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'feed'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-feed'] });
    },
  });
}

/**
 * Mutation composable for acknowledging an assigned issue.
 * Halts SLA unacknowledged timer and transitions issue to Acknowledged.
 */
export function useAcknowledgeMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (issueId: string) => {
      await http.post(`/issues/${issueId}/acknowledge`);
      return issueId;
    },
    onSuccess: (issueId) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(issueId) });
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'feed'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-feed'] });
    },
  });
}

/**
 * Mutation composable for starting active investigation on an Acknowledged issue.
 * Transitions status to Investigating.
 */
export function useStartInvestigationMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (issueId: string) => {
      await http.post(`/issues/${issueId}/start-investigation`);
      return issueId;
    },
    onSuccess: (issueId) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(issueId) });
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'feed'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-feed'] });
    },
  });
}

/**
 * Mutation composable for adding a chronological investigation note.
 */
export function useAddNoteMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (vars: AddNoteMutationVariables) => {
      const response = await http.post<NoteDto>(`/issues/${vars.issueId}/notes`, {
        noteText: vars.noteText,
      });
      return response.data;
    },
    onSuccess: (_data, vars) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(vars.issueId) });
    },
  });
}

/**
 * Mutation composable for recording a corrective remediation action.
 * Supersedes prior verification records.
 */
export function useRecordRemediationMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (vars: RecordRemediationMutationVariables) => {
      await http.post(`/issues/${vars.issueId}/remediation`, {
        actionCategory: vars.actionCategory,
        summaryDescription: vars.summaryDescription,
      });
      return vars;
    },
    onSuccess: (vars) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(vars.issueId) });
    },
  });
}

/**
 * Mutation composable for recording a manual verification check (M-03).
 */
export function useRecordVerificationMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (vars: RecordVerificationMutationVariables) => {
      await http.post(`/issues/${vars.issueId}/verification/manual`, {
        cleanPassed: vars.cleanPassed,
        evidenceNotes: vars.evidenceNotes,
      });
      return vars;
    },
    onSuccess: (vars) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(vars.issueId) });
    },
  });
}

/**
 * Mutation composable for closing an issue as Resolved (BR-PRIMA-004 verification gate).
 */
export function useResolveIssueMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (issueId: string) => {
      await http.post(`/issues/${issueId}/resolve`);
      return issueId;
    },
    onSuccess: (issueId) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(issueId) });
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'feed'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-feed'] });
    },
  });
}

/**
 * Mutation composable for triggering an automated webhook probe re-run (D-2).
 * Evaluates probe findings and unlocks Verification Gate on clean pass (BR-PRIMA-004).
 */
export function useTriggerVerificationRunMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (issueId: string) => {
      const response = await http.post<VerificationResultDto>(
        `/issues/${issueId}/verification/trigger-run`
      );
      return response.data;
    },
    onSuccess: (_data, issueId) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(issueId) });
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'feed'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-feed'] });
      queryClient.invalidateQueries({ queryKey: ['detectors'] });
    },
  });
}

/**
 * Mutation composable for dismissing an issue (M-01) with mandatory justification rationale (BR-PRIMA-006).
 */
export function useDismissIssueMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (vars: DismissMutationVariables) => {
      await http.post(`/issues/${vars.issueId}/dismiss`, {
        dismissalReason: vars.dismissalReason,
        justificationRationale: vars.justificationRationale,
      });
      return vars;
    },
    onSuccess: (vars) => {
      queryClient.invalidateQueries({ queryKey: issueDetailKeys.detail(vars.issueId) });
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'feed'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-feed'] });
    },
  });
}
