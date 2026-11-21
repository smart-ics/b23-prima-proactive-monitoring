<script setup lang="ts">
import { computed } from 'vue';
import Skeleton from 'primevue/skeleton';
import type { BreakdownItemDto } from '../types/history.types';

const props = withDefaults(
  defineProps<{
    scopeBreakdown?: BreakdownItemDto[];
    severityBreakdown?: BreakdownItemDto[];
    isLoading?: boolean;
  }>(),
  {
    scopeBreakdown: () => [],
    severityBreakdown: () => [],
    isLoading: false,
  }
);

interface BreakdownRow {
  category: string;
  label: string;
  count: number;
  percentage: number;
  colorClass: string;
}

const SCOPE_META: ReadonlyArray<{ category: string; label: string; colorClass: string }> = [
  { category: 'DataIntegrity', label: 'Data Integrity', colorClass: 'bg-indigo-500' },
  { category: 'ProcessIntegrity', label: 'Process Integrity', colorClass: 'bg-purple-500' },
  { category: 'DataSync', label: 'Data Sync', colorClass: 'bg-cyan-500' },
  { category: 'Infrastructure', label: 'Infrastructure', colorClass: 'bg-emerald-500' },
];

const SEVERITY_META: ReadonlyArray<{ category: string; label: string; colorClass: string }> = [
  { category: 'Critical', label: 'Critical', colorClass: 'bg-rose-600' },
  { category: 'High', label: 'High', colorClass: 'bg-orange-500' },
  { category: 'Medium', label: 'Medium', colorClass: 'bg-amber-500' },
  { category: 'Low', label: 'Low', colorClass: 'bg-blue-500' },
];

function buildBreakdownRows(
  items: BreakdownItemDto[] | undefined,
  metaList: ReadonlyArray<{ category: string; label: string; colorClass: string }>,
  fallbackColor: string
): BreakdownRow[] {
  const byCategory = new Map<string, BreakdownItemDto>();
  for (const item of items ?? []) {
    byCategory.set(item.category, item);
  }

  const rows: BreakdownRow[] = metaList.map((meta) => {
    const match = byCategory.get(meta.category);
    return {
      category: meta.category,
      label: meta.label,
      count: match?.count ?? 0,
      percentage: match?.percentage ?? 0,
      colorClass: meta.colorClass,
    };
  });

  // Preserve any additional categories returned by the API outside the canonical 4
  const knownCategories = new Set(metaList.map((m) => m.category));
  for (const item of items ?? []) {
    if (!knownCategories.has(item.category)) {
      rows.push({
        category: item.category,
        label: item.category,
        count: item.count,
        percentage: item.percentage,
        colorClass: fallbackColor,
      });
    }
  }

  return rows;
}

function clampPercentage(value: number): number {
  if (!Number.isFinite(value)) {
    return 0;
  }
  return Math.max(0, Math.min(100, value));
}

const scopeRows = computed<BreakdownRow[]>(() =>
  buildBreakdownRows(props.scopeBreakdown, SCOPE_META, 'bg-slate-500')
);

const severityRows = computed<BreakdownRow[]>(() =>
  buildBreakdownRows(props.severityBreakdown, SEVERITY_META, 'bg-slate-500')
);

const totalScopeCount = computed(() =>
  scopeRows.value.reduce((sum, row) => sum + row.count, 0)
);

const totalSeverityCount = computed(() =>
  severityRows.value.reduce((sum, row) => sum + row.count, 0)
);
</script>

<template>
  <section
    class="grid grid-cols-1 md:grid-cols-2 gap-4"
    aria-label="Scope and Severity Breakdown"
  >
    <!-- Left Column: Scope Breakdown -->
    <div class="bg-white border border-slate-200 rounded-lg p-4 shadow-2xs">
      <div class="flex items-center justify-between border-b border-slate-100 pb-2.5 mb-2.5">
        <h2 class="text-xs font-semibold uppercase tracking-wider text-slate-500">
          Issues by Monitoring Scope
        </h2>
        <span class="text-[11px] font-mono text-slate-500 tabular-nums">
          {{ totalScopeCount }} total
        </span>
      </div>

      <div v-if="isLoading" class="space-y-3 py-1" aria-busy="true">
        <div v-for="idx in 4" :key="idx" class="flex items-center justify-between gap-3">
          <Skeleton width="7.5rem" height="0.85rem" />
          <Skeleton width="100%" height="0.5rem" />
          <Skeleton width="4.5rem" height="0.85rem" />
        </div>
      </div>

      <div v-else class="divide-y divide-slate-100/80">
        <div
          v-for="item in scopeRows"
          :key="item.category"
          class="flex items-center justify-between text-xs py-2"
        >
          <span class="w-36 truncate font-medium text-slate-700 flex items-center gap-2 shrink-0">
            <span class="w-2 h-2 rounded-full shrink-0" :class="item.colorClass"></span>
            <span class="truncate">{{ item.label }}</span>
          </span>

          <div class="flex-1 mx-3 h-2 bg-slate-100 rounded-full overflow-hidden">
            <div
              class="h-full rounded-full transition-all duration-500"
              :class="item.colorClass"
              :style="{ width: `${clampPercentage(item.percentage)}%` }"
            ></div>
          </div>

          <span class="w-10 text-right font-mono font-semibold text-slate-900 tabular-nums shrink-0">
            {{ item.count }}
          </span>
          <span class="w-14 text-right font-mono text-slate-500 tabular-nums shrink-0">
            {{ clampPercentage(item.percentage).toFixed(1) }}%
          </span>
        </div>
      </div>
    </div>

    <!-- Right Column: Severity Breakdown -->
    <div class="bg-white border border-slate-200 rounded-lg p-4 shadow-2xs">
      <div class="flex items-center justify-between border-b border-slate-100 pb-2.5 mb-2.5">
        <h2 class="text-xs font-semibold uppercase tracking-wider text-slate-500">
          Issues by Severity
        </h2>
        <span class="text-[11px] font-mono text-slate-500 tabular-nums">
          {{ totalSeverityCount }} total
        </span>
      </div>

      <div v-if="isLoading" class="space-y-3 py-1" aria-busy="true">
        <div v-for="idx in 4" :key="idx" class="flex items-center justify-between gap-3">
          <Skeleton width="7.5rem" height="0.85rem" />
          <Skeleton width="100%" height="0.5rem" />
          <Skeleton width="4.5rem" height="0.85rem" />
        </div>
      </div>

      <div v-else class="divide-y divide-slate-100/80">
        <div
          v-for="item in severityRows"
          :key="item.category"
          class="flex items-center justify-between text-xs py-2"
        >
          <span class="w-36 truncate font-medium text-slate-700 flex items-center gap-2 shrink-0">
            <span class="w-2 h-2 rounded-full shrink-0" :class="item.colorClass"></span>
            <span class="truncate">{{ item.label }}</span>
          </span>

          <div class="flex-1 mx-3 h-2 bg-slate-100 rounded-full overflow-hidden">
            <div
              class="h-full rounded-full transition-all duration-500"
              :class="item.colorClass"
              :style="{ width: `${clampPercentage(item.percentage)}%` }"
            ></div>
          </div>

          <span class="w-10 text-right font-mono font-semibold text-slate-900 tabular-nums shrink-0">
            {{ item.count }}
          </span>
          <span class="w-14 text-right font-mono text-slate-500 tabular-nums shrink-0">
            {{ clampPercentage(item.percentage).toFixed(1) }}%
          </span>
        </div>
      </div>
    </div>
  </section>
</template>
