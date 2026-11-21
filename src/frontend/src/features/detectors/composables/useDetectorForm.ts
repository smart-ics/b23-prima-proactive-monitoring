import { ref, computed, type Ref, type ComputedRef } from 'vue';
import type {
  RegisterDetectorRequest,
  UpdateDetectorRequest,
} from '@/shared/types/api.generated';
import type {
  CadenceUnit,
  DetectorFormValues,
} from '../schemas/detectorForm.schema';

/**
 * Converts split cadence (numeric value and unit) into total integer seconds.
 */
export function cadenceToSeconds(value: number, unit: CadenceUnit): number {
  const num = Number(value) || 0;
  if (num <= 0) return 0;
  return unit === 'Hours' ? Math.round(num * 3600) : Math.round(num * 60);
}

/**
 * Converts total seconds into split cadence value and unit.
 * Defaults to minutes unless the seconds represent exact whole hours >= 1 hour.
 */
export function secondsToCadence(totalSeconds: number): {
  cadenceValue: number;
  cadenceUnit: CadenceUnit;
} {
  if (!totalSeconds || totalSeconds <= 0) {
    return { cadenceValue: 5, cadenceUnit: 'Minutes' };
  }

  if (totalSeconds >= 3600 && totalSeconds % 3600 === 0) {
    return {
      cadenceValue: Math.round(totalSeconds / 3600),
      cadenceUnit: 'Hours',
    };
  }

  return {
    cadenceValue: Math.max(1, Math.round(totalSeconds / 60)),
    cadenceUnit: 'Minutes',
  };
}

/**
 * Generates concise cadence summary text (e.g. "Cadence: 5 min").
 */
export function getCadenceSummaryText(value: number, unit: CadenceUnit): string {
  const num = Number(value) || 0;
  const unitLabel = unit === 'Hours' ? (num === 1 ? 'hr' : 'hrs') : 'min';
  return `Cadence: ${num} ${unitLabel}`;
}

/**
 * Generates concise silence threshold text (e.g. "Silence threshold: 10 min").
 * Silence threshold is 2x expected cadence per BR-PRIMA-010.
 */
export function getSilenceThresholdText(value: number, unit: CadenceUnit): string {
  const num = Number(value) || 0;
  const threshold = num * 2;
  const unitLabel = unit === 'Hours' ? (threshold === 1 ? 'hr' : 'hrs') : 'min';
  return `Silence threshold: ${threshold} ${unitLabel}`;
}

/**
 * Generates combined cadence and silence threshold text (e.g. "Cadence: 5 min · Silence threshold: 10 min").
 */
export function getCombinedCadenceHelperText(value: number, unit: CadenceUnit): string {
  return `${getCadenceSummaryText(value, unit)} · ${getSilenceThresholdText(value, unit)}`;
}

/**
 * Generates human-friendly silence explanation sentence for form alert banners.
 */
export function getSilenceExplanationText(value: number, unit: CadenceUnit): string {
  const num = Number(value) || 0;
  const threshold = num * 2;
  const unitStr = unit === 'Hours' ? (num === 1 ? 'hour' : 'hours') : (num === 1 ? 'minute' : 'minutes');
  const threshUnitStr = unit === 'Hours' ? (threshold === 1 ? 'hour' : 'hours') : (threshold === 1 ? 'minute' : 'minutes');
  return `Detector expected every ${num} ${unitStr}. PrIMA will flag detector as Silent if no heartbeat arrives for ${threshold} ${threshUnitStr}.`;
}

export interface UseDetectorFormOptions {
  initialCadenceSeconds?: number;
  initialCadenceValue?: number;
  initialCadenceUnit?: CadenceUnit;
}

export interface UseDetectorFormReturn {
  cadenceValue: Ref<number>;
  cadenceUnit: Ref<CadenceUnit>;
  totalCadenceSeconds: ComputedRef<number>;
  cadenceSummaryText: ComputedRef<string>;
  silenceThresholdText: ComputedRef<string>;
  combinedHelperText: ComputedRef<string>;
  silenceExplanationText: ComputedRef<string>;
  setFromSeconds: (seconds: number) => void;
  toRegisterPayload: (formValues: DetectorFormValues) => RegisterDetectorRequest;
  toUpdatePayload: (formValues: DetectorFormValues) => UpdateDetectorRequest;
}

/**
 * Composable for managing detector cadence input state, unit conversion,
 * and real-time calculated 2x silence threshold helper text.
 */
