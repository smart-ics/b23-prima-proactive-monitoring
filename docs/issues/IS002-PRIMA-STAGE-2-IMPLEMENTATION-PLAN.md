---
Title: PrIMA Stage 2 — Issue Triage, Investigation & Verification-Gated Resolution Implementation Plan
Code: PRIMA-STAGE-2
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-11-21
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Stage 2 ("Issue Triage, Investigation & Verification-Gated Resolution") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`), establishing the full operational resolution workbench for EDP officers and delivering the **Complete End-to-End Manual Issue Resolution Loop** milestone.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:
- ISSUE: `docs/issues/IS002-PRIMA-STAGE-2-ISSUE.md`
- FEATURE: `docs/PRIMA-DEVELOPMENT-ROADMAP.md` (Stage 2), `docs/PRIMA-USE-CASE-DISCOVERY.md` (A-3, B-1, B-2, B-3, C-1, C-2, C-3, D-1, D-3, D-4), `docs/PRIMA-SCREEN-INVENTORY.md` (S-02, M-01, M-02, M-03), `docs/PRIMA-UI-UX-GUIDELINE.md`
- ARCHITECTURE: `docs/issues/IS002-PRIMA-STAGE-2-ARCHITECTURE.md` (and baseline `docs/PRIMA-ARCHITECTURE.md`)
- FEASIBILITY-ASSESSMENT: `docs/issues/IS002-PRIMA-STAGE-2-FEASIBILITY-ASSESSMENT.md` (`Status: READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED  
The target architecture artifact `docs/issues/IS002-PRIMA-STAGE-2-ARCHITECTURE.md` authoritatively defines the technical realization, domain aggregate invariants (`BR-PRIMA-004`, `BR-PRIMA-006`, `BR-PRIMA-008`), database migration `003_IssueActivityLogs.sql`, audit trail transactional persistence (`BR-PRIMA-009`), Minimal API command and composite query endpoints, verification invalidation on active recurrence and remediation retry (`GAP-005`, `OQ-005`), and frontend `S-02` Issue Detail Workbench components and modals (`M-01`–`M-03`).

---

# 2. Planning Scope

The scope of this implementation plan encompasses the complete delivery of Stage 2 across backend, database, frontend, tooling, and test automation:

1. **Database Migration & Audit Trail Schema:**
   - Migration script `003_IssueActivityLogs.sql` creating `dbo.IssueActivityLogs` table and composite index `IX_IssueActivityLogs_IssueId_Time` on `(IssueId, TimestampUtc DESC)`.
   - Shared atomic audit logging helper (`InsertActivityLogAsync`) recording immutable lifecycle events within primary command and ingestion database transactions.

2. **Backend Domain Aggregate Invariant Enforcement (`PrIMA.Api/Domain/Issues/Issue.cs`):**
   - Explicit lifecycle command methods: `TakeIt`, `Assign`, `Acknowledge`, `StartInvestigation`, `AddNote`, `RecordRemediation`, `RecordVerification`, `MarkResolved`, `Dismiss`.
   - Enforcement of Verification Gate (`BR-PRIMA-004`): `MarkResolved` strictly requires an un-superseded `CleanPassed` verification record, returning `409 Conflict` (ProblemDetails) on failure.
   - Enforcement of Dismissal Justification (`BR-PRIMA-006`): `Dismiss` strictly requires trimmed rationale length `>= 20` characters, returning `422 Unprocessable Entity` on failure.
   - Encapsulated child collections for investigation notes, remediation records, verification records, dismissal records, and activity logs.
   - Verification invalidation rules (`GAP-005`, `OQ-005`): marking active verifications `IsSuperseded = 1` when telemetry recurrence arrives on an active `Investigating` issue, and superseding prior failed verifications upon remediation retry.

3. **Backend Minimal API Endpoints (`PrIMA.Api/Features/`):**
   - `GET /api/officers`: Lookup of active EDP officers from `dbo.Officers` where `IsActive = 1`.
   - `GET /api/issues/{id}`: High-performance composite query projection via Dapper `QueryMultipleAsync` returning 7 result sets in a single round-trip.
   - `POST /api/issues/{id}/take-it`: Single-action triage (acknowledge + assign to caller + transition to `Investigating`).
   - `POST /api/issues/{id}/assign`: Explicit assignment to an active officer (transitions `Open`/`Reopened` to `Acknowledged`; preserves `Investigating` status).
   - `POST /api/issues/{id}/acknowledge`: Explicit acknowledgement without changing assignment.
   - `POST /api/issues/{id}/start-investigation`: Assigned officer begins active investigation (`Acknowledged` -> `Investigating`).
   - `POST /api/issues/{id}/notes`: Append-only investigation notes.
   - `POST /api/issues/{id}/remediation`: Record operational remediation action (`M-02`) and supersede prior verifications.
   - `POST /api/issues/{id}/verification/manual`: Record manual verification outcome (`M-03`, `CleanPassed` vs. `AnomalyStillPresent`).
   - `POST /api/issues/{id}/resolve`: Verification-gated resolution transition to `Resolved`.
   - `POST /api/issues/{id}/dismiss`: Documented issue dismissal (`M-01`).

4. **Frontend Feature Slice (`src/frontend/src/features/issues/`):**
   - Single-screen operational workbench `IssueDetailView.vue` registered at `/issues/:id`.
   - Desktop 2-column asymmetric grid (`lg:grid-cols-12`):
     - Left column (8 cols): `WorkbenchHeader` (sticky top header, badges, copyable correlation key, detector tooltip badge, context-sensitive Action Bar), `LatestEvidenceCard` (metadata grid, null-safe IDR financial comparison strip, dark monospace trace block with copy button), `OccurrencesTimeline` (collapsible list, newest-first, inline evidence expansion), `InvestigationNotesStream` (chronological append-only notes stream, `Ctrl+Enter` composer, draft preservation).
     - Right column (4 cols): `VerificationGateCard` (3-step checklist, locked/unlocked indicator, solid emerald banner when resolved), `VerificationRecordCard`, `RemediationHistoryCard`, `DismissalRecordCard`, `OwnershipTimestampsCard` (with `Reassign` button), `ActivityLogAccordion` (collapsed-by-default audit trail).
   - Workflow dialogs: `DismissIssueModal.vue` (`M-01`, live min 20 char counter), `RecordRemediationModal.vue` (`M-02`, category select + SOP description), `ManualVerificationModal.vue` (`M-03`, CleanPassed vs. AnomalyStillPresent radio cards), `AssignOfficerModal.vue` (officer select).
   - TanStack Query composable `useIssueDetail.ts`: 10-second non-disruptive polling, form draft preservation, dialog isolation, and query cache invalidation.

5. **Dashboard S-01 Linkage & Router Navigation:**
   - Table rows in `ActiveIssueFeed.vue` clickable to navigate to `/issues/:id` (excluding copy buttons).
   - Header back navigation in `WorkbenchHeader.vue` returning to `/` (Dashboard).

6. **Development Tooling, Seeding & Automated Tests:**
   - Updated `DevelopmentDemoDataSeeder.cs` seeding realistic demo issues representing all Stage 2 lifecycle states.
   - Automated unit test suite covering domain aggregate invariants (`IssueLifecycleTests.cs`).
   - Automated integration test suite covering API endpoints, verification gate, dismissal validation, recurrence invalidation, and audit persistence (`IssueLifecycleEndpointsTests.cs`).
   - End-to-end smoke verification of the complete manual issue resolution loop.

**Explicitly Out of Scope (Deferred to Later Stages):**
- Stage 3: Automated detector portfolio screens (`S-03`–`S-06`), background supervisor worker (`MonitoringSupervisorWorker`), cadence silence detection (`BR-PRIMA-010`), SLA/recurrence escalation (`BR-PRIMA-003`), `ExecutionFailed` infrastructure anomaly synthesis (`BR-PRIMA-005`), top Alert Strip on `S-01`, and detector webhook execution (`D-2`).
- Stage 4: Historical retrospective screens (`S-07`), reliability KPIs, MTTR analytics, and v1.0 quality gate.
- Stage 5: SIMRS pilot detector suite and hospital on-premise production rollout (`BR-PRIMA-007`).

