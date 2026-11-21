<script setup lang="ts">
import Dialog from 'primevue/dialog';
import Button from 'primevue/button';
import { AlertTriangle, ShieldAlert } from 'lucide-vue-next';

interface Props {
  visible: boolean;
  detectorName?: string;
  isLoading?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  detectorName: 'this detector',
  isLoading: false,
});

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void;
  (e: 'confirm'): void;
}>();

function onCancel(): void {
  if (props.isLoading) return;
  emit('update:visible', false);
}

function onConfirm(): void {
  emit('confirm');
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    header="Permanent Detector Retirement"
    :style="{ width: '32rem', maxWidth: '95vw' }"
    :closable="!isLoading"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4 pt-1">
      <!-- Warning Banner -->
      <div class="bg-rose-50 border border-rose-200 rounded-lg p-4 flex items-start gap-3">
        <ShieldAlert class="w-5 h-5 text-rose-600 shrink-0 mt-0.5" />
        <div class="text-xs text-rose-900 leading-relaxed">
          <p class="font-semibold text-rose-950">
            Irreversible Administrative Action
          </p>
          <p class="mt-1">
            Are you sure you want to retire <strong class="text-rose-950 font-bold">{{ detectorName }}</strong>?
            Decommissioning a detector permanently halts all supervision.
          </p>
        </div>
      </div>

      <!-- Impact List -->
      <div class="rounded-lg bg-slate-50 border border-slate-200 p-3.5 text-xs text-slate-700 space-y-2">
        <div class="font-semibold text-slate-900 flex items-center gap-1.5">
          <AlertTriangle class="w-3.5 h-3.5 text-amber-600" />
          Consequences of retirement:
        </div>
        <ul class="list-disc list-inside space-y-1 text-slate-600 pl-1">
          <li>Automated silence monitoring and SLA supervision will cease immediately.</li>
          <li>Manual and scheduled webhook inspection runs will be disabled.</li>
          <li>The detector configuration will become read-only and cannot be reopened.</li>
          <li>Existing historical inspection run logs remain archived and queryable.</li>
        </ul>
      </div>

      <!-- Action Buttons -->
      <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
        <Button
          type="button"
          label="Cancel"
          severity="secondary"
          text
          :disabled="isLoading"
          @click="onCancel"
        />
        <Button
          type="button"
          label="Yes, Retire Detector"
          severity="danger"
          :loading="isLoading"
          class="!bg-rose-600 hover:!bg-rose-700 !text-white !border-transparent shadow-xs"
          @click="onConfirm"
        />
      </div>
    </div>
  </Dialog>
</template>
