<script setup lang="ts">
import { computed } from 'vue';
import Accordion from 'primevue/accordion';
import AccordionPanel from 'primevue/accordionpanel';
import AccordionHeader from 'primevue/accordionheader';
import AccordionContent from 'primevue/accordioncontent';
import { History, User, Clock } from 'lucide-vue-next';
import type { IssueDetailDto, IssueActivityLogDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

const props = defineProps<{
  issueDetail?: IssueDetailDto;
  activityLogs?: IssueActivityLogDto[];
}>();

const allLogs = computed<IssueActivityLogDto[]>(() => {
  if (props.activityLogs) {
    return [...props.activityLogs].sort(
      (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
    );
  }
  const fromIssue = props.issueDetail?.activityLogs ?? [];
  return [...fromIssue].sort(
    (a, b) => new Date(b.timestampUtc).getTime() - new Date(a.timestampUtc).getTime()
  );
});

function formatActivityTypeLabel(type: string): string {
  switch (type) {
    case 'Created':
      return 'Discovery';
    case 'Acknowledged':
      return 'Acknowledged';
    case 'Assigned':
      return 'Assigned';
    case 'Reassigned':
      return 'Reassigned';
    case 'InvestigationStarted':
      return 'Investigation Started';
    case 'NoteAdded':
      return 'Note Added';
    case 'RemediationRecorded':
      return 'Remediation Recorded';
    case 'VerificationRecorded':
      return 'Verification Recorded';
    case 'Resolved':
      return 'Resolved';
    case 'Dismissed':
      return 'Dismissed';
    case 'Reopened':
      return 'Reopened';
    default:
      return type;
  }
}

function getActivityTypeBadgeClass(type: string): string {
  switch (type) {
    case 'Created':
      return 'bg-blue-50 text-blue-700 border-blue-300';
    case 'Acknowledged':
      return 'bg-amber-50 text-amber-700 border-amber-300';
    case 'Assigned':
    case 'Reassigned':
      return 'bg-cyan-50 text-cyan-700 border-cyan-300';
    case 'InvestigationStarted':
      return 'bg-indigo-50 text-indigo-700 border-indigo-300';
    case 'NoteAdded':
      return 'bg-sky-50 text-sky-700 border-sky-300';
    case 'RemediationRecorded':
      return 'bg-purple-50 text-purple-700 border-purple-300';
    case 'VerificationRecorded':
      return 'bg-teal-50 text-teal-700 border-teal-300';
    case 'Resolved':
      return 'bg-emerald-50 text-emerald-700 border-emerald-300';
    case 'Dismissed':
      return 'bg-slate-100 text-slate-700 border-slate-300';
    case 'Reopened':
      return 'bg-rose-50 text-rose-700 border-rose-300';
    default:
      return 'bg-slate-100 text-slate-700 border-slate-300';
  }
}
</script>

<template>
  <div class="bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm text-slate-900">
    <!-- PrimeVue Accordion (collapsed by default via value=null) -->
    <Accordion :value="null" class="activity-accordion">
      <AccordionPanel value="activity-log-panel">
        <AccordionHeader class="!bg-white !hover:bg-slate-50 !border-none !p-5 cursor-pointer">
          <div class="flex items-center justify-between w-full pr-2">
            <div class="flex items-center gap-2">
              <History class="w-5 h-5 text-indigo-600" />
              <span class="font-semibold text-slate-900 text-sm tracking-wide uppercase">Activity Audit Trail</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-xs px-2 py-0.5 rounded font-mono font-medium bg-slate-100 text-slate-700 border border-slate-300">
                {{ allLogs.length }} {{ allLogs.length === 1 ? 'event' : 'events' }}
              </span>
            </div>
          </div>
        </AccordionHeader>

        <AccordionContent class="!bg-white !border-t !border-slate-200 !p-5">
          <!-- Empty State -->
          <div
            v-if="allLogs.length === 0"
            class="py-6 text-center text-slate-500 border border-dashed border-slate-300 rounded-lg text-xs"
          >
            <Clock class="w-6 h-6 mx-auto mb-2 text-slate-400" />
            No activity log events recorded yet.
          </div>

          <!-- Chronological Audit Trail Timeline -->
          <div v-else class="relative border-l border-slate-200 ml-3 space-y-4">
            <div
              v-for="log in allLogs"
              :key="log.id"
              class="relative pl-6 space-y-1.5 group"
            >
              <!-- Timeline Dot -->
              <div class="absolute -left-1.5 top-1.5 w-3 h-3 rounded-full bg-white border-2 border-indigo-500 group-hover:scale-110 transition-transform"></div>

              <!-- Header with Badge & Timestamp -->
              <div class="flex items-center justify-between flex-wrap gap-2">
                <div class="flex items-center gap-2">
                  <span
                    class="px-2 py-0.5 rounded-full text-[11px] font-medium border"
                    :class="getActivityTypeBadgeClass(log.activityType)"
                  >
                    {{ formatActivityTypeLabel(log.activityType) }}
                  </span>
                  <span class="text-xs font-medium text-slate-700 flex items-center gap-1">
                    <User class="w-3 h-3 text-slate-400" />
                    {{ log.actorName }}
                  </span>
                </div>

                <span
                  class="text-[11px] text-slate-500"
                  :title="formatWibDateTime(log.timestampUtc, true)"
                >
                  {{ formatRelativeTime(log.timestampUtc) }} • {{ formatWibDateTime(log.timestampUtc, true) }}
                </span>
              </div>

              <!-- Summary Text -->
              <p class="text-xs text-slate-900 leading-relaxed font-sans">
                {{ log.summary }}
              </p>

              <!-- Optional Details JSON snippet if present -->
              <div
                v-if="log.detailsJson"
                class="text-[11px] text-slate-600 font-mono bg-slate-50 p-2 rounded border border-slate-200 overflow-x-auto whitespace-pre-wrap"
              >
                {{ log.detailsJson }}
              </div>
            </div>
          </div>
        </AccordionContent>
      </AccordionPanel>
    </Accordion>
  </div>
</template>

<style scoped>
:deep(.p-accordionheader) {
  color: inherit;
}
:deep(.p-accordioncontent-content) {
  background: transparent !important;
  padding: 0 !important;
}
</style>
