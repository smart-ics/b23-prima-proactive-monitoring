<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRoute, RouterLink } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import {
  ArrowLeft,
  Play,
  Pause,
  RotateCcw,
  Edit,
  Trash2,
  RefreshCw,
  XCircle,
  Radio,
  Clock,
  Globe,
  Tag,
  Copy,
  Check,
  Server,
  AlertTriangle,
  Cpu,
} from 'lucide-vue-next';

import {
  useDetectorDetailQuery,
  useTriggerManualRunMutation,
  useToggleDetectorStatusMutation,
} from '@/features/detectors/api/useDetectorQueries';
import {
  ReliabilityStatsCard,
  RecentRunsTable,
  LinkedActiveIssuesList,
  ConfirmRetireDialog,
} from '@/features/detectors/components';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';
import {
  secondsToCadence,
  getCadenceSummaryText,
  getSilenceThresholdText,
} from '@/features/detectors/composables/useDetectorForm';

const route = useRoute();
const toast = useToast();

const detectorId = computed(() => {
  const raw = route.params.id;
  return typeof raw === 'string' ? raw : (raw?.[0] ?? '');
});

// Primary detail query with 15-second background polling
const {
  data: detector,
  isPending,
  isError,
  error,
  refetch,
  isRefetching,
} = useDetectorDetailQuery(detectorId);

// Mutation hooks
const triggerMutation = useTriggerManualRunMutation(detectorId);
const toggleMutation = useToggleDetectorStatusMutation(detectorId);

// State for dialogs and copy button
const isRetireDialogOpen = ref(false);
const isWebhookCopied = ref(false);

const adminStatus = computed(() => detector.value?.adminStatus ?? 'Active');
const hasWebhook = computed(() => Boolean(detector.value?.webhookUrl && detector.value.webhookUrl.trim().length > 0));

const isActionLoading = computed(() => {
  return triggerMutation.isPending.value || toggleMutation.isPending.value;
});

// Cadence display info
const cadenceInfo = computed(() => {
  if (!detector.value?.expectedCadenceSeconds) {
    return { summary: '5 min', threshold: '10 min', totalSeconds: 300 };
  }
  const split = secondsToCadence(detector.value.expectedCadenceSeconds);
  return {
    summary: getCadenceSummaryText(split.cadenceValue, split.cadenceUnit),
    threshold: getSilenceThresholdText(split.cadenceValue, split.cadenceUnit),
    totalSeconds: detector.value.expectedCadenceSeconds,
  };
});

// Copy webhook URL helper
async function copyWebhookUrl(): Promise<void> {
  if (!detector.value?.webhookUrl) return;
  try {
    await navigator.clipboard.writeText(detector.value.webhookUrl);
    isWebhookCopied.value = true;
    setTimeout(() => {
      isWebhookCopied.value = false;
    }, 2000);
  } catch (err) {
    console.error('Failed to copy webhook URL:', err);
  }
}

// Action Handlers
async function handleTriggerRun(): Promise<void> {
  if (!detector.value || !hasWebhook.value || isActionLoading.value) return;

  try {
    const result = await triggerMutation.mutateAsync(detectorId.value);
    const outcomeSeverity =
      result.outcome === 'Passed' ? 'success' : result.outcome === 'FindingDiscovered' ? 'warn' : 'error';

    toast.add({
      severity: outcomeSeverity,
      summary: `Manual Run: ${result.outcome}`,
      detail: `Completed in ${result.durationMs}ms with ${result.findingsCount} findings.`,
      life: 5000,
    });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Unknown execution failure';
    toast.add({
      severity: 'error',
      summary: 'Trigger Run Failed',
      detail: message,
      life: 6000,
    });
  }
}

async function handlePause(): Promise<void> {
  if (isActionLoading.value) return;
  try {
    await toggleMutation.mutateAsync({ id: detectorId.value, action: 'Pause' });
    toast.add({
      severity: 'info',
      summary: 'Detector Paused',
      detail: 'Scheduled runs and silence checks are temporarily suspended.',
      life: 4000,
    });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Failed to pause detector';
    toast.add({
      severity: 'error',
      summary: 'Action Failed',
      detail: message,
      life: 5000,
    });
  }
}

async function handleResume(): Promise<void> {
  if (isActionLoading.value) return;
  try {
    await toggleMutation.mutateAsync({ id: detectorId.value, action: 'Resume' });
    toast.add({
      severity: 'success',
      summary: 'Detector Resumed',
      detail: 'Detector is now Active. Supervision and schedule restored.',
      life: 4000,
    });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Failed to resume detector';
    toast.add({
      severity: 'error',
      summary: 'Action Failed',
      detail: message,
      life: 5000,
    });
  }
}

