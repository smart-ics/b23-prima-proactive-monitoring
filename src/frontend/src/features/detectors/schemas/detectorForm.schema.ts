import { z } from 'zod';

export const DETECTOR_SCOPES = [
  'DataQuality',
  'BusinessLogic',
  'Infrastructure',
  'ProcessFlow',
] as const;

export const CADENCE_UNITS = ['Minutes', 'Hours'] as const;

export type CadenceUnit = (typeof CADENCE_UNITS)[number];
export type DetectorScope = (typeof DETECTOR_SCOPES)[number];

/**
 * Zod validation schema for Detector Registration & Edit forms (S-05).
 * Validates name, description, scope, target module, split cadence, and optional webhook URL.
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
    invalid_type_error: 'Monitoring scope must be one of: DataQuality, BusinessLogic, Infrastructure, ProcessFlow',
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