---

# 3. Dependencies

## External Prerequisites
- .NET 8 SDK installed on host.
- Node.js (v20+ or v22 LTS) and npm installed on host.
- Microsoft SQL Server 2022 (local instance accessible via connection string configured in `appsettings.Development.json`).
- Stage 1 platform foundation verified and operational in codebase.

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
| P1 - Database Migration & Core Domain Model | COMPLETED | GO | 2/2 |
| P2 - Persistence Layer & Ingestion Invalidation | COMPLETED | GO | 2/2 |
| P3 - Backend API Features & Officer Directory | COMPLETED | GO | 6/6 |
| P4 - Frontend UI Features & Workflow Modals | COMPLETED | GO | 5/5 |
| P5 - Demo Seeding, Automated Testing & Verification | COMPLETED | GO | 3/3 |

---

# 5. Phases

## P1 - Database Migration & Core Domain Model

Implementation Status: COMPLETED  
Review Status: GO

### P1-S01

Title: Database Migration 003_IssueActivityLogs & Audit Schema

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Create DbUp migration script `003_IssueActivityLogs.sql` establishing the `dbo.IssueActivityLogs` table, foreign keys, and composite index.

Depends On: None  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api/Migrations/Scripts/003_IssueActivityLogs.sql` is created as an embedded resource matching Target Architecture §8.1:
   - Table `dbo.IssueActivityLogs`:
     - `Id UNIQUEIDENTIFIER PRIMARY KEY`
     - `IssueId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Issues(Id)`
     - `ActivityType NVARCHAR(50) NOT NULL`
     - `ActorName NVARCHAR(100) NOT NULL`
     - `TimestampUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()`
     - `Summary NVARCHAR(500) NOT NULL`
     - `DetailsJson NVARCHAR(MAX) NULL`
   - Composite index: `CREATE INDEX IX_IssueActivityLogs_IssueId_Time ON dbo.IssueActivityLogs(IssueId, TimestampUtc DESC);`
2. Migration executes cleanly on startup via `MigrationRunner.cs` without altering existing Stage 1 tables.
3. Verification query against SQL Server confirms `dbo.IssueActivityLogs` table and index exist.

Notes: Implemented `003_IssueActivityLogs.sql` embedded resource script. Verified compilation in `PrIMA.Api.dll`, clean migration execution via DbUp in both development (`PrIMA`) and test (`PrIMA_IntegrationTests`) databases, table column definitions, and index `IX_IssueActivityLogs_IssueId_Time`. All 43 integration and unit tests pass cleanly.

---

### P1-S02

Title: Domain Aggregate Expansion & Invariant Enforcement

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Expand the `Issue` aggregate root (`PrIMA.Api/Domain/Issues/Issue.cs`) to encapsulate full lifecycle progression, child governance collections, verification gate enforcement (`BR-PRIMA-004`), dismissal rationale validation (`BR-PRIMA-006`), and domain error types.

Depends On: None  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Domain types and enums created or expanded under `src/backend/PrIMA.Api/Domain/Issues/`:
   - `RemediationActionCategory.cs`: enum `DataCorrection`, `ServiceRestart`, `ConfigurationAdjustment`, `CodeRemediationRequested`.
   - `DismissalReason.cs`: enum `FalsePositive`, `AcceptedOperationalException`, `DuplicateManualReport`.
   - `InvestigationNote.cs`: entity with `Id`, `IssueId`, `OfficerName`, `NoteText`, `CreatedAtUtc`.
   - `RemediationRecord.cs`: entity/value object with `Id`, `IssueId`, `ActionCategory`, `SummaryDescription`, `PerformedBy`, `TimestampUtc`, `IsSuperseded`.
   - `VerificationRecord.cs`: entity/value object with `Id`, `IssueId`, `VerificationMethod`, `CleanPassed`, `EvidenceNotes`, `VerifiedBy`, `TimestampUtc`, `IsSuperseded`.
   - `DismissalRecord.cs`: entity/value object with `Id`, `IssueId`, `DismissalReason`, `JustificationRationale`, `DismissedBy`, `TimestampUtc`, `IsSuperseded`.
2. `Issue.cs` domain methods implemented returning `Result`:
   - `TakeIt(string officerName, DateTime timestampUtc)`: Transitions to `Investigating`, sets `CurrentAssignee = officerName`, sets `AcknowledgedAtUtc ??= timestampUtc`. Fails if `Resolved` or `Dismissed`.
   - `Assign(string officerName, DateTime timestampUtc)`: Sets `CurrentAssignee = officerName`. If `Open` or `Reopened`, transitions to `Acknowledged` and sets `AcknowledgedAtUtc = timestampUtc`. If already `Investigating`, preserves status (`OQ-007`). Fails if `Resolved` or `Dismissed`.
   - `Acknowledge(string officerName, DateTime timestampUtc)`: Transitions `Open`/`Reopened` to `Acknowledged`, sets `AcknowledgedAtUtc = timestampUtc`.
   - `StartInvestigation(string officerName, DateTime timestampUtc)`: Transitions `Acknowledged` to `Investigating`, sets `CurrentAssignee = officerName`.
   - `AddNote(string noteText, string officerName, DateTime timestampUtc)`: Appends to internal `_notes` list. Fails if closed or note text is whitespace.
   - `RecordRemediation(string category, string description, string officerName, DateTime timestampUtc)`: Requires `Status == Investigating`. Validates description. Sets `LatestRemediation`. Clears/supersedes prior verification record (`GAP-005`).
   - `RecordVerification(string method, bool cleanPassed, string evidenceNotes, string verifiedBy, DateTime timestampUtc)`: Requires `Status == Investigating` and `LatestRemediation != null`. Validates evidenceNotes. Sets `LatestVerification`.
   - `MarkResolved(string officerName, DateTime timestampUtc)`: Enforces Verification Gate (`BR-PRIMA-004`). Fails with `IssueErrors.VerificationGateFailed` if `LatestVerification == null`, `!LatestVerification.CleanPassed`, or `LatestVerification.IsSuperseded`. Sets `Status = Resolved`, `ResolvedAtUtc = timestampUtc`.
   - `Dismiss(string reason, string rationale, string officerName, DateTime timestampUtc)`: Enforces `BR-PRIMA-006`. Fails with `IssueErrors.DismissalRationaleTooShort` if trimmed rationale length `< 20`. Sets `Status = Dismissed`, `DismissedAtUtc = timestampUtc`, sets `Dismissal`.
3. `IssueErrors.cs` updated with error codes: `VerificationGateFailed` (409), `DismissalRationaleTooShort` (422), `RemediationRequiredBeforeVerification` (409), `EmptyRemediationDescription` (422), `EmptyVerificationEvidence` (422), `EmptyNoteText` (422), `InvalidStateTransition` (409).
4. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Enforces `BR-PRIMA-004`, `BR-PRIMA-006`, `BR-PRIMA-008`, `OQ-002`, `OQ-005`, and `OQ-007`. Fully implemented and verified with clean compilation and all domain unit tests passing.

---

## P2 - Persistence Layer & Ingestion Invalidation

Implementation Status: COMPLETED  
Review Status: GO

### P2-S03

Title: Dapper Persistence Methods & Audit Log Helper

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement repository persistence methods in `IssueRepository.cs` and `IIssueRepository.cs` supporting lifecycle commands, child entity insertions, verification supersession, and atomic audit trail persistence in `dbo.IssueActivityLogs`.

Depends On: P1-S01, P1-S02  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `IIssueRepository.cs` and `IssueRepository.cs` are extended with methods supporting ambient or explicit `IDbTransaction`:
   - `TakeItAsync(Guid issueId, string officerName, DateTime timestampUtc, IDbConnection conn, IDbTransaction tx)`
   - `AssignAsync(Guid issueId, string officerName, IssueStatus newStatus, DateTime timestampUtc, IDbConnection conn, IDbTransaction tx)`
   - `AcknowledgeAsync(Guid issueId, string officerName, DateTime timestampUtc, IDbConnection conn, IDbTransaction tx)`
   - `StartInvestigationAsync(Guid issueId, string officerName, DateTime timestampUtc, IDbConnection conn, IDbTransaction tx)`
   - `AddNoteAsync(InvestigationNote note, IDbConnection conn, IDbTransaction tx)`
   - `RecordRemediationAsync(RemediationRecord remediation, IDbConnection conn, IDbTransaction tx)`
   - `RecordVerificationAsync(VerificationRecord verification, IDbConnection conn, IDbTransaction tx)`
   - `ResolveIssueAsync(Guid issueId, DateTime resolvedAtUtc, IDbConnection conn, IDbTransaction tx)`
   - `DismissIssueAsync(Guid issueId, DismissalRecord dismissal, IDbConnection conn, IDbTransaction tx)`
   - `SupersedeActiveVerificationsAsync(Guid issueId, IDbConnection conn, IDbTransaction tx)` executing `UPDATE dbo.VerificationRecords SET IsSuperseded = 1 WHERE IssueId = @IssueId AND IsSuperseded = 0;`
   - `GetIssueWithGovernanceByIdAsync(Guid issueId, IDbConnection conn, IDbTransaction? tx = null)` returning hydrated `Issue` aggregate with active child governance records for command authorization.
2. Shared audit log helper method implemented:
   - `InsertActivityLogAsync(IDbConnection conn, IDbTransaction tx, Guid issueId, string activityType, string actorName, string summary, string? detailsJson = null)` inserting into `dbo.IssueActivityLogs`.
3. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented all repository methods in `IIssueRepository.cs` and `IssueRepository.cs` supporting explicit and ambient connection/transaction contexts. Implemented `GetIssueWithGovernanceByIdAsync` multi-query projection hydrating active child records (remediation, verification, dismissal, notes). Implemented shared audit logging helper `InsertActivityLogAsync` (instance & static). Added full test suite in `IssueRepositoryTests.cs`. All 55 tests pass cleanly.

---

### P2-S04

Title: Active Recurrence Verification Invalidation in Telemetry Ingestion

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Update `IngestTelemetryHandler.cs` so that when a recurring finding matches an active `Investigating` issue, any active `VerificationRecords` are marked as `IsSuperseded = 1` inside the ingestion SQL transaction.

Depends On: P2-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `IngestTelemetryHandler.cs` finding processing loop updated:
   - When matching issue is active and `issue.Status == IssueStatus.Investigating`:
     - Invokes `_issueRepository.SupersedeActiveVerificationsAsync(issue.Id, conn, tx)` within the active ingestion transaction.
     - Writes activity log entry via `InsertActivityLogAsync` (`ActivityType: "VerificationInvalidated"` or details in summary) documenting that recurrence occurred during investigation.
2. Verifies that existing Stage 1 tests continue passing and telemetry ingestion preserves backward-compatibility.
3. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Updated `IngestTelemetryHandler.cs` finding processing loop: when recurrence matches an `Investigating` issue, executes `_issueRepository.SupersedeActiveVerificationsAsync` and records an atomic activity log with `ActivityType: "VerificationInvalidated"` inside the ingestion SQL transaction (`GAP-005`, `OQ-005`). Also added `Created` and `Reopened` activity log entries during telemetry ingestion. Added comprehensive integration tests in `IngestionEndpointsTests.cs`. All 56 tests pass cleanly.

---

## P3 - Backend API Features & Officer Directory

Implementation Status: COMPLETED  
Review Status: GO

### P3-S05

Title: Officer Directory Feature (GET /api/officers)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `GET /api/officers` in `Features/Officers/` returning active EDP officers (`Id`, `Username`, `FullName`, `Role`) from `dbo.Officers` for assignment dropdowns.

Depends On: P1-S01  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. DTO created: `src/backend/PrIMA.Api/Features/Officers/OfficerDto.cs` (`Guid Id`, `string Username`, `string FullName`, `string Role`).
2. Handler created: `src/backend/PrIMA.Api/Features/Officers/GetOfficersHandler.cs` executing direct Dapper query:
   `SELECT Id, Username, FullName, Role FROM dbo.Officers WHERE IsActive = 1 ORDER BY FullName ASC;`
3. Endpoint mapped: `src/backend/PrIMA.Api/Features/Officers/GetOfficersEndpoint.cs` mapping `GET /api/officers` secured via `.RequireAuthorization()`.
4. Endpoint documented with OpenAPI metadata (`WithTags("Officers")`, `Produces<List<OfficerDto>>(200)`).
5. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented `OfficerDto`, `GetOfficersHandler`, and `GetOfficersEndpoint` under `Features/Officers/` and mapped in `Program.cs`. Query selects active officers sorted by `FullName ASC`. Validated with integration tests in `OfficerEndpointsTests.cs` (401 Unauthorized check, 200 OK ordering verification, inactive officer exclusion). All tests pass. Implements `GAP-004` and supports `OQ-002`/`OQ-003`.

---

### P3-S06

Title: Composite Detail Query Feature (GET /api/issues/{id})

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `GET /api/issues/{id}` executing a single-roundtrip Dapper `QueryMultipleAsync` returning composite `IssueDetailDto` containing metadata, occurrences, notes, remediations, verifications, dismissal, and activity logs.

Depends On: P2-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. DTOs created under `src/backend/PrIMA.Api/Features/Issues/GetIssueById/`:
   - `IssueDetailDto.cs` combining all 7 sections.
   - `OccurrenceItemDto.cs`, `InvestigationNoteDto.cs`, `RemediationRecordDto.cs`, `VerificationRecordDto.cs`, `DismissalRecordDto.cs`, `IssueActivityLogDto.cs`.
2. Handler created: `GetIssueByIdHandler.cs` executing the 7-statement SQL batch via `QueryMultipleAsync` matching Target Architecture §4.3:
   - Result 1: Issue metadata (`dbo.Issues`)
   - Result 2: Occurrences (`dbo.Occurrences` ORDER BY `OccurrenceNumber DESC`)
   - Result 3: Notes (`dbo.InvestigationNotes` ORDER BY `CreatedAtUtc ASC`)
   - Result 4: Remediation records (`dbo.RemediationRecords` ORDER BY `TimestampUtc DESC`)
   - Result 5: Verification records (`dbo.VerificationRecords` ORDER BY `TimestampUtc DESC`)
   - Result 6: Active dismissal record (`dbo.DismissalRecords` WHERE `IsSuperseded = 0`)
   - Result 7: Activity logs (`dbo.IssueActivityLogs` ORDER BY `TimestampUtc DESC`)
3. Returns `404 Not Found` ProblemDetails if issue does not exist.
4. Endpoint mapped: `GetIssueByIdEndpoint.cs` mapping `GET /api/issues/{id}` secured via `.RequireAuthorization()`.
5. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implements `OQ-008` and `GAP-001`. Created 7 section DTOs under `src/backend/PrIMA.Api/Features/Issues/GetIssueById/`, `GetIssueByIdHandler` executing single round-trip `QueryMultipleAsync` 7-statement SQL batch, and `GetIssueByIdEndpoint` mapping `GET /api/issues/{id}` secured with `.RequireAuthorization()`. Registered handler and endpoint in `Program.cs`. Added integration test suite in `GetIssueByIdEndpointTests.cs` validating 401 unauthorized, 404 not found problem details, and full 7-section composite projection matching architecture ordering rules. All tests pass cleanly.

---

### P3-S07

Title: Issue Triage & Assignment Command Endpoints

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement triage and assignment command endpoints: `POST /api/issues/{id}/take-it`, `POST /api/issues/{id}/assign`, `POST /api/issues/{id}/acknowledge`, and `POST /api/issues/{id}/start-investigation` with atomic activity audit logging.

Depends On: P2-S03, P3-S05  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `TakeItEndpoint.cs` & `TakeItHandler.cs`:
   - Route `POST /api/issues/{id}/take-it` with zero request body.
   - Authenticated: extracts officer name from `CurrentOfficer`.
   - Transitions issue to `Investigating`, sets `CurrentAssignee = officerName`, writes `ActivityType = "InvestigationStarted"` (or `Assigned`) to `dbo.IssueActivityLogs`.
   - Returns `204 NoContent`, or `409 Conflict` if issue is closed.
2. `AssignIssueEndpoint.cs` & `AssignIssueHandler.cs`:
   - Route `POST /api/issues/{id}/assign` accepting `AssignIssueRequest { Guid OfficerId }`.
   - Validates `OfficerId` exists in `dbo.Officers` where `IsActive = 1`; returns `422 Unprocessable Entity` if not found.
   - Updates `CurrentAssignee`. If `Open`/`Reopened`, transitions to `Acknowledged` and sets `AcknowledgedAtUtc = now`. If `Investigating`, preserves status (`OQ-007`).
   - Writes `ActivityType = "Assigned"` (or `"Reassigned"`) to `dbo.IssueActivityLogs`.
   - Returns `204 NoContent`.
3. `AcknowledgeIssueEndpoint.cs` & `AcknowledgeIssueHandler.cs`:
   - Route `POST /api/issues/{id}/acknowledge`.
   - Moves `Open`/`Reopened` -> `Acknowledged`, sets `AcknowledgedAtUtc = now`, writes activity log. Returns `204 NoContent`.
4. `StartInvestigationEndpoint.cs` & `StartInvestigationHandler.cs`:
   - Route `POST /api/issues/{id}/start-investigation`.
   - Moves `Acknowledged` -> `Investigating`, sets caller as assignee if unassigned, writes activity log. Returns `204 NoContent`.
5. All endpoints secured via `.RequireAuthorization()`.
6. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented all four command endpoints and handlers under `Features/Issues/TakeIt/`, `Features/Issues/AssignIssue/`, `Features/Issues/AcknowledgeIssue/`, and `Features/Issues/StartInvestigation/` with atomic SQL transactions and audit activity logging into `dbo.IssueActivityLogs`. Added `GetCurrentOfficerName` helper to `CurrentOfficerExtensions`. Added comprehensive integration test suite `IssueTriageCommandEndpointsTests.cs` (20 test cases covering authentication, 404, 409, 422, state transitions, assignee retention, and audit logs). All 80 backend tests pass cleanly. Implements `GAP-001`, `OQ-002`, `OQ-003`, `OQ-007`, and `BR-PRIMA-008`.

---

### P3-S08

Title: Investigation Notes, Remediation & Verification Command Endpoints

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement diagnostic and corrective action command endpoints: `POST /api/issues/{id}/notes`, `POST /api/issues/{id}/remediation` (with verification supersession), and `POST /api/issues/{id}/verification/manual` with atomic activity audit logging.

Depends On: P2-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `AddNoteEndpoint.cs` & `AddNoteHandler.cs`:
   - Route `POST /api/issues/{id}/notes` accepting `AddNoteRequest { string NoteText }`.
   - FluentValidation ensures non-empty note text; returns `422` if empty.
   - Appends to `dbo.InvestigationNotes` with caller identity and UTC timestamp; writes `ActivityType = "NoteAdded"` to `dbo.IssueActivityLogs`.
   - Returns `201 Created` with `NoteDto`.
2. `RecordRemediationEndpoint.cs` & `RecordRemediationHandler.cs`:
   - Route `POST /api/issues/{id}/remediation` accepting `RecordRemediationRequest { string ActionCategory, string SummaryDescription }`.
   - Validates category is valid enum (`DataCorrection`, `ServiceRestart`, `ConfigurationAdjustment`, `CodeRemediationRequested`) and description non-empty.
   - Issue must be in `Investigating` status (returns `409` if not).
   - Inserts into `dbo.RemediationRecords`.
   - Supersedes prior verification records (`UPDATE dbo.VerificationRecords SET IsSuperseded = 1 WHERE IssueId = @IssueId AND IsSuperseded = 0;`).
   - Writes `ActivityType = "RemediationRecorded"` to `dbo.IssueActivityLogs`.
   - Returns `204 NoContent`.
3. `RecordManualVerificationEndpoint.cs` & `RecordManualVerificationHandler.cs`:
   - Route `POST /api/issues/{id}/verification/manual` accepting `RecordManualVerificationRequest { bool CleanPassed, string EvidenceNotes }`.
   - FluentValidation ensures evidence notes non-empty.
   - Issue must be in `Investigating` status with `LatestRemediation != null` (returns `409` if not).
   - Inserts into `dbo.VerificationRecords`.
   - Writes `ActivityType = "VerificationRecorded"` to `dbo.IssueActivityLogs` (summary notes whether CleanPassed or AnomalyStillPresent).
   - Returns `204 NoContent`.
4. All endpoints secured via `.RequireAuthorization()`.
5. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented `AddNoteEndpoint`, `RecordRemediationEndpoint`, and `RecordManualVerificationEndpoint` with their respective handlers, FluentValidation validators, and request/response DTOs. Enforced verification supersession upon remediation recording, validation against domain rules, and atomic activity audit log insertion. Registered handlers and endpoints in `Program.cs`. Added comprehensive integration tests covering all positive, validation (422), authorization (401), missing issue (404), and state conflict (409) scenarios. All 113 tests in the test suite pass cleanly.

---

### P3-S09

Title: Issue Resolution & Dismissal Command Endpoints

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement closure command endpoints: `POST /api/issues/{id}/resolve` (enforcing Verification Gate `BR-PRIMA-004`) and `POST /api/issues/{id}/dismiss` (enforcing min 20 chars rationale `BR-PRIMA-006`) with atomic activity audit logging.

Depends On: P2-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `ResolveIssueEndpoint.cs` & `ResolveIssueHandler.cs`:
   - Route `POST /api/issues/{id}/resolve` with zero body.
   - Issue must be in `Investigating` status.
   - Verification Gate Enforcement (`BR-PRIMA-004`): checks `dbo.VerificationRecords` for an un-superseded (`IsSuperseded = 0`) record with `CleanPassed = 1`. If absent or failed, returns `409 Conflict` RFC 7807 ProblemDetails ("Cannot resolve issue. Mandatory verification gate requires a clean, passed verification record.").
   - Updates `dbo.Issues` setting `Status = 'Resolved'`, `ResolvedAtUtc = now`.
   - Writes `ActivityType = "Resolved"` to `dbo.IssueActivityLogs`.
   - Returns `204 NoContent`.
2. `DismissIssueEndpoint.cs` & `DismissIssueHandler.cs`:
   - Route `POST /api/issues/{id}/dismiss` accepting `DismissIssueRequest { string DismissalReason, string JustificationRationale }`.
   - Validates reason is valid enum (`FalsePositive`, `AcceptedOperationalException`, `DuplicateManualReport`).
   - Dismissal Rationale Validation (`BR-PRIMA-006`): trimmed rationale length must be `>= 20` characters; returns `422 Unprocessable Entity` ProblemDetails if under 20 chars.
   - Issue must be in active status (`Open`, `Acknowledged`, `Investigating`, `Reopened`); returns `409 Conflict` if already closed.
   - Updates `dbo.Issues` setting `Status = 'Dismissed'`, `DismissedAtUtc = now`.
   - Inserts into `dbo.DismissalRecords`.
   - Writes `ActivityType = "Dismissed"` to `dbo.IssueActivityLogs`.
   - Returns `204 NoContent`.
3. All endpoints secured via `.RequireAuthorization()`.
4. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Implemented `ResolveIssueEndpoint.cs` and `ResolveIssueHandler.cs` enforcing `BR-PRIMA-004` verification gate check (un-superseded `CleanPassed = 1` verification record requirement, returning 409 ProblemDetails on failure), resolving issue and appending immutable activity audit log atomically within SQL transaction. Implemented `DismissIssueEndpoint.cs`, `DismissIssueHandler.cs`, `DismissIssueRequest.cs`, and `DismissIssueValidator.cs` enforcing `BR-PRIMA-006` (enum validation for `FalsePositive`, `AcceptedOperationalException`, `DuplicateManualReport` and trimmed rationale length >= 20 chars, returning 422 ProblemDetails on failure; and requiring active issue status, returning 409 Conflict if already closed). Registered handlers and mapped endpoints in `Program.cs`. Created full integration test suite in `IssueResolutionAndDismissalEndpointsTests.cs` (17 tests covering authorization, validations, gate checks, state transitions, database updates, and audit logging), all passing cleanly. Clean compilation with 0 warnings/errors.

---

### P3-S10

Title: API Endpoint Registration, OpenAPI Metadata & Contract Generation

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Register all new Stage 2 endpoints and handlers in `Program.cs`, configure OpenAPI Swagger metadata, and synchronize frontend TypeScript contracts via code generation.

Depends On: P3-S05, P3-S06, P3-S07, P3-S08, P3-S09  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `Program.cs` updated:
   - Registers all new command and query handlers in DI.
   - Maps route groups `app.MapOfficerEndpoints()` and `app.MapIssueLifecycleEndpoints()`.
2. OpenAPI Swagger document at `/swagger/v1/swagger.json` verified to include all 10 new endpoints with request/response schemas, parameter types, and RFC 7807 error responses.
3. API contract synchronization executed:
   - Run `npm run generate:api` (or equivalent `openapi-typescript` script) in `src/frontend/`.
   - Generates updated `src/frontend/src/shared/types/api.generated.ts` containing all new Stage 2 types.
4. Backend compiles cleanly (`dotnet build`) and frontend type-checking passes (`npm run build` or `vue-tsc`).

Notes: Implemented `IssueLifecycleEndpoints.cs` grouping all 10 Stage 2 issue lifecycle endpoints and registered `app.MapOfficerEndpoints()` and `app.MapIssueLifecycleEndpoints()` in `Program.cs`. Unified `OfficerDto` in `Features.Officers` across `LoginResponse` and officer lookup to eliminate Swashbuckle schema ID collision. Configured Swashbuckle with `SupportNonNullableReferenceTypes()` and `RequireNonNullablePropertiesSchemaFilter` to accurately reflect required properties in OpenAPI metadata. Created automated verification test in `SwaggerDocumentTests.cs` asserting all 11 Stage 2 endpoints, parameter types (`{id:guid}`), request/response schemas, and RFC 7807 problem details, exporting `src/backend/PrIMA.Api/swagger.json`. Configured `src/frontend/scripts/generate-api.js` and `npm run generate:api` using `openapi-typescript` to produce `src/frontend/src/shared/types/api.generated.ts` with all Stage 1 and Stage 2 types and convenience exports. Verified backend compiles cleanly (`dotnet build`), all 114 backend tests pass, and frontend type-checking (`vue-tsc -b`) and build (`npm run build`) pass cleanly. Implements `RISK-005` and unblocks Phase 4 frontend implementation.

---

## P4 - Frontend UI Features & Workflow Modals

Implementation Status: COMPLETED  
Review Status: GO

### P4-S11

Title: Frontend API Queries, Composables & Validation Schemas

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Create query key factory, TanStack Query composables, mutations with cache invalidation, and Zod form validation schemas under `src/frontend/src/features/issues/`.

Depends On: P3-S10  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/features/issues/api/issueDetail.keys.ts`:
   - Query key factory: `issueDetailKeys.detail(id)`, `issueDetailKeys.officers()`.
