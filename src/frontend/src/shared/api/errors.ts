import type { ProblemDetails } from '@/shared/types/api.generated';

/**
 * Custom error class translating RFC 7807 ProblemDetails into typed application error.
 */
export class ApiError extends Error {
  public readonly status?: number;
  public readonly title?: string;
  public readonly detail?: string;
  public readonly instance?: string;
  public readonly errors?: Record<string, string[]>;
  public readonly problemDetails?: ProblemDetails;

  constructor(message: string, problemDetails?: ProblemDetails, status?: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status ?? problemDetails?.status ?? undefined;
    this.title = problemDetails?.title ?? undefined;
    this.detail = problemDetails?.detail ?? undefined;
    this.instance = problemDetails?.instance ?? undefined;
    this.errors = problemDetails?.errors ?? undefined;
    this.problemDetails = problemDetails;

    // Restore prototype chain for instanceof checks
    Object.setPrototypeOf(this, ApiError.prototype);
  }
}
