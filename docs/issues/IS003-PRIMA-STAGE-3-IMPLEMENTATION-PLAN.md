---
Title: PrIMA Stage 3 — Detector Portfolio, Automated Supervision & Webhook Verification Implementation Plan
Code: PRIMA-STAGE-3
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-11-21
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Stage 3 ("Detector Portfolio, Automated Supervision & Webhook Verification") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`), establishing autonomous background supervision, detector portfolio management, and external webhook verification, delivering the **Autonomous Proactive Supervision & Self-Monitoring** milestone.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:
- ISSUE: `docs/issues/IS003-PRIMA-STAGE-3-ISSUE.md`
- FEATURE: `docs/PRIMA-DEVELOPMENT-ROADMAP.md` (Stage 3), `docs/PRIMA-USE-CASE-DISCOVERY.md` (`D-2`, `E-1`–`E-7`, `F-1`–`F-2`, `G-1`–`G-2`), `docs/PRIMA-SCREEN-INVENTORY.md` (`S-03`–`S-06`, `S-01`/`S-02` upgrades), `docs/PRIMA-UI-UX-GUIDELINE.md`
- ARCHITECTURE: `docs/issues/IS003-PRIMA-STAGE-3-ARCHITECTURE.md` (and baseline `docs/PRIMA-ARCHITECTURE.md`)
- FEASIBILITY-ASSESSMENT: `docs/issues/IS003-PRIMA-STAGE-3-FEASIBILITY-ASSESSMENT.md` (`Status: READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED  
The target architecture artifact `docs/issues/IS003-PRIMA-STAGE-3-ARCHITECTURE.md` authoritatively defines the technical realization, domain aggregate invariants (`BR-PRIMA-003`, `BR-PRIMA-005`, `BR-PRIMA-010`), background worker architecture (`MonitoringSupervisorWorker`), webhook client resiliency (`DetectorWebhookClient`), extended telemetry ingestion (`IngestTelemetryHandler`), Minimal API query and command endpoints (`Features/Detectors/*`, `Features/Issues/*`), and frontend `S-03`–`S-06` views and cross-feature integrations (`S-01`, `S-02`, `AppLayout.vue`).

---

# 2. Planning Scope

The scope of this implementation plan encompasses the complete delivery of Stage 3 across backend, background supervision, webhook client, frontend, tooling, and test automation:

1. **Backend Domain Aggregate Invariant Enforcement:**
   - `Detector.cs`: Implement configuration updates (`UpdateConfiguration`), administrative transitions (`Pause`, `Resume`, `Retire`), health tracking (`MarkSilent`, `MarkFailing`, `RecordSuccessfulRun`), and silence threshold evaluation (`IsSilenceThresholdExceeded`).
   - `Issue.cs`: Implement dynamic severity escalation (`EscalateSeverity`, `CheckSlaEscalation`, `CheckRecurrenceEscalation`) enforcing multi-step escalation capped at `Critical` (`DEC-006`, `DEC-007`) and atomic audit logging.
   - Domain errors in `DetectorErrors.cs` and `IssueErrors.cs`.

2. **Data Access & Concurrency Persistence Layer:**
   - Dapper persistence in `IDetectorRepository.cs` and `DetectorRepository.cs` supporting single-row updates, configuration changes, administrative lifecycle transitions, health updates, and concurrency locks (`UPDLOCK, ROWLOCK`).

3. **Autonomous Supervision & Webhook Ingestion Engine:**
   - `IngestTelemetryHandler.cs` extended for `outcome == "ExecutionFailed"` handling, `FAIL:{DetectorId}` issue synthesis (`BR-PRIMA-005`), and detector recovery with preserved EDP governance (`DEC-004`).
   - `DetectorWebhookClient.cs` typed `HttpClient` with strict 15-second timeout, handling network timeouts/failures, recording failed runs, marking detectors `Failing`, synthesizing failure issues, and returning RFC 7807 problem details (`DEC-001`, `DEC-002`).
   - `MonitoringSupervisorWorker.cs` hosted `BackgroundService` executing a 60-second periodic evaluation loop:
     - Phase 1 (Silence Supervision): Evaluates active detectors where `COALESCE(LastHealthyHeartbeatUtc, CreatedAtUtc) < (UtcNow - ExpectedCadenceSeconds * 2.0)` (`DEC-003`), updates health to `Silent`, synthesizes `SILENT:{DetectorId}` infrastructure issues, and appends cadence-multiplied occurrences (`DEC-005`).
     - Phase 2 (Dynamic Severity Escalation): Evaluates unacknowledged issues past the SLA timeout and recurring issues (`>= 5` occurrences), dynamically escalating severities and writing audit logs.

4. **Minimal API Endpoints & Contract Generation:**
   - `GET /api/detectors`: Portfolio list with 30-day clean pass rate (`DEC-008`), total runs, and active issues (`S-03`).
   - `GET /api/detectors/summary`: Global health summary counts (`DEC-010`).
   - `GET /api/detectors/{id}`: Composite detail query with 30-day reliability statistics, recent 10 runs, linked active issues (`S-04`).
   - `POST /api/detectors`: Register new detector aggregate (`S-05`).
   - `PUT /api/detectors/{id}`: Update detector configuration with immutable name (`S-05`).
   - `POST /api/detectors/{id}/toggle-status`: Administrative transitions (`Pause`, `Resume`, `Retire`) preserving linked issue autonomy (`DEC-009`).
   - `POST /api/detectors/{id}/trigger-run`: Synchronous on-demand run via webhook (`S-04`, `E-7`).
   - `GET /api/detectors/{id}/runs`: Historical inspection run log with date filtering and pagination (`S-06`).
   - `POST /api/issues/{id}/verification/trigger-run`: Automated verification re-run via webhook (`S-02`, `D-2`).
   - `GET /api/issues/dashboard-feed`: Extended with `EscalatedCount` and `EscalatedOnly` query filter (`DEC-010`).
   - OpenAPI documentation and contract synchronization via `npm run generate:api` producing `src/frontend/src/shared/types/api.generated.ts`.

5. **Frontend Feature Slice (`src/frontend/src/features/detectors/`):**
   - TanStack Query composables with 15-second polling, form draft preservation, stable run expansion IDs.
   - `S-03` Detector Portfolio List (`/detectors`): 4-state Health Summary Bar, multi-attribute filter strips, instant search, virtualized table/cards, `+ Register Detector` CTA.
   - `S-04` Detector Detail (`/detectors/:id`): Sticky header, status-sensitive Action Bar, 4-column 30-day reliability card, recent 10 runs with inline detail expansion, linked active issues table.
   - `S-05` Detector Form (`/detectors/new`, `/detectors/:id/edit`): VeeValidate + Zod form, split cadence picker with real-time 2× silence threshold calculation, optional validated webhook.
   - `S-06` Inspection Run Log (`/detectors/:id/runs`): 7-day date range picker, outcome and trigger filter pills, expandable rows with monospace trace/failure blocks and copy buttons.

6. **Cross-Feature Integrations & Linkages:**
   - `AppLayout.vue`: Sidebar `Detectors` route activated with amber/rose alert indicator dot when `(FailingCount + SilentCount) > 0`.
   - `S-01 Issue Dashboard`: Conditional top Alert Strip (`⚠ N issues escalated · M issues reopened`), `↑ Escalated` badge chips, and dedicated `escalatedOnly` filter.
   - `S-02 Issue Detail Workbench`: Active router link on header detector badge (`/detectors/:id`), automated verification re-run trigger with graceful fallback to `M-03`.

7. **Development Seeding, Automated Tests & Verification:**
   - `DevelopmentDemoDataSeeder.cs` expanded to seed detectors in all lifecycle/health states, historical runs, and escalated issues.
   - Automated unit test suites covering `Detector` domain invariants and `Issue` severity escalation.
   - Automated integration test suites covering detector endpoints, webhook execution, supervisor silence/escalation loops, and execution failed ingestion.
   - End-to-end automated smoke test verifying the complete autonomous supervision and self-monitoring loop.

**Explicitly Out of Scope (Deferred to Later Stages):**
- Stage 4: Retrospective reporting, period-close KPI reporting (`S-07`, `H-1`, `H-2`), multi-period trend visualizations, MTTR analytics, and full v1.0 quality gate.
- Stage 5: Pilot SIMRS detector suite across hospital modules (`BR-PRIMA-007`) and hospital on-premise production deployment.

---

# 3. Dependencies

## External Prerequisites
- .NET 8 SDK installed on host.
- Node.js (v20+ or v22 LTS) and npm installed on host.
- Microsoft SQL Server 2022 (local instance accessible via connection string configured in `appsettings.Development.json`).
- Stage 1 and Stage 2 platforms verified and operational in codebase.

## Slice Dependency Rules
- `Depends On` declares implementation prerequisites between slices.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.
- Dependency satisfaction does not require review status `GO`.
- Dependencies represent real implementation prerequisites (e.g. required code, types, tables).
- Slices with no mutual dependencies can and should be implemented concurrently.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Core Domain Aggregate Invariants & Repository Persistence | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P2 - Ingestion Resilience, Webhook Integration & Autonomous Supervision | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P3 - Backend Minimal API Endpoints & Contract Generation | NOT-STARTED | NOT-REVIEWED | 0/5 |
| P4 - Frontend UI Features & Cross-Screen Integrations | NOT-STARTED | NOT-REVIEWED | 0/6 |
| P5 - Demo Seeding, Automated Testing & Verification | NOT-STARTED | NOT-REVIEWED | 0/3 |

---

# 5. Phases

## P1 - Core Domain Aggregate Invariants & Repository Persistence

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P1-S01

Title: Detector Aggregate Expansion & Administrative Lifecycle

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Expand the `Detector` aggregate root (`src/backend/PrIMA.Api/Domain/Detectors/Detector.cs`) to encapsulate configuration update methods, silence threshold calculations, administrative lifecycle transitions (`Pause`, `Resume`, `Retire`), and health tracking methods returning `Result`.

Depends On: None  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `Detector.cs` domain methods implemented returning `Result`:
   - `UpdateConfiguration(string description, MonitoringScope scope, string targetModule, int expectedCadenceSeconds, string? webhookUrl)`:
     - Fails with `DetectorErrors.CannotModifyRetiredDetector` if `IsRetired`.
     - Validates non-empty description, non-empty target module, cadence `> 0`.
     - Updates mutable fields; trims strings; sets `WebhookUrl` (null if whitespace).
   - `Pause()`: Transitions `AdminStatus = Paused`. Fails if `IsRetired` or `IsPaused`.
   - `Resume()`: Transitions `AdminStatus = Active`. Fails if `IsRetired` or `IsActive`.
   - `Retire()`: Transitions `AdminStatus = Retired`. Fails if `IsRetired`.
   - `IsSilenceThresholdExceeded(DateTime currentUtc)`: Returns `bool`. Returns `false` if `!IsActive`. Evaluates `baseline = LastHealthyHeartbeatUtc ?? CreatedAtUtc`. Returns `true` if `(currentUtc - baseline).TotalSeconds > (ExpectedCadenceSeconds * 2.0)`.
   - `MarkSilent(DateTime timestampUtc)`: Sets `HealthStatus = DetectorHealth.Silent`.
   - `MarkFailing(DateTime timestampUtc)`: Sets `HealthStatus = DetectorHealth.Failing` and `LastRunAtUtc = timestampUtc`.
   - `RecordSuccessfulRun(DateTime timestampUtc)`: Sets `HealthStatus = DetectorHealth.Healthy`, `LastRunAtUtc = timestampUtc`, and `LastHealthyHeartbeatUtc = timestampUtc`.
