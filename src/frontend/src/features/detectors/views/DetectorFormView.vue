<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useForm } from 'vee-validate';
import { toTypedSchema } from '@vee-validate/zod';
import InputText from 'primevue/inputtext';
import Textarea from 'primevue/textarea';
import Dropdown from 'primevue/select';
import Button from 'primevue/button';
import Message from 'primevue/message';
import ProgressSpinner from 'primevue/progressspinner';
import { useToast } from 'primevue/usetoast';
import {
  ArrowLeft,
  Shield,
  Clock,
  Lock,
  Globe,
  AlertTriangle,
  Info,
  Sparkles,
  Terminal,
  Copy,
  Check,
} from 'lucide-vue-next';

import {
  detectorFormSchema,
  detectorEditFormSchema,
  DETECTOR_SCOPE_OPTIONS,
  DETECTOR_SCOPE_LABELS,
  type DetectorScope,
  type CadenceUnit,
  type DetectorFormValues,
} from '../schemas/detectorForm.schema';
import {
  secondsToCadence,
  cadenceToSeconds,
  getCadenceSummaryText,
  getSilenceThresholdText,
  getSilenceExplanationText,
} from '../composables/useDetectorForm';
import {
  useDetectorDetailQuery,
  useRegisterDetectorMutation,
  useUpdateDetectorMutation,
} from '../api/useDetectorQueries';
import { EnrollmentTokenModal } from '../components';
import { ApiError } from '@/shared/api/errors';
import type {
  RegisterDetectorRequest,
  UpdateDetectorRequest,
} from '@/shared/types/api.generated';

const route = useRoute();
const router = useRouter();
const toast = useToast();

// Extract route param :id
const detectorId = computed(() => {
  const raw = route.params.id;
  return typeof raw === 'string' ? raw : (raw?.[0] ?? '');
});

const isEditMode = computed(() => Boolean(detectorId.value));

// Registration mode switcher state: 'handshake' (Recommended) | 'manual' (§DEC-004)
const registrationMode = ref<'handshake' | 'manual'>('handshake');
const isEnrollmentModalOpen = ref(false);
const runnerSnippetCopied = ref(false);
const runnerSnippetText = `PRIMA_ENROLLMENT_TOKEN="<token>"\nPRIMA_SERVER_URL="https://prima.hospital.internal"\ndotnet run --project src/backend/PrIMA.Detectors`;

async function copyRunnerSnippet(): Promise<void> {
  try {
    await navigator.clipboard.writeText(runnerSnippetText);
    runnerSnippetCopied.value = true;
    toast.add({
      severity: 'info',
      summary: 'Copied to Clipboard',
      detail: 'Runner launch snippet copied.',
      life: 2500,
    });
    setTimeout(() => {
      runnerSnippetCopied.value = false;
    }, 2000);
  } catch (err) {
    console.error('Failed to copy snippet:', err);
  }
}

// Scope options for PrimeVue Dropdown with canonical values and backward compatibility
const scopeOptions = computed(() => {
  const options: Array<{ label: string; value: DetectorScope; description: string }> = [
    ...DETECTOR_SCOPE_OPTIONS,
  ];

  // If in edit mode and the existing detector has a legacy scope, append gracefully
  if (
    detectorData.value?.scope &&
    !options.some((o) => o.value === detectorData.value?.scope)
  ) {
    const legacyVal = detectorData.value.scope as DetectorScope;
    options.push({
      label: DETECTOR_SCOPE_LABELS[legacyVal] ?? legacyVal,
      value: legacyVal,
      description: 'Legacy monitoring scope from prior configuration.',
    });
  }

  return options;
});

// VeeValidate form initialization
// We choose the schema based on mode: in edit mode, name is immutable/optional
const formSchema = computed(() =>
  toTypedSchema(isEditMode.value ? detectorEditFormSchema : detectorFormSchema)
);

const {
  handleSubmit,
  defineField,
  errors,
  isSubmitting,
  resetForm,
} = useForm<DetectorFormValues>({
  validationSchema: formSchema,
  initialValues: {
    name: '',
    targetModule: '',
    scope: 'DataIntegrity',
    description: '',
    cadenceValue: 5,
    cadenceUnit: 'Minutes',
    webhookUrl: '',
  },
});

