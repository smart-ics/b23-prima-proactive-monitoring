<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import Dialog from 'primevue/dialog';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import { useToast } from 'primevue/usetoast';
import {
  Key,
  Copy,
  Check,
  AlertTriangle,
  Terminal,
} from 'lucide-vue-next';

import { useGenerateEnrollmentTokenMutation } from '../api/useEnrollmentQueries';
import { formatWibDateTime } from '@/shared/utils/dateUtils';
import type { GenerateEnrollmentTokenResponse } from '@/shared/types/api.generated';

const props = defineProps<{
  visible: boolean;
}>();

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void;
  (e: 'generated', payload: GenerateEnrollmentTokenResponse): void;
}>();

const toast = useToast();
const generateMutation = useGenerateEnrollmentTokenMutation();

// Modal step: 'generate' (Step 1) | 'result' (Step 2)
const step = ref<'generate' | 'result'>('generate');

// Form state
const tokenLabel = ref('');
const errorMessage = ref('');
const generatedToken = ref<GenerateEnrollmentTokenResponse | null>(null);

// Copy states
const tokenCopied = ref(false);
const snippetCopied = ref(false);

const isPending = computed(() => generateMutation.isPending.value);

const dialogTitle = computed(() => {
  return step.value === 'generate'
    ? 'Quick Enroll (Handshake)'
    : 'Enrollment Token Generated';
});

const formattedExpiry = computed(() => {
  if (!generatedToken.value?.expiresAtUtc) return '24 hours from generation';
  return formatWibDateTime(generatedToken.value.expiresAtUtc, true);
});

const envSnippet = computed(() => {
  const token = generatedToken.value?.token || 'PRM-TOKEN-...';
  return `PRIMA_ENROLLMENT_TOKEN="${token}"\nPRIMA_SERVER_URL="https://prima.hospital.internal"`;
});

// Watch visible to reset state when modal opens
watch(
  () => props.visible,
  (newVal) => {
    if (newVal) {
      resetState();
    }
  }
);

function resetState() {
  step.value = 'generate';
  tokenLabel.value = '';
  errorMessage.value = '';
  generatedToken.value = null;
  tokenCopied.value = false;
  snippetCopied.value = false;
}

function handleClose() {
  if (isPending.value) return;
  emit('update:visible', false);
  // Clean up plaintext secret and state immediately on close
  resetState();
}

async function onGenerateToken() {
  errorMessage.value = '';
  try {
    const payload = tokenLabel.value.trim()
      ? { label: tokenLabel.value.trim() }
      : {};

    const result = await generateMutation.mutateAsync(payload);
    generatedToken.value = result;
    step.value = 'result';
    emit('generated', result);

    toast.add({
      severity: 'success',
      summary: 'Token Generated',
      detail: '24-hour enrollment token generated successfully.',
      life: 3000,
    });
  } catch (err: unknown) {
    if (err && typeof err === 'object' && 'detail' in err && typeof (err as { detail: unknown }).detail === 'string') {
      errorMessage.value = (err as { detail: string }).detail;
    } else if (err instanceof Error) {
      errorMessage.value = err.message;
    } else {
      errorMessage.value = 'An unexpected error occurred while generating enrollment token.';
    }
  }
}

