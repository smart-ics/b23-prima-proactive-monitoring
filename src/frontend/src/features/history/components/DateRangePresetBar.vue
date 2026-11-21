<script setup lang="ts">
import { computed } from 'vue';
import DatePicker from 'primevue/datepicker';
import type { DatePreset } from '@/shared/utils/dateUtils';

const props = withDefaults(
  defineProps<{
    preset: DatePreset;
    customRange: [Date | null, Date | null];
    disabled?: boolean;
  }>(),
  {
    disabled: false,
  }
);

const emit = defineEmits<{
  (e: 'update:preset', value: DatePreset): void;
  (e: 'update:customRange', value: [Date | null, Date | null]): void;
  (e: 'change', payload: { preset: DatePreset; customRange: [Date | null, Date | null] }): void;
}>();

const PRESETS: readonly DatePreset[] = [
  'Today',
  'This Week',
  'This Month',
  'Last Month',
  'Custom',
];

function selectPreset(nextPreset: DatePreset) {
  if (props.disabled || nextPreset === props.preset) {
    return;
  }
  emit('update:preset', nextPreset);
  emit('change', { preset: nextPreset, customRange: props.customRange });
}

const dateRangeModel = computed<Date[] | null>({
  get: () => {
    const [start, end] = props.customRange;
    if (start && end) {
      return [start, end];
    }
    if (start) {
      return [start];
    }
    return null;
  },
  set: (value) => {
    if (!Array.isArray(value)) {
      const empty: [Date | null, Date | null] = [null, null];
      emit('update:customRange', empty);
      emit('change', { preset: props.preset, customRange: empty });
      return;
    }

    const start = value[0] instanceof Date && !Number.isNaN(value[0].getTime()) ? value[0] : null;
    const end = value[1] instanceof Date && !Number.isNaN(value[1].getTime()) ? value[1] : null;
    const nextRange: [Date | null, Date | null] = [start, end];

    emit('update:customRange', nextRange);
    emit('change', { preset: props.preset, customRange: nextRange });
  },
});
</script>

<template>
  <div
    class="flex flex-wrap items-center gap-3"
    role="group"
    aria-label="Historical date range presets"
  >
    <div class="inline-flex flex-wrap items-center rounded-lg bg-slate-100 border border-slate-200 p-0.5 shadow-2xs">
      <button
        v-for="item in PRESETS"
        :key="item"
        type="button"
        :disabled="disabled"
        :aria-pressed="preset === item"
        @click="selectPreset(item)"
        class="px-3 py-1.5 text-xs font-medium rounded-md transition-all cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-900 disabled:opacity-50 disabled:cursor-not-allowed"
        :class="[
          preset === item
            ? 'bg-white text-slate-900 shadow-2xs font-semibold'
            : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/50',
        ]"
      >
        <span class="inline-flex items-center gap-1.5">
          <i v-if="item === 'Custom'" class="pi pi-calendar text-[11px]" aria-hidden="true"></i>
          <span>{{ item }}</span>
        </span>
      </button>
    </div>

    <div v-if="preset === 'Custom'" class="flex items-center gap-2">
      <DatePicker
        v-model="dateRangeModel"
        selectionMode="range"
        dateFormat="dd M yy"
        :manualInput="false"
        :disabled="disabled"
        showIcon
        iconDisplay="input"
        placeholder="Select start – end date"
        class="w-64 text-xs"
        inputClass="text-xs py-1.5"
        aria-label="Custom date range picker"
      />
      <span class="text-[11px] text-slate-500 font-medium hidden sm:inline">
        WIB (UTC+7)
      </span>
    </div>
  </div>
</template>
