<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useForm } from 'vee-validate';
import { toTypedSchema } from '@vee-validate/zod';
import Dialog from 'primevue/dialog';
import Textarea from 'primevue/textarea';
import Button from 'primevue/button';
import Message from 'primevue/message';
import { useToast } from 'primevue/usetoast';

import {
  manualVerificationSchema,
  type ManualVerificationFormValues,
} from '@/features/issues/schemas/issueModals.schema';
import { useRecordVerificationMutation } from '@/features/issues/api/useIssueDetailQueries';
import { ApiError } from '@/shared/api/errors';

const props = defineProps<{
  visible: boolean;
  issueId: string;
}>();

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void;
  (e: 'verified'): void;
}>();

const toast = useToast();
const verificationMutation = useRecordVerificationMutation();
const errorMessage = ref<string>('');

const { handleSubmit, defineField, errors, isSubmitting, resetForm } =
  useForm<ManualVerificationFormValues>({
    validationSchema: toTypedSchema(manualVerificationSchema),
    initialValues: {
      cleanPassed: undefined as unknown as boolean,
      evidenceNotes: '',
    },
  });

const [cleanPassed] = defineField('cleanPassed');
const [evidenceNotes] = defineField('evidenceNotes');

const isSubmitDisabled = computed(() => {
  return (
    cleanPassed.value === undefined ||
    cleanPassed.value === null ||
    !(evidenceNotes.value || '').trim() ||
    isSubmitting.value ||
    verificationMutation.isPending.value
  );
});

function handleClose() {
  if (isSubmitting.value || verificationMutation.isPending.value) return;
  emit('update:visible', false);
  errorMessage.value = '';
}

// Reset error message when dialog opens
watch(
  () => props.visible,
  (newVal) => {
    if (newVal) {
      errorMessage.value = '';
    }
  }
);

function selectOutcome(outcome: boolean) {
  cleanPassed.value = outcome;
}