2. `src/frontend/src/features/issues/api/useIssueDetailQueries.ts`:
   - `useIssueDetailQuery(id)`: TanStack `useQuery` with `refetchInterval: 10_000`, `refetchOnWindowFocus: true`, `staleTime: 5_000`.
   - `useOfficersQuery()`: TanStack `useQuery` fetching `GET /api/officers`.
   - Mutations with cache invalidation:
     - `useTakeItMutation()`, `useAssignMutation()`, `useAcknowledgeMutation()`, `useStartInvestigationMutation()`, `useAddNoteMutation()`, `useRecordRemediationMutation()`, `useRecordVerificationMutation()`, `useResolveIssueMutation()`, `useDismissIssueMutation()`.
     - Invalidation policy: each mutation invalidates `issueDetailKeys.detail(id)`. Mutating lifecycle transitions (`TakeIt`, `Assign`, `Resolve`, `Dismiss`) also invalidate `['dashboard', 'feed']`.
3. `src/frontend/src/features/issues/schemas/issueModals.schema.ts`:
   - `dismissIssueSchema`: Zod schema validating `dismissalReason` in enum, `justificationRationale` min 20 characters (`BR-PRIMA-006`).
   - `recordRemediationSchema`: Zod schema validating `actionCategory` in enum, `summaryDescription` min 1 character.
   - `manualVerificationSchema`: Zod schema validating `cleanPassed` boolean, `evidenceNotes` min 1 character.
