/**
 * PrIMA API TypeScript definitions matching Target Architecture v1.0
 * Auto-generated / mapped specification for Stage 1.
 */

export interface paths {
  '/api/auth/login': {
    post: {
      requestBody: {
        content: {
          'application/json': components['schemas']['LoginRequest'];
        };
      };
      responses: {
        200: {
          content: {
            'application/json': components['schemas']['LoginResponse'];
          };
        };
        400: {
          content: {
            'application/problem+json': components['schemas']['ProblemDetails'];
          };
        };
        401: {
          content: {
            'application/problem+json': components['schemas']['ProblemDetails'];
          };
        };
      };
    };
  };
  '/api/ingestion/telemetry': {
    post: {
      parameters: {
        header: {
          'X-Detector-API-Key': string;
        };
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['IngestTelemetryRequest'];
        };
      };
      responses: {
        200: {
          content: {
            'application/json': components['schemas']['IngestTelemetryResponse'];
          };
        };
        401: {
          content: {
            'application/problem+json': components['schemas']['ProblemDetails'];
          };
        };
        404: {
          content: {
            'application/problem+json': components['schemas']['ProblemDetails'];
          };
        };
        409: {
          content: {
            'application/problem+json': components['schemas']['ProblemDetails'];
          };
        };
        422: {
          content: {
            'application/problem+json': components['schemas']['ProblemDetails'];
          };
        };
      };
    };
  };
  '/api/issues/dashboard-feed': {
    get: {
      parameters: {
        query?: {
          status?: 'Open' | 'Acknowledged' | 'Investigating' | 'Reopened';
          severity?: 'Critical' | 'High' | 'Medium' | 'Low';
          scope?: 'DataIntegrity' | 'ProcessIntegrity' | 'DataSync' | 'Infrastructure';
          search?: string;
        };
      };
      responses: {
        200: {
          content: {
            'application/json': components['schemas']['GetDashboardFeedResponse'];
          };
        };
        401: {
          content: {
            'application/problem+json': components['schemas']['ProblemDetails'];
          };
        };
        422: {
          content: {
            'application/problem+json': components['schemas']['ProblemDetails'];
          };
        };
      };
    };
  };
}

export interface components {
  schemas: {
    OfficerRole: 'EdpOfficer' | 'SystemAnalyst' | 'Lead';
    MonitoringScope: 'DataIntegrity' | 'ProcessIntegrity' | 'DataSync' | 'Infrastructure';
    IssueSeverity: 'Low' | 'Medium' | 'High' | 'Critical';
    IssueStatus: 'Open' | 'Acknowledged' | 'Investigating' | 'Resolved' | 'Dismissed' | 'Reopened';
    TriggerSource: 'Scheduled' | 'Manual' | 'EventHook';
    TelemetryOutcome: 'Passed' | 'FindingDiscovered' | 'ExecutionFailed';

    OfficerDto: {
      id: string;
      username: string;
      fullName: string;
      role: components['schemas']['OfficerRole'];
    };

    LoginRequest: {
      username: string;
      password: string;
    };

    LoginResponse: {
      token: string;
      expiresAtUtc: string;
      officer: components['schemas']['OfficerDto'];
    };

    TelemetryEvidenceDto: {
      module: string;
      sourceTable?: string | null;
      recordKeys?: string | null;
      debitTotal?: number | null;
      creditTotal?: number | null;
      varianceDelta?: number | null;
      trace?: string | null;
    };

    FindingItemDto: {
      targetEntityReference: string;
      title: string;
      baseSeverity: components['schemas']['IssueSeverity'];
      evidence: components['schemas']['TelemetryEvidenceDto'];
    };

    IngestTelemetryRequest: {
      detectorId: string;
      executionTimestampUtc: string;
      triggerSource: components['schemas']['TriggerSource'];
      outcome: components['schemas']['TelemetryOutcome'];
      durationMs: number;
      failureReason?: string | null;
      findings?: components['schemas']['FindingItemDto'][] | null;
    };

    IngestResultItemDto: {
      targetEntityReference: string;
      correlationKey: string;
      action: 'Created' | 'Appended' | 'Reopened';
      issueId: string;
    };

    IngestTelemetryResponse: {
      inspectionRunId: string;
      detectorId: string;
      outcome: string;
      processedFindingsCount: number;
      results: components['schemas']['IngestResultItemDto'][];
    };

    DashboardSummaryDto: {
      criticalCount: number;
      highCount: number;
      openCount: number;
      reopenedCount: number;
      resolvedTodayCount: number;
    };

    IssueFeedItemDto: {
      id: string;
      correlationKey: string;
      title: string;
      detectorId: string;
      detectorName: string;
      scope: components['schemas']['MonitoringScope'];
      currentSeverity: components['schemas']['IssueSeverity'];
      severityRank: number;
      status: components['schemas']['IssueStatus'];
      escalationLevel: number;
      occurrenceCount: number;
      currentAssignee?: string | null;
      createdAtUtc: string;
      lastOccurrenceAtUtc: string;
    };

    GetDashboardFeedResponse: {
      summary: components['schemas']['DashboardSummaryDto'];
      items: components['schemas']['IssueFeedItemDto'][];
    };

    ProblemDetails: {
      type?: string | null;
      title?: string | null;
      status?: number | null;
      detail?: string | null;
      instance?: string | null;
      errors?: Record<string, string[]> | null;
      [key: string]: unknown;
    };
  };
}

export type OfficerRole = components['schemas']['OfficerRole'];
export type MonitoringScope = components['schemas']['MonitoringScope'];
export type IssueSeverity = components['schemas']['IssueSeverity'];
export type IssueStatus = components['schemas']['IssueStatus'];
export type TriggerSource = components['schemas']['TriggerSource'];
export type TelemetryOutcome = components['schemas']['TelemetryOutcome'];

export type OfficerDto = components['schemas']['OfficerDto'];
export type LoginRequest = components['schemas']['LoginRequest'];
export type LoginResponse = components['schemas']['LoginResponse'];
export type TelemetryEvidenceDto = components['schemas']['TelemetryEvidenceDto'];
export type FindingItemDto = components['schemas']['FindingItemDto'];
export type IngestTelemetryRequest = components['schemas']['IngestTelemetryRequest'];
export type IngestResultItemDto = components['schemas']['IngestResultItemDto'];
export type IngestTelemetryResponse = components['schemas']['IngestTelemetryResponse'];
export type DashboardSummaryDto = components['schemas']['DashboardSummaryDto'];
export type IssueFeedItemDto = components['schemas']['IssueFeedItemDto'];
export type GetDashboardFeedResponse = components['schemas']['GetDashboardFeedResponse'];
export type ProblemDetails = components['schemas']['ProblemDetails'];
