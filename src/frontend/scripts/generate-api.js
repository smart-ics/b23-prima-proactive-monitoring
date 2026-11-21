import { execSync } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const swaggerPath = resolve(__dirname, '../../backend/PrIMA.Api/swagger.json');
const outputPath = resolve(__dirname, '../src/shared/types/api.generated.ts');

console.log('Generating TypeScript API definitions from OpenAPI swagger...');
execSync(`npx openapi-typescript "${swaggerPath}" -o "${outputPath}"`, { stdio: 'inherit' });

const typeAliases = `
// Convenience Type Aliases matching Target Architecture v1.0 & Stage 2 Domain Models
export type OfficerDto = components["schemas"]["OfficerDto"];
export type LoginRequest = components["schemas"]["LoginRequest"];
export type LoginResponse = components["schemas"]["LoginResponse"];
export type ProblemDetails = components["schemas"]["ProblemDetails"] & {
    errors?: Record<string, string[]>;
};
export type HttpValidationProblemDetails = components["schemas"]["HttpValidationProblemDetails"];

export type IssueFeedItemDto = components["schemas"]["IssueFeedItemDto"];
export type DashboardFeedSummaryDto = components["schemas"]["DashboardFeedSummaryDto"];
export type DashboardSummaryDto = components["schemas"]["DashboardFeedSummaryDto"];
export type GetDashboardFeedResponse = components["schemas"]["GetDashboardFeedResponse"];

export type IngestTelemetryRequest = components["schemas"]["IngestTelemetryRequest"];
export type IngestTelemetryResponse = components["schemas"]["IngestTelemetryResponse"];
export type FindingItem = components["schemas"]["FindingItem"];
export type FindingEvidenceItem = components["schemas"]["FindingEvidenceItem"];
export type FindingResultItem = components["schemas"]["FindingResultItem"];

// Stage 2 Issue Lifecycle & Governance Types
export type IssueDetailDto = components["schemas"]["IssueDetailDto"];
export type OccurrenceItemDto = components["schemas"]["OccurrenceItemDto"];
export type InvestigationNoteDto = components["schemas"]["InvestigationNoteDto"];
export type RemediationRecordDto = components["schemas"]["RemediationRecordDto"];
export type VerificationRecordDto = components["schemas"]["VerificationRecordDto"];
export type DismissalRecordDto = components["schemas"]["DismissalRecordDto"];
export type IssueActivityLogDto = components["schemas"]["IssueActivityLogDto"];

export type AssignIssueRequest = components["schemas"]["AssignIssueRequest"];
export type AddNoteRequest = components["schemas"]["AddNoteRequest"];
export type NoteDto = components["schemas"]["NoteDto"];
export type RecordRemediationRequest = components["schemas"]["RecordRemediationRequest"];
export type RecordManualVerificationRequest = components["schemas"]["RecordManualVerificationRequest"];
export type DismissIssueRequest = components["schemas"]["DismissIssueRequest"];

// Domain Enums and Values
export type OfficerRole = 'EdpOfficer' | 'SystemAnalyst' | 'Lead';
export type MonitoringScope = 'DataIntegrity' | 'ProcessIntegrity' | 'DataSync' | 'Infrastructure';
export type IssueSeverity = 'Low' | 'Medium' | 'High' | 'Critical';
export type IssueStatus = 'Open' | 'Acknowledged' | 'Investigating' | 'Resolved' | 'Dismissed' | 'Reopened';
export type TriggerSource = 'Scheduled' | 'Manual' | 'EventHook' | 'AutomatedReRun';
export type TelemetryOutcome = 'Passed' | 'FindingDiscovered' | 'ExecutionFailed';
export type RemediationActionCategory = 'DataCorrection' | 'ServiceRestart' | 'ConfigurationAdjustment' | 'CodeRemediationRequested';
export type DismissalReason = 'FalsePositive' | 'AcceptedOperationalException' | 'DuplicateManualReport';
export type VerificationMethod = 'ManualVerification' | 'AutomatedSupervisor' | 'TelemetryRecurrence' | 'AutomatedReRun';

// Stage 3 Detector Management & Telemetry Types
export type DetectorListItemDto = components["schemas"]["DetectorListItemDto"];
export type DetectorSummaryDto = components["schemas"]["DetectorSummaryDto"];
export type DetectorDetailDto = components["schemas"]["DetectorDetailDto"];
export type DetectorReliabilityStatsDto = components["schemas"]["DetectorReliabilityStatsDto"];
export type RecentRunSummaryDto = components["schemas"]["RecentRunSummaryDto"];
export type LinkedActiveIssueDto = components["schemas"]["LinkedActiveIssueDto"];
export type InspectionRunLogResponse = components["schemas"]["InspectionRunLogResponse"];
export type InspectionRunDetailDto = components["schemas"]["InspectionRunDetailDto"];
export type RegisterDetectorRequest = components["schemas"]["RegisterDetectorRequest"];
export type UpdateDetectorRequest = components["schemas"]["UpdateDetectorRequest"];
export type ToggleDetectorStatusRequest = components["schemas"]["ToggleDetectorStatusRequest"];
export type InspectionRunResultDto = components["schemas"]["InspectionRunResultDto"];
export type VerificationResultDto = components["schemas"]["VerificationResultDto"];

export type DetectorHealth = 'Healthy' | 'Degraded' | 'Failing' | 'Silent';
export type AdminStatus = 'Active' | 'Paused' | 'Retired';

// Stage 4 Historical Reliability Analytics & Reporting Types
export type ReliabilityKpiResponse = components["schemas"]["ReliabilityKpiResponse"];
export type BreakdownItemDto = components["schemas"]["BreakdownItemDto"];
export type HistoricalIssuesResponse = components["schemas"]["HistoricalIssuesResponse"];
export type HistoricalIssueItemDto = components["schemas"]["HistoricalIssueItemDto"];
`;

appendFileSync(outputPath, typeAliases, 'utf8');
console.log('Successfully appended convenience type aliases to api.generated.ts');