async function handleConfirmRetire(): Promise<void> {
  try {
    await toggleMutation.mutateAsync({ id: detectorId.value, action: 'Retire' });
    isRetireDialogOpen.value = false;
    toast.add({
      severity: 'warn',
      summary: 'Detector Retired',
      detail: 'This detector has been permanently decommissioned.',
      life: 6000,
    });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Failed to retire detector';
    toast.add({
      severity: 'error',
      summary: 'Retirement Failed',
      detail: message,
      life: 5000,
    });
  }
}

function getHealthBadgeClass(health: string): string {
  switch (health) {
    case 'Healthy':
      return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    case 'Failing':
      return 'bg-rose-50 text-rose-700 border-rose-200';
    case 'Silent':
      return 'bg-amber-50 text-amber-700 border-amber-200';
    case 'Degraded':
      return 'bg-amber-50 text-amber-700 border-amber-200';
    default:
      return 'bg-slate-100 text-slate-700 border-slate-200';
  }
}

function getAdminBadgeClass(admin: string): string {
  switch (admin) {
    case 'Active':
      return 'bg-blue-50 text-blue-700 border-blue-200';
    case 'Paused':
      return 'bg-amber-50 text-amber-700 border-amber-200';
    case 'Retired':
      return 'bg-slate-100 text-slate-600 border-slate-300';
    default:
      return 'bg-slate-50 text-slate-600 border-slate-200';
  }
}
</script>