4. `src/frontend/src/features/issues/composables/useIssueDetail.ts`:
   - Local reactive state: modal visibility flags (`isDismissModalOpen`, `isRemediationModalOpen`, `isVerificationModalOpen`, `isAssignModalOpen`), in-progress note draft text (`noteDraftText = ref('')`), expanded occurrence IDs (`expandedOccurrenceIds = ref<Set<string>>(new Set())`).
5. Frontend compiles cleanly via `npm run build` inside `src/frontend/`.

Notes: Implemented `issueDetail.keys.ts` with `issueDetailKeys.detail(id)` and `issueDetailKeys.officers()`. Implemented `issueModals.schema.ts` with Zod validation schemas for dismissal (`dismissIssueSchema` enforcing `BR-PRIMA-006` min 20 trimmed chars and enum validation), remediation (`recordRemediationSchema` with category enum and min 1 char description), and verification (`manualVerificationSchema` with boolean outcome and min 1 char evidence notes), along with inferred TypeScript form types and UI label options. Implemented `useIssueDetailQueries.ts` with `useIssueDetailQuery` (configured with 10s polling, window focus refetch, 5s staleTime, and keepPreviousData), `useOfficersQuery`, and all 9 lifecycle command mutations with selective cache invalidation (`issueDetailKeys.detail(id)` and dashboard feed). Implemented `useIssueDetail.ts` composable encapsulating reactive modal visibility flags, uncommitted note composer draft text, and occurrence expansion set across background refetches. Frontend build (`npm run build`) and type check (`vue-tsc -b`) pass cleanly. Implements `GAP-003`, `ASM-006`, and `ASM-007`.