2. `DetectorErrors.cs` created or expanded under `Domain/Detectors/`:
   - `CannotModifyRetiredDetector` (409 Conflict)
   - `EmptyDescription` (422 Unprocessable Entity)
   - `EmptyTargetModule` (422 Unprocessable Entity)
   - `InvalidCadence` (422 Unprocessable Entity)
   - `AlreadyPaused` (409 Conflict)
   - `AlreadyActive` (409 Conflict)
   - `AlreadyRetired` (409 Conflict)
   - `DuplicateName` (409 Conflict)
3. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Enforces `DEC-003`, `DEC-009`, `BR-PRIMA-005`, and `BR-PRIMA-010`. Implemented configuration updates, administrative transitions (`Pause`, `Resume`, `Retire`), silence threshold checking (`IsSilenceThresholdExceeded`), and health recording methods (`MarkSilent`, `MarkFailing`, `RecordSuccessfulRun`) returning `Result` in `Detector.cs`. Expanded `DetectorErrors.cs` with RFC 7807 problem details mapped domain errors. Added comprehensive unit tests in `DetectorAggregateTests.cs` covering all positive and negative transition paths, silence threshold edge cases, and configuration validation. Project compiles with 0 warnings and 0 errors.
Changed files:
- `src/backend/PrIMA.Api/Domain/Detectors/Detector.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/DetectorErrors.cs`
- `src/backend/PrIMA.Api.Tests/Domain/DetectorAggregateTests.cs`

---

### P1-S02

Title: Issue Aggregate Dynamic Severity Escalation

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Expand the `Issue` aggregate root (`src/backend/PrIMA.Api/Domain/Issues/Issue.cs`) to encapsulate dynamic multi-step severity escalation capped at `Critical` (`DEC-006`), SLA timeout checks, and recurrence threshold checks (`DEC-007`).

Depends On: None  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `Issue.cs` domain methods implemented returning `Result` or `bool`:
   - `EscalateSeverity(string reason, DateTime utcNow)`:
     - Fails with `IssueErrors.ActionRequiresActiveStatus` if issue is `Resolved` or `Dismissed`.
     - If `CurrentSeverity == IssueSeverity.Critical`, returns `Result.Success()` (already capped).
     - Stepping: `Low` -> `Medium` -> `High` -> `Critical`.
     - Updates `SeverityRank = (byte)CurrentSeverity`.
     - Increments `EscalationLevel++`.
     - Returns `Result.Success()`.
   - `CheckSlaEscalation(TimeSpan slaTimeout, DateTime utcNow, out string reason)`:
     - Returns `false` if status is not `Open` or `Reopened` (`DEC-007`).
     - Returns `false` if `AcknowledgedAtUtc != null`.
     - Returns `false` if `CurrentSeverity >= IssueSeverity.Critical`.
     - Evaluates `unackDuration = utcNow - CreatedAtUtc`. If `unackDuration > (slaTimeout * (EscalationLevel + 1))`:
       - Sets `reason = $"Issue unacknowledged for {(int)unackDuration.TotalMinutes} minutes (SLA window: {(int)slaTimeout.TotalMinutes}m)."` and returns `true`.
   - `CheckRecurrenceEscalation(DateTime utcNow, out string reason)`:
     - Returns `false` if status is `Resolved` or `Dismissed`.
     - Returns `false` if `CurrentSeverity >= IssueSeverity.Critical`.
     - If `OccurrenceCount >= 5`:
       - Sets `reason = $"Issue recurred {OccurrenceCount} times across monitoring cycles."` and returns `true`.
2. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Enforces `BR-PRIMA-003`, `DEC-006`, and `DEC-007`. Implemented `EscalateSeverity`, `CheckSlaEscalation`, and `CheckRecurrenceEscalation` domain methods in `Issue.cs`. Added unit tests covering severity stepping, critical severity capping, closed status rejection, SLA timeout window scaling (`slaTimeout * (EscalationLevel + 1)`), acknowledgement guards, and recurrence threshold (`OccurrenceCount >= 5`) checks in `IssueAggregateTests.cs`. All unit tests pass cleanly and project compiles with 0 errors and 0 warnings.
Changed files:
- `src/backend/PrIMA.Api/Domain/Issues/Issue.cs`
- `src/backend/PrIMA.Api.Tests/Domain/IssueAggregateTests.cs`

---

### P1-S03

Title: Detector Repository Dapper Data Access & Locking Queries

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Expand `IDetectorRepository.cs` and `DetectorRepository.cs` under `src/backend/PrIMA.Api/Infrastructure/Persistence/` with Dapper data access methods supporting detector lookups, configuration updates, lifecycle transitions, health updates, concurrency locking (`UPDLOCK, ROWLOCK`), and `dbo.InspectionRuns` recording with `FailureReason`.

Depends On: P1-S01  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `IDetectorRepository.cs` and `DetectorRepository.cs` extended with methods supporting ambient or explicit `IDbTransaction`:
   - `GetByIdAsync(Guid id, IDbConnection conn, IDbTransaction? tx = null)`: Loads single `Detector` aggregate.
   - `GetByNameAsync(string name, IDbConnection conn, IDbTransaction? tx = null)`: Checks name uniqueness.
   - `InsertAsync(Detector detector, IDbConnection conn, IDbTransaction tx)`: Inserts new row into `dbo.Detectors`.
   - `UpdateConfigurationAsync(Detector detector, IDbConnection conn, IDbTransaction tx)`: Updates `Description`, `Scope`, `TargetModule`, `ExpectedCadenceSeconds`, `WebhookUrl`.
   - `UpdateAdminStatusAsync(Guid id, AdminStatus status, IDbConnection conn, IDbTransaction tx)`: Updates `AdminStatus`.
   - `UpdateHealthAndHeartbeatAsync(Guid id, DetectorHealth health, DateTime? lastRunUtc, DateTime? lastHealthyUtc, IDbConnection conn, IDbTransaction tx)`: Updates health and liveness timestamps.
   - `GetActiveSilentCandidatesAsync(DateTime silenceCutoffUtc, IDbConnection conn, IDbTransaction? tx = null)`: Queries `dbo.Detectors` WHERE `AdminStatus = 'Active' AND COALESCE(LastHealthyHeartbeatUtc, CreatedAtUtc) < @SilenceCutoffUtc`.
   - `LockDetectorForUpdateAsync(Guid id, IDbConnection conn, IDbTransaction tx)`: Executes `SELECT ... FROM dbo.Detectors WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id`.
   - `RecordInspectionRunAsync(InspectionRun run, IDbConnection conn, IDbTransaction tx)`: Inserts row into `dbo.InspectionRuns` with `Outcome`, `DurationMs`, `FailureReason`, and `TriggerSource`.
2. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implements database persistence for `GAP-001`, `GAP-002`, `GAP-003`, `GAP-004`, and `RISK-003`. Extended `IDetectorRepository.cs` and `DetectorRepository.cs` with transaction-aware Dapper query methods for aggregate retrieval, uniqueness checking, configuration modification, administrative status changes, health and heartbeat updates with `COALESCE` protection, silent candidate querying, pessimistic concurrency locking via `WITH (UPDLOCK, ROWLOCK)`, and telemetry run logging supporting `FailureReason`. Added 8 comprehensive integration tests in `DetectorRepositoryTests.cs` verifying persistence, configuration updates, health transitions, silence candidate filtering, transaction locking, and inspection run recording against the test database. Project compiles cleanly with 0 warnings and 0 errors, and all 217 tests pass.
Changed files:
- `src/backend/PrIMA.Api/Domain/Detectors/IDetectorRepository.cs`
- `src/backend/PrIMA.Api/Infrastructure/Persistence/DetectorRepository.cs`
- `src/backend/PrIMA.Api.Tests/Integration/DetectorRepositoryTests.cs`

---

## P2 - Ingestion Resilience, Webhook Integration & Autonomous Supervision

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P2-S04

Title: Telemetry Ingestion Execution Failure & Health Recovery

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Extend `IngestTelemetryHandler.cs` under `src/backend/PrIMA.Api/Features/Ingestion/` to process `outcome == "ExecutionFailed"` telemetry, synthesize/append `FAIL:{DetectorId}` infrastructure issues per `BR-PRIMA-005`, and reset detector health to `Healthy` on clean recovery with audit log generation (`DEC-004`).

Depends On: P1-S01, P1-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `IngestTelemetryHandler.cs` updated:
   - Evaluates `request.Outcome`:
     - If `request.Outcome == "ExecutionFailed"`:
       - Persists `InspectionRun` with `FailureReason = request.FailureReason`.
       - Updates detector `HealthStatus = DetectorHealth.Failing` via `_detectorRepository.UpdateHealthAndHeartbeatAsync`.
       - Infrastructure anomaly correlation (`BR-PRIMA-005`):
         - Queries active `dbo.Issues` where `CorrelationKey = $"FAIL:{detector.Id}"`.
         - If not found: creates new issue (`Scope = MonitoringScope.Infrastructure`, `Title = $"Execution Failure: {detector.Name}"`, `BaseSeverity = IssueSeverity.High`, `TraceString = request.FailureReason ?? "Detector reported execution failure."`), inserts occurrence #1, and records `dbo.IssueActivityLogs` (`ActivityType = "Created"`).
         - If found: appends occurrence, updates `OccurrenceCount`, and records activity log (`ActivityType = "RecurrenceObserved"`).
       - Returns `200 OK` with `TelemetryIngestionResponseDto { TotalFindingsProcessed = 0, NewIssuesCreated = 1, RecurrencesRecorded = 0 }`.
     - If `request.Outcome == "Passed"` or `"FindingDiscovered"`:
       - If current detector `HealthStatus` is `Silent` or `Failing` (`DEC-004`):
         - Resets `HealthStatus = DetectorHealth.Healthy`, updates `LastHealthyHeartbeatUtc = request.ExecutionTimestampUtc`.
         - Queries active issues matching `SILENT:{detector.Id}` or `FAIL:{detector.Id}`.
         - For each matching active issue, appends to `dbo.IssueActivityLogs`:
           - `ActivityType = "DetectorRecovered"`
           - `ActorName = "System"`
           - `Summary = $"Detector resumed healthy reporting via run '{inspectionRun.Id}'. Issue remains open for EDP verification."`
2. Existing Stage 1 deduplication and Stage 2 recurrence invalidation behavior remains fully preserved.
3. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implements `GAP-002`, `DEC-004`, `BR-PRIMA-004`, and `BR-PRIMA-005`. Updated `IngestTelemetryHandler.cs` to handle `ExecutionFailed` outcomes by persisting inspection runs with `FailureReason`, updating detector health status to `Failing`, and synthesizing or appending `FAIL:{DetectorId}` infrastructure anomaly issues with occurrence tracking and activity logging. For clean recovery (`Passed` or `FindingDiscovered` from `Silent` or `Failing` detectors), resets detector health to `Healthy`, updates `LastHealthyHeartbeatUtc`, and appends `DetectorRecovered` activity logs to matching open synthetic issues while keeping them open for EDP verification per `DEC-004`. All existing Stage 1 deduplication and Stage 2 recurrence invalidation behaviors are fully preserved. Added comprehensive integration tests in `IngestionEndpointsTests.cs`. Project compiles cleanly with 0 warnings and 0 errors, and all 274 test cases pass.
Changed files:
- `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryHandler.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryResponse.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryValidator.cs`
- `src/backend/PrIMA.Api.Tests/Integration/IngestionEndpointsTests.cs`

---

### P2-S05

Title: External Webhook Client (DetectorWebhookClient)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `IDetectorWebhookClient` and `DetectorWebhookClient` in `src/backend/PrIMA.Api/Infrastructure/Webhooks/` utilizing a typed `HttpClient` with a strict 15-second timeout, error resilience, and automated failure recording (`DEC-001`, `DEC-002`).