const [name] = defineField('name');
const [targetModule] = defineField('targetModule');
const [scope] = defineField('scope');
const [description] = defineField('description');
const [cadenceValue] = defineField('cadenceValue');
const [cadenceUnit] = defineField('cadenceUnit');
const [webhookUrl] = defineField('webhookUrl');

// Real-time calculated 2× silence threshold text (BR-PRIMA-010)
const cadenceSummaryText = computed(() => {
  const val = Number(cadenceValue.value) || 0;
  const unit = (cadenceUnit.value as CadenceUnit) || 'Minutes';
  return getCadenceSummaryText(val, unit);
});

const silenceThresholdText = computed(() => {
  const val = Number(cadenceValue.value) || 0;
  const unit = (cadenceUnit.value as CadenceUnit) || 'Minutes';
  return getSilenceThresholdText(val, unit);
});

const silenceExplanationText = computed(() => {
  const val = Number(cadenceValue.value) || 0;
  const unit = (cadenceUnit.value as CadenceUnit) || 'Minutes';
  return getSilenceExplanationText(val, unit);
});

function setCadenceUnit(unit: CadenceUnit) {
  cadenceUnit.value = unit;
}

// Detached Query Synchronization:
// Initial draft values are loaded once when query resolves; subsequent background
// polling refetches are ignored to prevent clearing or overwriting operator edits.
const isInitialized = ref(!isEditMode.value);
const detectorDetailQuery = isEditMode.value
  ? useDetectorDetailQuery(detectorId)
  : null;

const detectorData = computed(() => detectorDetailQuery?.data.value);
const isRetired = computed(() => detectorData.value?.adminStatus === 'Retired');

watch(
  () => detectorDetailQuery?.data.value,
  (detail) => {
    if (detail && isEditMode.value && !isInitialized.value) {
      const cadence = secondsToCadence(detail.expectedCadenceSeconds);
      resetForm({
        values: {
          name: detail.name,
          targetModule: detail.targetModule,
          scope: detail.scope as DetectorScope,
          description: detail.description,
          cadenceValue: cadence.cadenceValue,
          cadenceUnit: cadence.cadenceUnit,
          webhookUrl: detail.webhookUrl ?? '',
        },
      });
      isInitialized.value = true;
    }
  },
  { immediate: true }
);

// Reset form when transitioning between create and edit mode
watch(
  detectorId,
  (newId) => {
    if (!newId) {
      isInitialized.value = true;
      resetForm({
        values: {
          name: '',
          targetModule: '',
          scope: 'DataIntegrity',
          description: '',
          cadenceValue: 5,
          cadenceUnit: 'Minutes',
          webhookUrl: '',
        },
      });
    } else {
      isInitialized.value = false;
    }
  }
);

// Mutation handlers
const serverError = ref<string>('');
const registerMutation = useRegisterDetectorMutation();
const updateMutation = useUpdateDetectorMutation(detectorId);

const isSaving = computed(() => {
  return (
    isSubmitting.value ||
    registerMutation.isPending.value ||
    updateMutation.isPending.value
  );
});

function handleCancel() {
  if (window.history.length > 1) {
    router.back();
  } else if (isEditMode.value && detectorId.value) {
    router.push(`/detectors/${detectorId.value}`);
  } else {
    router.push('/detectors');
  }
}

