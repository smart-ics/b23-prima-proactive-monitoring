<script setup lang="ts">
import { ref, computed } from 'vue';
import Dialog from 'primevue/dialog';
import Button from 'primevue/button';
import { useToast } from 'primevue/usetoast';
import {
  Key,
  ChevronDown,
  ChevronUp,
  Clock,
  User,
  ShieldAlert,
  Trash2,
} from 'lucide-vue-next';

import {
  usePendingEnrollmentTokensQuery,
  useRevokeEnrollmentTokenMutation,
} from '../api/useEnrollmentQueries';
import { formatWibDateTime } from '@/shared/utils/dateUtils';
import type { PendingEnrollmentTokenDto } from '@/shared/types/api.generated';

const props = defineProps<{
  tokens?: PendingEnrollmentTokenDto[];
}>();

const toast = useToast();

// TanStack Query & Mutation
const { data: fetchedTokens } = usePendingEnrollmentTokensQuery();
const revokeMutation = useRevokeEnrollmentTokenMutation();

// Active tokens
const pendingTokens = computed<PendingEnrollmentTokenDto[]>(() => {
  return props.tokens ?? fetchedTokens.value ?? [];
});

const tokenCount = computed(() => pendingTokens.value.length);
const hasTokens = computed(() => tokenCount.value > 0);

// Collapsible state - defaults to collapsed per DEC-004 specification
const isCollapsed = ref(true);

function toggleCollapse() {
  isCollapsed.value = !isCollapsed.value;
}

// Revoke confirmation modal state
const confirmDialogVisible = ref(false);
const tokenToRevoke = ref<PendingEnrollmentTokenDto | null>(null);

function openRevokeConfirm(token: PendingEnrollmentTokenDto) {
  tokenToRevoke.value = token;
  confirmDialogVisible.value = true;
}

function closeRevokeConfirm() {
  if (revokeMutation.isPending.value) return;
  confirmDialogVisible.value = false;
  tokenToRevoke.value = null;
}

async function handleConfirmRevoke() {
  if (!tokenToRevoke.value) return;

  try {
    await revokeMutation.mutateAsync(tokenToRevoke.value.id);
    toast.add({
      severity: 'success',
      summary: 'Token Revoked',
      detail: `Enrollment token ${tokenToRevoke.value.tokenPrefix}... has been successfully revoked.`,
      life: 3500,
    });
    confirmDialogVisible.value = false;
    tokenToRevoke.value = null;
  } catch (err: unknown) {
    const errorMsg = err instanceof Error ? err.message : 'Failed to revoke token';
    toast.add({
      severity: 'error',
      summary: 'Revocation Failed',
      detail: errorMsg,
      life: 5000,
    });
  }
}

/**
 * Computes human-friendly expiry countdown string (e.g. "Expires in 23 hours", "Expires in 45 minutes", "Expired").
 */
function formatExpiryCountdown(expiresAtUtc?: string | null): string {
  if (!expiresAtUtc) return '-';
  const diffMs = new Date(expiresAtUtc).getTime() - Date.now();
  if (diffMs <= 0) return 'Expired';

  const totalMinutes = Math.floor(diffMs / 60_000);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (hours > 0) {
    return `Expires in ${hours} hour${hours > 1 ? 's' : ''}${minutes > 0 ? ` ${minutes}m` : ''}`;
  }
  return `Expires in ${minutes} minute${minutes > 1 ? 's' : ''}`;
}
</script>