Depends On: P1-S01, P1-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Contracts created under `Infrastructure/Webhooks/`:
   - `IDetectorWebhookClient.cs`:
     `Task<Result<WebhookExecutionResult>> ExecuteRunAsync(Guid detectorId, string webhookUrl, string triggerSource, Guid? issueId = null, CancellationToken cancellationToken = default);`
   - `WebhookExecutionResult.cs`: `string Outcome, int DurationMs, string? FailureReason, IReadOnlyList<WebhookFindingDto> Findings`.
   - `WebhookFindingDto.cs` & `WebhookEvidenceDto.cs`.
2. `DetectorWebhookClient.cs` implemented:
   - Configures typed `HttpClient` with `Timeout = TimeSpan.FromSeconds(15)` and `User-Agent: PrIMA-Supervisor/1.0`.
   - Sends `POST {webhookUrl}` with JSON payload: `{ detectorId, triggerSource, issueId, dispatchedAtUtc }`.
   - Deserializes response conforming to standard telemetry JSON.
   - Comprehensive error handling (`DEC-002`, `RISK-001`):
     - Catches `TaskCanceledException` (timeout), `HttpRequestException`, or HTTP 5xx:
       - Measures elapsed duration.
       - Records `InspectionRun` with `Outcome = "ExecutionFailed"`, `FailureReason = ex.Message`.
       - Updates detector `HealthStatus = DetectorHealth.Failing`.
       - Synthesizes or appends `FAIL:{detectorId}` infrastructure issue per `BR-PRIMA-005`.
       - Returns `Result.Failure` containing appropriate error code (`WebhookTimeout` -> 504, `WebhookUnreachable` -> 502).
3. Registered in `Program.cs` via `builder.Services.AddHttpClient<IDetectorWebhookClient, DetectorWebhookClient>()`.
4. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented `IDetectorWebhookClient` and `DetectorWebhookClient` with typed `HttpClient` configured for 15s timeout and `PrIMA-Supervisor/1.0` User-Agent. Outbound requests transmit `{ detectorId, triggerSource, issueId, dispatchedAtUtc }` with delivery header `X-PrIMA-Delivery-Id`. Comprehensive failure handling catches timeouts, network unreachability, and HTTP 5xx/non-success status codes, recording `ExecutionFailed` inspection runs, transitioning detector health to `Failing`, synthesizing or appending `FAIL:{detectorId}` infrastructure anomaly issues with occurrence tracking and activity logging, and returning RFC 7807 504 Gateway Timeout or 502 Bad Gateway failure results. Registered client in `Program.cs`. Added 5 comprehensive integration tests in `DetectorWebhookClientTests.cs` verifying success deserialization, timeout recovery, HTTP 500 error recording, repeated failure recurrence tracking, and closed issue reopening. Project compiles cleanly with 0 warnings and 0 errors, and all 257 test cases pass.
Changed files:
- `src/backend/PrIMA.Api/Infrastructure/Webhooks/IDetectorWebhookClient.cs`
- `src/backend/PrIMA.Api/Infrastructure/Webhooks/WebhookExecutionResult.cs`
- `src/backend/PrIMA.Api/Infrastructure/Webhooks/WebhookFindingDto.cs`
- `src/backend/PrIMA.Api/Infrastructure/Webhooks/WebhookEvidenceDto.cs`
- `src/backend/PrIMA.Api/Infrastructure/Webhooks/WebhookErrors.cs`
- `src/backend/PrIMA.Api/Infrastructure/Webhooks/DetectorWebhookClient.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `src/backend/PrIMA.Api/Infrastructure/Persistence/DetectorRepository.cs`
- `src/backend/PrIMA.Api.Tests/Integration/DetectorWebhookClientTests.cs`

---

### P2-S06

Title: Autonomous Background Supervisor Engine (MonitoringSupervisorWorker)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `MonitoringSupervisorWorker.cs` as a hosted `BackgroundService` executing every 60 seconds with scoped database execution: Phase 1 evaluates silence and synthesizes `SILENT:{DetectorId}` issues with cadence-multiplied occurrences (`DEC-003`, `DEC-005`); Phase 2 executes dynamic SLA and recurrence severity escalation (`DEC-006`, `DEC-007`).

Depends On: P1-S01, P1-S02, P1-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api/BackgroundServices/MonitoringSupervisorWorker.cs` created:
   - Inherits `BackgroundService` using `PeriodicTimer(TimeSpan.FromSeconds(pollingIntervalSeconds))`.
   - Configurable settings via `IConfiguration`: `Supervisor:PollingIntervalSeconds` (default 60), `Supervisor:UnacknowledgedSlaTimeoutMinutes` (default 30).
   - Creates `IServiceScope` per tick.
2. **Phase 1: Silence Supervision (`BR-PRIMA-010`, `BR-PRIMA-005`, `DEC-003`, `DEC-005`):**
   - Query: Select active detectors where `COALESCE(LastHealthyHeartbeatUtc, CreatedAtUtc) < DATEADD(SECOND, -(ExpectedCadenceSeconds * 2.0), SYSUTCDATETIME())`.
   - For each detected candidate:
     - Begin explicit transaction with `(UPDLOCK, ROWLOCK)` on `dbo.Detectors`.
     - Re-evaluate silence condition under lock to avoid race conditions with incoming recovery (`RISK-003`).
     - Update detector `HealthStatus = DetectorHealth.Silent`.
     - Query active issues by `CorrelationKey = $"SILENT:{detector.Id}"`.
     - If not found:
       - Create new issue: `Scope = MonitoringScope.Infrastructure`, `Title = $"Silent Detector: {detector.Name} has missed cadence"`, `BaseSeverity = IssueSeverity.High`, `TraceString = $"Detector '{detector.Name}' missed heartbeat. Cadence: {detector.ExpectedCadenceSeconds}s. Threshold: {detector.ExpectedCadenceSeconds * 2}s."`.
       - Insert Occurrence #1 and activity log (`ActivityType = "Created"`).
     - If active issue found (`DEC-005`):
       - Calculate `multiples = (int)(elapsedSeconds / detector.ExpectedCadenceSeconds)`.
       - If `multiples >= 3` and `multiples > (issue.OccurrenceCount + 1)`:
         - Append new Occurrence with `OccurrenceNumber = issue.OccurrenceCount + 1`.
         - Update `dbo.Issues` (`OccurrenceCount`, `LastOccurrenceAtUtc`).
         - Insert activity log: `ActivityType = "RecurrenceObserved"`, `Summary = $"Prolonged silence breached {multiples}x cadence window."`.
     - Commit transaction.
3. **Phase 2: Dynamic Severity Escalation (`BR-PRIMA-003`, `DEC-006`, `DEC-007`):**
   - SLA unacknowledged escalation:
     - Query active issues where `Status IN ('Open', 'Reopened') AND AcknowledgedAtUtc IS NULL AND CurrentSeverity != 'Critical'`.
     - Check `issue.CheckSlaEscalation(slaTimeout, utcNow, out var reason)`.
     - If true: call `issue.EscalateSeverity(reason, utcNow)`, update `dbo.Issues` (`CurrentSeverity`, `SeverityRank`, `EscalationLevel`), insert activity log (`ActivityType = "SeverityEscalated"`, `Summary = reason`).
   - Recurrence threshold escalation:
     - Query active issues where `Status IN ('Open', 'Acknowledged', 'Investigating', 'Reopened') AND OccurrenceCount >= 5 AND CurrentSeverity != 'Critical'`.
     - Check `issue.CheckRecurrenceEscalation(utcNow, out var reason)`.
     - If true: call `issue.EscalateSeverity(reason, utcNow)`, update `dbo.Issues`, insert activity log (`ActivityType = "SeverityEscalated"`, `Summary = reason`).