<template>
  <div class="min-h-full pb-16 bg-slate-50/50">
    <!-- Initial Loading State -->
    <div
      v-if="isPending"
      class="p-12 flex flex-col items-center justify-center min-h-[50vh] text-slate-500 space-y-3"
    >
      <RefreshCw class="w-8 h-8 animate-spin text-slate-400" />
      <p class="text-sm font-medium">Loading detector details...</p>
    </div>

    <!-- Error State -->
    <div
      v-else-if="isError || !detector"
      class="p-8 max-w-2xl mx-auto my-12 bg-white rounded-xl border border-rose-200 shadow-sm text-center space-y-4"
    >
      <div class="w-12 h-12 rounded-full bg-rose-50 text-rose-600 flex items-center justify-center mx-auto">
        <XCircle class="w-6 h-6" />
      </div>
      <div>
        <h2 class="text-lg font-bold text-slate-900">Detector Not Found</h2>
        <p class="text-xs text-slate-600 mt-1">
          {{ error?.message || 'Unable to load detector data. The detector might have been removed or the ID is invalid.' }}
        </p>
      </div>
      <div class="flex items-center justify-center gap-3 pt-2">
        <RouterLink
          to="/detectors"
          class="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg text-xs font-semibold bg-slate-900 text-white hover:bg-slate-800 transition-colors"
        >
          <ArrowLeft class="w-4 h-4" />
          <span>Back to Portfolio</span>
        </RouterLink>
        <button
          type="button"
          class="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg text-xs font-semibold bg-white border border-slate-300 text-slate-700 hover:bg-slate-50 transition-colors cursor-pointer"
          @click="() => refetch()"
        >
          <RefreshCw class="w-3.5 h-3.5" />
          <span>Retry</span>
        </button>
      </div>
    </div>

    <!-- Detail View Content -->
    <div v-else class="space-y-6">
      <!-- 1. STICKY HEADER & OPERATIONAL ACTION BAR -->
      <header class="sticky top-0 z-20 bg-white/95 backdrop-blur-md border-b border-slate-200 shadow-xs">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-3.5 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <!-- Left: Identity & Badges -->
          <div class="flex items-center gap-3 min-w-0">
            <!-- Back Button -->
            <RouterLink
              to="/detectors"
              class="shrink-0 p-2 text-slate-500 hover:text-slate-900 hover:bg-slate-100 rounded-lg transition-colors border border-transparent hover:border-slate-200"
              title="Back to Detector Portfolio"
            >
              <ArrowLeft class="w-5 h-5" />
            </RouterLink>

            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2">
                <h1 class="text-lg sm:text-xl font-bold text-slate-900 truncate">
                  {{ detector.name }}
                </h1>

                <!-- Module Pill -->
                <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-slate-100 text-slate-700 border border-slate-200">
                  <Tag class="w-3 h-3 text-slate-500" />
                  {{ detector.targetModule }}
                </span>

                <!-- Version Pill (if present) -->
                <span
                  v-if="detector.version"
                  class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-mono font-medium bg-indigo-50 text-indigo-700 border border-indigo-200"
                  :title="`Detector Runtime Version: ${detector.version}`"
                >
                  <Cpu class="w-3 h-3 text-indigo-500" />
                  v{{ detector.version }}
                </span>

                <!-- Cadence Drift Badge (if present) -->
                <span
                  v-if="detector.cadenceDriftWarning"
                  class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-amber-100 text-amber-800 border border-amber-300"
                  :title="detector.cadenceDriftWarning"
                >
                  <AlertTriangle class="w-3 h-3 text-amber-600" />
                  Cadence Drift
                </span>

                <!-- Health Badge -->
                <span
                  class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold border"
                  :class="getHealthBadgeClass(detector.healthStatus)"
                >
                  <span
                    class="w-1.5 h-1.5 rounded-full"
                    :class="detector.healthStatus === 'Healthy' ? 'bg-emerald-500' : detector.healthStatus === 'Failing' ? 'bg-rose-500' : 'bg-amber-500'"
                  />
                  {{ detector.healthStatus }}
                </span>

                <!-- Admin Status Badge -->
                <span
                  class="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold border"
                  :class="getAdminBadgeClass(detector.adminStatus)"
                >
                  {{ detector.adminStatus }}
                </span>
              </div>

              <!-- Subtitle / Cadence preview -->
              <p class="text-xs text-slate-500 mt-0.5 flex items-center gap-2 truncate">
                <span>Scope: <strong class="text-slate-700 font-medium">{{ detector.scope }}</strong></span>
                <span>·</span>
                <span>{{ cadenceInfo.summary }} ({{ cadenceInfo.threshold }})</span>
                <span v-if="isRefetching" class="inline-flex items-center gap-1 text-[11px] text-blue-600 animate-pulse">
                  <RefreshCw class="w-3 h-3 animate-spin" />
                  syncing...
                </span>
              </p>
            </div>
          </div>

          <!-- Right: Status-Sensitive Operational Action Bar (§4.6.2) -->
          <div class="flex flex-wrap items-center gap-2.5 shrink-0">
            <!-- Full Run Log Link -->
            <RouterLink
              :to="`/detectors/${detector.id}/runs`"
              class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg text-slate-700 bg-white border border-slate-300 hover:bg-slate-50 transition-colors shadow-2xs"
            >
              <Clock class="w-3.5 h-3.5 text-slate-500" />
              <span>Full Run Log</span>
            </RouterLink>

            <!-- Case 1: Active with Webhook -->
            <template v-if="adminStatus === 'Active' && hasWebhook">
              <!-- Trigger Run (Primary) -->
              <button
                type="button"
                class="inline-flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-slate-900 text-white hover:bg-slate-800 disabled:opacity-50 transition-colors shadow-xs cursor-pointer"
                :disabled="isActionLoading"
                @click="handleTriggerRun"
              >
                <component
                  :is="triggerMutation.isPending.value ? RefreshCw : Play"
                  :class="['w-3.5 h-3.5', triggerMutation.isPending.value ? 'animate-spin' : 'fill-current']"
                />
                <span>{{ triggerMutation.isPending.value ? 'Triggering...' : 'Trigger Run' }}</span>
              </button>

              <!-- Pause Button -->
              <button
                type="button"
                class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-amber-50 text-amber-700 border border-amber-300 hover:bg-amber-100 disabled:opacity-50 transition-colors shadow-2xs cursor-pointer"
                :disabled="isActionLoading"
                @click="handlePause"
              >
                <Pause class="w-3.5 h-3.5" />
                <span>Pause</span>
              </button>

              <!-- Edit Button -->
              <RouterLink
                :to="`/detectors/${detector.id}/edit`"
                class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-white border border-slate-300 text-slate-700 hover:bg-slate-50 transition-colors shadow-2xs"
              >
                <Edit class="w-3.5 h-3.5 text-slate-500" />
                <span>Edit</span>
              </RouterLink>

              <!-- Retire Button -->
              <button
                type="button"
                class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg text-rose-600 hover:bg-rose-50 border border-transparent hover:border-rose-200 disabled:opacity-50 transition-colors cursor-pointer"
                :disabled="isActionLoading"
                @click="isRetireDialogOpen = true"
              >
                <Trash2 class="w-3.5 h-3.5" />
                <span>Retire</span>
              </button>
            </template>

            <!-- Case 2: Active without Webhook -->
            <template v-else-if="adminStatus === 'Active' && !hasWebhook">
              <!-- Trigger Run Disabled with Tooltip -->
              <span
                v-tooltip.bottom="'No webhook configured. Add webhook URL in settings.'"
                class="inline-block"
                title="No webhook configured. Add webhook URL in settings."
              >
                <button
                  type="button"
                  disabled
                  class="inline-flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-slate-200 text-slate-400 border border-slate-300 cursor-not-allowed opacity-60"
                >
                  <Play class="w-3.5 h-3.5 fill-current" />
                  <span>Trigger Run</span>
                </button>
              </span>

              <!-- Pause Button -->
              <button
                type="button"
                class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-amber-50 text-amber-700 border border-amber-300 hover:bg-amber-100 disabled:opacity-50 transition-colors shadow-2xs cursor-pointer"
                :disabled="isActionLoading"
                @click="handlePause"
              >
                <Pause class="w-3.5 h-3.5" />
                <span>Pause</span>
              </button>

              <!-- Edit Button -->
              <RouterLink
                :to="`/detectors/${detector.id}/edit`"
                class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-white border border-slate-300 text-slate-700 hover:bg-slate-50 transition-colors shadow-2xs"
              >
                <Edit class="w-3.5 h-3.5 text-slate-500" />
                <span>Edit</span>
              </RouterLink>

              <!-- Retire Button -->
              <button
                type="button"
                class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg text-rose-600 hover:bg-rose-50 border border-transparent hover:border-rose-200 disabled:opacity-50 transition-colors cursor-pointer"
                :disabled="isActionLoading"
                @click="isRetireDialogOpen = true"
              >
                <Trash2 class="w-3.5 h-3.5" />
                <span>Retire</span>
              </button>
            </template>

            <!-- Case 3: Paused -->
            <template v-else-if="adminStatus === 'Paused'">
              <!-- Resume (Primary Emerald) -->
              <button
                type="button"
                class="inline-flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white disabled:opacity-50 transition-colors shadow-xs cursor-pointer"
                :disabled="isActionLoading"
                @click="handleResume"
              >
                <RotateCcw class="w-3.5 h-3.5" />
                <span>Resume</span>
              </button>

              <!-- Edit Button -->
              <RouterLink
                :to="`/detectors/${detector.id}/edit`"
                class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-white border border-slate-300 text-slate-700 hover:bg-slate-50 transition-colors shadow-2xs"
              >
                <Edit class="w-3.5 h-3.5 text-slate-500" />
                <span>Edit</span>
              </RouterLink>

              <!-- Retire Button -->
              <button
                type="button"
                class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg text-rose-600 hover:bg-rose-50 border border-transparent hover:border-rose-200 disabled:opacity-50 transition-colors cursor-pointer"
                :disabled="isActionLoading"
                @click="isRetireDialogOpen = true"
              >
                <Trash2 class="w-3.5 h-3.5" />
                <span>Retire</span>
              </button>
            </template>

            <!-- Case 4: Retired (Read-only banner handles messaging) -->
          </div>
        </div>
      </header>

      <!-- Main Body Container -->
      <main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 space-y-6">
        <!-- Cadence Drift Advisory Banner (§DEC-003, BR-PRIMA-010) -->
        <div
          v-if="detector.cadenceDriftWarning"
          class="bg-amber-50 border border-amber-300 rounded-xl p-4 flex items-start gap-3 text-amber-900 shadow-2xs"
        >
          <AlertTriangle class="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
          <div class="text-xs min-w-0">
            <div class="flex flex-wrap items-center gap-2">
              <span class="font-bold uppercase tracking-wider text-amber-950">Cadence Drift Warning</span>
              <span
                v-if="detector.lastActualIntervalSeconds"
                class="font-mono text-amber-900 text-[11px] bg-amber-100/90 px-2 py-0.5 rounded border border-amber-300"
              >
                Actual: {{ detector.lastActualIntervalSeconds }}s vs Expected: {{ detector.expectedCadenceSeconds }}s
              </span>
            </div>
            <p class="text-xs text-amber-900 mt-1 leading-relaxed">
              {{ detector.cadenceDriftWarning }}
            </p>
            <p class="text-[11px] text-amber-800 mt-1">
              Scheduled execution interval deviated by &gt; ±50%. Telemetry ingestion continues normally; check detector host timer/scheduler if persistent.
            </p>
          </div>
        </div>

        <!-- Decommissioned Banner if Retired -->
        <div
          v-if="adminStatus === 'Retired'"
          class="bg-slate-100 border border-slate-300 rounded-xl p-4 flex items-center gap-3 text-slate-700 shadow-2xs"
        >
          <XCircle class="w-5 h-5 text-slate-500 shrink-0" />
          <div class="text-xs">
            <span class="font-bold text-slate-900 uppercase tracking-wider mr-2">Decommissioned:</span>
            <span>This detector is permanently decommissioned. Runs and silence checks are inactive.</span>
          </div>
        </div>

        <!-- Paused Advisory Banner if Paused -->
        <div
          v-else-if="adminStatus === 'Paused'"
          class="bg-amber-50 border border-amber-200 rounded-xl p-4 flex items-center gap-3 text-amber-900 shadow-2xs"
        >
          <Pause class="w-5 h-5 text-amber-600 shrink-0" />
          <div class="text-xs">
            <span class="font-bold uppercase tracking-wider mr-2">Supervision Paused:</span>
            <span>Cadence monitoring and automated probe checks are paused. Click <strong>Resume</strong> in the action bar above to restore active supervision.</span>
          </div>
        </div>

        <!-- SECTION 1: DETECTOR OVERVIEW & CONFIGURATION CARD -->
        <div class="bg-white rounded-xl border border-slate-200 p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between border-b border-slate-100 pb-3">
            <h3 class="text-sm font-semibold text-slate-900 uppercase tracking-wider flex items-center gap-2">
              <Server class="w-4 h-4 text-slate-500" />
              Detector Overview & Configuration
            </h3>
            <span class="text-xs text-slate-500 font-mono">ID: {{ detector.id }}</span>
          </div>

          <!-- Description -->
          <div>
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Description</span>
            <p class="text-sm text-slate-800 mt-1 leading-relaxed">
              {{ detector.description }}
            </p>
          </div>

          <!-- Configuration Grid -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 pt-2">
            <!-- Scope -->
            <div class="bg-slate-50 p-3 rounded-lg border border-slate-200">
              <span class="text-xs text-slate-500 block">Monitoring Scope</span>
              <span class="text-sm font-semibold text-slate-900 mt-0.5 block">{{ detector.scope }}</span>
            </div>

            <!-- Target Module -->
            <div class="bg-slate-50 p-3 rounded-lg border border-slate-200">
              <span class="text-xs text-slate-500 block">Target Module</span>
              <span class="text-sm font-semibold text-slate-900 mt-0.5 block">{{ detector.targetModule }}</span>
            </div>

            <!-- Runtime Version (§DEC-001, DEC-004) -->
            <div class="bg-slate-50 p-3 rounded-lg border border-slate-200">
              <span class="text-xs text-slate-500 block">Detector Version</span>
              <span class="text-sm font-semibold text-slate-900 mt-0.5 block font-mono">
                {{ detector.version ? `v${detector.version}` : 'Manual / Legacy' }}
              </span>
            </div>

            <!-- Expected Cadence & Threshold -->
            <div class="bg-slate-50 p-3 rounded-lg border border-slate-200">
              <span class="text-xs text-slate-500 block">Expected Cadence & Threshold</span>
              <span class="text-sm font-semibold text-slate-900 mt-0.5 block">
                {{ cadenceInfo.summary }}
              </span>
              <span class="text-[11px] text-slate-500 block">
                {{ cadenceInfo.threshold }} (2x silence threshold)
              </span>
            </div>

            <!-- Last Actual Interval & Drift Status (§DEC-003, BR-PRIMA-010) -->
            <div
              class="p-3 rounded-lg border transition-colors"
              :class="detector.cadenceDriftWarning ? 'bg-amber-50/70 border-amber-300' : 'bg-slate-50 border-slate-200'"
            >
              <div class="flex items-center justify-between">
                <span class="text-xs text-slate-500 block">Last Actual Interval</span>
                <span
                  v-if="detector.cadenceDriftWarning"
                  class="text-[10px] font-semibold text-amber-800 bg-amber-100 px-1.5 py-0.5 rounded border border-amber-300"
                >
                  Drifted (&gt;±50%)
                </span>
                <span
                  v-else-if="detector.lastActualIntervalSeconds != null"
                  class="text-[10px] font-semibold text-emerald-700 bg-emerald-100 px-1.5 py-0.5 rounded border border-emerald-300"
                >
                  Within Target
                </span>
              </div>
              <span
                class="text-sm font-semibold mt-0.5 block font-mono"
                :class="detector.cadenceDriftWarning ? 'text-amber-950 font-bold' : 'text-slate-900'"
              >
                {{ detector.lastActualIntervalSeconds != null ? `${detector.lastActualIntervalSeconds}s` : 'Pending telemetry' }}
              </span>
              <span class="text-[11px] block text-slate-500">
                Target: {{ detector.expectedCadenceSeconds }}s (tolerance ±50%)
              </span>
            </div>

            <!-- Registered Date -->
            <div class="bg-slate-50 p-3 rounded-lg border border-slate-200">
              <span class="text-xs text-slate-500 block">Registered At (WIB)</span>
              <span class="text-sm font-semibold text-slate-900 mt-0.5 block truncate" :title="formatWibDateTime(detector.createdAtUtc, true)">
                {{ formatWibDateTime(detector.createdAtUtc, false) }}
              </span>
              <span class="text-[11px] text-slate-500 block">
                {{ formatRelativeTime(detector.createdAtUtc) }}
              </span>
            </div>
          </div>

          <!-- Heartbeat and Webhook Strip -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-1">
            <!-- Last Healthy Heartbeat -->
            <div class="p-3 rounded-lg bg-slate-50 border border-slate-200 flex items-start gap-3">
              <Radio class="w-4 h-4 text-slate-500 shrink-0 mt-0.5" />
              <div class="text-xs min-w-0">
                <span class="text-slate-500 block">Last Healthy Heartbeat</span>
                <span class="text-sm font-semibold text-slate-900 mt-0.5 block truncate" :title="formatWibDateTime(detector.lastHealthyHeartbeatUtc, true)">
                  {{ detector.lastHealthyHeartbeatUtc ? formatWibDateTime(detector.lastHealthyHeartbeatUtc, false) : 'No heartbeat recorded' }}
                </span>
                <span class="text-[11px] text-slate-500 block">
                  {{ detector.lastHealthyHeartbeatUtc ? formatRelativeTime(detector.lastHealthyHeartbeatUtc) : 'Awaiting initial probe' }}
                </span>
              </div>
            </div>

            <!-- Probe Webhook URL -->
            <div class="p-3 rounded-lg bg-slate-50 border border-slate-200 flex items-start justify-between gap-3">
              <div class="flex items-start gap-2.5 min-w-0">
                <Globe class="w-4 h-4 text-slate-500 shrink-0 mt-0.5" />
                <div class="text-xs min-w-0">
                  <span class="text-slate-500 block">Probe Webhook URL</span>
                  <div v-if="detector.webhookUrl" class="text-xs font-mono text-slate-900 truncate mt-0.5 select-all" :title="detector.webhookUrl">
                    {{ detector.webhookUrl }}
                  </div>
                  <div v-else class="text-xs text-slate-500 italic mt-0.5">
                    None (Passive telemetry or manual only)
                  </div>
                </div>
              </div>

              <button
                v-if="detector.webhookUrl"
                type="button"
                class="shrink-0 p-1.5 text-slate-500 hover:text-slate-900 hover:bg-white rounded border border-transparent hover:border-slate-300 transition-colors cursor-pointer"
                title="Copy Webhook URL"
                @click="copyWebhookUrl"
              >
                <component :is="isWebhookCopied ? Check : Copy" class="w-4 h-4 text-slate-600" />
              </button>
            </div>
          </div>
        </div>

        <!-- SECTION 2: 30-DAY RELIABILITY METRICS CARD -->
        <ReliabilityStatsCard :stats="detector.reliabilityStats" />

        <!-- SECTION 3: LINKED ACTIVE ISSUES LIST -->
        <LinkedActiveIssuesList :issues="detector.activeIssues" />

        <!-- SECTION 4: RECENT INSPECTION RUNS TABLE -->
        <RecentRunsTable
          :runs="detector.recentRuns"
          :detector-id="detector.id"
        />
      </main>
    </div>

    <!-- Confirmation Modal for Decommissioning/Retirement -->
    <ConfirmRetireDialog
      v-model:visible="isRetireDialogOpen"
      :detector-name="detector?.name"
      :is-loading="toggleMutation.isPending.value"
      @confirm="handleConfirmRetire"
    />
  </div>
</template>