const onSubmit = handleSubmit(async (values) => {
  errorMessage.value = '';

  try {
    await verificationMutation.mutateAsync({
      issueId: props.issueId,
      cleanPassed: values.cleanPassed,
      evidenceNotes: values.evidenceNotes.trim(),
    });

    toast.add({
      severity: values.cleanPassed ? 'success' : 'warn',
      summary: values.cleanPassed
        ? 'Verification Passed'
        : 'Verification Failed (Anomaly Still Present)',
      detail: values.cleanPassed
        ? 'Manual verification passed clean. Issue is now eligible for resolution.'
        : 'Anomaly still present. Issue requires further remediation.',
      life: 4000,
    });

    emit('verified');
    emit('update:visible', false);
    resetForm();
  } catch (err: unknown) {
    if (err instanceof ApiError) {
      errorMessage.value =
        err.detail || 'Failed to record verification due to an API error.';
    } else if (err instanceof Error) {
      errorMessage.value = err.message;
    } else {
      errorMessage.value =
        'An unexpected error occurred while recording verification.';
    }

    toast.add({
      severity: 'error',
      summary: 'Verification Failed',
      detail: errorMessage.value,
      life: 5000,
    });
  }
});
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    header="Record Manual Verification"
    :style="{ width: '36rem' }"
    :closable="!isSubmitting && !verificationMutation.isPending.value"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4 pt-1">
      <!-- Explanatory Header Alert -->
      <p class="text-xs text-slate-600 leading-relaxed">
        Verify whether the issue has been resolved by re-running the inspection query or inspecting system state.
        A <strong>Clean Passed</strong> outcome is required by governance rule
        (<code class="text-[11px] font-mono bg-slate-100 px-1 py-0.5 rounded">BR-PRIMA-004</code>)
        to unlock the <strong>Mark Resolved</strong> transition.
      </p>

      <!-- Error Message Banner -->
      <Message
        v-if="errorMessage"
        severity="error"
        :closable="false"
        class="text-xs"
        role="alert"
      >
        {{ errorMessage }}
      </Message>

      <!-- Verification Form -->
      <form @submit.prevent="onSubmit" class="space-y-4">
        <!-- Selectable Radio Cards for Outcome -->
        <div>
          <label class="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-2">
            Verification Outcome <span class="text-rose-500">*</span>
          </label>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3" role="radiogroup" aria-label="Verification Outcome">
            <!-- Card 1: CleanPassed -->
            <button
              type="button"
              role="radio"
              :aria-checked="cleanPassed === true"
              @click="selectOutcome(true)"
              class="flex flex-col text-left p-3.5 rounded-lg transition-all border cursor-pointer focus:outline-none focus:ring-2 focus:ring-emerald-500"
              :class="
                cleanPassed === true
                  ? 'border-emerald-500 bg-emerald-50/70 shadow-xs ring-1 ring-emerald-500'
                  : 'border-slate-200 bg-white hover:border-emerald-300 hover:bg-slate-50/50'
              "
            >
              <div class="flex items-center justify-between w-full mb-1.5">
                <div class="flex items-center gap-2">
                  <i
                    class="pi text-base"
                    :class="cleanPassed === true ? 'pi-check-circle text-emerald-600' : 'pi-circle text-slate-400'"
                  ></i>
                  <span class="text-xs font-bold text-slate-900">Clean Passed</span>
                </div>
                <span
                  class="text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full border"
                  :class="
                    cleanPassed === true
                      ? 'bg-emerald-100 text-emerald-800 border-emerald-200'
                      : 'bg-slate-100 text-slate-600 border-slate-200'
                  "
                >
                  Resolved
                </span>
              </div>
              <p class="text-[11px] text-slate-600 leading-snug">
                Anomaly Resolved / Clean Query. Query returned zero anomalous rows or matched expected baseline.
              </p>
            </button>

            <!-- Card 2: AnomalyStillPresent -->
            <button
              type="button"
              role="radio"
              :aria-checked="cleanPassed === false"
              @click="selectOutcome(false)"
              class="flex flex-col text-left p-3.5 rounded-lg transition-all border cursor-pointer focus:outline-none focus:ring-2 focus:ring-rose-500"
              :class="
                cleanPassed === false
                  ? 'border-rose-500 bg-rose-50/70 shadow-xs ring-1 ring-rose-500'
                  : 'border-slate-200 bg-white hover:border-rose-300 hover:bg-slate-50/50'
              "
            >
              <div class="flex items-center justify-between w-full mb-1.5">
                <div class="flex items-center gap-2">
                  <i
                    class="pi text-base"
                    :class="cleanPassed === false ? 'pi-times-circle text-rose-600' : 'pi-circle text-slate-400'"
                  ></i>
                  <span class="text-xs font-bold text-slate-900">Still Present</span>
                </div>
                <span
                  class="text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full border"
                  :class="
                    cleanPassed === false
                      ? 'bg-rose-100 text-rose-800 border-rose-200'
                      : 'bg-slate-100 text-slate-600 border-slate-200'
                  "
                >
                  Failed
                </span>
              </div>
              <p class="text-[11px] text-slate-600 leading-snug">
                Anomaly Still Present / Failed. Query still reports discrepancy or unexpected database state.
              </p>
            </button>
          </div>

          <p
            v-if="errors.cleanPassed"
            class="text-xs text-rose-600 mt-1.5 font-medium"
            role="alert"
          >
            {{ errors.cleanPassed }}
          </p>
        </div>

        <!-- Evidence Notes Textarea -->
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <label
              for="evidence-notes"
              class="block text-xs font-semibold text-slate-700 uppercase tracking-wider"
            >
              Evidence Notes <span class="text-rose-500">*</span>
            </label>
            <span class="text-[11px] text-slate-500">
              Query row count, log output, or observation
            </span>
          </div>

          <Textarea
            id="evidence-notes"
            v-model="evidenceNotes"
            rows="3"
            autoResize
            placeholder="e.g. Executed verification query in staging DB: returned 0 rows. Variance is 0.00 IDR."
            class="w-full text-xs font-sans"
            :invalid="!!errors.evidenceNotes"
            :disabled="isSubmitting || verificationMutation.isPending.value"
          />

          <p
            v-if="errors.evidenceNotes"
            class="text-xs text-rose-600 mt-1 font-medium"
            role="alert"
          >
            {{ errors.evidenceNotes }}
          </p>
        </div>

        <!-- Dynamic Status Notice Banner -->
        <div
          v-if="cleanPassed === true"
          class="p-2.5 rounded-lg bg-emerald-50 border border-emerald-200 flex items-start gap-2 text-xs text-emerald-900"
        >
          <i class="pi pi-check-circle text-emerald-600 mt-0.5 shrink-0"></i>
          <span>
            Recording <strong>Clean Passed</strong> unlocks the <strong>Mark Resolved</strong> action in the workbench header.
          </span>
        </div>
        <div
          v-else-if="cleanPassed === false"
          class="p-2.5 rounded-lg bg-rose-50 border border-rose-200 flex items-start gap-2 text-xs text-rose-900"
        >
          <i class="pi pi-exclamation-triangle text-rose-600 mt-0.5 shrink-0"></i>
          <span>
            Recording <strong>Still Present</strong> preserves the issue in <strong>Investigating</strong> status and flags that additional remediation is required.
          </span>
        </div>

        <!-- Modal Actions Footer -->
        <div class="flex items-center justify-end gap-2.5 pt-3 border-t border-slate-100">
          <Button
            type="button"
            label="Cancel"
            text
            severity="secondary"
            class="text-xs"
            :disabled="isSubmitting || verificationMutation.isPending.value"
            @click="handleClose"
          />
          <Button
            type="submit"
            :label="cleanPassed === false ? 'Record Verification (Failed)' : 'Record Verification'"
            :icon="cleanPassed === false ? 'pi pi-times' : 'pi pi-check'"
            :severity="cleanPassed === false ? 'warn' : 'success'"
            class="text-xs"
            :class="cleanPassed === true ? 'bg-emerald-600 hover:bg-emerald-700 border-emerald-600 text-white' : ''"
            :loading="isSubmitting || verificationMutation.isPending.value"
            :disabled="isSubmitDisabled"
          />
        </div>
      </form>
    </div>
  </Dialog>
</template>