4. Registered in `Program.cs` via `builder.Services.AddHostedService<MonitoringSupervisorWorker>()`.
5. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented `MonitoringSupervisorWorker.cs` as a hosted `BackgroundService` executing every 60 seconds with scoped execution per tick. Phase 1 detects silent detectors exceeding 2x expected cadence, acquires pessimistic `(UPDLOCK, ROWLOCK)` concurrency locks, re-verifies liveness, updates `HealthStatus` to `Silent`, synthesizes `SILENT:{DetectorId}` infrastructure anomaly issues with occurrence #1 and activity logs, and records cadence-multiplied occurrences for prolonged silence exceeding 3x cadence. Phase 2 evaluates active unacknowledged issues for SLA timeout breaches scaling by escalation level, and issues with >= 5 occurrences for recurrence threshold breaches, stepping severity up to `Critical` and writing structured audit activity logs. Service is registered in `Program.cs`. All 4 integration test scenarios pass cleanly in `MonitoringSupervisorWorkerTests.cs` and project builds with 0 errors and 0 warnings.
Changed files:
- `src/backend/PrIMA.Api/BackgroundServices/MonitoringSupervisorWorker.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `src/backend/PrIMA.Api.Tests/Integration/MonitoringSupervisorWorkerTests.cs`

---

## P3 - Backend Minimal API Endpoints & Contract Generation

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P3-S07

Title: Detector Portfolio Queries (GET /api/detectors & GET /api/detectors/summary)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `GET /api/detectors` (portfolio list with 30-day clean pass rate and active issues count for `S-03`) and `GET /api/detectors/summary` (global health counts for `S-03` and sidebar dot) in `src/backend/PrIMA.Api/Features/Detectors/`.

Depends On: P1-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. DTOs created under `Features/Detectors/GetDetectorList/`:
   - `DetectorListItemDto.cs`: `Guid Id, string Name, string Description, string Scope, string TargetModule, int ExpectedCadenceSeconds, string? WebhookUrl, string AdminStatus, string HealthStatus, DateTime? LastRunAtUtc, DateTime? LastHealthyHeartbeatUtc, decimal PassRate30d, int TotalRuns30d, int ActiveIssuesCount`.
   - `DetectorSummaryDto.cs`: `int TotalCount, int HealthyCount, int FailingCount, int SilentCount, int PausedCount`.
2. Handlers created:
   - `GetDetectorListHandler.cs`: Executes parameterized Dapper query matching Architecture §8.2, joining 30-day `InspectionRuns` aggregation (`COUNT(Outcome == 'Passed') * 100.0 / NULLIF(COUNT(*), 0)`) (`DEC-008`) and active issues count (`Status NOT IN ('Resolved', 'Dismissed')`). Supports query filters (`Scope?`, `AdminStatus?`, `HealthStatus?`, `Search?`). Orders by health priority (`Failing` -> `Silent` -> `Healthy`), then `Name ASC`.
   - `GetDetectorSummaryHandler.cs`: Fast Dapper aggregation query projecting `DetectorSummaryDto`.
3. Endpoints mapped:
   - `GetDetectorListEndpoint.cs`: `GET /api/detectors`. Secured via `.RequireAuthorization()`.
   - `GetDetectorSummaryEndpoint.cs`: `GET /api/detectors/summary`. Secured via `.RequireAuthorization()`.
4. Documented with OpenAPI metadata (`WithTags("Detectors")`, typed response schemas).
5. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented `GET /api/detectors` and `GET /api/detectors/summary` endpoints per `GAP-004`, `DEC-008`, and `DEC-010`. Created `DetectorListItemDto` and `DetectorSummaryDto` under `Features/Detectors/GetDetectorList/`. Implemented `GetDetectorListHandler` with parameterized Dapper SQL joining 30-day inspection run reliability metrics (`COUNT(CASE WHEN Outcome = 'Passed' THEN 1 END) * 100.0 / NULLIF(COUNT(*), 0)`) and unclosed active issues count, supporting `Scope`, `AdminStatus`, `HealthStatus`, and text search filters with health priority sorting (`Failing` -> `Silent` -> `Healthy`, then `Name ASC`). Implemented `GetDetectorSummaryHandler` projecting 4-state summary counts (`HealthyCount`, `FailingCount`, `SilentCount`, `PausedCount`, and `TotalCount`). Secured both endpoints with `.RequireAuthorization()`, registered in `DetectorEndpoints.cs` and `Program.cs`, and verified OpenAPI tags and typed response schemas. Added 6 comprehensive integration tests in `DetectorPortfolioEndpointsTests.cs` verifying authentication guards, health priority ordering, 30-day pass rate calculations, issue joins, filter permutations, and summary counts. Project compiles cleanly with 0 warnings and 0 errors, and all tests pass.
Changed files:
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorList/DetectorListItemDto.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorList/DetectorSummaryDto.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorList/GetDetectorListQuery.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorList/GetDetectorListHandler.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorList/GetDetectorListEndpoint.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorSummary/GetDetectorSummaryHandler.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorSummary/GetDetectorSummaryEndpoint.cs`
- `src/backend/PrIMA.Api/Features/Detectors/DetectorEndpoints.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `src/backend/PrIMA.Api.Tests/Integration/DetectorPortfolioEndpointsTests.cs`

---

### P3-S08

Title: Detector Detail & Historical Run Log Queries (GET /api/detectors/{id} & GET /api/detectors/{id}/runs)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `GET /api/detectors/{id}` (composite detail query for `S-04`) and `GET /api/detectors/{id}/runs` (historical run log for `S-06`) under `src/backend/PrIMA.Api/Features/Detectors/`.

Depends On: P1-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. DTOs created under `Features/Detectors/GetDetectorById/` and `Features/Detectors/GetInspectionRuns/`:
   - `DetectorDetailDto.cs`: combines metadata, `DetectorReliabilityStatsDto`, `IReadOnlyList<RecentRunSummaryDto>` (recent 10 runs), and `IReadOnlyList<LinkedActiveIssueDto>`.
   - `InspectionRunLogResponse.cs` & `InspectionRunDetailDto.cs`: run details with `ExecutionTimestampUtc`, `TriggerSource`, `Outcome`, `DurationMs`, `FailureReason`, and linked discovered issue IDs.
2. Handlers created:
   - `GetDetectorByIdHandler.cs`: Executes composite Dapper query via `QueryMultipleAsync` (detector row, 30-day stats, top 10 recent runs, active linked issues). Returns `404 Not Found` if missing.
   - `GetInspectionRunsHandler.cs`: Queries `dbo.InspectionRuns` with date range filtering (`StartDateUtc?`, `EndDateUtc?`, defaulting to last 7 days), outcome filter, trigger source filter, pagination/limit, and maps discovered issues.
3. Endpoints mapped:
   - `GetDetectorByIdEndpoint.cs`: `GET /api/detectors/{id}`. Secured via `.RequireAuthorization()`.
   - `GetInspectionRunsEndpoint.cs`: `GET /api/detectors/{id}/runs`. Secured via `.RequireAuthorization()`.
4. Documented with OpenAPI metadata.
5. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implements `GAP-004`, `E-2`, `F-1`, and `F-2`. Implemented composite detector detail query with 4-part Dapper `QueryMultipleAsync` (detector metadata, 30-day reliability statistics, recent 10 runs with occurrence counts, active issues prioritized by severity rank). Implemented historical inspection run log query with flexible date filtering (default last 7 days), outcome and trigger source filtering, pagination, and linked discovered issue mapping from `dbo.Occurrences`. Registered endpoints under `Features/Detectors/DetectorEndpoints.cs` and mapped in `Program.cs`. Authored comprehensive integration tests in `DetectorDetailAndRunLogEndpointTests.cs` verifying authentication guards, 404 problem details, multi-part data mappings, filters, pagination, and OpenAPI schemas.
Changed files:
- `src/backend/PrIMA.Api/Features/Detectors/DetectorEndpoints.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorById/DetectorDetailDto.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorById/DetectorReliabilityStatsDto.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorById/RecentRunSummaryDto.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorById/LinkedActiveIssueDto.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorById/GetDetectorByIdHandler.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetDetectorById/GetDetectorByIdEndpoint.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetInspectionRuns/InspectionRunDetailDto.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetInspectionRuns/InspectionRunLogResponse.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetInspectionRuns/GetInspectionRunsQuery.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetInspectionRuns/GetInspectionRunsHandler.cs`
- `src/backend/PrIMA.Api/Features/Detectors/GetInspectionRuns/GetInspectionRunsEndpoint.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `src/backend/PrIMA.Api/swagger.json`
- `src/backend/PrIMA.Api.Tests/Integration/DetectorDetailAndRunLogEndpointTests.cs`

---

### P3-S09

Title: Detector Management Commands (Register, Update, Toggle Status)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement detector lifecycle commands: `POST /api/detectors` (register for `S-05`), `PUT /api/detectors/{id}` (update config for `S-05`), and `POST /api/detectors/{id}/toggle-status` (pause/resume/retire for `S-04`) in `Features/Detectors/`.

Depends On: P1-S01, P1-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `RegisterDetectorEndpoint.cs` & `RegisterDetectorHandler.cs`:
   - Route `POST /api/detectors` accepting `RegisterDetectorRequest { Name, Description, Scope, TargetModule, ExpectedCadenceSeconds, WebhookUrl }`.
   - Validates unique `Name` (returns `409 Conflict` if duplicate), valid scope, cadence `> 0`, optional valid URL.
   - Creates and inserts `Detector` aggregate. Returns `201 Created` with `DetectorDetailDto`.
2. `UpdateDetectorEndpoint.cs` & `UpdateDetectorHandler.cs`:
   - Route `PUT /api/detectors/{id}` accepting `UpdateDetectorRequest { Description, Scope, TargetModule, ExpectedCadenceSeconds, WebhookUrl }`.
   - Enforces name immutability (`Name` is not updated).
   - Rejects retired detector with `409 Conflict` (`DetectorErrors.CannotModifyRetiredDetector`).
   - Updates configuration via `_detectorRepository.UpdateConfigurationAsync`. Returns `204 NoContent`.
3. `ToggleDetectorStatusEndpoint.cs` & `ToggleDetectorStatusHandler.cs`:
   - Route `POST /api/detectors/{id}/toggle-status` accepting `ToggleDetectorStatusRequest { string Action }` (`"Pause"`, `"Resume"`, `"Retire"`).
   - Invokes domain method on `Detector` aggregate (`Pause()`, `Resume()`, or `Retire()`).
   - Retiring preserves linked active issues in their current state (`DEC-009`).
   - Updates `AdminStatus` in database. Returns `204 NoContent`.
4. All endpoints secured via `.RequireAuthorization()`.
5. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented detector lifecycle command endpoints, handlers, and FluentValidation validators:
- `POST /api/detectors` (Register): Validates name uniqueness (409 Conflict on duplicate), scope, cadence, optional URL; creates `Detector` aggregate and persists; returns 201 Created with initial `DetectorDetailDto`.
- `PUT /api/detectors/{id}` (Update): Enforces name immutability, guards against modifying retired detectors (409 Conflict), updates configuration in `dbo.Detectors`, and returns 204 NoContent.
- `POST /api/detectors/{id}/toggle-status` (Toggle Status): Validates action ("Pause", "Resume", "Retire"), invokes aggregate domain methods, preserves linked active issues on retire (DEC-009), updates `AdminStatus` in DB, and returns 204 NoContent.
- Mapped endpoints in `DetectorEndpoints.cs`, registered handlers in `Program.cs`, and secured all routes via `.RequireAuthorization()`.
- Added 18 comprehensive integration tests in `DetectorManagementCommandEndpointTests.cs` verifying authentication, duplicate detection, validation errors, name immutability, retired state rejections, and full Pause -> Resume -> Retire lifecycle transitions.
Changed files:
- `src/backend/PrIMA.Api/Features/Detectors/RegisterDetector/RegisterDetectorRequest.cs`
- `src/backend/PrIMA.Api/Features/Detectors/RegisterDetector/RegisterDetectorValidator.cs`
- `src/backend/PrIMA.Api/Features/Detectors/RegisterDetector/RegisterDetectorHandler.cs`
- `src/backend/PrIMA.Api/Features/Detectors/RegisterDetector/RegisterDetectorEndpoint.cs`
- `src/backend/PrIMA.Api/Features/Detectors/UpdateDetector/UpdateDetectorRequest.cs`
- `src/backend/PrIMA.Api/Features/Detectors/UpdateDetector/UpdateDetectorValidator.cs`
- `src/backend/PrIMA.Api/Features/Detectors/UpdateDetector/UpdateDetectorHandler.cs`
- `src/backend/PrIMA.Api/Features/Detectors/UpdateDetector/UpdateDetectorEndpoint.cs`
- `src/backend/PrIMA.Api/Features/Detectors/ToggleDetectorStatus/ToggleDetectorStatusRequest.cs`
- `src/backend/PrIMA.Api/Features/Detectors/ToggleDetectorStatus/ToggleDetectorStatusValidator.cs`
- `src/backend/PrIMA.Api/Features/Detectors/ToggleDetectorStatus/ToggleDetectorStatusHandler.cs`
- `src/backend/PrIMA.Api/Features/Detectors/ToggleDetectorStatus/ToggleDetectorStatusEndpoint.cs`
- `src/backend/PrIMA.Api/Features/Detectors/DetectorEndpoints.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/IDetectorRepository.cs`
- `src/backend/PrIMA.Api/Infrastructure/Persistence/DetectorRepository.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `src/backend/PrIMA.Api.Tests/Fixtures/PrIMAApiFactory.cs`
- `src/backend/PrIMA.Api.Tests/Integration/DetectorManagementCommandEndpointTests.cs`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

### P3-S10

Title: Webhook-Driven Execution Endpoints (Trigger Manual Run & Verification Re-run)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement on-demand probe execution endpoints: `POST /api/detectors/{id}/trigger-run` (`S-04`, `E-7`) and `POST /api/issues/{id}/verification/trigger-run` (`S-02`, `D-2`) integrating `DetectorWebhookClient` with immediate outcome persistence.

Depends On: P2-S05, P1-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `TriggerManualRunEndpoint.cs` & `TriggerManualRunHandler.cs` under `Features/Detectors/`:
   - Route `POST /api/detectors/{id}/trigger-run`.
   - Validates detector exists, is not retired, is not paused, and has a configured `WebhookUrl`; returns `409 Conflict` if not runnable.
   - Invokes `_webhookClient.ExecuteRunAsync(detector.Id, detector.WebhookUrl, "Manual")`.
   - Persists `InspectionRun` with `TriggerSource = "Manual"`.
   - Returns `200 OK` with `InspectionRunResultDto { RunId, Outcome, DurationMs, FailureReason, FindingsCount }` or maps webhook timeout/failure to `504`/`502` ProblemDetails.
2. `TriggerVerificationRunEndpoint.cs` & `TriggerVerificationRunHandler.cs` under `Features/Issues/`:
   - Route `POST /api/issues/{id}/verification/trigger-run`.
   - Validates issue exists and is in `Investigating` status with `LatestRemediation != null`.
   - Resolves detector's `WebhookUrl`; returns `409 Conflict` if no webhook configured.
   - Invokes `_webhookClient.ExecuteRunAsync(detector.Id, detector.WebhookUrl, "AutomatedReRun", issue.Id)`.
   - Evaluates webhook result:
     - If `Outcome == "Passed"` and `Findings.Count == 0`:
       - Inserts `VerificationRecord` (`VerificationMethod = "AutomatedReRun"`, `CleanPassed = true`, `EvidenceNotes = $"Automated verification run '{runId}' completed with zero findings."`).
       - Unlocks Verification Gate (`BR-PRIMA-004`).
     - If `FindingDiscovered` or `ExecutionFailed`:
       - Inserts `VerificationRecord` (`CleanPassed = false`). Verification Gate remains locked.
   - Appends activity log (`ActivityType = "VerificationRecorded"`).
   - Returns `200 OK` with `VerificationResultDto`.
3. All endpoints secured via `.RequireAuthorization()`.
4. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented on-demand probe execution endpoints per `GAP-003`, `DEC-001`, `DEC-002`, `D-2`, `E-7`, and `BR-PRIMA-004`:
- `TriggerManualRunHandler` & `TriggerManualRunEndpoint`: Dispatches probe run via `DetectorWebhookClient`, validates detector runnability (404 on missing, 409 on paused/retired/missing webhook), persists `InspectionRun` with `TriggerSource = Manual`, updates health status, and returns 200 OK with `InspectionRunResultDto` or maps failures to 502/504 ProblemDetails.
- `TriggerVerificationRunHandler` & `TriggerVerificationRunEndpoint`: Dispatches automated re-run for issue remediation verification (`AutomatedReRun`), validates issue investigating status and remediation presence (409 Conflict), resolves webhook URL, evaluates findings (clean pass unlocks `BR-PRIMA-004` Verification Gate while finding or execution failure keeps gate locked), persists `VerificationRecord` and `InspectionRun`, appends `VerificationRecorded` activity log, and returns 200 OK with `VerificationResultDto`.
- Added `TriggerSource.AutomatedReRun` enum, `DetectorErrors.NoWebhookConfigured`, and RFC 7807 502/504 title handling.
- Registered endpoints and scoped handlers in `DetectorEndpoints.cs`, `IssueLifecycleEndpoints.cs`, and `Program.cs`. Secured both routes with `.RequireAuthorization()`.
- Authored 15 comprehensive integration tests in `WebhookExecutionEndpointsTests.cs` covering unauthenticated requests, 404 missing lookups, 409 precondition conflicts, 502/504 webhook gateway errors, clean pass Verification Gate resolution flows, and finding failure gate-lock flows. Project compiles with 0 errors and 0 warnings, and all 15 tests pass.
Changed files:
- `src/backend/PrIMA.Api/Domain/Detectors/TriggerSource.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/DetectorErrors.cs`
- `src/backend/PrIMA.Api/Common/ProblemDetailsExtensions.cs`
- `src/backend/PrIMA.Api/Features/Detectors/TriggerManualRun/InspectionRunResultDto.cs`
- `src/backend/PrIMA.Api/Features/Detectors/TriggerManualRun/TriggerManualRunHandler.cs`
- `src/backend/PrIMA.Api/Features/Detectors/TriggerManualRun/TriggerManualRunEndpoint.cs`
- `src/backend/PrIMA.Api/Features/Detectors/DetectorEndpoints.cs`
- `src/backend/PrIMA.Api/Features/Issues/TriggerVerificationRun/VerificationResultDto.cs`
- `src/backend/PrIMA.Api/Features/Issues/TriggerVerificationRun/TriggerVerificationRunHandler.cs`
- `src/backend/PrIMA.Api/Features/Issues/TriggerVerificationRun/TriggerVerificationRunEndpoint.cs`
- `src/backend/PrIMA.Api/Features/Issues/IssueLifecycleEndpoints.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `src/backend/PrIMA.Api.Tests/Integration/WebhookExecutionEndpointsTests.cs`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

