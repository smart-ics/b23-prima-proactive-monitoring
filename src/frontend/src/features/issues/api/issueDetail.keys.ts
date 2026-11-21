import { unref, type MaybeRef } from 'vue';

/**
 * TanStack Query Key Factory for the Issue Detail feature slice.
 * Provides hierarchical, type-safe query keys for caching and selective invalidation.
 */
export const issueDetailKeys = {
  all: ['issues'] as const,
  details: () => [...issueDetailKeys.all, 'detail'] as const,
  detail: (id: MaybeRef<string>) => [...issueDetailKeys.details(), unref(id)] as const,
  officers: () => ['officers'] as const,
};
