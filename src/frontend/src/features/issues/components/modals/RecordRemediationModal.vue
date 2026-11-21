<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useForm } from 'vee-validate';
import { toTypedSchema } from '@vee-validate/zod';
import Dialog from 'primevue/dialog';
import Select from 'primevue/select';
import Textarea from 'primevue/textarea';
import Button from 'primevue/button';
import Message from 'primevue/message';
import { useToast } from 'primevue/usetoast';

import {
  recordRemediationSchema,
  REMEDIATION_CATEGORY_OPTIONS,
  type RecordRemediationFormValues,
} from '@/features/issues/schemas/issueModals.schema';
import { useRecordRemediationMutation } from '@/features/issues/api/useIssueDetailQueries';
import { ApiError } from '@/shared/api/errors';
import type { RemediationActionCategory } from '@/shared/types/api.generated';

const props = defineProps<{
  visible: boolean;
  issueId: string;
}>();

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void;
  (e: 'recorded'): void;
}>();

const toast = useToast();
const remediationMutation = useRecordRemediationMutation();
const errorMessage = ref<string>('');

const { handleSubmit, defineField, errors, isSubmitting, resetForm } =
  useForm<RecordRemediationFormValues>({
    validationSchema: toTypedSchema(recordRemediationSchema),
    initialValues: {
      actionCategory: '' as RemediationActionCategory,
      summaryDescription: '',
    },
  });

const [actionCategory] = defineField('actionCategory');
const [summaryDescription] = defineField('summaryDescription');

const isSubmitDisabled = computed(() => {
  return (
    !actionCategory.value ||
    !(summaryDescription.value || '').trim() ||
    isSubmitting.value ||
    remediationMutation.isPending.value
  );
});

function handleClose() {
  if (isSubmitting.value || remediationMutation.isPending.value) return;
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

const onSubmit = handleSubmit(async (values) => {
  errorMessage.value = '';

  try {
    await remediationMutation.mutateAsync({
      issueId: props.issueId,
      actionCategory: values.actionCategory,
      summaryDescription: values.summaryDescription.trim(),
    });

    toast.add({
      severity: 'success',
      summary: 'Remediation Recorded',
      detail:
        'Remediation action recorded successfully. Active verifications have been superseded.',
      life: 4000,
    });

    emit('recorded');
    emit('update:visible', false);
    resetForm();
  } catch (err: unknown) {
    if (err instanceof ApiError) {
      errorMessage.value =
        err.detail || 'Failed to record remediation due to an API error.';
    } else if (err instanceof Error) {
      errorMessage.value = err.message;
    } else {
      errorMessage.value =
        'An unexpected error occurred while recording remediation.';
    }

    toast.add({
      severity: 'error',
      summary: 'Remediation Failed',
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
    header="Record Remediation Action"
    :style="{ width: '34rem' }"
    :closable="!isSubmitting && !remediationMutation.isPending.value"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4 pt-1">
      <!-- Explanatory Header Alert -->
      <p class="text-xs text-slate-600 leading-relaxed">
        Record the operational fix or corrective action applied to address this anomaly.
        Per system policy (<code class="text-[11px] font-mono bg-slate-100 px-1 py-0.5 rounded">OQ-005</code>),
        recording a new remediation automatically supersedes any existing verification records,
        requiring fresh verification before the issue can be resolved.
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

      <!-- Remediation Form -->
      <form @submit.prevent="onSubmit" class="space-y-4">
        <!-- Action Category Select -->
        <div>
          <label
            for="remediation-category"
            class="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5"
          >
            Action Category <span class="text-rose-500">*</span>
          </label>
          <Select
            id="remediation-category"
            v-model="actionCategory"
            :options="REMEDIATION_CATEGORY_OPTIONS"
            optionLabel="label"
            optionValue="value"
            placeholder="Select action category..."
            class="w-full text-xs"
            :invalid="!!errors.actionCategory"
            :disabled="isSubmitting || remediationMutation.isPending.value"
          />
          <p
            v-if="errors.actionCategory"
            class="text-xs text-rose-600 mt-1 font-medium"
            role="alert"
          >
            {{ errors.actionCategory }}
          </p>
        </div>

        <!-- Summary Description Textarea -->
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <label
              for="summary-description"
              class="block text-xs font-semibold text-slate-700 uppercase tracking-wider"
            >
              Summary Description <span class="text-rose-500">*</span>
            </label>
            <span class="text-[11px] text-slate-500">
              SOP, SQL script ID, or action taken
            </span>
          </div>

          <Textarea
            id="summary-description"
            v-model="summaryDescription"
            rows="4"
            autoResize
            placeholder="Reference executed SQL script, SOP step, service restart, or bug report ID..."
            class="w-full text-xs font-sans"
            :invalid="!!errors.summaryDescription"
            :disabled="isSubmitting || remediationMutation.isPending.value"
          />

          <p
            v-if="errors.summaryDescription"
            class="text-xs text-rose-600 mt-1 font-medium"
            role="alert"
          >
            {{ errors.summaryDescription }}
          </p>
        </div>

        <!-- Notice Pill -->
        <div class="p-2.5 rounded-lg bg-indigo-50/70 border border-indigo-100 flex items-start gap-2 text-xs text-indigo-900">
          <i class="pi pi-info-circle text-indigo-600 mt-0.5 shrink-0"></i>
          <span>
            Once recorded, you will be prompted to perform a <strong>Manual Verification</strong> check to verify the anomaly is resolved.
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
            :disabled="isSubmitting || remediationMutation.isPending.value"
            @click="handleClose"
          />
          <Button
            type="submit"
            label="Record Remediation"
            icon="pi pi-wrench"
            severity="primary"
            class="text-xs bg-slate-900 hover:bg-slate-800 text-white"
            :loading="isSubmitting || remediationMutation.isPending.value"
            :disabled="isSubmitDisabled"
          />
        </div>
      </form>
    </div>
  </Dialog>
</template>