### P3-S11

Title: Dashboard Feed Escalation Extension, API Registration & Contract Generation

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Extend `GetDashboardFeedHandler.cs` with `EscalatedCount` and `EscalatedOnly` filter (`DEC-010`), register all new Stage 3 endpoints in `Program.cs`, verify OpenAPI metadata, and generate frontend TypeScript contracts.

Depends On: P2-S06, P3-S07, P3-S08, P3-S09, P3-S10  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `GetDashboardFeedHandler.cs` & `GetDashboardFeedEndpoint.cs` updated:
   - `DashboardFeedSummaryDto` extended with `int EscalatedCount`.
   - `GetDashboardFeedQuery` extended with `bool? EscalatedOnly`.
   - When `EscalatedOnly == true`, SQL query filters `AND (EscalationLevel > 0 OR CurrentSeverity = 'Critical')`.
2. `Program.cs` updated:
   - Registers all new handlers and background service in DI.
   - Maps route groups `app.MapDetectorEndpoints()` and webhook verification endpoint.
3. OpenAPI verification & code generation:
   - Verify `/swagger/v1/swagger.json` exports all 8 new detector endpoints and verification trigger endpoint.
   - Run `npm run generate:api` in `src/frontend/` to produce `src/frontend/src/shared/types/api.generated.ts` with all Stage 3 schemas.
4. Backend compiles cleanly (`dotnet build`) and frontend type-checking passes (`vue-tsc -b`).

Notes: Extended `GetDashboardFeedHandler.cs`, `GetDashboardFeedResponse.cs`, `GetDashboardFeedQuery.cs`, and `GetDashboardFeedEndpoint.cs` with `EscalatedCount` in `DashboardFeedSummaryDto` and `bool? EscalatedOnly` query filter per `DEC-010` and `RISK-005`. All 8 detector endpoints (`GET /api/detectors`, `GET /api/detectors/summary`, `GET /api/detectors/{id}`, `GET /api/detectors/{id}/runs`, `POST /api/detectors`, `PUT /api/detectors/{id}`, `POST /api/detectors/{id}/toggle-status`, `POST /api/detectors/{id}/trigger-run`), the verification trigger endpoint (`POST /api/issues/{id}/verification/trigger-run`), and `MonitoringSupervisorWorker` are registered and mapped in `Program.cs`. Updated `SwaggerDocumentTests.cs` to assert all Stage 3 paths and schemas, exporting synchronized OpenAPI definitions to `src/backend/PrIMA.Api/swagger.json`. Updated `src/frontend/scripts/generate-api.js` and regenerated `src/frontend/src/shared/types/api.generated.ts` with all Stage 3 models, enums (`DetectorHealth`, `AdminStatus`, `TriggerSource`), and convenience type aliases. Authored integration tests in `DashboardFeedTests.cs` verifying `EscalatedCount` calculation and `EscalatedOnly` filtering. All 275 backend tests pass and frontend build (`vue-tsc -b && vite build`) compiles with 0 errors.
Changed files:
- `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/GetDashboardFeedResponse.cs`
- `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/GetDashboardFeedQuery.cs`
- `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/GetDashboardFeedHandler.cs`
- `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/GetDashboardFeedEndpoint.cs`
- `src/backend/PrIMA.Api.Tests/Integration/DashboardFeedTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/SwaggerDocumentTests.cs`
- `src/backend/PrIMA.Api/swagger.json`
- `src/frontend/scripts/generate-api.js`
- `src/frontend/src/shared/types/api.generated.ts`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

## P4 - Frontend UI Features & Cross-Screen Integrations

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P4-S12

Title: Frontend Detector API Layer, Query Composables & Form Schemas

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Create TanStack Query key factory, composables with 15-second polling, mutations with selective cache invalidation, Zod validation schemas, and cadence conversion helpers under `src/frontend/src/features/detectors/`.