---

### P4-S12

Title: Workflow Dialog Modals (M-01, M-02, M-03 & Assign Modal)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement isolated PrimeVue dialog workflow modals with VeeValidate + Zod client-side validation under `src/frontend/src/features/issues/components/modals/`.

Depends On: P4-S11  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `DismissIssueModal.vue` (`M-01`):
   - PrimeVue `Dialog` with header "Dismiss Issue".
   - Reason dropdown (`FalsePositive`, `AcceptedOperationalException`, `DuplicateManualReport`).
   - Rationale textarea with live character counter displaying `X / 20 characters minimum` (rose color when `< 20`, emerald when `>= 20`).
   - VeeValidate + Zod validation; submit disabled or blocked when `< 20` chars.
   - Submits `useDismissIssueMutation()`, closes modal on success, and shows PrimeVue Toast.
2. `RecordRemediationModal.vue` (`M-02`):
   - PrimeVue `Dialog` with header "Record Remediation Action".
   - Action Category select (`DataCorrection`, `ServiceRestart`, `ConfigurationAdjustment`, `CodeRemediationRequested`).
   - Summary description textarea (SOP, SQL script reference, or action taken).
   - Submits `useRecordRemediationMutation()`, closes modal on success, and shows Toast.
3. `ManualVerificationModal.vue` (`M-03`):
   - PrimeVue `Dialog` with header "Record Manual Verification".
   - Selectable radio cards for outcome:
     - Card 1: `CleanPassed` (Emerald border/badge, "Anomaly Resolved / Clean Query").
     - Card 2: `AnomalyStillPresent` (Rose border/badge, "Anomaly Still Present / Failed").
   - Evidence notes textarea (e.g. query result row count or observation).
   - Submits `useRecordVerificationMutation()`, closes modal on success, and shows Toast.
