import { z } from 'zod';
import type {
  DismissalReason,
  RemediationActionCategory,
} from '@/shared/types/api.generated';

/**
 * Zod validation schema for Issue Dismissal dialog (M-01).
 * Enforces BR-PRIMA-006: Justification rationale must be at least 20 trimmed characters.
 */
export const dismissIssueSchema = z.object({
  dismissalReason: z.enum([
    'FalsePositive',
    'AcceptedOperationalException',
    'DuplicateManualReport',
  ], {
    required_error: 'Please select a dismissal reason',
    invalid_type_error: 'Invalid dismissal reason selected',
  }),
  justificationRationale: z
    .string({
      required_error: 'Justification rationale is required',
    })
    .transform((val) => val.trim())
    .refine((val) => val.length >= 20, {
      message: 'Justification rationale must be at least 20 characters',
    }),
});

export type DismissIssueFormValues = z.infer<typeof dismissIssueSchema>;

/**
 * Zod validation schema for Record Remediation dialog (M-02).
 */
export const recordRemediationSchema = z.object({
  actionCategory: z.enum([
    'DataCorrection',
    'ServiceRestart',
    'ConfigurationAdjustment',
    'CodeRemediationRequested',
  ], {
    required_error: 'Please select an action category',
    invalid_type_error: 'Invalid action category selected',
  }),
  summaryDescription: z
    .string({
      required_error: 'Summary description is required',
    })
    .transform((val) => val.trim())
    .refine((val) => val.length >= 1, {
      message: 'Summary description is required',
    }),
});

export type RecordRemediationFormValues = z.infer<typeof recordRemediationSchema>;

/**
 * Zod validation schema for Manual Verification dialog (M-03).
 */
export const manualVerificationSchema = z.object({
  cleanPassed: z.boolean({
    required_error: 'Verification outcome must be specified',
  }),
  evidenceNotes: z
    .string({
      required_error: 'Evidence notes are required',
    })
    .transform((val) => val.trim())
    .refine((val) => val.length >= 1, {
      message: 'Evidence notes are required',
    }),
});

export type ManualVerificationFormValues = z.infer<typeof manualVerificationSchema>;

/**
 * UI label options for dismissal reasons.
 */
export const DISMISSAL_REASON_OPTIONS: Array<{ value: DismissalReason; label: string }> = [
  { value: 'FalsePositive', label: 'False Positive' },
  { value: 'AcceptedOperationalException', label: 'Accepted Operational Exception' },
  { value: 'DuplicateManualReport', label: 'Duplicate Manual Report' },
];

/**
 * UI label options for remediation action categories.
 */
export const REMEDIATION_CATEGORY_OPTIONS: Array<{ value: RemediationActionCategory; label: string }> = [
  { value: 'DataCorrection', label: 'Data Correction' },
  { value: 'ServiceRestart', label: 'Service Restart' },
  { value: 'ConfigurationAdjustment', label: 'Configuration Adjustment' },
  { value: 'CodeRemediationRequested', label: 'Code Remediation Requested' },
];