async function copyToClipboard(text: string, type: 'token' | 'snippet') {
  try {
    if (navigator?.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
    } else {
      const textArea = document.createElement('textarea');
      textArea.value = text;
      textArea.style.position = 'fixed';
      textArea.style.opacity = '0';
      document.body.appendChild(textArea);
      textArea.select();
      document.execCommand('copy');
      document.body.removeChild(textArea);
    }

    if (type === 'token') {
      tokenCopied.value = true;
      setTimeout(() => {
        tokenCopied.value = false;
      }, 2000);
      toast.add({
        severity: 'success',
        summary: 'Token Copied',
        detail: 'Plaintext enrollment token copied to clipboard.',
        life: 2500,
      });
    } else {
      snippetCopied.value = true;
      setTimeout(() => {
        snippetCopied.value = false;
      }, 2000);
      toast.add({
        severity: 'success',
        summary: 'Snippet Copied',
        detail: 'Environment variable snippet copied to clipboard.',
        life: 2500,
      });
    }
  } catch {
    toast.add({
      severity: 'error',
      summary: 'Copy Failed',
      detail: 'Could not access clipboard. Please select and copy manually.',
      life: 3000,
    });
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :draggable="false"
    :closable="!isPending"
    class="w-full max-w-lg"
    @update:visible="handleClose"
  >
    <template #header>
      <div class="flex items-center gap-2.5">
        <div class="p-2 rounded-lg bg-indigo-50 text-indigo-600">
          <Key class="w-5 h-5" />
        </div>
        <div>
          <h2 class="text-base font-semibold text-slate-900 leading-snug">
            {{ dialogTitle }}
          </h2>
          <p class="text-xs text-slate-500">
            {{ step === 'generate' ? 'Generate 24-hour token for automated detector runner handshake' : 'Copy credentials to configure remote detector runner' }}
          </p>
        </div>
      </div>
    </template>

    <!-- STEP 1: Generation -->
    <div v-if="step === 'generate'" class="space-y-4 pt-2">
      <div class="text-xs text-slate-600 leading-relaxed bg-slate-50 rounded-lg p-3 border border-slate-200">
        <p>
          Automated enrollment allows remote detector runners to bootstrap and synchronize their probe manifests idempotently.
          The token expires in <strong class="text-slate-800">24 hours</strong>.
        </p>
      </div>

      <div class="space-y-1.5">
        <label for="enrollment-token-label" class="block text-xs font-semibold text-slate-700">
          Token Label <span class="text-slate-400 font-normal">(Optional)</span>
        </label>
        <InputText
          id="enrollment-token-label"
          v-model="tokenLabel"
          placeholder="e.g., SIMRS Accounting Daemon, Ward B Spooler Agent"
          class="w-full text-sm"
          :disabled="isPending"
          maxlength="100"
          autofocus
          @keyup.enter="onGenerateToken"
        />
        <p class="text-[11px] text-slate-500">
          Descriptive tag visible to EDP officers in the pending handshake list.
        </p>
      </div>

      <!-- Error Message -->
      <div
        v-if="errorMessage"
        class="bg-rose-50 border border-rose-200 rounded-lg p-3 text-xs text-rose-800 flex items-start gap-2"
      >
        <AlertTriangle class="w-4 h-4 text-rose-600 shrink-0 mt-0.5" />
        <span>{{ errorMessage }}</span>
      </div>

      <!-- Action Footer -->
      <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-200">
        <Button
          type="button"
          label="Cancel"
          severity="secondary"
          text
          :disabled="isPending"
          @click="handleClose"
        />
        <Button
          type="button"
          label="Generate Enrollment Token"
          icon="pi pi-key"
          severity="primary"
          :loading="isPending"
          class="!bg-indigo-600 hover:!bg-indigo-700 !text-white shadow-xs"
          @click="onGenerateToken"
        />
      </div>
    </div>

    <!-- STEP 2: Result & Snippet -->
    <div v-else-if="step === 'result'" class="space-y-4 pt-2">
      <!-- Warning Alert -->
      <div class="bg-amber-50 border border-amber-300 rounded-lg p-3.5 flex items-start gap-3">
        <AlertTriangle class="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
        <div class="text-xs text-amber-950 leading-relaxed space-y-1">
          <p class="font-semibold text-amber-900">
            Copy this token secret now
          </p>
          <p class="text-amber-800">
            For security, the plaintext token secret is displayed <strong class="underline font-semibold">only once</strong> and cannot be retrieved later. It will expire in 24 hours (at {{ formattedExpiry }}).
          </p>
        </div>
      </div>

      <!-- Monospace Plaintext Token Box -->
      <div class="space-y-1.5">
        <div class="flex items-center justify-between">
          <label class="text-xs font-semibold text-slate-700">
            Enrollment Token Secret
          </label>
          <span class="text-[11px] font-mono text-slate-500">Expires: {{ formattedExpiry }}</span>
        </div>
        <div class="bg-slate-900 border border-slate-800 rounded-lg p-3 flex items-center justify-between gap-3">
          <span class="font-mono text-xs text-emerald-400 break-all select-all">
            {{ generatedToken?.token }}
          </span>
          <Button
            type="button"
            severity="secondary"
            size="small"
            class="shrink-0 !bg-slate-800 hover:!bg-slate-700 !text-slate-100 !border-slate-700 text-xs py-1 px-2.5"
            @click="copyToClipboard(generatedToken?.token || '', 'token')"
          >
            <component :is="tokenCopied ? Check : Copy" class="w-3.5 h-3.5 mr-1" :class="tokenCopied ? 'text-emerald-400' : 'text-slate-300'" />
            {{ tokenCopied ? 'Copied' : 'Copy' }}
          </Button>
        </div>
      </div>

      <!-- Environment Variables Code Snippet Block -->
      <div class="space-y-1.5">
        <div class="flex items-center justify-between">
          <label class="text-xs font-semibold text-slate-700 flex items-center gap-1.5">
            <Terminal class="w-3.5 h-3.5 text-slate-500" />
            Detector Runner Environment Variables
          </label>
          <Button
            type="button"
            severity="secondary"
            text
            size="small"
            class="text-xs py-0.5 px-2 text-indigo-600 hover:text-indigo-800 font-medium"
            @click="copyToClipboard(envSnippet, 'snippet')"
          >
            <component :is="snippetCopied ? Check : Copy" class="w-3.5 h-3.5 mr-1" />
            {{ snippetCopied ? 'Snippet Copied' : 'Copy Snippet' }}
          </Button>
        </div>
        <div class="bg-slate-950 border border-slate-800 rounded-lg p-3 font-mono text-xs text-slate-200 whitespace-pre overflow-x-auto select-all">
{{ envSnippet }}
        </div>
        <p class="text-[11px] text-slate-500">
          Provide these variables in the detector runner's <code>.env</code> file or deployment container environment.
        </p>
      </div>

      <!-- Action Footer -->
      <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-200">
        <Button
          type="button"
          label="Done"
          severity="primary"
          class="!bg-slate-900 hover:!bg-slate-800 !text-white px-5 shadow-xs"
          @click="handleClose"
        />
      </div>
    </div>
  </Dialog>
</template>
