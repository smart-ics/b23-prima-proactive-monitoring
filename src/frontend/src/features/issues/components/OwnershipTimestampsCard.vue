<script setup lang="ts">
import { computed } from 'vue';
import { User, Calendar, Clock, UserCheck, UserPlus, CheckCircle2, Ban, RotateCcw } from 'lucide-vue-next';
import type { IssueDetailDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

const props = defineProps<{
  issueDetail?: IssueDetailDto;
  assignee?: string | null;
  createdAtUtc?: string | null;
  acknowledgedAtUtc?: string | null;
  lastOccurrenceAtUtc?: string | null;
  resolvedAtUtc?: string | null;
  dismissedAtUtc?: string | null;
  reopenedAtUtc?: string | null;
  status?: string;
}>();

const emit = defineEmits<{
  (e: 'reassign'): void;
  (e: 'open-assign-modal'): void;
}>();

const resolvedIssue = computed(() => props.issueDetail?.issue ?? props.issueDetail);

const currentStatus = computed(() => {
  if (props.status) return props.status;
  return resolvedIssue.value?.status ?? props.issueDetail?.status ?? 'Open';
});

const currentAssignee = computed(() => {
  if (props.assignee !== undefined) return props.assignee;
  return resolvedIssue.value?.currentAssignee ?? props.issueDetail?.currentAssignee ?? null;
});

const createdAt = computed(() => {
  if (props.createdAtUtc !== undefined) return props.createdAtUtc;
  return resolvedIssue.value?.createdAtUtc ?? props.issueDetail?.createdAtUtc ?? null;
});

const acknowledgedAt = computed(() => {
  if (props.acknowledgedAtUtc !== undefined) return props.acknowledgedAtUtc;
  return resolvedIssue.value?.acknowledgedAtUtc ?? props.issueDetail?.acknowledgedAtUtc ?? null;
});

const lastSeenAt = computed(() => {
  if (props.lastOccurrenceAtUtc !== undefined) return props.lastOccurrenceAtUtc;
  return resolvedIssue.value?.lastOccurrenceAtUtc ?? props.issueDetail?.lastOccurrenceAtUtc ?? null;
});

const resolvedAt = computed(() => {
  if (props.resolvedAtUtc !== undefined) return props.resolvedAtUtc;
  return resolvedIssue.value?.resolvedAtUtc ?? props.issueDetail?.resolvedAtUtc ?? null;
});

const dismissedAt = computed(() => {
  if (props.dismissedAtUtc !== undefined) return props.dismissedAtUtc;
  return resolvedIssue.value?.dismissedAtUtc ?? props.issueDetail?.dismissedAtUtc ?? null;
});

const reopenedAt = computed(() => {
  if (props.reopenedAtUtc !== undefined) return props.reopenedAtUtc;
  return resolvedIssue.value?.reopenedAtUtc ?? props.issueDetail?.reopenedAtUtc ?? null;
});

const isActive = computed(() => {
  return currentStatus.value !== 'Resolved' && currentStatus.value !== 'Dismissed';
});

function handleReassign() {
  emit('reassign');
  emit('open-assign-modal');
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-sm text-slate-900 flex flex-col gap-4">
    <!-- Card Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <User class="w-5 h-5 text-indigo-600" />
        <h3 class="font-semibold text-slate-900 text-sm tracking-wide uppercase">Ownership & Timeline</h3>
      </div>
    </div>

    <!-- Current Assignee Section -->
    <div class="bg-slate-50 border border-slate-200 rounded-lg p-3.5 space-y-2">
      <div class="flex items-center justify-between">
        <span class="text-xs text-slate-500 font-medium">Assigned Officer</span>
        <!-- Reassign / Assign Button for Active Issues -->
        <button
          v-if="isActive"
          type="button"
          class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-medium rounded-md bg-indigo-50 text-indigo-700 hover:bg-indigo-100 border border-indigo-300 transition-colors cursor-pointer"
          @click="handleReassign"
        >
          <UserCheck v-if="currentAssignee" class="w-3.5 h-3.5 text-indigo-600" />
          <UserPlus v-else class="w-3.5 h-3.5 text-indigo-600" />
          {{ currentAssignee ? 'Reassign' : 'Assign Officer' }}
        </button>
      </div>

      <div class="flex items-center gap-2.5 pt-0.5">
        <div class="w-8 h-8 rounded-full flex items-center justify-center font-semibold text-xs border"
          :class="[
            currentAssignee
              ? 'bg-indigo-50 text-indigo-700 border-indigo-300'
              : 'bg-slate-100 text-slate-500 border-slate-300'
          ]"
        >
          <User class="w-4 h-4" />
        </div>
        <div>
          <div class="text-sm font-medium" :class="currentAssignee ? 'text-slate-900' : 'text-slate-500 italic'">
            {{ currentAssignee || 'Unassigned' }}
          </div>
          <div class="text-[11px] text-slate-500">
            {{ currentAssignee ? 'Lead operational handler' : 'Awaiting officer triage' }}
          </div>
        </div>
      </div>
    </div>

    <!-- Timestamps Breakdown -->
    <div class="space-y-2.5 text-xs">
      <!-- Created At -->
      <div class="flex items-center justify-between py-1.5 border-b border-slate-200">
        <div class="flex items-center gap-2 text-slate-500">
          <Calendar class="w-3.5 h-3.5 text-slate-400" />
          <span>Created</span>
        </div>
        <div class="text-right" :title="formatWibDateTime(createdAt, true)">
          <div class="text-slate-900 font-medium">{{ formatRelativeTime(createdAt) }}</div>
          <div class="text-[11px] text-slate-500">{{ formatWibDateTime(createdAt, false) }} WIB</div>
        </div>
      </div>

      <!-- Acknowledged At -->
      <div class="flex items-center justify-between py-1.5 border-b border-slate-200">
        <div class="flex items-center gap-2 text-slate-500">
          <Clock class="w-3.5 h-3.5 text-slate-400" />
          <span>Acknowledged</span>
        </div>
        <div class="text-right">
          <template v-if="acknowledgedAt">
            <div class="text-slate-900 font-medium" :title="formatWibDateTime(acknowledgedAt, true)">
              {{ formatRelativeTime(acknowledgedAt) }}
            </div>
            <div class="text-[11px] text-slate-500">{{ formatWibDateTime(acknowledgedAt, false) }} WIB</div>
          </template>
          <span v-else class="text-slate-500 italic">Not acknowledged yet</span>
        </div>
      </div>

      <!-- Last Seen / Occurred At -->
      <div class="flex items-center justify-between py-1.5 border-b border-slate-200">
        <div class="flex items-center gap-2 text-slate-500">
          <Clock class="w-3.5 h-3.5 text-slate-400" />
          <span>Last Recurred</span>
        </div>
        <div class="text-right" :title="formatWibDateTime(lastSeenAt, true)">
          <div class="text-slate-900 font-medium">{{ formatRelativeTime(lastSeenAt) }}</div>
          <div class="text-[11px] text-slate-500">{{ formatWibDateTime(lastSeenAt, false) }} WIB</div>
        </div>
      </div>

      <!-- Reopened At (if applicable) -->
      <div v-if="reopenedAt" class="flex items-center justify-between py-1.5 border-b border-slate-200">
        <div class="flex items-center gap-2 text-purple-600">
          <RotateCcw class="w-3.5 h-3.5 text-purple-600" />
          <span>Reopened</span>
        </div>
        <div class="text-right" :title="formatWibDateTime(reopenedAt, true)">
          <div class="text-purple-700 font-medium">{{ formatRelativeTime(reopenedAt) }}</div>
          <div class="text-[11px] text-purple-500">{{ formatWibDateTime(reopenedAt, false) }} WIB</div>
        </div>
      </div>

      <!-- Resolved At (if applicable) -->
      <div v-if="resolvedAt" class="flex items-center justify-between py-1.5 border-b border-slate-200">
        <div class="flex items-center gap-2 text-emerald-600">
          <CheckCircle2 class="w-3.5 h-3.5 text-emerald-600" />
          <span>Resolved</span>
        </div>
        <div class="text-right" :title="formatWibDateTime(resolvedAt, true)">
          <div class="text-emerald-700 font-medium">{{ formatRelativeTime(resolvedAt) }}</div>
          <div class="text-[11px] text-emerald-500">{{ formatWibDateTime(resolvedAt, false) }} WIB</div>
        </div>
      </div>

      <!-- Dismissed At (if applicable) -->
      <div v-if="dismissedAt" class="flex items-center justify-between py-1.5 border-b border-slate-200">
        <div class="flex items-center gap-2 text-rose-600">
          <Ban class="w-3.5 h-3.5 text-rose-600" />
          <span>Dismissed</span>
        </div>
        <div class="text-right" :title="formatWibDateTime(dismissedAt, true)">
          <div class="text-rose-700 font-medium">{{ formatRelativeTime(dismissedAt) }}</div>
          <div class="text-[11px] text-rose-500">{{ formatWibDateTime(dismissedAt, false) }} WIB</div>
        </div>
      </div>
    </div>
  </div>
</template>