<template>
  <div v-if="hasTokens" class="rounded-lg border border-amber-200/90 bg-amber-50/70 shadow-2xs overflow-hidden transition-all">
    <!-- Header Banner / Summary Row -->
    <div
      class="px-3.5 py-2.5 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5 cursor-pointer select-none"
      @click="toggleCollapse"
    >
      <div class="flex items-center gap-2.5 min-w-0">
        <div class="w-7 h-7 rounded-md bg-amber-100 text-amber-800 border border-amber-300 flex items-center justify-center shrink-0">
          <Key class="w-3.5 h-3.5" />
        </div>
        <div class="min-w-0">
          <div class="flex items-center gap-2 flex-wrap">
            <span class="text-xs font-semibold text-amber-950">
              Pending Enrollment Tokens (Awaiting Handshake)
            </span>
            <span class="px-2 py-0.5 rounded-full text-[11px] font-mono font-bold bg-amber-200 text-amber-900 border border-amber-300">
              {{ tokenCount }} awaiting
            </span>
          </div>
          <p class="text-[11px] text-amber-800/80 truncate">
            Generated tokens awaiting detector runner boot handshake. Single-use and expire within 24h.
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2 shrink-0 self-end sm:self-auto">
        <button
          type="button"
          class="inline-flex items-center gap-1 px-2.5 py-1 rounded text-xs font-medium text-amber-900 bg-amber-100/80 hover:bg-amber-200/90 border border-amber-300/80 transition-colors cursor-pointer"
          :title="isCollapsed ? 'Expand list of pending tokens' : 'Collapse strip'"
        >
          <span>{{ isCollapsed ? 'Show Details' : 'Hide Details' }}</span>
          <component :is="isCollapsed ? ChevronDown : ChevronUp" class="w-3.5 h-3.5" />
        </button>
      </div>
    </div>

    <!-- Expanded Token List -->
    <div v-if="!isCollapsed" class="border-t border-amber-200/70 bg-white p-3 space-y-2">
      <div class="overflow-x-auto">
        <table class="w-full text-left text-xs">
          <thead>
            <tr class="border-b border-slate-100 text-[11px] font-medium text-slate-400 uppercase tracking-wider">
              <th class="pb-2 pl-1">Token Prefix</th>
              <th class="pb-2">Label</th>
              <th class="pb-2">Created By</th>
              <th class="pb-2">Created Date</th>
              <th class="pb-2">Expiry Countdown</th>
              <th class="pb-2 pr-1 text-right">Action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="token in pendingTokens"
              :key="token.id"
              class="hover:bg-slate-50/80 transition-colors"
            >
              <td class="py-2 pl-1 whitespace-nowrap">
                <code class="font-mono text-xs font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-800 border border-slate-200">
                  {{ token.tokenPrefix }}...
                </code>
              </td>
              <td class="py-2 text-slate-800 font-medium">
                {{ token.label || '-' }}
              </td>
              <td class="py-2 text-slate-600 whitespace-nowrap">
                <div class="flex items-center gap-1.5">
                  <User class="w-3.5 h-3.5 text-slate-400" />
                  <span>{{ token.createdByOfficer }}</span>
                </div>
              </td>
              <td class="py-2 text-slate-600 font-mono text-[11px] whitespace-nowrap">
                {{ formatWibDateTime(token.createdAtUtc) }}
              </td>
              <td class="py-2 whitespace-nowrap">
                <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-amber-50 text-amber-800 border border-amber-200">
                  <Clock class="w-3 h-3 text-amber-600" />
                  <span>{{ formatExpiryCountdown(token.expiresAtUtc) }}</span>
                </span>
              </td>
              <td class="py-2 pr-1 text-right whitespace-nowrap">
                <button
                  type="button"
                  @click="openRevokeConfirm(token)"
                  class="inline-flex items-center gap-1 px-2 py-1 rounded text-xs font-medium text-rose-700 bg-rose-50 hover:bg-rose-100 border border-rose-200 transition-colors cursor-pointer"
                  title="Revoke enrollment token"
                >
                  <Trash2 class="w-3 h-3 text-rose-600" />
                  <span>Revoke</span>
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- PrimeVue Revoke Confirmation Dialog -->
    <Dialog
      :visible="confirmDialogVisible"
      modal
      header="Revoke Enrollment Token"
      :style="{ width: '30rem', maxWidth: '95vw' }"
      :closable="!revokeMutation.isPending.value"
      @update:visible="confirmDialogVisible = $event"
    >
      <div class="space-y-4 pt-1">
        <div class="bg-rose-50 border border-rose-200 rounded-lg p-3.5 flex items-start gap-3">
          <ShieldAlert class="w-5 h-5 text-rose-600 shrink-0 mt-0.5" />
          <div class="text-xs text-rose-900 leading-relaxed">
            <p class="font-semibold text-rose-950">
              Immediate Token Invalidation
            </p>
            <p class="mt-1">
              Are you sure you want to revoke token
              <strong class="font-mono text-rose-950 font-bold">{{ tokenToRevoke?.tokenPrefix }}...</strong>
              <span v-if="tokenToRevoke?.label"> ({{ tokenToRevoke.label }})</span>?
            </p>
            <p class="mt-1 text-rose-800">
              Any detector runner attempting to handshake with this token will be rejected. This action cannot be undone.
            </p>
          </div>
        </div>

        <div class="flex items-center justify-end gap-2.5 pt-3 border-t border-slate-200">
          <Button
            type="button"
            label="Cancel"
            severity="secondary"
            text
            :disabled="revokeMutation.isPending.value"
            @click="closeRevokeConfirm"
          />
          <Button
            type="button"
            label="Revoke Token"
            severity="danger"
            :loading="revokeMutation.isPending.value"
            class="!bg-rose-600 hover:!bg-rose-700 !text-white !border-transparent shadow-xs"
            @click="handleConfirmRevoke"
          />
        </div>
      </div>
    </Dialog>
  </div>
</template>
