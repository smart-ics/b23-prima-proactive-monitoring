<script setup lang="ts">
import { computed, ref } from 'vue';
import { MessageSquare, Send, User } from 'lucide-vue-next';
import type { InvestigationNoteDto } from '@/shared/types/api.generated';
import { formatWibDateTime, formatRelativeTime } from '@/shared/utils/dateUtils';

const props = defineProps<{
  notes: InvestigationNoteDto[];
  modelValue: string;
  isSubmitting?: boolean;
  isReadOnly?: boolean;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
  (e: 'submit'): void;
}>();

const textareaRef = ref<HTMLTextAreaElement | null>(null);

// Chronological append-only (oldest to newest for natural reading stream)
const sortedNotes = computed(() => {
  if (!props.notes) return [];
  return [...props.notes].sort(
    (a, b) => new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime()
  );
});

function handleInput(event: Event) {
  const target = event.target as HTMLTextAreaElement;
  emit('update:modelValue', target.value);
}

function handleKeydown(event: KeyboardEvent) {
  // Ctrl+Enter or Cmd+Enter to submit
  if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
    event.preventDefault();
    if (props.modelValue.trim() && !props.isSubmitting && !props.isReadOnly) {
      emit('submit');
    }
  }
}

function submitNote() {
  if (props.modelValue.trim() && !props.isSubmitting && !props.isReadOnly) {
    emit('submit');
  }
}

defineExpose({
  focusComposer: () => {
    textareaRef.value?.focus();
  },
  focus: () => {
    textareaRef.value?.focus();
  },
});
</script>

<template>
  <div class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm space-y-4">
    <!-- Stream Header -->
    <div class="flex items-center justify-between pb-3 border-b border-slate-200">
      <div class="flex items-center gap-2">
        <MessageSquare class="w-4 h-4 text-indigo-600" />
        <h2 class="text-sm font-semibold text-slate-900">Investigation Notes Stream</h2>
      </div>
      <span class="text-xs font-medium text-slate-500">
        {{ notes.length }} {{ notes.length === 1 ? 'note' : 'notes' }}
      </span>
    </div>

    <!-- Notes List (Chronological) -->
    <div v-if="sortedNotes.length === 0" class="py-6 text-center text-sm text-slate-500">
      No investigation notes yet. Add observations or triage details below.
    </div>

    <div v-else class="space-y-3 max-h-96 overflow-y-auto pr-1 scrollbar-thin scrollbar-thumb-slate-300">
      <div
        v-for="note in sortedNotes"
        :key="note.id"
        class="p-3.5 rounded-lg bg-slate-50 border border-slate-200 space-y-2"
      >
        <!-- Note Header: Officer Badge & Timestamps -->
        <div class="flex items-center justify-between flex-wrap gap-2">
          <div class="flex items-center gap-2">
            <span
              class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-200"
            >
              <User class="w-3 h-3 text-indigo-600" />
              {{ note.officerName }}
            </span>
          </div>

          <div class="text-xs text-slate-500">
            <span>{{ formatRelativeTime(note.createdAtUtc) }}</span>
            <span class="text-slate-400 mx-1">·</span>
            <span class="text-slate-400" :title="note.createdAtUtc">{{ formatWibDateTime(note.createdAtUtc, true) }}</span>
          </div>
        </div>

        <!-- Note Text Body -->
        <p class="text-sm text-slate-900 whitespace-pre-wrap leading-relaxed">
          {{ note.noteText }}
        </p>
      </div>
    </div>

    <!-- Note Composer Section -->
    <div v-if="!isReadOnly" class="pt-2 border-t border-slate-200 space-y-2.5">
      <div class="relative">
        <textarea
          ref="textareaRef"
          :value="modelValue"
          @input="handleInput"
          @keydown="handleKeydown"
          :disabled="isSubmitting"
          rows="3"
          placeholder="Append investigation findings or diagnostic notes... (Ctrl+Enter to submit)"
          class="w-full rounded-lg bg-white border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 disabled:opacity-50 transition-colors resize-y min-h-[80px]"
        ></textarea>
      </div>

      <div class="flex items-center justify-between">
        <span class="text-xs text-slate-500 hidden sm:inline">
          Press <kbd class="px-1.5 py-0.5 rounded bg-slate-100 border border-slate-300 text-slate-600 font-mono text-[10px]">Ctrl</kbd> + <kbd class="px-1.5 py-0.5 rounded bg-slate-100 border border-slate-300 text-slate-600 font-mono text-[10px]">Enter</kbd> to submit
        </span>

        <button
          type="button"
          @click="submitNote"
          :disabled="!modelValue.trim() || isSubmitting"
          class="ml-auto inline-flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-semibold bg-indigo-600 hover:bg-indigo-500 text-white shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-white focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Send class="w-3.5 h-3.5" />
          <span>{{ isSubmitting ? 'Posting...' : 'Add Note' }}</span>
        </button>
      </div>
    </div>

    <!-- Read-Only Notice -->
    <div v-else class="pt-2 border-t border-slate-200 text-center py-2 text-xs text-slate-500 italic">
      Issue is closed. Adding notes is disabled.
    </div>
  </div>
</template>