Depends On: P3-S11  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/features/detectors/api/detector.keys.ts`:
   - Query key factory: `detectorKeys.list(params)`, `detectorKeys.summary()`, `detectorKeys.detail(id)`, `detectorKeys.runs(id, params)`.
2. `src/frontend/src/features/detectors/api/useDetectorQueries.ts`:
   - `useDetectorListQuery(params)`: `refetchInterval: 15_000`, `staleTime: 7_500`.
   - `useDetectorSummaryQuery()`: `refetchInterval: 15_000`, `staleTime: 7_500`.
   - `useDetectorDetailQuery(id)`: `refetchInterval: 15_000`, `staleTime: 7_500`.
   - `useInspectionRunsQuery(id, params)`: `staleTime: 10_000`.
   - Mutations with cache invalidation:
     - `useRegisterDetectorMutation()`: invalidates `list`, `summary`.
     - `useUpdateDetectorMutation(id)`: invalidates `detail(id)`, `list`, `summary`.
     - `useToggleDetectorStatusMutation(id)`: invalidates `detail(id)`, `list`, `summary`.
     - `useTriggerManualRunMutation(id)`: invalidates `detail(id)`, `runs(id)`, `dashboard.feed`.
3. `src/frontend/src/features/detectors/schemas/detectorForm.schema.ts`:
   - Zod schema for registration & edit forms: `name` (required, min 3 chars), `description` (required, min 5 chars), `scope` (enum `DataQuality`, `BusinessLogic`, `Infrastructure`, `ProcessFlow`), `targetModule` (required), `cadenceValue` (number > 0), `cadenceUnit` (`Minutes` or `Hours`), `webhookUrl` (optional, valid URL format if provided).
4. `src/frontend/src/features/detectors/composables/useDetectorForm.ts`:
   - Helper converting between total seconds and split value/unit (`cadenceValue`, `cadenceUnit`).
   - Dynamic calculated helper text: `cadenceSummaryText` and `silenceThresholdText` (e.g. `"Cadence: 5 min · Silence threshold: 10 min"`).
5. Frontend compiles cleanly via `npm run build` in `src/frontend/`.

Notes: Implements frontend API foundation and TanStack Query caching architecture per `GAP-005` and Architecture §4.6.
- Created `detector.keys.ts` defining hierarchical TanStack query key factory (`all`, `lists`, `list(params)`, `summaries`, `summary()`, `details`, `detail(id)`, `runsAll`, `runs(id, params)`).
- Created `useDetectorQueries.ts` with 15-second background polling (`staleTime: 7_500`) for list, summary, and detail queries; 10-second stale time for run logs; and mutation hooks (`useRegisterDetectorMutation`, `useUpdateDetectorMutation`, `useToggleDetectorStatusMutation`, `useTriggerManualRunMutation`) with selective query cache invalidation.
- Created `detectorForm.schema.ts` defining Zod validation schemas for registration and edit forms enforcing required fields, scope enumeration (`DataQuality`, `BusinessLogic`, `Infrastructure`, `ProcessFlow`), numeric cadence values, unit constraints, and optional HTTP/HTTPS webhook URL format.
- Created `useDetectorForm.ts` with bidirectional unit conversion helpers (`cadenceToSeconds`, `secondsToCadence`), dynamic 2x silence threshold calculation helpers, and reactive form composables.
- Added `detectorView.types.ts` for unified detector UI state typing.
- Frontend builds cleanly with zero errors via `npm --prefix src/frontend run build`.
Changed files:
- `src/frontend/src/features/detectors/api/detector.keys.ts`
- `src/frontend/src/features/detectors/api/useDetectorQueries.ts`
- `src/frontend/src/features/detectors/types/detectorView.types.ts`
- `src/frontend/src/features/detectors/schemas/detectorForm.schema.ts`
- `src/frontend/src/features/detectors/composables/useDetectorForm.ts`
- `src/backend/PrIMA.Api/Domain/Detectors/MonitoringScope.cs`
- `src/backend/PrIMA.Api/Features/Detectors/RegisterDetector/RegisterDetectorValidator.cs`
- `src/backend/PrIMA.Api/Features/Detectors/UpdateDetector/UpdateDetectorValidator.cs`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

### P4-S13

Title: S-03 Detector Portfolio Screen & Navigation Alert Integration

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `DetectorListView.vue` (`S-03`) at `/detectors` with 4-state Health Summary Bar, multi-attribute filter pills, virtualized table/cards with color-coded pass rates, and activate the `Detectors` sidebar alert indicator dot in `AppLayout.vue`.

Depends On: P4-S12  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Components created under `features/detectors/components/`:
   - `HealthSummaryBar.vue`: 4 interactive count chips (`Healthy`, `Failing`, `Silent`, `Paused`). Clicking a chip filters the table by that health/admin status; clicking again clears filter.
   - `DetectorFilterBar.vue`: Scope selector, AdminStatus pills (`All`, `Active`, `Paused`, `Retired`), HealthStatus pills (`All`, `Healthy`, `Failing`, `Silent`), instant search input.
   - `DetectorTable.vue`: Virtualized data table with columns: Status (health/admin badge), Name & Target Module, Cadence (formatted as `Every 5m`), Last Heartbeat (WIB formatted), 30-Day Clean Pass Rate (color-coded badge), Active Issues count, Action button (`View →`).
   - `DetectorCard.vue`: Grid card representation for smaller desktop viewports.
2. View created: `src/frontend/src/features/detectors/views/DetectorListView.vue`:
   - Page header with title `"Detector Portfolio"`, subtitle, and `+ Register Detector` CTA button navigating to `/detectors/new`.
   - Connects `useDetectorListQuery` and `useDetectorSummaryQuery` with 15-second background polling.
   - Row click navigates to `/detectors/${item.id}`.
3. `AppLayout.vue` navigation sidebar updated:
   - Enables `Detectors` sidebar route (`/detectors`), removing "Coming in Stage 3" tooltip.
   - Polls `useDetectorSummaryQuery()` every 15 seconds.
   - Renders animated amber/rose alert indicator dot on the `Detectors` nav item when `(FailingCount + SilentCount) > 0` (`DEC-010`).
4. Router updated: registers `/detectors` mapped to `DetectorListView.vue` with `requiresAuth: true`.
5. Frontend compiles cleanly via `npm run build`.

Notes: Implemented `DetectorListView.vue` (`S-03`), supporting components (`HealthSummaryBar.vue`, `DetectorFilterBar.vue`, `DetectorTable.vue`, `DetectorCard.vue`), navigation route `/detectors`, and alert dot integration in `AppLayout.vue` per `DEC-008`, `DEC-010`, `E-1`, and Architecture §4.6.
- Created `HealthSummaryBar.vue` with 4 interactive count chips (`Healthy`, `Failing`, `Silent`, `Paused`) with toggle click filtering and total count badge.
- Created `DetectorFilterBar.vue` supporting scope selection, debounced instant text search, `HealthStatus` pills (`All`, `Healthy`, `Failing`, `Silent`), and `AdminStatus` pills (`All`, `Active`, `Paused`, `Retired`).
- Created `DetectorTable.vue` with virtualized scrolling (`virtualScrollerOptions`), formatted cadence (`Every 5m`), WIB-formatted heartbeat, DEC-008 color-coded pass rate badges (Emerald/Amber/Rose/Slate), active issue counts, and row navigation.
- Created `DetectorCard.vue` providing a responsive grid card view for smaller viewports or card mode.
- Created `DetectorListView.vue` at `/detectors` wired to `useDetectorListQuery` and `useDetectorSummaryQuery` with 15s background polling, live sync indicators, table/card view toggle, and register CTA button.
- Updated `AppLayout.vue` activating the `Detectors` sidebar navigation item, background polling `useDetectorSummaryQuery()`, and displaying animated pinging amber/rose alert dots and count pills when `(FailingCount + SilentCount) > 0`.
- Registered `/detectors` route in `router/index.ts`.
- Verified clean compilation with `vue-tsc -b && vite build`.
Changed files:
- `src/frontend/src/features/detectors/components/HealthSummaryBar.vue`
- `src/frontend/src/features/detectors/components/DetectorFilterBar.vue`
- `src/frontend/src/features/detectors/components/DetectorTable.vue`
- `src/frontend/src/features/detectors/components/DetectorCard.vue`
- `src/frontend/src/features/detectors/components/index.ts`
- `src/frontend/src/features/detectors/composables/useDetectorForm.ts`
- `src/frontend/src/features/detectors/views/DetectorListView.vue`
- `src/frontend/src/app/layouts/AppLayout.vue`
- `src/frontend/src/app/router/index.ts`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

### P4-S14

Title: S-04 Detector Detail Screen & Operational Action Bar

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `DetectorDetailView.vue` (`S-04`) at `/detectors/:id` with sticky header, status-sensitive Action Bar (`Trigger Run`, `Pause`/`Resume`, `Edit`, `Retire`), 4-column 30-day reliability card, recent 10 inspection runs with inline detail expansion, and linked active issues.

Depends On: P4-S12  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Components created under `features/detectors/components/`:
   - `ReliabilityStatsCard.vue`: 4-column metrics card: `Last Successful Run`, `Last Failure`, `Total Runs (30d)`, and `Clean Pass Rate` (Emerald `>= 95%`, Amber `85%–94%`, Rose `< 85%`, Slate if 0 runs).
   - `RecentRunsTable.vue`: Renders recent 10 inspection runs with expandable rows. Tracks open rows via reactive `Set<string>` of run IDs to preserve open state during 15s background polling refetches. Monospace failure block with copy button for failed runs.
   - `LinkedActiveIssuesList.vue`: Lists linked active issues with severity chip, title, escalation badge, and direct router link to `/issues/:id`.
   - `ConfirmRetireDialog.vue`: Modal dialog requiring confirmation before executing irreversible retirement.
2. View created: `src/frontend/src/features/detectors/views/DetectorDetailView.vue`:
   - Sticky header with back button to `/detectors`, detector name, target module, health badge, and admin status badge.
   - Status-sensitive Action Bar matching Architecture §4.6.2:
     - `Active` with Webhook: `Trigger Run` (Primary, with loading spinner), `Pause`, `Edit`, `Retire`.
     - `Active` without Webhook: `Trigger Run` disabled with tooltip, `Pause`, `Edit`, `Retire`.
     - `Paused`: `Resume` (Primary, Emerald), `Edit`, `Retire`.
     - `Retired`: Read-only banner `"This detector is permanently decommissioned."`
   - Connects `useDetectorDetailQuery(id)` with 15s polling.
   - Header button linking to full run log (`/detectors/:id/runs`).
3. Router updated: registers `/detectors/:id` mapped to `DetectorDetailView.vue` with `requiresAuth: true`.
4. Frontend compiles cleanly via `npm run build`.

Notes: Implemented `DetectorDetailView.vue` (`S-04`) and associated components per Architecture §4.6.2, `E-2`, `E-5`, `E-6`, `E-7`, and `DEC-008`:
- Created `ReliabilityStatsCard.vue`: 4-column operational metrics card displaying `Last Successful Run` (WIB + relative), `Last Failure` (WIB + relative/None), `Total Runs (30d)`, and `Clean Pass Rate` with threshold-based color coding (Emerald `>= 95%`, Amber `85%–94%`, Rose `< 85%`, Slate if 0 runs).
- Created `RecentRunsTable.vue`: Renders recent 10 inspection runs with expandable rows using a reactive `Set<string>` of run IDs to persist open state during 15-second background polling refetches, duration, trigger source and outcome badges, and monospace failure diagnostics block with clipboard copy action.
- Created `LinkedActiveIssuesList.vue`: Renders linked unclosed issues originating from or correlating to the detector with severity chips, escalation badges, occurrence counts, and direct router navigation to `/issues/:id`.
- Created `ConfirmRetireDialog.vue`: Irreversible confirmation modal warning of permanent supervision cessation before decommissioning a detector.
- Created `DetectorDetailView.vue` at `/detectors/:id`: Sticky header with back navigation to `/detectors`, target module pill, health badge, admin status badge, link to `/detectors/:id/runs`, and status-sensitive Action Bar dynamically handling Active with webhook (`Trigger Run`, `Pause`, `Edit`, `Retire`), Active without webhook (disabled `Trigger Run` with tooltip), Paused (`Resume`, `Edit`, `Retire`), and Retired (permanent decommission advisory banner). Connected to `useDetectorDetailQuery` with 15-second background polling and mutation composables for manual triggering and status toggling.
- Registered `/detectors/:id` route in `src/frontend/src/app/router/index.ts` with `requiresAuth: true`.
- Clean compilation verified via `npm --prefix src/frontend run build`.
Changed files:
- `src/frontend/src/features/detectors/components/ReliabilityStatsCard.vue`
- `src/frontend/src/features/detectors/components/RecentRunsTable.vue`
- `src/frontend/src/features/detectors/components/LinkedActiveIssuesList.vue`
- `src/frontend/src/features/detectors/components/ConfirmRetireDialog.vue`
- `src/frontend/src/features/detectors/components/index.ts`
- `src/frontend/src/features/detectors/views/DetectorDetailView.vue`
- `src/frontend/src/features/detectors/views/DetectorFormView.vue`
- `src/frontend/src/app/router/index.ts`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

### P4-S15

Title: S-05 Detector Registration & Edit Form Screen

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `DetectorFormView.vue` (`S-05`) at `/detectors/new` and `/detectors/:id/edit` with VeeValidate + Zod validation, immutable `Name` in edit mode, split cadence picker, and real-time calculated 2× silence threshold helper text.

Depends On: P4-S12  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. View created: `src/frontend/src/features/detectors/views/DetectorFormView.vue`:
   - Supports both Create (`/detectors/new`) and Edit (`/detectors/:id/edit`) modes based on route params.
   - Form state managed via VeeValidate and `detectorForm.schema.ts`, detached from background queries so polling never clears in-progress input drafts.
   - Form fields:
     - Detector Name: input field. Editable in Create mode; read-only disabled with lock icon in Edit mode (immutable identity).
     - Target Module: text input (e.g. `Billing`, `Pharmacy`, `Laboratory`).
     - Monitoring Scope: PrimeVue `Dropdown` (`DataQuality`, `BusinessLogic`, `Infrastructure`, `ProcessFlow`).
     - Description: textarea input.
     - Split Cadence Picker: numeric input (`cadenceValue`) + unit toggle buttons (`Minutes` / `Hours`).
     - Dynamic Silence Helper: real-time computed banner below cadence input displaying `cadenceSummaryText` and `silenceThresholdText` (e.g. `"Detector expected every 5 minutes. PrIMA will flag detector as Silent if no heartbeat arrives for 10 minutes."`).
     - Webhook URL: optional URL input with placeholder `https://simrs.hospital.local/probes/...`.
   - Submit button with loading state; cancel button returning to previous screen.
2. Router updated: registers `/detectors/new` and `/detectors/:id/edit` mapped to `DetectorFormView.vue` with `requiresAuth: true`.
3. Frontend compiles cleanly via `npm run build`.