const onSubmit = handleSubmit(async (values) => {
  if (isRetired.value) return;
  serverError.value = '';

  const seconds = cadenceToSeconds(Number(values.cadenceValue), values.cadenceUnit as CadenceUnit);

  try {
    if (isEditMode.value) {
      const payload: UpdateDetectorRequest = {
        description: values.description.trim(),
        scope: values.scope,
        targetModule: values.targetModule.trim(),
        expectedCadenceSeconds: seconds,
        webhookUrl: values.webhookUrl?.trim() || null,
      };

      await updateMutation.mutateAsync({
        id: detectorId.value,
        data: payload,
      });

      toast.add({
        severity: 'success',
        summary: 'Detector Updated',
        detail: `Configuration for '${values.name || detectorData.value?.name}' saved successfully.`,
        life: 4000,
      });

      await router.push(`/detectors/${detectorId.value}`);
    } else {
      const payload: RegisterDetectorRequest = {
        name: values.name.trim(),
        description: values.description.trim(),
        scope: values.scope,
        targetModule: values.targetModule.trim(),
        expectedCadenceSeconds: seconds,
        webhookUrl: values.webhookUrl?.trim() || null,
      };

      const created = await registerMutation.mutateAsync(payload);

      toast.add({
        severity: 'success',
        summary: 'Detector Registered',
        detail: `Detector '${created.name}' registered successfully.`,
        life: 4000,
      });

      if (created.id) {
        await router.push(`/detectors/${created.id}`);
      } else {
        await router.push('/detectors');
      }
    }
  } catch (err: unknown) {
    if (err instanceof ApiError) {
      if (err.status === 409) {
        serverError.value =
          err.detail || 'A detector with this name already exists or the detector cannot be modified.';
      } else {
        serverError.value =
          err.detail || err.title || 'An API error occurred while saving the detector.';
      }
    } else if (err instanceof Error) {
      serverError.value = err.message;
    } else {
      serverError.value = 'An unexpected error occurred while saving the detector.';
    }

    toast.add({
      severity: 'error',
      summary: 'Save Failed',
      detail: serverError.value,
      life: 5000,
    });
  }
});
</script>

