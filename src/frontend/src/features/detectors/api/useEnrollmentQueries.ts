import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query';
import http from '@/shared/api/http';
import type {
  GenerateEnrollmentTokenRequest,
  GenerateEnrollmentTokenResponse,
  PendingEnrollmentTokenDto,
} from '@/shared/types/api.generated';
import { enrollmentTokenKeys } from './detector.keys';

export { enrollmentTokenKeys };

/**
 * Query composable for S-03 Pending Enrollment Tokens Strip.
 * Fetches active unconsumed 24h enrollment tokens awaiting detector handshake.
 * - 15-second background polling
 * - 7.5-second stale time
 */
export function usePendingEnrollmentTokensQuery() {
  return useQuery<PendingEnrollmentTokenDto[], Error>({
    queryKey: enrollmentTokenKeys.pending(),
    queryFn: async () => {
      const response = await http.get<PendingEnrollmentTokenDto[]>('/detectors/enrollment-tokens');
      return response.data;
    },
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
    staleTime: 7_500,
  });
}

/**
 * Mutation composable for generating a short-lived (24h) detector enrollment token.
 * Authenticated EDP officer request returns plaintext secret once.
 * Selectively invalidates pending enrollment token cache.
 */
export function useGenerateEnrollmentTokenMutation() {
  const queryClient = useQueryClient();

  return useMutation<GenerateEnrollmentTokenResponse, Error, GenerateEnrollmentTokenRequest | void>({
    mutationFn: async (payload) => {
      const response = await http.post<GenerateEnrollmentTokenResponse>(
        '/detectors/enrollment-tokens',
        payload ?? {}
      );
      return response.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: enrollmentTokenKeys.all });
    },
  });
}

/**
 * Mutation composable for revoking an unconsumed detector enrollment token.
 * Selectively invalidates pending enrollment token cache.
 */
export function useRevokeEnrollmentTokenMutation() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, string>({
    mutationFn: async (id: string) => {
      await http.delete(`/detectors/enrollment-tokens/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: enrollmentTokenKeys.all });
    },
  });
}
