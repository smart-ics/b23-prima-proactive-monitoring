<script setup lang="ts">
import { RouterLink } from 'vue-router';
import {
  AlertCircle,
  Flame,
  ArrowRight,
  ShieldCheck,
  Repeat,
  Clock,
} from 'lucide-vue-next';
import type { LinkedActiveIssueDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

interface Props {
  issues?: LinkedActiveIssueDto[] | null;
}

defineProps<Props>();

function getSeverityBadgeClass(severity: string): string {
  switch (severity) {
    case 'Critical':
      return 'bg-rose-100 text-rose-800 border-rose-300 font-semibold';
    case 'High':
      return 'bg-orange-100 text-orange-800 border-orange-300 font-medium';
    case 'Medium':
      return 'bg-amber-100 text-amber-800 border-amber-300 font-medium';
    case 'Low':
      return 'bg-blue-100 text-blue-800 border-blue-300 font-medium';
    default:
      return 'bg-slate-100 text-slate-700 border-slate-300 font-medium';
  }
}

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'Open':
      return 'bg-rose-50 text-rose-700 border-rose-200';
    case 'Acknowledged':
      return 'bg-blue-50 text-blue-700 border-blue-200';
    case 'Investigating':
      return 'bg-amber-50 text-amber-700 border-amber-200';
    case 'Reopened':
      return 'bg-purple-50 text-purple-700 border-purple-200';
    default:
      return 'bg-slate-50 text-slate-700 border-slate-200';
  }
}
</script>

<template>
  <div class="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
    <!-- Header -->
    <div class="px-5 py-4 border-b border-slate-200 flex items-center justify-between">
      <div class="flex items-center gap-2">
        <AlertCircle class="w-4 h-4 text-slate-500" />
        <h3 class="text-sm font-semibold text-slate-900 uppercase tracking-wider">
          Linked Active Issues
        </h3>
        <span
          v-if="issues && issues.length > 0"
          class="ml-1 inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-rose-100 text-rose-700"
        >
          {{ issues.length }} active
        </span>
      </div>
      <span class="text-xs text-slate-500 font-normal">
        Unclosed issues originating from or correlating to this detector
      </span>
    </div>

    <!-- Empty State -->
    <div
      v-if="!issues || issues.length === 0"
      class="p-8 text-center"
    >
      <div class="w-10 h-10 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center mx-auto mb-2.5">
        <ShieldCheck class="w-5 h-5" />
      </div>
      <p class="text-sm font-semibold text-slate-800">No active issues detected</p>
      <p class="text-xs text-slate-500 mt-1 max-w-sm mx-auto">
        This detector has no unclosed anomalies, silence breaches, or execution failures in progress.
      </p>
    </div>

    <!-- Active Issues List -->
    <div v-else class="divide-y divide-slate-100">
      <div
        v-for="issue in issues"
        :key="issue.id"
        class="p-4 hover:bg-slate-50/70 transition-colors flex flex-col sm:flex-row sm:items-center justify-between gap-3"
      >
        <div class="space-y-1.5 min-w-0">
          <div class="flex flex-wrap items-center gap-2">
            <!-- Severity Chip -->
            <span
              class="inline-flex items-center px-2 py-0.5 rounded text-[11px] border"
              :class="getSeverityBadgeClass(issue.currentSeverity)"
            >
              {{ issue.currentSeverity }}
            </span>

            <!-- Status Badge -->
            <span
              class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border"
              :class="getStatusBadgeClass(issue.status)"
            >
              {{ issue.status }}
            </span>

            <!-- Dynamic Escalation Badge -->
            <span
              v-if="issue.escalationLevel > 0"
              class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold bg-rose-50 text-rose-700 border border-rose-200 animate-pulse"
            >
              <Flame class="w-3 h-3 text-rose-600" />
              Escalated (Lv {{ issue.escalationLevel }})
            </span>

            <!-- Correlation Key Monospace -->
            <code class="text-[11px] font-mono text-slate-600 bg-slate-100 px-1.5 py-0.5 rounded">
              {{ issue.correlationKey }}
            </code>
          </div>

          <!-- Title -->
          <h4 class="text-sm font-semibold text-slate-900 truncate">
            {{ issue.title }}
          </h4>

          <!-- Metadata row -->
          <div class="flex flex-wrap items-center gap-4 text-xs text-slate-500">
            <span class="inline-flex items-center gap-1">
              <Repeat class="w-3.5 h-3.5 text-slate-400" />
              <strong class="font-medium text-slate-700">{{ issue.occurrenceCount }}</strong> {{ issue.occurrenceCount === 1 ? 'occurrence' : 'occurrences' }}
            </span>
            <span class="inline-flex items-center gap-1" :title="formatWibDateTime(issue.lastOccurrenceAtUtc, true)">
              <Clock class="w-3.5 h-3.5 text-slate-400" />
              Last observed: {{ formatRelativeTime(issue.lastOccurrenceAtUtc) }}
            </span>
          </div>
        </div>

        <!-- Direct router link to S-02 Issue Detail -->
        <div class="sm:shrink-0 flex items-center">
          <RouterLink
            :to="`/issues/${issue.id}`"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-900 text-white hover:bg-slate-800 transition-colors shadow-xs"
          >
            <span>Investigate Issue</span>
            <ArrowRight class="w-3.5 h-3.5" />
          </RouterLink>
        </div>
      </div>
    </div>
  </div>
</template>