<template>
  <div class="max-w-4xl mx-auto space-y-6 pb-12">
    <!-- Breadcrumb / Back button -->
    <div class="flex items-center justify-between">
      <button
        type="button"
        @click="handleCancel"
        class="inline-flex items-center gap-1.5 text-xs text-slate-500 hover:text-slate-800 transition-colors font-medium cursor-pointer"
      >
        <ArrowLeft class="w-3.5 h-3.5" />
        <span>{{ isEditMode ? 'Back to Detector Detail' : 'Back to Detectors' }}</span>
      </button>

      <span
        v-if="isEditMode && detectorData"
        class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-mono font-medium border"
        :class="
          isRetired
            ? 'bg-slate-100 text-slate-600 border-slate-300'
            : 'bg-emerald-50 text-emerald-700 border-emerald-200'
        "
      >
        <span
          class="w-1.5 h-1.5 rounded-full"
          :class="isRetired ? 'bg-slate-400' : 'bg-emerald-500'"
        ></span>
        <span>{{ detectorData.adminStatus }}</span>
      </span>
    </div>

    <!-- Page Header Bar -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-200 pb-4">
      <div>
        <div class="flex items-center gap-2">
          <Shield class="w-5 h-5 text-slate-700" />
          <h1 class="text-xl font-bold text-slate-900 leading-tight">
            {{ isEditMode ? 'Edit Detector Configuration' : 'Register New Detector' }}
          </h1>
        </div>
        <p class="text-xs text-slate-500 mt-1">
          {{
            isEditMode
              ? 'Update operational cadence, monitoring scope, or webhook integration for this probe (S-05)'
              : 'Register a proactive detector probe to continuously monitor SIMRS subsystem integrity (S-05)'
          }}
        </p>
      </div>
    </div>

    <!-- Segmented Registration Mode Switcher (S-05, DEC-004) -->
    <div v-if="!isEditMode" class="bg-slate-100 p-1 rounded-xl border border-slate-200">
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-1">
        <button
          type="button"
          class="flex items-center justify-center gap-2 py-2.5 px-4 rounded-lg text-xs font-semibold transition-all cursor-pointer"
          :class="
            registrationMode === 'handshake'
              ? 'bg-white text-slate-900 shadow-xs border border-slate-200/80'
              : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/60'
          "
          @click="registrationMode = 'handshake'"
        >
          <Sparkles class="w-4 h-4 text-amber-500" />
          <span>Automated Handshake (Recommended)</span>
        </button>

        <button
          type="button"
          class="flex items-center justify-center gap-2 py-2.5 px-4 rounded-lg text-xs font-semibold transition-all cursor-pointer"
          :class="
            registrationMode === 'manual'
              ? 'bg-white text-slate-900 shadow-xs border border-slate-200/80'
              : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/60'
          "
          @click="registrationMode = 'manual'"
        >
          <Shield class="w-4 h-4 text-slate-500" />
          <span>Manual Registration</span>
        </button>
      </div>
    </div>

    <!-- Automated Handshake Overview & Enrollment CTA Card (§DEC-004) -->
    <div
      v-if="!isEditMode && registrationMode === 'handshake'"
      class="bg-white rounded-xl border border-slate-200 shadow-2xs overflow-hidden divide-y divide-slate-100"
    >
      <!-- Hero Header -->
      <div class="p-6 bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 text-white space-y-3">
        <div class="flex items-center gap-2 text-amber-400 font-semibold text-xs tracking-wider uppercase">
          <Sparkles class="w-4 h-4" />
          <span>Zero-Drift Automated Enrollment Protocol</span>
        </div>
        <h2 class="text-lg font-bold text-white tracking-tight">
          Enroll Detector Probes with Automated Handshake
        </h2>
        <p class="text-xs text-slate-300 max-w-2xl leading-relaxed">
          Standardized detector runners automatically synchronize metadata, monitoring scope, cadence parameters,
          and secure per-detector hashed API keys directly into PrIMA. No manual configuration drift or key mishandling.
        </p>
        <div class="pt-2">
          <Button
            type="button"
            label="Generate Enrollment Token"
            icon="pi pi-key"
            class="!bg-amber-500 hover:!bg-amber-400 !text-slate-950 !border-transparent !font-bold !text-xs shadow-md"
            @click="isEnrollmentModalOpen = true"
          />
        </div>
      </div>

      <!-- 3-Step Protocol Walkthrough -->
      <div class="p-6 space-y-4">
        <h3 class="text-xs font-bold text-slate-900 uppercase tracking-wider">
          How Automated Handshake Works
        </h3>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <!-- Step 1 -->
          <div class="bg-slate-50 rounded-xl p-4 border border-slate-200 space-y-2">
            <div class="w-7 h-7 rounded-lg bg-amber-100 text-amber-800 flex items-center justify-center text-xs font-bold font-mono">
              1
            </div>
            <h4 class="text-xs font-semibold text-slate-900">Generate 24h Token</h4>
            <p class="text-[11px] text-slate-600 leading-relaxed">
              Generate a single-use cryptographically signed token secret with 24h TTL attributed to your EDP officer account.
            </p>
          </div>

          <!-- Step 2 -->
          <div class="bg-slate-50 rounded-xl p-4 border border-slate-200 space-y-2">
            <div class="w-7 h-7 rounded-lg bg-blue-100 text-blue-800 flex items-center justify-center text-xs font-bold font-mono">
              2
            </div>
            <h4 class="text-xs font-semibold text-slate-900">Launch Detector Runner</h4>
            <p class="text-[11px] text-slate-600 leading-relaxed">
              Export the enrollment token into the runner's environment variables and boot the .NET 8 probe service.
            </p>
          </div>

          <!-- Step 3 -->
          <div class="bg-slate-50 rounded-xl p-4 border border-slate-200 space-y-2">
            <div class="w-7 h-7 rounded-lg bg-emerald-100 text-emerald-800 flex items-center justify-center text-xs font-bold font-mono">
              3
            </div>
            <h4 class="text-xs font-semibold text-slate-900">Zero-Drift Synchronization</h4>
            <p class="text-[11px] text-slate-600 leading-relaxed">
              The runner executes <code class="font-mono bg-slate-200 px-1 py-0.5 rounded text-[10px]">POST /api/detectors/handshake</code>, provisions its unique API key, syncs manifest parameters, and activates monitoring.
            </p>
          </div>
        </div>
      </div>

      <!-- Quick Launch Snippet Block -->
      <div class="p-6 bg-slate-50/50 space-y-3">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <Terminal class="w-4 h-4 text-slate-600" />
            <span class="text-xs font-semibold text-slate-800 uppercase tracking-wider">
              Runner Bootstrap Environment Snippet
            </span>
          </div>

          <button
            type="button"
            class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-medium text-slate-600 hover:text-slate-900 bg-white border border-slate-300 rounded hover:bg-slate-50 transition-colors shadow-2xs cursor-pointer"
            @click="copyRunnerSnippet"
          >
            <component :is="runnerSnippetCopied ? Check : Copy" class="w-3.5 h-3.5 text-slate-600" />
            <span>{{ runnerSnippetCopied ? 'Copied' : 'Copy Snippet' }}</span>
          </button>
        </div>

        <div class="bg-slate-900 text-slate-100 font-mono text-xs rounded-lg p-3.5 overflow-x-auto select-all shadow-inner">
          <pre class="m-0 leading-relaxed"><code># Set environment variables and run .NET 8 detector runner