4. `AssignOfficerModal.vue`:
   - PrimeVue `Dialog` with header "Assign Officer".
   - Officer select dropdown populated via `useOfficersQuery()`.
   - Submits `useAssignMutation()`, closes modal on success.
5. All modals preserve open state and user input across 10-second background polling ticks.
6. Frontend compiles cleanly via `npm run build` inside `src/frontend/`.

Notes: Implemented all 4 workflow dialog modals with VeeValidate and Zod validation under `src/frontend/src/features/issues/components/modals/`:
1. `DismissIssueModal.vue` (`M-01`): Implements PrimeVue Dialog, dismissal reason select, live character counter (`X / 20 characters minimum` with rose/emerald color transitions), VeeValidate + Zod schema validation enforcing `BR-PRIMA-006` min 20 trimmed chars, `useDismissIssueMutation()`, PrimeVue Toast notification, and form reset.
2. `RecordRemediationModal.vue` (`M-02`): Implements PrimeVue Dialog, remediation category select with options (`DataCorrection`, `ServiceRestart`, `ConfigurationAdjustment`, `CodeRemediationRequested`), summary description textarea with schema validation, policy notice indicating verification supersession, `useRecordRemediationMutation()`, and Toast feedback.
3. `ManualVerificationModal.vue` (`M-03`): Implements PrimeVue Dialog, accessible selectable outcome cards (`CleanPassed` with emerald badge/ring and `AnomalyStillPresent` with rose badge/ring), evidence notes textarea, dynamic outcome notice banner, `useRecordVerificationMutation()`, and Toast feedback.
4. `AssignOfficerModal.vue`: Implements PrimeVue Dialog with contextual header ("Assign Officer" vs "Reassign Officer"), searchable Select dropdown populated via `useOfficersQuery()` with officer full name, username, and role badges, validation against selecting current assignee, `useAssignMutation()`, and Toast feedback.
5. Registered PrimeVue `ToastService` and `<Toast position="top-right" />` in `main.ts` and `App.vue`. Created modal barrel exports in `modals/index.ts`. All modals isolate reactive form state from 10s background query polling ticks. Verified clean compilation via `npm run build` (`vue-tsc -b && vite build`) with zero errors. Implements `S-02` modals `M-01`, `M-02`, `M-03`, `RISK-004`, and `BR-PRIMA-006`.

---

### P4-S13

Title: Left-Column Workbench Components (Header, Evidence, Timeline, Notes)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement left-column operational workbench components for `S-02`: `WorkbenchHeader.vue`, `LatestEvidenceCard.vue`, `OccurrencesTimeline.vue`, and `InvestigationNotesStream.vue`.

Depends On: P4-S11  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `WorkbenchHeader.vue`:
   - Sticky top header (`sticky top-0 z-20 backdrop-blur bg-slate-900/90`).
   - Back button (`/` or `router.back()`) with chevron-left icon.
   - Title, severity badge (`Critical`, `High`, `Medium`, `Low`), status badge (`Open`, `Acknowledged`, `Investigating`, `Resolved`, `Dismissed`, `Reopened`), and copyable correlation key button.
   - Detector reference rendered as styled badge with tooltip (`'Detector portfolio detail available in Stage 3'`) and no active router link (`OQ-006`).
   - Context-sensitive Action Bar strictly implementing the state matrix in Target Architecture §4.6.2:
     - `Open` / `Reopened`: primary `Take It`, secondary `Assign to…`, `Dismiss`.
     - `Acknowledged`: primary `Start Investigating`, secondary `Reassign`, `Dismiss`.
     - `Investigating` without remediation: primary `Record Remediation` (`M-02`), disabled `Mark Resolved` (`🔒 Remediation & verification required`), secondary `Add Note`, `Dismiss`.
     - `Investigating` with remediation, without verification: primary `Manual Verification` (`M-03` promoted per `OQ-001`), disabled `Mark Resolved` (`🔒 Clean verification required`), secondary `Add Note`, `Dismiss`.
     - `Investigating` with failed verification (`CleanPassed == false`): primary `Record Remediation` (Retry), secondary `Manual Verification`, disabled `Mark Resolved`.
     - `Investigating` with clean verification (`CleanPassed == true`): primary `Mark Resolved` (`bg-emerald-600 text-white`, unlocked per `BR-PRIMA-004`), secondary `Add Note`.
     - `Resolved` / `Dismissed`: read-only state badge; all mutating buttons hidden.
2. `LatestEvidenceCard.vue`:
   - 2x2 metadata well: Module, Source Table, Record Keys, Inspection Run ID.
   - Null-safe IDR financial comparison strip: Debit Total, Credit Total, Variance Delta formatted via `Intl.NumberFormat('id-ID', { style: 'currency', currency: 'IDR' })`. Hidden if financial fields are null.
   - Monospace trace block with dark slate styling and one-click copy button.
3. `OccurrencesTimeline.vue`:
   - Collapsible occurrence list sorted newest-first (`#N`, `#N-1`, ...).
   - Relative + WIB timestamps (`Asia/Jakarta`).
   - Inline evidence expansion preserving expanded IDs across 10-second polling ticks.
4. `InvestigationNotesStream.vue`:
   - Chronological append-only notes stream with officer badge, relative + WIB timestamp.
   - Note composer textarea with `Ctrl+Enter` submit shortcut.
   - Draft preservation: note input retained in local reactive ref across background refetches.
5. Frontend compiles cleanly via `npm run build` inside `src/frontend/`.

