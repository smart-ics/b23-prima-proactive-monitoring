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
  dismissIssueSchema,
  DISMISSAL_REASON_OPTIONS,
  type DismissIssueFormValues,
} from '@/features/issues/schemas/issueModals.schema';
import { useDismissIssueMutation } from '@/features/issues/api/useIssueDetailQueries';
import { ApiError } from '@/shared/api/errors';
import type { DismissalReason } from '@/shared/types/api.generated';

const props = defineProps<{
  visible: boolean;
  issueId: string;
}>();

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void;
  (e: 'dismissed'): void;
}>();

const toast = useToast();
const dismissMutation = useDismissIssueMutation();
const errorMessage = ref<string>('');

const { handleSubmit, defineField, errors, isSubmitting, resetForm } =
  useForm<DismissIssueFormValues>({
    validationSchema: toTypedSchema(dismissIssueSchema),
    initialValues: {
      dismissalReason: '' as DismissalReason,
      justificationRationale: '',
    },
  });

const [dismissalReason] = defineField('dismissalReason');
const [justificationRationale] = defineField('justificationRationale');

const trimmedRationaleLength = computed(() => {
  return (justificationRationale.value || '').trim().length;
});

const isRationaleValid = computed(() => {
  return trimmedRationaleLength.value >= 20;
});

const isSubmitDisabled = computed(() => {
  return (
    !dismissalReason.value ||
    !isRationaleValid.value ||
    isSubmitting.value ||
    dismissMutation.isPending.value
  );
});

function handleClose() {
  if (isSubmitting.value || dismissMutation.isPending.value) return;
  emit('update:visible', false);
  errorMessage.value = '';
}

// Reset validation state when dialog opens
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
    await dismissMutation.mutateAsync({
      issueId: props.issueId,
      dismissalReason: values.dismissalReason,
      justificationRationale: values.justificationRationale.trim(),
    });

    toast.add({
      severity: 'info',
      summary: 'Issue Dismissed',
      detail: 'The issue has been marked as dismissed.',
      life: 4000,
    });

    emit('dismissed');
    emit('update:visible', false);
    resetForm();
  } catch (err: unknown) {
    if (err instanceof ApiError) {
      errorMessage.value =
        err.detail || 'Failed to dismiss issue due to an API error.';
    } else if (err instanceof Error) {
      errorMessage.value = err.message;
    } else {
      errorMessage.value = 'An unexpected error occurred while dismissing the issue.';
    }

    toast.add({
      severity: 'error',
      summary: 'Dismissal Failed',
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
    header="Dismiss Issue"
    :style="{ width: '34rem' }"
    :closable="!isSubmitting && !dismissMutation.isPending.value"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4 pt-1">
      <!-- Explanatory Header Alert -->
      <p class="text-xs text-slate-600 leading-relaxed">
        Dismissing an issue closes it without requiring remediation or clean verification.
        A detailed justification rationale (<strong class="text-slate-900">minimum 20 characters</strong>)
        is required by system policy for operational audit compliance (<code class="text-[11px] font-mono bg-slate-100 px-1 py-0.5 rounded">BR-PRIMA-006</code>).
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

      <!-- Dismissal Form -->
      <form @submit.prevent="onSubmit" class="space-y-4">
        <!-- Dismissal Reason Select -->
        <div>
          <label
            for="dismissal-reason"
            class="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5"
          >
            Dismissal Reason <span class="text-rose-500">*</span>
          </label>
          <Select
            id="dismissal-reason"
            v-model="dismissalReason"
            :options="DISMISSAL_REASON_OPTIONS"
            optionLabel="label"
            optionValue="value"
            placeholder="Select a reason..."
            class="w-full text-xs"
            :invalid="!!errors.dismissalReason"
            :disabled="isSubmitting || dismissMutation.isPending.value"
          />
          <p
            v-if="errors.dismissalReason"
            class="text-xs text-rose-600 mt-1 font-medium"
            role="alert"
          >
            {{ errors.dismissalReason }}
          </p>
        </div>

        <!-- Justification Rationale Textarea -->
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <label
              for="justification-rationale"
              class="block text-xs font-semibold text-slate-700 uppercase tracking-wider"
            >
              Justification Rationale <span class="text-rose-500">*</span>
            </label>
            <!-- Live Character Counter -->
            <span
              class="text-xs transition-colors"
              :class="isRationaleValid ? 'text-emerald-600 font-semibold' : 'text-rose-600 font-medium'"
              aria-live="polite"
            >
              {{ trimmedRationaleLength }} / 20 characters minimum
            </span>
          </div>

          <Textarea
            id="justification-rationale"
            v-model="justificationRationale"
            rows="4"
            autoResize
            placeholder="Provide clear rationale explaining why this finding is dismissed (minimum 20 characters)..."
            class="w-full text-xs font-sans"
            :invalid="!!errors.justificationRationale || (trimmedRationaleLength > 0 && !isRationaleValid)"
            :disabled="isSubmitting || dismissMutation.isPending.value"
          />

          <p
            v-if="errors.justificationRationale"
            class="text-xs text-rose-600 mt-1 font-medium"
            role="alert"
          >
            {{ errors.justificationRationale }}
          </p>
        </div>

        <!-- Modal Actions Footer -->
        <div class="flex items-center justify-end gap-2.5 pt-3 border-t border-slate-100">
          <Button
            type="button"
            label="Cancel"
            text
            severity="secondary"
            class="text-xs"
            :disabled="isSubmitting || dismissMutation.isPending.value"
            @click="handleClose"
          />
          <Button
            type="submit"
            label="Dismiss Issue"
            icon="pi pi-times-circle"
            severity="danger"
            class="text-xs"
            :loading="isSubmitting || dismissMutation.isPending.value"
            :disabled="isSubmitDisabled"
          />
        </div>
      </form>
    </div>
  </Dialog>
</template>