PRIMA_ENROLLMENT_TOKEN="PRM-TOKEN-..."
PRIMA_SERVER_URL="https://prima.hospital.internal"
dotnet run --project src/backend/PrIMA.Detectors</code></pre>
        </div>

        <div class="pt-2 flex items-center justify-between text-xs text-slate-500">
          <span>Need to register legacy detectors manually without handshake?</span>
          <button
            type="button"
            class="text-xs font-semibold text-blue-600 hover:text-blue-800 hover:underline cursor-pointer"
            @click="registrationMode = 'manual'"
          >
            Switch to Manual Registration &rarr;
          </button>
        </div>
      </div>
    </div>

    <!-- Loading State in Edit Mode -->
    <div
      v-if="isEditMode && !isInitialized && detectorDetailQuery?.isLoading.value"
      class="flex flex-col items-center justify-center py-16 bg-white rounded-xl border border-slate-200 text-slate-500 gap-3 shadow-2xs"
    >
      <ProgressSpinner style="width: 40px; height: 40px" strokeWidth="4" />
      <span class="text-xs font-medium">Loading detector configuration...</span>
    </div>

    <!-- Error State in Edit Mode -->
    <div
      v-else-if="isEditMode && !isInitialized && detectorDetailQuery?.isError.value"
      class="p-6 bg-white rounded-xl border border-rose-200 text-rose-800 shadow-2xs space-y-3"
    >
      <div class="flex items-center gap-2 font-semibold text-sm">
        <AlertTriangle class="w-4 h-4 text-rose-600" />
        <span>Failed to load detector details</span>
      </div>
      <p class="text-xs text-rose-700">
        {{ detectorDetailQuery?.error.value?.message || 'Could not retrieve detector record from server.' }}
      </p>
      <div class="flex items-center gap-2 pt-2">
        <Button
          label="Retry"
          icon="pi pi-refresh"
          severity="danger"
          size="small"
          class="text-xs"
          @click="() => detectorDetailQuery?.refetch()"
        />
        <Button
          label="Go Back"
          severity="secondary"
          size="small"
          class="text-xs"
          @click="handleCancel"
        />
      </div>
    </div>

    <!-- Retired Detector Read-Only Alert in Edit Mode -->
    <div
      v-else-if="isEditMode && isRetired"
      class="p-4 rounded-xl bg-slate-100 border border-slate-300 text-slate-700 flex items-start gap-3 shadow-2xs"
    >
      <Lock class="w-5 h-5 text-slate-500 shrink-0 mt-0.5" />
      <div>
        <h3 class="text-xs font-semibold text-slate-900">Detector is Permanently Retired</h3>
        <p class="text-xs text-slate-600 mt-0.5 leading-relaxed">
          This detector has been decommissioned. In accordance with system policy (DEC-008 / BR-PRIMA-008),
          retired detectors are permanently immutable and cannot be updated or reactivated.
        </p>
      </div>
    </div>

    <!-- Main Form Container -->
    <form
      v-else-if="registrationMode === 'manual' || isEditMode"
      @submit.prevent="onSubmit"
      class="bg-white rounded-xl border border-slate-200 shadow-2xs overflow-hidden"
    >
      <!-- Server Error Alert -->
      <div v-if="serverError" class="p-4 border-b border-rose-200 bg-rose-50">
        <Message severity="error" :closable="false" class="text-xs" role="alert">
          {{ serverError }}
        </Message>
      </div>

      <div class="p-6 space-y-6">
        <!-- Section 1: Identification & Target -->
        <div class="space-y-4">
          <div class="border-b border-slate-100 pb-2">
            <h2 class="text-xs font-bold text-slate-900 uppercase tracking-wider">
              1. Identification & Target Context
            </h2>
            <p class="text-[11px] text-slate-500">
              Core identity and hospital module boundaries
            </p>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <!-- Field: Detector Name -->
            <div class="sm:col-span-2">
              <label
                for="detector-name"
                class="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5"
              >
                Detector Name <span v-if="!isEditMode" class="text-rose-500">*</span>
              </label>

              <!-- Edit mode: Immutable / Read-only with lock icon -->
              <div v-if="isEditMode" class="relative">
                <InputText
                  id="detector-name"
                  :model-value="name"
                  disabled
                  class="w-full text-xs font-mono bg-slate-100 text-slate-700 cursor-not-allowed pr-9 border-slate-200"
                />
                <div class="absolute inset-y-0 right-0 flex items-center pr-3 pointer-events-none text-slate-400">
                  <Lock class="w-3.5 h-3.5" />
                </div>
              </div>

              <!-- Create mode: Editable -->
              <div v-else>
                <InputText
                  id="detector-name"
                  v-model="name"
                  placeholder="e.g. SIMRS Billing Discrepancy Detector"
                  class="w-full text-xs font-medium"
                  :invalid="!!errors.name"
                  :disabled="isSaving"
                />
                <p v-if="errors.name" class="text-xs text-rose-600 mt-1 font-medium" role="alert">
                  {{ errors.name }}
                </p>
              </div>

              <p class="text-[11px] text-slate-500 mt-1 flex items-center gap-1.5">
                <Info class="w-3 h-3 text-slate-400 shrink-0" />
                <span>
                  {{
                    isEditMode
                      ? 'Detector name is immutable and defines aggregate identity (BR-PRIMA-008).'
                      : 'Must be unique across all detectors (3–100 characters). Cannot be changed once created.'
                  }}
                </span>
              </p>
            </div>

            <!-- Field: Target Module -->
            <div>
              <label
                for="detector-target-module"
                class="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5"
              >
                Target Module <span class="text-rose-500">*</span>
              </label>
              <InputText
                id="detector-target-module"
                v-model="targetModule"
                placeholder="e.g. Billing, Pharmacy, Laboratory"
                class="w-full text-xs"
                :invalid="!!errors.targetModule"
                :disabled="isSaving"
              />
              <p v-if="errors.targetModule" class="text-xs text-rose-600 mt-1 font-medium" role="alert">
                {{ errors.targetModule }}
              </p>
              <p class="text-[11px] text-slate-500 mt-1">
                Hospital SIMRS subsystem or functional domain being supervised.
              </p>
            </div>

            <!-- Field: Monitoring Scope -->
            <div>
              <label
                for="detector-scope"
                class="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5"
              >
                Monitoring Scope <span class="text-rose-500">*</span>
              </label>
              <Dropdown
                id="detector-scope"
                v-model="scope"
                :options="scopeOptions"
                optionLabel="label"
                optionValue="value"
                placeholder="Select monitoring scope..."
                class="w-full text-xs"
                :invalid="!!errors.scope"
                :disabled="isSaving"
              >
                <template #option="slotProps">
                  <div class="py-0.5">
                    <div class="text-xs font-semibold text-slate-800">{{ slotProps.option.label }}</div>
                    <div class="text-[11px] text-slate-500 leading-tight mt-0.5">
                      {{ slotProps.option.description }}
                    </div>
                  </div>
                </template>
              </Dropdown>
              <p v-if="errors.scope" class="text-xs text-rose-600 mt-1 font-medium" role="alert">
                {{ errors.scope }}
              </p>
              <p class="text-[11px] text-slate-500 mt-1">
                Classification for SLA calculation and issue triage.
              </p>
            </div>
          </div>
        </div>

        <!-- Section 2: Split Cadence & Silence Threshold Policy -->
        <div class="space-y-4 pt-2">
          <div class="border-b border-slate-100 pb-2">
            <h2 class="text-xs font-bold text-slate-900 uppercase tracking-wider">
              2. Execution Cadence & Silence Threshold Policy
            </h2>
            <p class="text-[11px] text-slate-500">
              Expected heartbeat frequency and automatic silence threshold rules (BR-PRIMA-010)
            </p>
          </div>

          <!-- Split Cadence Picker -->
          <div class="space-y-2">
            <label class="block text-xs font-semibold text-slate-700 uppercase tracking-wider">
              Expected Cadence <span class="text-rose-500">*</span>
            </label>

            <div class="flex items-center gap-3">
              <div class="relative w-36">
                <input
                  id="detector-cadence-value"
                  v-model.number="cadenceValue"
                  type="number"
                  min="1"
                  step="1"
                  placeholder="5"
                  class="w-full text-xs font-mono font-semibold px-3 py-2 border rounded-md focus:outline-none focus:ring-1 focus:ring-slate-900 border-slate-300"
                  :class="{ 'border-rose-500': !!errors.cadenceValue }"
                  :disabled="isSaving"
                />
              </div>

              <!-- Cadence Unit Toggle Buttons -->
              <div class="inline-flex rounded-lg border border-slate-200 p-0.5 bg-slate-100 text-xs">
                <button
                  type="button"
                  @click="setCadenceUnit('Minutes')"
                  :disabled="isSaving"
                  class="px-3.5 py-1.5 rounded-md font-medium transition-colors cursor-pointer text-xs"
                  :class="
                    cadenceUnit === 'Minutes'
                      ? 'bg-slate-900 text-white shadow-2xs font-semibold'
                      : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200'
                  "
                >
                  Minutes
                </button>
                <button
                  type="button"
                  @click="setCadenceUnit('Hours')"
                  :disabled="isSaving"
                  class="px-3.5 py-1.5 rounded-md font-medium transition-colors cursor-pointer text-xs"
                  :class="
                    cadenceUnit === 'Hours'
                      ? 'bg-slate-900 text-white shadow-2xs font-semibold'
                      : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200'
                  "
                >
                  Hours
                </button>
              </div>
            </div>

            <p v-if="errors.cadenceValue" class="text-xs text-rose-600 font-medium" role="alert">
              {{ errors.cadenceValue }}
            </p>
            <p v-if="errors.cadenceUnit" class="text-xs text-rose-600 font-medium" role="alert">
              {{ errors.cadenceUnit }}
            </p>

            <!-- Dynamic Silence Helper Banner -->
            <div
              class="mt-3 p-3.5 rounded-lg bg-sky-50 border border-sky-200 text-sky-900 flex items-start gap-3 shadow-2xs"
            >
              <Clock class="w-4 h-4 text-sky-600 shrink-0 mt-0.5" />
              <div class="space-y-1.5">
                <div class="flex flex-wrap items-center gap-2">
                  <span
                    class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-mono font-semibold bg-sky-100 text-sky-800 border border-sky-200"
                  >
                    {{ cadenceSummaryText }}
                  </span>
                  <span
                    class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-mono font-semibold bg-amber-100 text-amber-800 border border-amber-200"
                  >
                    {{ silenceThresholdText }}
                  </span>
                </div>
                <p class="text-xs text-sky-900 font-medium leading-relaxed">
                  {{ silenceExplanationText }}
                </p>
                <p class="text-[11px] text-sky-700 leading-normal">
                  Per BR-PRIMA-010, the Monitoring Supervisor runs every 60s and synthesizes a
                  <code class="px-1 py-0.2 rounded bg-sky-100 font-mono text-[10px]">SILENT:{detectorId}</code>
                  issue if no telemetry heartbeat arrives within 2× the expected cadence.
                </p>
              </div>
            </div>
          </div>
        </div>

        <!-- Section 3: Webhook & Probe Integration (Optional) -->
        <div class="space-y-4 pt-2">
          <div class="border-b border-slate-100 pb-2">
            <h2 class="text-xs font-bold text-slate-900 uppercase tracking-wider">
              3. Webhook Integration (Optional)
            </h2>
            <p class="text-[11px] text-slate-500">
              Direct probe execution endpoint for on-demand trigger and automated verification re-runs
            </p>
          </div>

          <div>
            <label
              for="detector-webhook-url"
              class="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5"
            >
              Webhook URL
            </label>
            <div class="relative">
              <InputText
                id="detector-webhook-url"
                v-model="webhookUrl"
                placeholder="https://simrs.hospital.local/probes/billing-discrepancy"
                class="w-full text-xs font-mono"
                :invalid="!!errors.webhookUrl"
                :disabled="isSaving"
              />
              <div class="absolute inset-y-0 right-0 flex items-center pr-3 pointer-events-none text-slate-400">
                <Globe class="w-3.5 h-3.5" />
              </div>
            </div>
            <p v-if="errors.webhookUrl" class="text-xs text-rose-600 mt-1 font-medium" role="alert">
              {{ errors.webhookUrl }}
            </p>
            <p class="text-[11px] text-slate-500 mt-1">
              If configured, allows operators to trigger manual runs from the Detector Detail screen (S-04)
              and automated remediation verification from the Issue Workbench (S-02 / D-2).
            </p>
          </div>
        </div>

        <!-- Section 4: Operational Description -->
        <div class="space-y-4 pt-2">
          <div class="border-b border-slate-100 pb-2">
            <h2 class="text-xs font-bold text-slate-900 uppercase tracking-wider">
              4. Operational Description
            </h2>
            <p class="text-[11px] text-slate-500">
              Clear context and troubleshooting notes for EDP engineers and hospital analysts
            </p>
          </div>

          <div>
            <div class="flex items-center justify-between mb-1.5">
              <label
                for="detector-description"
                class="block text-xs font-semibold text-slate-700 uppercase tracking-wider"
              >
                Description & SOP Guidelines <span class="text-rose-500">*</span>
              </label>
              <span class="text-[11px] text-slate-400 font-mono">
                {{ (description || '').trim().length }}/500 chars (min 5)
              </span>
            </div>
            <Textarea
              id="detector-description"
              v-model="description"
              rows="4"
              placeholder="Describe what anomaly this detector monitors, conditions that trigger alerts, and resolution guidance..."
              class="w-full text-xs leading-relaxed"
              :invalid="!!errors.description"
              :disabled="isSaving"
            />
            <p v-if="errors.description" class="text-xs text-rose-600 mt-1 font-medium" role="alert">
              {{ errors.description }}
            </p>
            <p class="text-[11px] text-slate-500 mt-1">
              Provide actionable guidance to assist engineers when triaging issues raised by this probe.
            </p>
          </div>
        </div>
      </div>

      <!-- Action Footer -->
      <div class="p-4 sm:px-6 bg-slate-50 border-t border-slate-200 flex items-center justify-between gap-3">
        <Button
          type="button"
          label="Cancel"
          text
          severity="secondary"
          size="small"
          class="text-xs"
          :disabled="isSaving"
          @click="handleCancel"
        />

        <div class="flex items-center gap-3">
          <Button
            type="submit"
            :label="isEditMode ? 'Save Changes' : 'Register Detector'"
            :icon="isSaving ? 'pi pi-spin pi-spinner' : isEditMode ? 'pi pi-check' : 'pi pi-plus'"
            severity="contrast"
            size="small"
            class="text-xs font-medium"
            :loading="isSaving"
            :disabled="isSaving || (isEditMode && isRetired)"
          />
        </div>
      </div>
    </form>

    <!-- Enrollment Token Modal (Quick Enroll §DEC-004) -->
    <EnrollmentTokenModal
      v-model:visible="isEnrollmentModalOpen"
    />
  </div>
</template>
