<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import Dialog from 'primevue/dialog';
import Select from 'primevue/select';
import Button from 'primevue/button';
import Message from 'primevue/message';
import ProgressSpinner from 'primevue/progressspinner';
import { useToast } from 'primevue/usetoast';

import {
  useOfficersQuery,
  useAssignMutation,
} from '@/features/issues/api/useIssueDetailQueries';
import { ApiError } from '@/shared/api/errors';
import type { OfficerDto } from '@/shared/types/api.generated';

const props = defineProps<{
  visible: boolean;
  issueId: string;
  currentOfficerId?: string | null;
  isReassign?: boolean;
}>();

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void;
  (e: 'assigned', officerId: string): void;
}>();

const toast = useToast();
const officersQuery = useOfficersQuery();
const assignMutation = useAssignMutation();

const selectedOfficerId = ref<string>('');
const errorMessage = ref<string>('');
const isSubmitting = ref<boolean>(false);

const modalTitle = computed(() => {
  return props.isReassign ? 'Reassign Officer' : 'Assign Officer';
});

const submitButtonLabel = computed(() => {
  return props.isReassign ? 'Reassign' : 'Assign';
});

const officersList = computed<OfficerDto[]>(() => {
  return officersQuery.data.value || [];
});

const isSubmitDisabled = computed(() => {
  if (!selectedOfficerId.value) return true;
  if (props.currentOfficerId && selectedOfficerId.value === props.currentOfficerId) {
    return true;
  }
  return isSubmitting.value || assignMutation.isPending.value;
});

function handleClose() {
  if (isSubmitting.value || assignMutation.isPending.value) return;
  emit('update:visible', false);
  errorMessage.value = '';
}

// Pre-fill or reset when dialog opens
watch(
  () => props.visible,
  (newVal) => {
    if (newVal) {
      errorMessage.value = '';
      selectedOfficerId.value = props.currentOfficerId || '';
    }
  }
);

async function onSubmit() {
  if (isSubmitDisabled.value) return;
  errorMessage.value = '';
  isSubmitting.value = true;

  try {
    await assignMutation.mutateAsync({
      issueId: props.issueId,
      assignedOfficerId: selectedOfficerId.value,
    });

    const assignedOfficer = officersList.value.find(
      (o) => o.id === selectedOfficerId.value
    );
    const officerName = assignedOfficer?.fullName || 'the selected officer';

    toast.add({
      severity: 'success',
      summary: props.isReassign ? 'Officer Reassigned' : 'Officer Assigned',
      detail: `Issue has been successfully ${props.isReassign ? 'reassigned' : 'assigned'} to ${officerName}.`,
      life: 4000,
    });

    emit('assigned', selectedOfficerId.value);
    emit('update:visible', false);
  } catch (err: unknown) {
    if (err instanceof ApiError) {
      errorMessage.value =
        err.detail || 'Failed to assign officer due to an API error.';
    } else if (err instanceof Error) {
      errorMessage.value = err.message;
    } else {
      errorMessage.value =
        'An unexpected error occurred while assigning the officer.';
    }

    toast.add({
      severity: 'error',
      summary: 'Assignment Failed',
      detail: errorMessage.value,
      life: 5000,
    });
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="modalTitle"
    :style="{ width: '32rem' }"
    :closable="!isSubmitting && !assignMutation.isPending.value"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4 pt-1">
      <!-- Explanatory Header Alert -->
      <p class="text-xs text-slate-600 leading-relaxed">
        {{
          props.isReassign
            ? 'Transfer ownership of this issue to another EDP officer. Investigation history and notes are preserved.'
            : 'Assign this issue to an EDP officer. Assigning moves the issue to Acknowledged status and sets ownership.'
        }}
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

      <!-- Loading Officers State -->
      <div
        v-if="officersQuery.isLoading.value"
        class="flex flex-col items-center justify-center py-6 text-slate-500 gap-2"
      >
        <ProgressSpinner
          style="width: 32px; height: 32px"
          strokeWidth="4"
          fill="transparent"
          animationDuration=".8s"
        />
        <span class="text-xs">Loading officer directory...</span>
      </div>

      <!-- Error Loading Officers State -->
      <div
        v-else-if="officersQuery.isError.value"
        class="p-3 rounded-lg bg-rose-50 border border-rose-200 text-xs text-rose-800"
      >
        <p class="font-semibold mb-1">Failed to load officer directory</p>
        <p class="text-[11px] mb-2">{{ officersQuery.error.value?.message || 'Network error' }}</p>
        <Button
          label="Retry"
          icon="pi pi-refresh"
          severity="danger"
          size="small"
          class="text-xs"
          @click="officersQuery.refetch()"
        />
      </div>

      <!-- Assign Form -->
      <form v-else @submit.prevent="onSubmit" class="space-y-4">
        <!-- Officer Select Dropdown -->
        <div>
          <label
            for="officer-select"
            class="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5"
          >
            Select Officer <span class="text-rose-500">*</span>
          </label>
          <Select
            id="officer-select"
            v-model="selectedOfficerId"
            :options="officersList"
            optionLabel="fullName"
            optionValue="id"
            filter
            placeholder="Search by officer name or username..."
            class="w-full text-xs"
            :disabled="isSubmitting || assignMutation.isPending.value"
          >
            <template #value="slotProps">
              <div v-if="slotProps.value" class="flex items-center justify-between w-full">
                <span class="font-medium text-slate-900">
                  {{
                    officersList.find((o) => o.id === slotProps.value)?.fullName ||
                    slotProps.value
                  }}
                </span>
                <span
                  v-if="officersList.find((o) => o.id === slotProps.value)"
                  class="text-[10px] uppercase font-semibold px-1.5 py-0.5 rounded bg-slate-100 text-slate-600 ml-2"
                >
                  {{ officersList.find((o) => o.id === slotProps.value)?.role }}
                </span>
              </div>
              <span v-else class="text-slate-400">
                {{ slotProps.placeholder }}
              </span>
            </template>
            <template #option="slotProps">
              <div class="flex items-center justify-between w-full py-0.5">
                <div class="flex flex-col">
                  <span class="text-xs font-semibold text-slate-900">
                    {{ slotProps.option.fullName }}
                  </span>
                  <span class="text-[11px] text-slate-500">
                    @{{ slotProps.option.username }}
                  </span>
                </div>
                <div class="flex items-center gap-1.5">
                  <span
                    v-if="props.currentOfficerId && slotProps.option.id === props.currentOfficerId"
                    class="text-[10px] font-bold uppercase tracking-wider px-1.5 py-0.5 rounded bg-amber-100 text-amber-800"
                  >
                    Current
                  </span>
                  <span
                    class="text-[10px] font-semibold uppercase tracking-wider px-1.5 py-0.5 rounded bg-slate-100 text-slate-600"
                  >
                    {{ slotProps.option.role }}
                  </span>
                </div>
              </div>
            </template>
          </Select>

          <p
            v-if="props.currentOfficerId && selectedOfficerId === props.currentOfficerId"
            class="text-[11px] text-amber-600 mt-1 font-medium"
          >
            This officer is already the assigned owner. Please select a different officer to reassign.
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
            :disabled="isSubmitting || assignMutation.isPending.value"
            @click="handleClose"
          />
          <Button
            type="submit"
            :label="submitButtonLabel"
            icon="pi pi-user-check"
            severity="primary"
            class="text-xs bg-slate-900 hover:bg-slate-800 text-white"
            :loading="isSubmitting || assignMutation.isPending.value"
            :disabled="isSubmitDisabled"
          />
        </div>
      </form>
    </div>
  </Dialog>
</template>