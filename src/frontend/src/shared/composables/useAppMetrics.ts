import { ref } from 'vue';

/**
 * Shared reactive counters across App Shell layout and dashboard views.
 */
export const activeIssuesCount = ref<number>(0);
