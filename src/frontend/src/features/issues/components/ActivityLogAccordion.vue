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
      return 'bg-blue-500/20 text-blue-300 border-blue-500/40';
    case 'Acknowledged':
      return 'bg-amber-500/20 text-amber-300 border-amber-500/40';
    case 'Assigned':
    case 'Reassigned':
      return 'bg-cyan-500/20 text-cyan-300 border-cyan-500/40';
    case 'InvestigationStarted':
      return 'bg-indigo-500/20 text-indigo-300 border-indigo-500/40';
    case 'NoteAdded':
      return 'bg-sky-500/20 text-sky-300 border-sky-500/40';
    case 'RemediationRecorded':
      return 'bg-purple-500/20 text-purple-300 border-purple-500/40';
    case 'VerificationRecorded':
      return 'bg-teal-500/20 text-teal-300 border-teal-500/40';
    case 'Resolved':
      return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40';
    case 'Dismissed':
      return 'bg-slate-700/60 text-slate-400 border-slate-600/50';
    case 'Reopened':
      return 'bg-rose-500/20 text-rose-300 border-rose-500/40';
    default:
      return 'bg-slate-800 text-slate-300 border-slate-700';
  }
}
</script>

<template>
  <div class="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-sm text-slate-100">
    <!-- PrimeVue Accordion (collapsed by default via value=null) -->
    <Accordion :value="null" class="activity-accordion">
      <AccordionPanel value="activity-log-panel">
        <AccordionHeader class="!bg-slate-900 !hover:bg-slate-800/60 !border-none !p-5 cursor-pointer">
          <div class="flex items-center justify-between w-full pr-2">
            <div class="flex items-center gap-2">
              <History class="w-5 h-5 text-indigo-400" />
              <span class="font-semibold text-slate-100 text-sm tracking-wide uppercase">Activity Audit Trail</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-xs px-2 py-0.5 rounded font-mono font-medium bg-slate-800 text-slate-300 border border-slate-700/60">
                {{ allLogs.length }} {{ allLogs.length === 1 ? 'event' : 'events' }}
              </span>
            </div>
          </div>
        </AccordionHeader>

        <AccordionContent class="!bg-slate-900 !border-t !border-slate-800 !p-5">
          <!-- Empty State -->
          <div
            v-if="allLogs.length === 0"
            class="py-6 text-center text-slate-500 border border-dashed border-slate-800 rounded-lg text-xs"
          >
            <Clock class="w-6 h-6 mx-auto mb-2 text-slate-600" />
            No activity log events recorded yet.
          </div>

          <!-- Chronological Audit Trail Timeline -->
          <div v-else class="relative border-l border-slate-800 ml-3 space-y-4">
            <div
              v-for="log in allLogs"
              :key="log.id"
              class="relative pl-6 space-y-1.5 group"
            >
              <!-- Timeline Dot -->
              <div class="absolute -left-1.5 top-1.5 w-3 h-3 rounded-full bg-slate-800 border-2 border-indigo-400 group-hover:scale-110 transition-transform"></div>

              <!-- Header with Badge & Timestamp -->
              <div class="flex items-center justify-between flex-wrap gap-2">
                <div class="flex items-center gap-2">
                  <span
                    class="px-2 py-0.5 rounded-full text-[11px] font-medium border"
                    :class="getActivityTypeBadgeClass(log.activityType)"
                  >
                    {{ formatActivityTypeLabel(log.activityType) }}
                  </span>
                  <span class="text-xs font-medium text-slate-300 flex items-center gap-1">
                    <User class="w-3 h-3 text-slate-500" />
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
              <p class="text-xs text-slate-300 leading-relaxed font-sans">
                {{ log.summary }}
              </p>

              <!-- Optional Details JSON snippet if present -->
              <div
                v-if="log.detailsJson"
                class="text-[11px] text-slate-400 font-mono bg-slate-950/60 p-2 rounded border border-slate-800/80 overflow-x-auto whitespace-pre-wrap"
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