Notes: Implemented `DetectorFormView.vue` (`S-05`) for detector registration and editing per `E-3`, `E-4`, `BR-PRIMA-008`, `BR-PRIMA-010`, and Architecture §4.6:
- Created `DetectorFormView.vue` supporting both Create (`/detectors/new`) and Edit (`/detectors/:id/edit`) modes based on route `:id` parameter.
- Managed form validation state via VeeValidate `useForm` and `@vee-validate/zod` with `detectorFormSchema` (create mode) and `detectorEditFormSchema` (edit mode where name is immutable). Form state is detached from background query polling to ensure in-progress input drafts are never cleared or overwritten.
- Enforced immutable detector identity in edit mode: name input is rendered read-only disabled with a lock icon and explanatory banner citing `BR-PRIMA-008`. In create mode, name is editable with unique name validation.
- Implemented target module text input and monitoring scope PrimeVue `Dropdown` / `Select` with rich descriptions for `DataQuality`, `BusinessLogic`, `Infrastructure`, and `ProcessFlow`.
- Implemented Split Cadence Picker with numeric cadence value input and toggle buttons for `Minutes` and `Hours`.
- Implemented dynamic 2x silence threshold helper banner displaying real-time computed `cadenceSummaryText` badge, `silenceThresholdText` badge, and human-friendly `silenceExplanationText` explaining automatic `SILENT:{id}` issue synthesis per `BR-PRIMA-010`.
- Implemented optional Webhook URL input with URL validation and placeholder for on-demand inspection runs (`S-04`) and automated verification re-runs (`S-02` / `D-2`).
- Handled retired detector state in edit mode by displaying a prominent decommission advisory and disabling mutations.
- Updated Vue router in `src/frontend/src/app/router/index.ts` registering `/detectors/new` (`detector-create`) and `/detectors/:id/edit` (`detector-edit`) with `requiresAuth: true`.
- Clean compilation verified via `npm --prefix src/frontend run build` with zero TypeScript or Vite errors.
Changed files:
- `src/frontend/src/features/detectors/views/DetectorFormView.vue`
- `src/frontend/src/app/router/index.ts`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

### P4-S16

Title: S-06 Historical Inspection Run Log Screen

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `InspectionRunLogView.vue` (`S-06`) at `/detectors/:id/runs` with 7-day date range picker, outcome and trigger filter pills, expandable execution details, monospace failure trace blocks with copy buttons, and links to discovered issues on `S-02`.

Depends On: P4-S12  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Components created under `features/detectors/components/`:
   - `RunLogFilterBar.vue`: Date range picker (defaulting to last 7 days), outcome pills (`All`, `Passed`, `FindingDiscovered`, `ExecutionFailed`), and trigger source pills (`All`, `Scheduled`, `Manual`, `AutomatedReRun`).
2. View created: `src/frontend/src/features/detectors/views/InspectionRunLogView.vue`:
   - Header with detector context breadcrumb and back button to `/detectors/:id`.
   - Virtualized data table with columns: Timestamp (WIB), Trigger Source, Outcome (color-coded badge), Duration (ms), Findings Count, Actions (expand details).
   - Expandable row details:
     - Shows execution trace, failure reason in dark monospace block with one-click copy button.
     - If findings discovered, lists links to created/updated issues navigating directly to `/issues/:issueId`.
   - Row expansion state preserved across query refreshes using reactive `Set<string>`.
3. Router updated: registers `/detectors/:id/runs` mapped to `InspectionRunLogView.vue` with `requiresAuth: true`.
4. Frontend compiles cleanly via `npm run build`.

Notes: Implemented `InspectionRunLogView.vue` (`S-06`) and `RunLogFilterBar.vue` per Architecture §4.6, `F-1`, and `F-2`:
- Created `RunLogFilterBar.vue` with 7-day date range picker defaults (including 24h, 7d, 30d quick preset toggles and custom date inputs), outcome filter pills (`All`, `Passed`, `FindingDiscovered`, `ExecutionFailed`), trigger source filter pills (`All`, `Scheduled`, `Manual`, `AutomatedReRun`), and reset filters button.
- Created `InspectionRunLogView.vue` mapped to `/detectors/:id/runs` with:
  - Header including breadcrumb hierarchy (`Detectors` -> `{detector.name}` -> `Inspection Run Log`), back button to `/detectors/:id`, target module and scope badges, and query refresh button.
  - Data table presenting execution timestamp formatted in Asia/Jakarta (WIB) with relative time, trigger source badge, color-coded outcome badges (`Clean Pass`, `Finding Discovered`, `Execution Failed`), probe duration in ms, findings count pill, and expand/collapse actions.
  - Expandable row details preserving open states across query polling refetches via a reactive `Set<string>` of run IDs (`expandAll` and `collapseAll` supported).
  - Dark monospace diagnostic block for failure reasons/traces with a one-click copy button (`Copy Trace` -> `Copied!`).
  - Linked discovered issue references providing direct navigation to Issue Detail Workbench (`S-02` at `/issues/:id`).
  - Integrated `useInspectionRunsQuery` and `useDetectorDetailQuery` composables.
- Updated `src/frontend/src/app/router/index.ts` to register `detectors/:id/runs` route with `requiresAuth: true`.
- Exported `RunLogFilterBar` in `src/frontend/src/features/detectors/components/index.ts`.
- Verified compilation: `npm --prefix src/frontend run build` compiles cleanly with 0 TypeScript and 0 Vite errors.
Changed files:
- `src/frontend/src/features/detectors/components/RunLogFilterBar.vue`
- `src/frontend/src/features/detectors/components/index.ts`
- `src/frontend/src/features/detectors/views/InspectionRunLogView.vue`
- `src/frontend/src/app/router/index.ts`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

### P4-S17

Title: Cross-Feature Upgrades on S-01 Dashboard & S-02 Issue Workbench

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Upgrade `S-01 Issue Dashboard` with the conditional top Alert Strip (`G-1`), `EscalatedOnly` feed filter, and visual escalation chips; upgrade `S-02 Issue Detail Workbench` with header detector route navigation and automated webhook verification re-run trigger with graceful fallback to `M-03`.

