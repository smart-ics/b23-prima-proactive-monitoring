import { z } from 'zod';

/**
 * Canonical MonitoringScope enum values per BR-PRIMA-007 and DEC-004.
 */
export const CANONICAL_DETECTOR_SCOPES = [
  'DataIntegrity',
  'ProcessIntegrity',
  'DataSync',
  'Infrastructure',
] as const;

export type CanonicalDetectorScope = (typeof CANONICAL_DETECTOR_SCOPES)[number];

/**
 * All allowed scopes for validation, including legacy scopes for backward compatibility with existing records.
 */
export const DETECTOR_SCOPES = [
  'DataIntegrity',
  'ProcessIntegrity',
  'DataSync',
  'Infrastructure',
  // Backward compatibility legacy values
  'DataQuality',
  'BusinessLogic',
  'ProcessFlow',
] as const;

export type DetectorScope = (typeof DETECTOR_SCOPES)[number];

/**
 * Canonical options map for scope selection dropdowns (S-05).
 */
export interface ScopeOption {
  label: string;
  value: DetectorScope;
  description: string;
}

export const DETECTOR_SCOPE_OPTIONS: readonly ScopeOption[] = [
  {
    label: 'Data Integrity (Ledger & Master Data)',
    value: 'DataIntegrity',
    description: 'Financial ledger balance, tariff integrity, duplicate AR, master data correctness.',
  },
  {
    label: 'Process Integrity (Billing & Workflow Pipeline)',
    value: 'ProcessIntegrity',
    description: 'Auto-journaling, patient bill settlement, workflow progression pipelines.',
  },
  {
    label: 'Data Synchronization (EMR, HFIS, BPJS)',
    value: 'DataSync',
    description: 'EMR sync logs, BPJS HFIS doctor schedules, external service bridging.',
  },
  {
    label: 'Infrastructure & Platform (Print, DB, Services)',
    value: 'Infrastructure',
    description: 'Print spooler queues, database connectivity, background worker heartbeat.',
  },
] as const;

/**
 * Formatted dictionary mapping each scope to its human-readable label.
 */
export const DETECTOR_SCOPE_LABELS: Record<string, string> = {
  DataIntegrity: 'Data Integrity (Ledger & Master Data)',
  ProcessIntegrity: 'Process Integrity (Billing & Workflow Pipeline)',
  DataSync: 'Data Synchronization (EMR, HFIS, BPJS)',
  Infrastructure: 'Infrastructure & Platform (Print, DB, Services)',
  // Legacy mappings for backward compatibility
  DataQuality: 'Data Integrity (Legacy: Data Quality)',
  BusinessLogic: 'Process Integrity (Legacy: Business Logic)',
  ProcessFlow: 'Process Integrity (Legacy: Process Flow)',
};

export const CADENCE_UNITS = ['Minutes', 'Hours'] as const;

export type CadenceUnit = (typeof CADENCE_UNITS)[number];

/**
 * Zod validation schema for Detector Registration & Edit forms (S-05).
 * Validates name, description, canonical scope, target module, split cadence, and optional webhook URL.
 */
export const detectorFormSchema = z.object({
  name: z
    .string({
      required_error: 'Detector name is required',
    })
    .trim()
    .min(3, 'Detector name must be at least 3 characters')
    .max(100, 'Detector name cannot exceed 100 characters'),

  description: z
    .string({
      required_error: 'Description is required',
    })
    .trim()
    .min(5, 'Description must be at least 5 characters')
    .max(500, 'Description cannot exceed 500 characters'),

  scope: z.enum(DETECTOR_SCOPES, {
    required_error: 'Monitoring scope is required',
    invalid_type_error:
      'Monitoring scope must be one of: DataIntegrity, ProcessIntegrity, DataSync, Infrastructure',
  }),

  targetModule: z
    .string({
      required_error: 'Target module is required',
    })
    .trim()
    .min(1, 'Target module is required')
    .max(100, 'Target module cannot exceed 100 characters'),

  cadenceValue: z.coerce
    .number({
      required_error: 'Cadence value is required',
      invalid_type_error: 'Cadence value must be a number',
    })
    .positive('Cadence value must be greater than 0'),

  cadenceUnit: z.enum(CADENCE_UNITS, {
    required_error: 'Cadence unit is required',
    invalid_type_error: 'Cadence unit must be Minutes or Hours',
  }),

  webhookUrl: z
    .string()
    .trim()
    .refine(
      (val) => {
        if (!val || val.length === 0) return true;
        try {
          const parsed = new URL(val);
          return parsed.protocol === 'http:' || parsed.protocol === 'https:';
        } catch {
          return false;
        }
      },
      { message: 'Webhook URL must be a valid HTTP or HTTPS URL' }
    )
    .optional()
    .nullable(),
});

export type DetectorFormValues = z.infer<typeof detectorFormSchema>;

/**
 * Schema for edit mode where detector name is immutable.
 */
export const detectorEditFormSchema = detectorFormSchema.extend({
  name: z.string().trim().optional(),
});

export type DetectorEditFormValues = z.infer<typeof detectorEditFormSchema>;