Notes: Implemented all 4 left-column operational workbench components for S-02:
1. `WorkbenchHeader.vue`: Sticky top header with blur effect, back navigation, severity badge, status badge, detector badge with Stage 3 tooltip and no router link (`OQ-006`), copyable correlation key with feedback, and complete context-sensitive Action Bar faithfully following Target Architecture §4.6.2 (Open/Reopened Take It, Acknowledged Start Investigating, Investigating states covering un-remediated, un-verified with M-03 promotion per `OQ-001`, failed verification retry, and unlocked Mark Resolved per `BR-PRIMA-004`, and read-only closed states).
2. `LatestEvidenceCard.vue`: 2x2 metadata well (Module, Source Table, Record Keys, Inspection Run ID), null-safe IDR financial comparison strip formatted with `Intl.NumberFormat('id-ID')`, and dark monospace trace block with one-click copy button.
3. `OccurrencesTimeline.vue`: Collapsible occurrence list sorted newest-first (#N, #N-1, ...), relative and WIB (`Asia/Jakarta`) timestamps, and inline evidence expansion supporting stable expanded entity IDs across background polling ticks.
4. `InvestigationNotesStream.vue`: Chronological append-only notes stream with officer badge, relative and WIB timestamps, note composer textarea with `Ctrl+Enter` submit shortcut, and draft preservation via two-way v-model binding decoupled from polling cache.
Frontend compiles and typechecks cleanly with 0 errors via `npm run build` (`vue-tsc -b && vite build`).

---

### P4-S14

Title: Right-Column Workbench Cards (Gate, History Cards, Activity Log)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement right-column governance and audit components for `S-02`: `VerificationGateCard.vue`, `VerificationRecordCard.vue`, `RemediationHistoryCard.vue`, `DismissalRecordCard.vue`, `OwnershipTimestampsCard.vue`, and `ActivityLogAccordion.vue`.

Depends On: P4-S11  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `VerificationGateCard.vue`:
   - 3-step checklist card communicating `BR-PRIMA-004` status:
     - Step 1: Investigation Started (checked when status is `Investigating`, `Resolved`, or `Dismissed`).
     - Step 2: Remediation Recorded (checked when `latestRemediation != null`).
     - Step 3: Verification Passed Clean (checked when `latestVerification?.cleanPassed == true && !latestVerification.isSuperseded`).
   - Visual lock/unlock indicator: amber/rose when locked, solid emerald banner when unlocked or resolved.
2. `VerificationRecordCard.vue`:
   - Displays latest and historical verification attempts with outcome chip (`CleanPassed` vs `AnomalyStillPresent`), verified by officer, relative+WIB timestamp, and evidence notes.
   - Rose warning banner displayed if latest verification failed (`AnomalyStillPresent`).
3. `RemediationHistoryCard.vue`:
   - Displays recorded remediation actions with category chip, SOP summary description, performed by officer, and timestamp.
4. `DismissalRecordCard.vue`:
   - Rendered when issue status is `Dismissed`, displaying dismissal reason chip, justification rationale, dismissed by officer, and timestamp.
5. `OwnershipTimestampsCard.vue`:
   - Displays current assignee (or `Unassigned`), CreatedAt, AcknowledgedAt, LastSeen, and Resolved/Dismissed timestamps.
   - Includes `Reassign` button opening `AssignOfficerModal` for active issues (`OQ-007`).
6. `ActivityLogAccordion.vue`:
   - Collapsed-by-default PrimeVue Accordion displaying chronological audit trail from `dbo.IssueActivityLogs`.
   - Each event displays activity type badge, actor name, summary, and relative + WIB timestamp.
7. Frontend compiles cleanly via `npm run build` inside `src/frontend/`.

Notes: Implemented all 6 right-column governance and audit cards under `src/frontend/src/features/issues/components/` and `src/frontend/src/features/issues/components/cards/` along with index exports:
1. `VerificationGateCard.vue`: 3-step checklist implementing `BR-PRIMA-004` verification gate; visual indicators for Resolved (emerald), Unlocked (emerald), Failed (rose warning), and Locked (amber);
2. `VerificationRecordCard.vue`: CleanPassed vs AnomalyStillPresent outcome chips, rose failure warning banner, verified by officer, relative + WIB timestamps, evidence notes, and historical attempts list;
3. `RemediationHistoryCard.vue`: Category chips for all 4 remediation categories, performed by officer, relative + WIB timestamps, and SOP descriptions;
4. `DismissalRecordCard.vue`: Conditionally rendered for Dismissed issues, displaying dismissal reason chip, officer, timestamp, and quotation callout for min-20-character rationale (`BR-PRIMA-006`);
5. `OwnershipTimestampsCard.vue`: Displays assignee with avatar, Reassign button opening `AssignOfficerModal` on active issues (`OQ-007`), and temporal breakdown (Created, Acknowledged, Last Recurred, Reopened, Resolved, Dismissed);
6. `ActivityLogAccordion.vue`: PrimeVue Accordion collapsed by default (`:value="null"`), displaying chronological timeline of immutable activity logs with semantic badges for all 11 activity types, actor names, summaries, and relative + WIB timestamps.
Verified clean frontend build via `npm run build` (`vue-tsc -b && vite build`) with zero TypeScript or Vue compiler errors.

---

### P4-S15

Title: Issue Detail View Orchestrator, Router Registration & S-01 Linkage

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Assemble `IssueDetailView.vue` (`/issues/:id`) orchestrating the 2-column desktop grid layout, register route in Vue router, and wire row-click navigation from `ActiveIssueFeed.vue` on `S-01`.

Depends On: P4-S12, P4-S13, P4-S14  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/features/issues/views/IssueDetailView.vue`:
   - Route orchestrator extracting `route.params.id`.
   - Desktop 2-column asymmetric layout (`grid-cols-1 lg:grid-cols-12 gap-6`): Left 8 cols, Right 4 cols.
   - Integrates all left-column components, right-column cards, and workflow modals.
   - Loading skeleton and error fallback states.
2. `src/frontend/src/app/router/index.ts`:
   - Registers route `/issues/:id` mapped to `IssueDetailView.vue` with auth navigation guard (`requiresAuth: true`).
3. `src/frontend/src/features/dashboard/components/ActiveIssueFeed.vue` (`S-01`):
   - Table rows updated with click handler navigating to `/issues/${item.id}` via `router.push()`.
   - Row cursor styled as pointer (`cursor-pointer hover:bg-slate-800/40`).
   - Ignores row click when copy correlation key button is clicked (`@click.stop`).
4. Frontend builds cleanly via `npm run build` with zero TypeScript or Vue compiler errors.

Notes: Implemented `IssueDetailView.vue` orchestrator, Vue router mapping, and dashboard feed linkage:
1. `IssueDetailView.vue`: Assembled single-screen operational workbench (`/issues/:id`) with desktop 2-column asymmetric layout (`grid-cols-1 lg:grid-cols-12 gap-6`: Left 8 cols, Right 4 cols). Connected TanStack Query `useIssueDetailQuery(id)` with 10s background polling, error fallback with retry, cold-load skeleton state, and full integration of left-column components (`WorkbenchHeader`, `LatestEvidenceCard`, `OccurrencesTimeline`, `InvestigationNotesStream`), right-column governance cards (`VerificationGateCard`, `VerificationRecordCard`, `RemediationHistoryCard`, `DismissalRecordCard`, `OwnershipTimestampsCard`, `ActivityLogAccordion`), and workflow dialog modals (`DismissIssueModal`, `RecordRemediationModal`, `ManualVerificationModal`, `AssignOfficerModal`). Wired lifecycle commands (`take-it`, `start-investigating`, `resolve`, `notes`) with PrimeVue Toast notifications.
2. `src/frontend/src/app/router/index.ts`: Registered route `/issues/:id` (`name: 'issue-detail'`) under AppLayout with auth navigation guard (`requiresAuth: true`) and dynamic document title handling.
3. `src/frontend/src/features/dashboard/components/ActiveIssueFeed.vue` (`S-01`): Added row click handler navigating to `/issues/${item.id}` via `router.push()`, row pointer styling (`cursor-pointer hover:bg-slate-800/40`), and copy button with `@click.stop` to prevent navigation when copying correlation key.
4. Clean frontend build: Verified clean production compilation (`npm run build` executing `vue-tsc -b && vite build`) with zero TypeScript or Vue compiler errors. Implements `GAP-003`, `GAP-006`, delivering the complete frontend workbench.

---

## P5 - Demo Seeding, Automated Testing & Verification

Implementation Status: COMPLETED  
Review Status: GO

### P5-S16

Title: Development Demo Data Seeder Expansion

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Update `DevelopmentDemoDataSeeder.cs` to seed realistic demo issues representing all Stage 2 lifecycle states and audit trails for local development and verification.

Depends On: P3-S10  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `DevelopmentDemoDataSeeder.cs` updated:
   - Gated on `IsDevelopment() && SeedDemoData == true`.
   - Idempotently seeds:
     - 1 issue in `Acknowledged` status assigned to `siti.rahma` with initial investigation notes and activity logs.
     - 1 issue in `Investigating` status assigned to `ahmad.budi` with notes, recorded remediation, awaiting verification.
     - 1 issue in `Investigating` status with failed verification (`CleanPassed = false`).
     - 1 issue in `Investigating` status with clean verification (`CleanPassed = true`), ready for resolution.
     - 1 issue in `Resolved` status with clean verification, remediation, and activity logs.
     - 1 issue in `Dismissed` status with valid dismissal justification (`BR-PRIMA-006`) and activity logs.
2. Verified by running the application locally and confirming seeded issues appear on `S-01` and navigate cleanly to `S-02`.
3. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Expanded `DevelopmentDemoDataSeeder.cs` to seed realistic demo issues covering all Stage 2 lifecycle states: Open (unassigned), Acknowledged (assigned to siti.rahma with notes and audit trail), Investigating awaiting verification (assigned to ahmad.budi with ServiceRestart remediation and notes), Investigating with failed verification (CleanPassed = false, ConfigurationAdjustment remediation), Investigating with clean verification (CleanPassed = true, ready for resolution), Resolved (with CleanPassed verification, DataCorrection remediation, and activity logs), and Dismissed (with BR-PRIMA-006 compliant rationale >= 20 characters and activity logs). Built with idempotent SQL queries and deterministic GUIDs for all issues, runs, occurrences, notes, remediations, verifications, and activity logs. Added comprehensive automated integration test suite in `DevelopmentDemoDataSeederTests.cs` (verifying environment/config gating, field mappings, activity log sequences, idempotent re-execution, and GetIssueByIdHandler composite projection). All tests pass cleanly. Backend compiles with 0 errors and 0 warnings.

---

### P5-S17

Title: Automated Domain & Integration Test Suite

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement automated unit and integration tests under `src/backend/PrIMA.Api.Tests/` covering domain invariants, verification gate enforcement, dismissal validation, recurrence invalidation, and API endpoints.

Depends On: P3-S10, P2-S04  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Unit tests created under `src/backend/PrIMA.Api.Tests/Domain/IssueLifecycleTests.cs`:
   - `TakeIt`: transitions Open/Reopened -> Investigating, assigns caller, sets AcknowledgedAtUtc; fails if closed.
   - `Assign`: transitions Open/Reopened -> Acknowledged, sets AcknowledgedAtUtc; preserves Investigating if already investigating (`OQ-007`).
   - `StartInvestigation`: transitions Acknowledged -> Investigating; fails from Open.
   - `AddNote`: appends note; fails if empty note or closed issue.
   - `RecordRemediation`: records remediation; supersedes prior verifications; fails if not Investigating.
   - `RecordVerification`: records verification; fails if no remediation or not Investigating.
   - `MarkResolved` Verification Gate (`BR-PRIMA-004`): fails if no verification, if `CleanPassed == false`, or if `IsSuperseded == true`; succeeds if CleanPassed and un-superseded.
   - `Dismiss` Justification (`BR-PRIMA-006`): fails if rationale `< 20` chars; succeeds if `>= 20` chars.
2. Integration tests created under `src/backend/PrIMA.Api.Tests/Integration/IssueLifecycleEndpointsTests.cs`:
   - Tests `POST /api/issues/{id}/take-it`.
   - Tests `POST /api/issues/{id}/assign` validating officer existence and state change.
   - Tests `POST /api/issues/{id}/notes`.
   - Tests `POST /api/issues/{id}/remediation` and `POST /api/issues/{id}/verification/manual`.
   - Tests `POST /api/issues/{id}/resolve`: asserts `409 Conflict` when unverified; asserts `204 NoContent` after clean verification.
   - Tests `POST /api/issues/{id}/dismiss`: asserts `422 Unprocessable Entity` when rationale `< 20` chars; asserts `204 NoContent` when `>= 20` chars.
   - Tests `GET /api/issues/{id}` verifying composite projection and activity logs.
   - Tests `GET /api/officers`.
   - Tests telemetry recurrence on active `Investigating` issue invalidating active verifications (`IsSuperseded = 1`).
3. All tests pass cleanly via `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj`.

Notes: Implemented comprehensive automated test suite across domain unit tests and API integration tests. Created 49 unit tests in `src/backend/PrIMA.Api.Tests/Domain/IssueLifecycleTests.cs` verifying all lifecycle invariants (`TakeIt`, `Assign` with `OQ-007`, `StartInvestigation`, `AddNote`, `RecordRemediation`, `RecordVerification`, `BR-PRIMA-004` Verification Gate, and `BR-PRIMA-006` Dismissal Justification). Created 10 integration tests in `src/backend/PrIMA.Api.Tests/Integration/IssueLifecycleEndpointsTests.cs` covering all lifecycle endpoints, composite projections, officer lookup, and recurrence invalidation. Added deadlock retry resilience in `PrIMAApiFactory.ResetDatabaseAsync` and configured `CollectionBehavior.CollectionPerAssembly` sequential test execution. All 176 backend tests pass with 100% success rate.

---

### P5-S18

Title: End-to-End Build Verification & Manual Resolution Loop Smoke Test

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Perform end-to-end build verification, frontend type-checking, and smoke test of the complete manual issue resolution loop against the live local development environment.

Depends On: P4-S15, P5-S16, P5-S17  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Backend solution compiles cleanly with zero warnings/errors via `dotnet build src/backend/PrIMA.slnx`.
2. Backend test suite passes 100% via `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj`.
3. Frontend compiles cleanly with zero TypeScript or Vite errors via `npm run build` in `src/frontend/`.
4. End-to-end smoke verification passes against local runtime:
   - Ingest finding via telemetry prober/simulator.
   - View newly discovered issue on active dashboard `S-01`.
   - Tap row to navigate to `S-02` Issue Detail Workbench.
   - Execute single-tap `Take It` triage action (`Open` -> `Investigating`).
   - Append an investigation note with `Ctrl+Enter`.
   - Record remediation action via modal `M-02`.
   - Observe Action Bar promote `Manual Verification` (`M-03`) to primary button.
   - Record clean manual verification outcome via modal `M-03`.
   - Observe Verification Gate unlock (`BR-PRIMA-004`).
   - Click `Mark Resolved` to resolve the issue.
   - Confirm issue is removed from active feed `S-01` and appears in resolved counter.
   - Confirm complete chronological activity audit log is persisted in `dbo.IssueActivityLogs`.

Notes: Executed complete end-to-end build verification, type-checking, and smoke testing for Stage 2.
1. Backend solution build: `dotnet build src/backend/PrIMA.slnx` succeeds with 0 warnings and 0 errors.
2. Backend test suite: `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj` passes with 100% success rate (177 tests passed across domain invariants, API integration, schema generation, demo seeder, and lifecycle flows).
3. Frontend compilation: `npm run build` (`vue-tsc -b && vite build`) executes cleanly with zero TypeScript or Vite errors.
4. End-to-end smoke verification: Implemented comprehensive automated E2E smoke test suite in `src/backend/PrIMA.Api.Tests/Integration/EndToEndManualResolutionSmokeTests.cs`. Test verifies the complete manual resolution workflow against running API endpoints and database: telemetry ingestion producing an Open issue with occurrence #1, verification of appearance in active dashboard feed (`GET /api/issues/dashboard-feed`), retrieval of composite projection (`GET /api/issues/{id}`), single-tap `Take It` triage action (`POST /api/issues/{id}/take-it`), appending investigation note (`POST /api/issues/{id}/notes`), verification gate locking `Mark Resolved` before remediation (409 Conflict), recording remediation action (`POST /api/issues/{id}/remediation`), verification gate locking `Mark Resolved` before verification (409 Conflict), recording clean manual verification (`POST /api/issues/{id}/verification/manual`), unlocking verification gate (`BR-PRIMA-004`), executing `Mark Resolved` (`POST /api/issues/{id}/resolve`), confirming removal from active dashboard feed and incrementing resolved summary counter, and validating complete chronological audit trail in `dbo.IssueActivityLogs` (`Created`, `InvestigationStarted`, `NoteAdded`, `RemediationRecorded`, `VerificationRecorded`, `Resolved`). All criteria satisfied.

---

# 6. Change Log

- 2026-11-21: Initial Implementation Plan created for PrIMA Stage 2 (`Code: PRIMA-STAGE-2`), structured into 5 phases and 18 slices following `docs/issues/IS002-PRIMA-STAGE-2-ARCHITECTURE.md` and approved feasibility decisions. Execution Approval granted (`APPROVED`).