Depends On: P3-S10, P3-S11, P4-S13, P4-S14  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/features/dashboard/views/DashboardView.vue` & `ActiveIssueFeed.vue` (`S-01`):
   - Conditional top Alert Strip rendered when `summary.escalatedCount > 0` or `summary.reopenedCount > 0`:
     - Alert text: `$"⚠ {summary.escalatedCount} issues escalated · {summary.reopenedCount} issues reopened"`.
     - Action link: `"Filter Escalated →"` activating `escalatedOnly = true` on the feed filter.
   - `DashboardFilterBar.vue`: Add `Escalated` toggle pill button.
   - Feed table rows: render amber `↑ Escalated (L{escalationLevel})` badge chip when `escalationLevel > 0`.
2. `src/frontend/src/features/issues/components/WorkbenchHeader.vue` (`S-02`):
   - Header detector badge: converted from plain text badge to active router link navigating to `/detectors/:detectorId` (`E-2`).
   - Action Bar updated for automated verification (`D-2`):
     - When issue is in `Investigating` with recorded remediation:
       - If issue's detector has a `WebhookUrl`: Action Bar primary button is `Trigger Verification Re-run` (with loading spinner); secondary button is `Manual Verification` (`M-03`).
       - If no webhook configured: Action Bar displays `Manual Verification` (`M-03`) as primary, with helper note `"No webhook configured for automated re-run"`.
       - Triggering webhook verification re-run dispatches `POST /api/issues/:id/verification/trigger-run`.
       - On success (`CleanPassed = true`): toast notification `"Verification Passed: No data anomalies detected"`, Verification Gate unlocks (`BR-PRIMA-004`).
       - On failure (`CleanPassed = false`): toast notification `"Verification Failed: Anomaly still present"`, Verification Gate remains locked.
3. Frontend builds cleanly via `npm run build` with zero TypeScript or Vite errors.

Notes: Implemented cross-feature upgrades across `S-01 Issue Dashboard` and `S-02 Issue Detail Workbench` per `GAP-007`, `DEC-010`, `D-2`, `E-2`, `G-1`, and `BR-PRIMA-004`:
- Updated `DashboardView.vue` with conditional top Alert Strip (`G-1`) rendering when `summary.escalatedCount > 0` or `summary.reopenedCount > 0`, displaying `⚠ {summary.escalatedCount} issues escalated · {summary.reopenedCount} issues reopened` with `"Filter Escalated →"` action button activating `escalatedOnly = true`.
- Updated `DashboardFilterBar.vue` with `Escalated` toggle pill button and filter reset support.
- Updated `useDashboardFeed.ts` with `escalatedOnly` parameter binding in TanStack Query.
- Updated `ActiveIssueFeed.vue` table rows to render amber `↑ Escalated (L{escalationLevel})` badge chips when `escalationLevel > 0`.
- Updated `WorkbenchHeader.vue` converting the header detector badge from plain text to an active Vue router link navigating to `/detectors/:detectorId` (`E-2`).
- Enhanced `WorkbenchHeader.vue` Action Bar with automated verification re-run support (`D-2`): when issue is in `Investigating` with recorded remediation, renders primary `Trigger Verification Re-run` button with loading spinner if the detector has a configured `WebhookUrl`, and `Manual Verification` (`M-03`) as secondary; gracefully falls back to `Manual Verification` as primary with `"No webhook configured for automated re-run"` helper note when no webhook is present.
- Updated `useIssueDetailQueries.ts` adding `useTriggerVerificationRunMutation` calling `POST /api/issues/:id/verification/trigger-run`.
- Updated `IssueDetailView.vue` connecting automated verification re-run mutation, showing toast notifications (`"Verification Passed: No data anomalies detected"` unlocking the `BR-PRIMA-004` Verification Gate on clean pass, or `"Verification Failed: Anomaly still present"` maintaining lock on failure).
- Updated `VerificationRecordCard.vue` supporting `AutomatedReRun` method display.
- Verified frontend compilation via `npm run build` with zero TypeScript or Vite errors, and backend integration tests (`dotnet test`) with all 294 tests passing.
Changed files:
- `src/frontend/src/features/dashboard/composables/useDashboardFeed.ts`
- `src/frontend/src/features/dashboard/components/DashboardFilterBar.vue`
- `src/frontend/src/features/dashboard/views/DashboardView.vue`
- `src/frontend/src/features/dashboard/components/ActiveIssueFeed.vue`
- `src/frontend/src/features/issues/components/WorkbenchHeader.vue`
- `src/frontend/src/features/issues/components/VerificationRecordCard.vue`
- `src/frontend/src/features/issues/api/useIssueDetailQueries.ts`
- `src/frontend/src/features/issues/views/IssueDetailView.vue`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

## P5 - Demo Seeding, Automated Testing & Verification

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P5-S18

Title: Development Demo Data Seeder Expansion

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Update `DevelopmentDemoDataSeeder.cs` to seed realistic demo detectors across all lifecycle/health states, historical runs with durations and failure reasons, and escalated issues with audit trails for local development and verification.

Depends On: P3-S11  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `DevelopmentDemoDataSeeder.cs` updated (gated on `IsDevelopment() && SeedDemoData == true`):
   - Seeds detectors covering all Stage 3 combinations:
     - 1 `Active` + `Healthy` detector with configured `WebhookUrl` and high pass rate (e.g. `DET-BILL-001`).
     - 1 `Active` + `Failing` detector with recent `ExecutionFailed` run and linked `FAIL:{id}` infrastructure issue.
     - 1 `Active` + `Silent` detector with missed heartbeat beyond 2× cadence and linked `SILENT:{id}` infrastructure issue.
     - 1 `Paused` detector.
     - 1 `Retired` detector with historical runs and active linked issues (`DEC-009`).
   - Seeds historical `dbo.InspectionRuns` (at least 30 runs per detector) covering `Passed`, `FindingDiscovered`, and `ExecutionFailed` outcomes.
   - Seeds at least 1 escalated issue (`EscalationLevel = 1` or `2`, `CurrentSeverity = High` or `Critical`) with corresponding `SeverityEscalated` activity logs.
2. Seeder is idempotent using `WHERE NOT EXISTS` or deterministic GUIDs.
3. Verified by running the application locally and confirming seeded detectors appear on `S-03` and navigate cleanly to `S-04` and `S-06`.
4. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Updated `DevelopmentDemoDataSeeder.cs` with full Stage 3 operational demo data coverage:
- Configured 6 deterministic detectors covering all lifecycle/health combinations:
  - `BillingDetectorId` (`3fa85f64-5717-4562-b3fc-2c963f66afa6`): `Active` + `Healthy`, `WebhookUrl` configured, high pass rate (> 90%).
  - `ReplicationDetectorId` (`b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c`): `Active` + `Failing`, recent `ExecutionFailed` run, linked `FAIL:{id}` infrastructure issue.
  - `AdmissionDetectorId` (`e2c04068-8314-469b-8326-880bebbff71d`): `Active` + `Silent`, missed cadence beyond 2× threshold (40m > 20m), linked `SILENT:{id}` infrastructure issue.
  - `LaboratoryDetectorId` (`c7b0d134-5e6f-6a7b-8c9d-0e1f2a3b4c5d`): `Paused`.
  - `RetiredDetectorId` (`f8c1e245-6f7a-7b8c-9d0e-1f2a3b4c5d6e`): `Retired` with active linked issue (`DEC-009`) in `Investigating` status.
  - `PrescriptionDetectorId` (`a5f8b912-3c4d-4e5f-6a7b-8c9d0e1f2a3b`): `Active` + `Healthy`, host for multi-stage escalated issue.
- Seeded historical `dbo.InspectionRuns` (at least 32 runs per detector, 45 runs for Billing) using deterministic GUIDs with `0xEE` namespace prefix, covering `Passed`, `FindingDiscovered`, and `ExecutionFailed` outcomes with realistic execution latencies and failure reasons.
- Seeded escalated issue (`EscalatedIssueId`, `RX-ESCALATED-099`) with `EscalationLevel = 2`, `CurrentSeverity = Critical`, and corresponding `SeverityEscalated` activity logs documenting SLA breach and recurrence threshold breaches.
- Ensured idempotency with `IF NOT EXISTS` / `UPDATE` clauses across all entities.
- Expanded `DevelopmentDemoDataSeederTests.cs` to assert all Stage 3 combinations, historical run distributions, escalation audit trails, double-seed idempotency, and query execution against `GetDetectorListHandler`, `GetDetectorSummaryHandler`, `GetDetectorByIdHandler`, and `GetInspectionRunsHandler`. Backend compiles with 0 errors and 0 warnings.
Changed files:
- `src/backend/PrIMA.Api/Seeding/DevelopmentDemoDataSeeder.cs`
- `src/backend/PrIMA.Api.Tests/Integration/DevelopmentDemoDataSeederTests.cs`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

### P5-S19

Title: Automated Domain, Worker & Integration Test Suites

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement comprehensive automated unit and integration tests under `src/backend/PrIMA.Api.Tests/` covering `Detector` domain methods, `Issue` severity escalation, Minimal API endpoints, webhook execution, supervisor silence/escalation loops, and execution failure telemetry ingestion.

Depends On: P2-S06, P3-S11, P5-S18  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Unit tests created:
   - `src/backend/PrIMA.Api.Tests/Domain/DetectorDomainTests.cs`: tests `UpdateConfiguration` (success and retired failure), `Pause`/`Resume`/`Retire` transitions, `IsSilenceThresholdExceeded` calculations, and health updates (`MarkSilent`, `MarkFailing`, `RecordSuccessfulRun`).
   - `src/backend/PrIMA.Api.Tests/Domain/IssueEscalationTests.cs`: tests `EscalateSeverity` (+1 stepping up to `Critical`), `CheckSlaEscalation`, and `CheckRecurrenceEscalation`.
2. Integration tests created:
   - `src/backend/PrIMA.Api.Tests/Integration/DetectorEndpointsTests.cs`: tests `GET /api/detectors`, `GET /api/detectors/summary`, `GET /api/detectors/{id}`, `POST /api/detectors`, `PUT /api/detectors/{id}`, `POST /api/detectors/{id}/toggle-status`, and `GET /api/detectors/{id}/runs`.
   - `src/backend/PrIMA.Api.Tests/Integration/WebhookExecutionEndpointsTests.cs`: tests manual run (`POST /api/detectors/{id}/trigger-run`) and automated verification re-run (`POST /api/issues/{id}/verification/trigger-run`) with simulated webhook responses (clean pass, finding discovered, timeout 504, error 502).
   - `src/backend/PrIMA.Api.Tests/Integration/MonitoringSupervisorWorkerTests.cs`: tests background silence detection, `SILENT:{id}` issue synthesis, cadence-multiplied occurrences (`DEC-005`), and dynamic SLA/recurrence escalation.
   - `src/backend/PrIMA.Api.Tests/Integration/TelemetryExecutionFailedTests.cs`: tests ingestion of `outcome == "ExecutionFailed"` producing `FAIL:{id}` issue, and recovery on subsequent clean telemetry resetting health to `Healthy` with recovery activity log (`DEC-004`).
3. All tests pass cleanly via `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj`.

Notes: Authored and verified comprehensive automated unit and integration test suites covering all Stage 3 requirements, business rules (`BR-PRIMA-003`, `BR-PRIMA-004`, `BR-PRIMA-005`, `BR-PRIMA-010`), and feasibility decisions (`DEC-001`–`DEC-010`):
- Created `DetectorDomainTests.cs` validating `UpdateConfiguration` (active success, retired rejection), `Pause`/`Resume`/`Retire` status transitions and guard rails, `IsSilenceThresholdExceeded` double-cadence calculations, and health state transitions (`MarkSilent`, `MarkFailing`, `RecordSuccessfulRun`).
- Created `IssueEscalationTests.cs` validating `EscalateSeverity` (+1 stepping capped at `Critical`), closed issue escalation rejections, `CheckSlaEscalation` window scaling (`timeout * (escalationLevel + 1)`) and unacknowledged guard, and `CheckRecurrenceEscalation` (threshold of 5 occurrences).
- Created `DetectorEndpointsTests.cs` exercising all 7 detector endpoints in complete end-to-end CRUD flows: authentication guards, registration (`POST /api/detectors`), detail retrieval (`GET /api/detectors/{id}`), configuration updates (`PUT /api/detectors/{id}`), status toggling (`POST /api/detectors/{id}/toggle-status`), summary metrics (`GET /api/detectors/summary`), portfolio querying with filters (`GET /api/detectors`), and run log history (`GET /api/detectors/{id}/runs`).
- Expanded `WebhookExecutionEndpointsTests.cs` with full coverage for `TriggerVerificationRun` under webhook timeout (504 Gateway Timeout), unreachability/5xx error (502 Bad Gateway), and execution failure (`CleanPassed = false`, gate locked), complementing existing manual probe run tests.
- Created `TelemetryExecutionFailedTests.cs` verifying ingestion of `Outcome == "ExecutionFailed"` producing synthetic `FAIL:{id}` infrastructure issues with occurrence #1, recurrence appending on repeated failures, and automatic recovery on subsequent clean telemetry resetting health to `Healthy` with `DetectorRecovered` activity logging while keeping the issue active for EDP verification (`DEC-004`).
- Configured `PrIMAApiFactory` to disable background supervisor worker polling during integration tests (`Supervisor:PollingIntervalSeconds = 3600`) to prevent race conditions during test execution.
- All 294 unit and integration tests pass cleanly via `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj`.
Changed files:
- `src/backend/PrIMA.Api.Tests/Domain/DetectorDomainTests.cs`
- `src/backend/PrIMA.Api.Tests/Domain/IssueEscalationTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/DetectorEndpointsTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/WebhookExecutionEndpointsTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/TelemetryExecutionFailedTests.cs`
- `src/backend/PrIMA.Api.Tests/Fixtures/PrIMAApiFactory.cs`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

### P5-S20

Title: End-to-End Build Verification & Autonomous Supervision Smoke Test

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Perform end-to-end build verification, frontend type-checking, and automated smoke test of the complete Stage 3 autonomous supervision, detector portfolio, and webhook verification loop against the local test database.

Depends On: P4-S17, P5-S18, P5-S19  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Backend solution compiles cleanly with zero warnings/errors via `dotnet build src/backend/PrIMA.slnx`.
2. Backend test suite passes 100% via `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj`.
3. Frontend compiles cleanly with zero TypeScript or Vite errors via `npm run build` in `src/frontend/`.
4. Automated end-to-end smoke test implemented in `src/backend/PrIMA.Api.Tests/Integration/EndToEndAutonomousSupervisionSmokeTests.cs`:
   - Registers new detector via `POST /api/detectors` with configured webhook URL.
   - Executes manual run via `POST /api/detectors/{id}/trigger-run`; verifies run appears in `GET /api/detectors/{id}/runs`.
   - Simulates silence breach past 2× cadence; executes supervisor pass and verifies detector health transitions to `Silent` with synthetic `SILENT:{id}` issue created.
   - Simulates SLA breach on unacknowledged issue; executes supervisor pass and verifies severity elevates by +1 step with `SeverityEscalated` activity log.
   - Ingests clean telemetry; verifies detector health recovers to `Healthy` and recovery activity log is appended to open infrastructure issue (`DEC-004`).
   - Ingests finding on `Investigating` issue, records remediation, triggers automated verification re-run via webhook, verifies Verification Gate unlocks on clean pass, and resolves issue.
   - Verifies dashboard feed includes escalated counts and `escalatedOnly` filter returns expected issues.

Notes: Delivers the **Autonomous Proactive Supervision & Self-Monitoring** milestone verification:
- Implemented `EndToEndAutonomousSupervisionSmokeTests.cs` executing the complete Stage 3 autonomous loop end-to-end:
  1. Registers new detector (`POST /api/detectors`) with webhook URL, verifying `Active` + `Healthy` initial state.
  2. Executes manual run (`POST /api/detectors/{id}/trigger-run`) via mock webhook, verifying run persistence in `GET /api/detectors/{id}/runs`.
  3. Simulates silence breach past 2× cadence, executes supervisor pass, verifying transition to `Silent` and creation of synthetic `SILENT:{id}` infrastructure issue.
  4. Simulates unacknowledged SLA timeout breach, executes supervisor pass, verifying dynamic severity escalation (+1 step from `High` to `Critical`) and atomic `SeverityEscalated` activity logging.
  5. Ingests clean telemetry, verifying detector health recovery to `Healthy` and `DetectorRecovered` activity logging while preserving the infrastructure issue open (`DEC-004`).
  6. Ingests finding on `Investigating` issue, records remediation (`M-02`), executes automated webhook verification re-run (`D-2`), verifies `BR-PRIMA-004` Verification Gate unlocks on clean pass, and resolves the issue.
  7. Queries dashboard feed verifying escalated counts and `escalatedOnly` filter isolation.
- Verified backend solution compiles cleanly with 0 warnings and 0 errors via `dotnet build src/backend/PrIMA.slnx`.
- Verified backend test suite passes 100% (295 passed, 0 failed, 0 skipped) via `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj`.
- Verified frontend compiles cleanly with 0 TypeScript and 0 Vite errors via `npm run build` in `src/frontend/`.
Changed files:
- `src/backend/PrIMA.Api.Tests/Integration/EndToEndAutonomousSupervisionSmokeTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/DetectorWebhookClientTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/TelemetryExecutionFailedTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/EndToEndManualResolutionSmokeTests.cs`
- `docs/issues/IS003-PRIMA-STAGE-3-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- 2026-11-21: Initial Implementation Plan created for PrIMA Stage 3 (`Code: PRIMA-STAGE-3`), structured into 5 phases and 20 slices following `docs/issues/IS003-PRIMA-STAGE-3-ARCHITECTURE.md` and approved feasibility decisions. Execution Approval granted (`APPROVED`).