export function useDetectorForm(options?: UseDetectorFormOptions): UseDetectorFormReturn {
  const initial = options?.initialCadenceSeconds
    ? secondsToCadence(options.initialCadenceSeconds)
    : {
        cadenceValue: options?.initialCadenceValue ?? 5,
        cadenceUnit: options?.initialCadenceUnit ?? 'Minutes',
      };

  const cadenceValue = ref<number>(initial.cadenceValue);
  const cadenceUnit = ref<CadenceUnit>(initial.cadenceUnit);

  const totalCadenceSeconds = computed(() =>
    cadenceToSeconds(cadenceValue.value, cadenceUnit.value)
  );

  const cadenceSummaryText = computed(() =>
    getCadenceSummaryText(cadenceValue.value, cadenceUnit.value)
  );

  const silenceThresholdText = computed(() =>
    getSilenceThresholdText(cadenceValue.value, cadenceUnit.value)
  );

  const combinedHelperText = computed(() =>
    getCombinedCadenceHelperText(cadenceValue.value, cadenceUnit.value)
  );

  const silenceExplanationText = computed(() =>
    getSilenceExplanationText(cadenceValue.value, cadenceUnit.value)
  );

  function setFromSeconds(seconds: number): void {
    const converted = secondsToCadence(seconds);
    cadenceValue.value = converted.cadenceValue;
    cadenceUnit.value = converted.cadenceUnit;
  }

  function toRegisterPayload(formValues: DetectorFormValues): RegisterDetectorRequest {
    return {
      name: formValues.name.trim(),
      description: formValues.description.trim(),
      scope: formValues.scope,
      targetModule: formValues.targetModule.trim(),
      expectedCadenceSeconds: cadenceToSeconds(formValues.cadenceValue, formValues.cadenceUnit),
      webhookUrl: formValues.webhookUrl?.trim() || null,
    };
  }

  function toUpdatePayload(formValues: DetectorFormValues): UpdateDetectorRequest {
    return {
      description: formValues.description.trim(),
      scope: formValues.scope,
      targetModule: formValues.targetModule.trim(),
      expectedCadenceSeconds: cadenceToSeconds(formValues.cadenceValue, formValues.cadenceUnit),
      webhookUrl: formValues.webhookUrl?.trim() || null,
    };
  }

  return {
    cadenceValue,
    cadenceUnit,
    totalCadenceSeconds,
    cadenceSummaryText,
    silenceThresholdText,
    combinedHelperText,
    silenceExplanationText,
    setFromSeconds,
    toRegisterPayload,
    toUpdatePayload,
  };
}

/**
 * Formats cadence in seconds to a human-friendly string (e.g. "Every 5m", "Every 1h", "Every 30s").
 * Conforms to Architecture §9.5 and S-03 display requirements.
 */
export function formatCadence(seconds?: number | null): string {
  if (!seconds || seconds <= 0) return '-';
  if (seconds < 60) return `Every ${seconds}s`;
  if (seconds < 3600) {
    const mins = Math.round(seconds / 60);
    return `Every ${mins}m`;
  }
  const hours = Math.round(seconds / 3600);
  return `Every ${hours}h`;
}

/**
 * Returns Tailwind CSS classes for color-coding the 30-day clean pass rate badge.
 * Conforms to DEC-008 & Architecture §9.5:
 * - 0 runs: Slate (100.0%)
 * - >= 95.0%: Emerald
 * - 85.0% - 94.9%: Amber
 * - < 85.0%: Rose
 */
export function getPassRateBadgeClass(passRate?: number | null, totalRuns = 1): string {
  if (totalRuns === 0) {
    return 'bg-slate-100 text-slate-600 border-slate-200';
  }
  const rate = Number(passRate ?? 100);
  if (rate >= 95.0) {
    return 'bg-emerald-50 text-emerald-700 border-emerald-200';
  }
  if (rate >= 85.0) {
    return 'bg-amber-50 text-amber-700 border-amber-200';
  }
  return 'bg-rose-50 text-rose-700 border-rose-200';
}

/**
 * Returns label, badge class, and dot class for detector status combinations.
 */
export function getDetectorStatusBadge(adminStatus?: string | null, healthStatus?: string | null): {
  label: string;
  badgeClass: string;
  dotClass: string;
} {
  if (adminStatus === 'Paused') {
    return {
      label: 'Paused',
      badgeClass: 'bg-slate-100 text-slate-700 border-slate-300',
      dotClass: 'bg-slate-400',
    };
  }
  if (adminStatus === 'Retired') {
    return {
      label: 'Retired',
      badgeClass: 'bg-gray-100 text-gray-500 border-gray-300',
      dotClass: 'bg-gray-400',
    };
  }
  switch (healthStatus) {
    case 'Healthy':
      return {
        label: 'Healthy',
        badgeClass: 'bg-emerald-50 text-emerald-700 border-emerald-200',
        dotClass: 'bg-emerald-500',
      };
    case 'Failing':
      return {
        label: 'Failing',
        badgeClass: 'bg-rose-50 text-rose-700 border-rose-200',
        dotClass: 'bg-rose-500 animate-pulse',
      };
    case 'Silent':
      return {
        label: 'Silent',
        badgeClass: 'bg-amber-50 text-amber-700 border-amber-200',
        dotClass: 'bg-amber-500 animate-pulse',
      };
    default:
      return {
        label: healthStatus || adminStatus || 'Unknown',
        badgeClass: 'bg-slate-100 text-slate-700 border-slate-200',
        dotClass: 'bg-slate-400',
      };
  }
}

