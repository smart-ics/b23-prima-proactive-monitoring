---
Title: PrIMA Stage 1 — Platform Foundation & Core Telemetry Ingestion Implementation Plan
Code: PRIMA-STAGE-1
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-11-20
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Stage 1 ("Platform Foundation & Core Telemetry Ingestion") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`), establishing the initial operational foundation of PrIMA (Proactive Inspection Monitoring App) and delivering the **Walking Skeleton & Live Ingestion Pipeline** milestone.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:
- ISSUE: `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md`
- FEATURE: `docs/PRIMA-DEVELOPMENT-ROADMAP.md` (Stage 1), `docs/PRIMA-USE-CASE-DISCOVERY.md` (A-1, A-2), `docs/PRIMA-SCREEN-INVENTORY.md` (S-00, S-01), `docs/PRIMA-UI-UX-GUIDELINE.md`
- ARCHITECTURE: `docs/issues/IS001-PRIMA-STAGE-1-ARCHITECTURE.md`
- FEASIBILITY-ASSESSMENT: `docs/issues/IS001-PRIMA-STAGE-1-FEASIBILITY-ASSESSMENT.md`

Architecture Applicability: ARCHITECTURE-REQUIRED
The target architecture artifact `docs/issues/IS001-PRIMA-STAGE-1-ARCHITECTURE.md` authoritatively defines the technical realization, schema fixes (`GAP-003`), batch ingestion contract (`GAP-002`), recurrence reopening updates (`GAP-005`), API key authentication (`OQ-001`), officer JWT authorization (`OQ-002`, `OQ-003`), dashboard feed query semantics (`OQ-005`), and frontend UI/UX boundaries (`OQ-006`).

---

# 2. Planning Scope

The scope of this implementation plan encompasses the complete delivery of Stage 1 across backend, database, frontend, tooling, and test automation:

1. **Backend Minimal API Foundation (`src/backend/PrIMA.Api/`):**
   - Single ASP.NET Core `.NET 8` Minimal API project with vertical slices and DDD aggregate roots.
   - Swagger / OpenAPI documentation exposed at `/swagger/v1/swagger.json`.
   - DbUp database migrations with automatic database creation (`EnsureDatabase.For.SqlDatabase`) against local SQL Server 2022.
   - RFC 7807 `ProblemDetails` exception and validation error mapping.
2. **Officer Authentication (`S-00`):**
   - `POST /api/auth/login` issuing 12-hour HS256 JWT Bearer tokens with PBKDF2 hash verification.
   - Baseline officer seed (`002_SeedInitialUsers.sql`) with accounts `ahmad.budi`, `siti.rahma`, `edp.lead`.
   - Flat v1 operational access for all active (`IsActive = 1`) officers (`EdpOfficer`, `SystemAnalyst`, `Lead`).
3. **Telemetry Ingestion Pipeline:**
   - `POST /api/ingestion/telemetry` authenticated via `X-Detector-API-Key` using constant-time byte comparison against configured keys.
   - Support for `Passed` (0 findings) and `FindingDiscovered` (`1..N` findings) in a single atomic database transaction.
   - Ingestion guardrails: `404 Not Found` for unknown detector; `409 Conflict` for `Paused` or `Retired` detector; `422 Unprocessable Entity` for `ExecutionFailed` (deferred to Stage 3).
   - Detector liveness updates (`LastRunAtUtc`, `LastHealthyHeartbeatUtc`, `HealthStatus = Healthy`).
4. **Deterministic Deduplication (`BR-PRIMA-001`) & Audit Trail (`BR-PRIMA-009`):**
   - Canonical `CorrelationKey` generation: `$"{detectorId:D}:{targetEntityReference.Trim().ToUpperInvariant()}"`.
   - Creation of `Open` issues with initial immutable `Occurrence` snapshot linked to `InspectionRunId`.
   - Appending new immutable `Occurrence` to existing active issues (`Open`, `Acknowledged`, `Investigating`, `Reopened`) without duplicating the issue.
   - Intra-batch duplicate rejection for duplicate `targetEntityReference` with `422 Unprocessable Entity`.
5. **Automatic Recurrence Reopening (`BR-PRIMA-002`):**
   - Reopening matching `Resolved` or `Dismissed` issues to `Reopened`.
   - Resetting `CurrentAssignee = null`, `AcknowledgedAtUtc = null`, `ResolvedAtUtc = null`, `DismissedAtUtc = null`.
   - Preserving `CurrentSeverity` and `EscalationLevel` (retaining `0` in Stage 1).
   - Marking prior `RemediationRecords`, `VerificationRecords`, and `DismissalRecords` as `IsSuperseded = 1` in SQL without data deletion.
6. **Live Active Issue Dashboard (`S-01`):**
   - `GET /api/issues/dashboard-feed` returning global summary counters (`criticalCount`, `highCount`, `openCount`, `reopenedCount`, `resolvedTodayCount`) and active issue feed items.
   - `Resolved Today` computed across `[startOfTodayWibUtc, startOfTomorrowWibUtc)` derived from `OperationalTimeZone` (`Asia/Jakarta`, UTC+7).
   - Feed items strictly sorted by `SeverityRank DESC, LastOccurrenceAtUtc DESC`, strictly excluding `Resolved` and `Dismissed` issues.
   - Server-side filtering by `status`, `severity`, `scope`, and text `search`. Rejecting closed status filter queries with `422`.
7. **Frontend Application (`src/frontend/`):**
   - Vue 3.5+ Composition API (`<script setup lang="ts">`), Vite 6, Tailwind CSS v4 CSS-first (`@tailwindcss/vite`), PrimeVue v4 (Aura preset).
   - Feature slices: `features/auth/` (`S-00`) and `features/dashboard/` (`S-01`).
   - Pinia `auth.store.ts` storing JWT in `localStorage` for F5 persistence; Axios interceptor handling `401` redirects.
   - TanStack Query polling active dashboard every 10 seconds with zero-disruption rendering (no skeleton flashing on background refresh).
   - Stage 1 navigation boundaries: `Dashboard` active; `Detectors` and `History & Reports` visually disabled with tooltips; `Resolved Today` counter tile and issue feed rows non-navigating.
   - Type generation script syncing OpenAPI JSON to `src/shared/types/api.generated.ts`.
8. **Tooling & Test Baseline:**
   - Zero-dependency Node.js CLI telemetry simulator (`src/tools/telemetry-simulator/simulate.mjs`).
   - Development-only demo seeder (`DevelopmentDemoDataSeeder`) gated on `IsDevelopment() && SeedDemoData == true`.
   - Automated test suite (`src/backend/PrIMA.Api.Tests/`) covering domain invariants, API authentication, ingestion batch rules, and dashboard queries.

**Explicitly Out of Scope:**
- Stage 2: `S-02` Issue Detail Workbench, modals `M-01`–`M-03`, manual investigation notes, manual remediation, verification triggers, issue resolve/dismiss endpoints (`BR-PRIMA-004`, `BR-PRIMA-006`, `BR-PRIMA-008`), and `dbo.IssueActivityLogs`.
- Stage 3: Detector portfolio screens (`S-03`–`S-06`), background supervisor worker (`MonitoringSupervisorWorker`), cadence silence detection (`BR-PRIMA-010`), SLA/recurrence escalation (`BR-PRIMA-003`), `ExecutionFailed` infrastructure issue synthesis (`BR-PRIMA-005`), top Alert Strip on `S-01` (`G-1`, `G-2`), and detector webhook execution (`D-2`).
- Stage 4: Historical retrospective screens (`S-07`), reliability KPIs, MTTR analytics, and v1.0 quality gate.
- Stage 5: SIMRS pilot detector suite and hospital on-premise production rollout (`BR-PRIMA-007`).

---

# 3. Dependencies

## External Prerequisites
- .NET 8 SDK installed on host.
- Node.js (v20+ or v22 LTS) and npm installed on host.
- Microsoft SQL Server 2022 (local instance accessible via connection string configured in `appsettings.Development.json`).

## Slice Dependency Rules
- `Depends On` declares implementation prerequisites between slices.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.
- Dependency satisfaction does not require review status `GO`.
- Dependencies represent real implementation prerequisites (e.g. required code/types/tables).

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Scaffolding & Foundation Infrastructure | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P2 - Core Domain & Data Access Layer | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P3 - Backend API Features & Tooling | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P4 - Frontend UI Features | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P5 - Automated Testing & Verification | NOT-STARTED | NOT-REVIEWED | 0/2 |

---

# 5. Phases

## P1 - Scaffolding & Foundation Infrastructure

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P1-S01

Title: Backend Project Setup & Infrastructure Foundation

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Initialize the ASP.NET Core .NET 8 Minimal API solution, project files, dependencies, configuration files, and common infrastructure classes.

Depends On: None  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api/PrIMA.Api.csproj` targeting `net8.0` is created with package references for `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore`, `Microsoft.Data.SqlClient`, `Dapper`, `dbup-sqlserver`, `FluentValidation.AspNetCore`, and `Microsoft.AspNetCore.Authentication.JwtBearer`.
2. `src/backend/PrIMA.Api/appsettings.json` and `appsettings.Development.json` are created containing `ConnectionStrings:PrIMADb`, `Jwt:SigningKey`, `Jwt:ExpirationHours` (12), `Ingestion:ApiKeys` (`["prima-dev-key-alpha-99"]`), `OperationalTimeZone` (`Asia/Jakarta`), and `SeedDemoData` (false in base, true in Development).
3. Common primitives are created under `src/backend/PrIMA.Api/Common/`:
   - `Result.cs` / `Result<T>.cs` for functional result handling.
   - `DomainError.cs` representing error code, message, and HTTP status code.
   - `ISqlConnectionFactory.cs` and `SqlConnectionFactory.cs` wrapping SQL Server connections.
   - `CurrentOfficer.cs` extracting claims from `ClaimsPrincipal`.
   - `ProblemDetailsExtensions.cs` mapping `DomainError` and exceptions to RFC 7807 `ProblemDetails`.
4. `src/backend/PrIMA.Api/Program.cs` bootstraps service registrations (Authentication, Swagger with JWT and API Key header definitions, CORS, FluentValidation, SqlConnectionFactory) and middleware pipeline.
5. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes: Slices P1-S01 and P1-S03 are independent and can be implemented concurrently.

Implementation Notes:
- Initialized `PrIMA.Api` project targeting `net8.0` with all required NuGet packages: `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore`, `Microsoft.Data.SqlClient`, `Dapper`, `dbup-sqlserver`, `FluentValidation.AspNetCore`, and `Microsoft.AspNetCore.Authentication.JwtBearer`.
- Created `appsettings.json` and `appsettings.Development.json` with `ConnectionStrings:PrIMADb`, `Jwt` settings (HS256 signing key, 12h expiration), `Ingestion:ApiKeys`, `OperationalTimeZone` (`Asia/Jakarta`), and `SeedDemoData` flag.
- Created common primitives under `Common/`: `Result`/`Result<T>`, `DomainError`, `ISqlConnectionFactory`, `SqlConnectionFactory`, `CurrentOfficer` claim extraction, and `ProblemDetailsExtensions` RFC 7807 mappings.
- Configured `Program.cs` with JWT Bearer authentication, Swagger (JWT Bearer + `X-Detector-API-Key` security schemes), CORS (`AllowFrontend`), FluentValidation, and `SqlConnectionFactory`.
- Verified clean build (`0 Warning(s)`, `0 Error(s)`) via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Changed Files:
- `src/backend/PrIMA.slnx`
- `src/backend/PrIMA.Api/PrIMA.Api.csproj`
- `src/backend/PrIMA.Api/appsettings.json`
- `src/backend/PrIMA.Api/appsettings.Development.json`
- `src/backend/PrIMA.Api/Program.cs`
- `src/backend/PrIMA.Api/Common/Result.cs`
- `src/backend/PrIMA.Api/Common/DomainError.cs`
- `src/backend/PrIMA.Api/Common/ISqlConnectionFactory.cs`
- `src/backend/PrIMA.Api/Common/SqlConnectionFactory.cs`
- `src/backend/PrIMA.Api/Common/CurrentOfficer.cs`
- `src/backend/PrIMA.Api/Common/ProblemDetailsExtensions.cs`

---

### P1-S02

Title: Database Migration Runner & Baseline SQL Schema

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Create the DbUp migration runner and baseline SQL migration scripts implementing all corrected schema tables, indexes, constraints, and default officer seeds.

Depends On: P1-S01  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api/Migrations/MigrationRunner.cs` is implemented using DbUp:
   - Invokes `EnsureDatabase.For.SqlDatabase(connectionString)` to automatically create the database if absent.
   - Executes embedded SQL scripts inside assembly with a single transaction per script.
2. `src/backend/PrIMA.Api/Migrations/Scripts/001_InitialSchema.sql` is embedded, creating tables and constraints matching Target Architecture §8.1:
   - `dbo.Detectors`: `Id`, `Name` (UNIQUE), `Description`, `Scope`, `TargetModule`, `ExpectedCadenceSeconds`, `WebhookUrl`, `AdminStatus`, `HealthStatus`, `LastRunAtUtc`, `LastHealthyHeartbeatUtc`, `CreatedAtUtc`.
   - `dbo.InspectionRuns`: `Id`, `DetectorId` (FK), `ExecutionTimestampUtc`, `TriggerSource`, `Outcome`, `DurationMs`, `FailureReason`, `CreatedAtUtc`. Index `IX_InspectionRuns_DetectorId_Time`.
   - `dbo.Issues`: `Id`, `CorrelationKey` (UX), `Title`, `DetectorId` (FK), `DetectorName`, `Scope`, `BaseSeverity`, `CurrentSeverity`, `SeverityRank` (TINYINT), `Status`, `EscalationLevel` (0), `OccurrenceCount` (1), `CurrentAssignee`, `CreatedAtUtc`, `LastOccurrenceAtUtc`, `AcknowledgedAtUtc`, `ResolvedAtUtc`, `DismissedAtUtc`, `ReopenedAtUtc`. Index `IX_Issues_Status_SeverityRank`.
   - `dbo.Occurrences`: `Id`, `IssueId` (FK), `InspectionRunId` (FK), `OccurrenceNumber`, `TimestampUtc`, `Module`, `SourceTable` (nullable), `RecordKeys` (nullable), `DebitTotal`, `CreditTotal`, `VarianceDelta`, `TraceString`. Indexes `IX_Occurrences_IssueId`, `IX_Occurrences_InspectionRunId`.
   - `dbo.InvestigationNotes`: `Id`, `IssueId` (FK), `OfficerName`, `NoteText`, `CreatedAtUtc`.
   - `dbo.RemediationRecords`: `Id`, `IssueId` (FK), `ActionCategory`, `SummaryDescription`, `PerformedBy`, `TimestampUtc`, `IsSuperseded` (BIT default 0).
   - `dbo.VerificationRecords`: `Id`, `IssueId` (FK), `VerificationMethod`, `CleanPassed` (BIT), `EvidenceNotes`, `VerifiedBy`, `TimestampUtc`, `IsSuperseded` (BIT default 0).
   - `dbo.DismissalRecords`: `Id`, `IssueId` (FK), `DismissalReason`, `JustificationRationale`, `DismissedBy`, `TimestampUtc`, `IsSuperseded` (BIT default 0).
   - `dbo.Officers`: `Id`, `Username` (UNIQUE), `FullName`, `PasswordHash`, `Role`, `IsActive` (BIT default 1).
3. `src/backend/PrIMA.Api/Migrations/Scripts/002_SeedInitialUsers.sql` is embedded, idempotently seeding baseline officers if not exists:
   - `ahmad.budi` (Role: `EdpOfficer`, Password: `Hospital@2026!` hashed via PBKDF2).
   - `siti.rahma` (Role: `SystemAnalyst`, Password: `Hospital@2026!` hashed via PBKDF2).
   - `edp.lead` (Role: `Lead`, Password: `Hospital@2026!` hashed via PBKDF2).
4. `MigrationRunner.Run(connectionString)` is invoked in `Program.cs` before the app starts listening.

Notes: Implements `GAP-003` schema corrections and `OQ-004` baseline seed isolation.

Implementation Notes:
- Configured embedded resource pattern `Migrations\Scripts\*.sql` in `PrIMA.Api.csproj`.
- Created `001_InitialSchema.sql` implementing all target schema tables, foreign keys, constraints, and indexes according to Architecture §8.1 (`Detectors`, `InspectionRuns`, `Issues`, `Occurrences`, `InvestigationNotes`, `RemediationRecords`, `VerificationRecords`, `DismissalRecords`, and `Officers`).
- Created `002_SeedInitialUsers.sql` idempotently seeding default accounts (`ahmad.budi`, `siti.rahma`, `edp.lead`) with PBKDF2 hashes verified against ASP.NET Core `PasswordHasher<object>`.
- Implemented `MigrationRunner.cs` wrapping DbUp `EnsureDatabase.For.SqlDatabase` and executing embedded scripts with single-transaction-per-script semantics.
- Registered and executed `MigrationRunner.Run(connectionString)` in `Program.cs` before server begins listening.
- Executed migration run against live SQL Server instance, validating database auto-creation, script execution, table indexing, and successful startup. Clean build verified via `dotnet build`.

Changed Files:
- `src/backend/PrIMA.Api/PrIMA.Api.csproj`
- `src/backend/PrIMA.Api/Migrations/MigrationRunner.cs`
- `src/backend/PrIMA.Api/Migrations/Scripts/001_InitialSchema.sql`
- `src/backend/PrIMA.Api/Migrations/Scripts/002_SeedInitialUsers.sql`
- `src/backend/PrIMA.Api/Program.cs`

---

### P1-S03

Title: Frontend Project Scaffolding & Design System Setup

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Initialize the Vue 3.5+ Vite 6 single-page application with TypeScript, Tailwind CSS v4, PrimeVue v4 with Aura preset, Pinia, Vue Router, and TanStack Vue Query.

Depends On: None  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/package.json` is initialized with dependencies: `vue` (^3.5), `vue-router` (^4.4), `pinia` (^2.2), `@tanstack/vue-query` (^5.50), `axios` (^1.7), `primevue` (^4.2), `@primevue/themes`, `primeicons` (^7.0), `vee-validate` (^4.13), `@vee-validate/zod` (^4.13), `zod` (^3.23), `lucide-vue-next` (^0.450), `@tailwindcss/vite` (^4.0), `tailwindcss` (^4.0), and `openapi-typescript` (^7.4).
2. `src/frontend/vite.config.ts` is configured with `@tailwindcss/vite` and dev server proxy forwarding `/api` to `http://localhost:5000` (changeOrigin: true).
3. `src/frontend/src/main.ts` is configured with Pinia, Vue Router, Vue Query, and PrimeVue using Aura preset (`options: { darkModeSelector: '.app-dark', cssLayer: false }`).
4. `src/frontend/src/assets/main.css` imports `@import "tailwindcss";` and defines PrIMA design tokens (workbench dark-slate palette, severity badge colors, high information density typography).
5. Script `"generate:api"` is added to `package.json` to generate `src/shared/types/api.generated.ts` via `openapi-typescript`.
6. Frontend compiles cleanly via `npm run build` inside `src/frontend/`.

Notes: Successfully initialized and verified frontend single-page application. `vue-tsc -b && vite build` compiles cleanly with zero errors. Design tokens for high-density workbench styling, severity badges, and row accents configured. Script `generate:api` configured in `package.json`.

---

## P2 - Core Domain & Data Access Layer

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P2-S04

Title: Domain Aggregate Roots & Value Objects

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement DDD domain aggregates, value objects, domain errors, and repository interfaces for Detectors and Issues.

Depends On: P1-S01  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Enums created under `src/backend/PrIMA.Api/Domain/`:
   - `MonitoringScope`: `DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`.
   - `AdminStatus`: `Active`, `Paused`, `Retired`.
   - `DetectorHealth`: `Healthy`, `Silent`, `Failing`.
   - `TriggerSource`: `Scheduled`, `Manual`, `EventHook`.
   - `InspectionOutcome`: `Passed`, `FindingDiscovered`, `ExecutionFailed`.
   - `IssueStatus`: `Open`, `Acknowledged`, `Investigating`, `Resolved`, `Dismissed`, `Reopened`.
   - `IssueSeverity`: `Low`, `Medium`, `High`, `Critical`.
   - `SeverityRank`: Helper mapping `Critical` -> 4, `High` -> 3, `Medium` -> 2, `Low` -> 1.
2. Value Objects created under `src/backend/PrIMA.Api/Domain/Issues/`:
   - `CorrelationKey`: Enforces canonical formula `$"{detectorId:D}:{targetEntityReference.Trim().ToUpperInvariant()}"` and length limit `<= 250`.
   - `TelemetryEvidence`: Captures `Module`, `SourceTable` (nullable), `RecordKeys` (nullable), `DebitTotal`, `CreditTotal`, `VarianceDelta`, `Trace`.
3. Aggregate Roots created:
   - `Detector`: Enforces invariants, checks `AdminStatus` (`Active`, `Paused`, `Retired`), and updates heartbeat via `RecordHeartbeat(DateTime timestamp, DetectorHealth health)`.
   - `InspectionRun`: Represents telemetry execution sweep linked to `DetectorId`.
   - `Occurrence`: Immutable observation snapshot with `InspectionRunId`, `OccurrenceNumber`, and `TelemetryEvidence`.
   - `Issue`:
     - Factory `Create(CorrelationKey, title, detectorId, detectorName, scope, baseSeverity, inspectionRunId, evidence, timestampUtc)`: initializes with `Status = Open`, `SeverityRank = SeverityRank.From(baseSeverity)`, `EscalationLevel = 0`, `OccurrenceCount = 1`, and initial occurrence (`OccurrenceNumber = 1`).
     - Method `RecordRecurringOccurrence(inspectionRunId, evidence, timestampUtc)`:
       - On active issues (`Open`, `Acknowledged`, `Investigating`, `Reopened`): appends occurrence, increments `OccurrenceCount`, updates `LastOccurrenceAtUtc`. Preserves `Title` and `BaseSeverity`.
       - On closed issues (`Resolved`, `Dismissed`): implements `BR-PRIMA-002` reopening: sets `Status = IssueStatus.Reopened`, sets `ReopenedAtUtc = timestampUtc`, resets `CurrentAssignee = null`, `AcknowledgedAtUtc = null`, `ResolvedAtUtc = null`, `DismissedAtUtc = null`, preserves `CurrentSeverity` and `EscalationLevel` (0), increments `OccurrenceCount`, updates `LastOccurrenceAtUtc`, and appends occurrence.
4. Repository interfaces created:
   - `IDetectorRepository.cs` in `Domain/Detectors/`.
   - `IIssueRepository.cs` in `Domain/Issues/`.

Notes: Enforces `BR-PRIMA-001`, `BR-PRIMA-002`, `BR-PRIMA-009`, `GAP-005`, and `GAP-007`.

Implementation Notes:
- Created domain enums in `Domain/Detectors/` (`MonitoringScope`, `AdminStatus`, `DetectorHealth`, `TriggerSource`, `InspectionOutcome`) and `Domain/Issues/` (`IssueStatus`, `IssueSeverity`).
- Implemented `SeverityRank` helper with mappings for `Critical` (4), `High` (3), `Medium` (2), `Low` (1), along with string parsing and reverse lookup helpers.
- Implemented value objects:
  - `CorrelationKey`: enforces canonical formula `$"{detectorId:D}:{targetEntityReference.Trim().ToUpperInvariant()}"`, max length `<= 250`, with implicit string conversions and equality invariants.
  - `TelemetryEvidence`: immutable snapshot capturing `Module`, `SourceTable`, `RecordKeys`, `DebitTotal`, `CreditTotal`, `VarianceDelta`, and `Trace` (`TraceString`).
- Implemented aggregate roots and entities:
  - `Detector`: manages administrative lifecycle transitions (`Active`, `Paused`, `Retired`), invariants, and `RecordHeartbeat(DateTime timestamp, DetectorHealth health)`.
  - `InspectionRun`: represents telemetry sweep execution linked to `DetectorId`.
  - `Occurrence`: immutable observation snapshot holding `InspectionRunId`, `OccurrenceNumber`, and `TelemetryEvidence` with schema projection accessors.
  - `Issue`: encapsulates factory `Create(...)` setting initial status `Open`, rank, and initial occurrence; `RecordRecurringOccurrence(...)` appending occurrences, incrementing counter, preserving title/base severity on active issues, and executing `BR-PRIMA-002` automatic reopening with audit field resets on closed issues.
- Implemented repository contracts `IDetectorRepository` and `IIssueRepository` supporting optional `IDbTransaction` context for atomic multi-entity ingestion transactions.
- Defined `DetectorErrors` and `IssueErrors` for RFC 7807 problem details mapping.
- Verified clean build (`0 Warning(s)`, `0 Error(s)`) via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Changed Files:
- `src/backend/PrIMA.Api/Domain/Detectors/AdminStatus.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/Detector.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/DetectorErrors.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/DetectorHealth.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/IDetectorRepository.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/InspectionOutcome.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/InspectionRun.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/MonitoringScope.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/TriggerSource.cs`
- `src/backend/PrIMA.Api/Domain/Issues/CorrelationKey.cs`
- `src/backend/PrIMA.Api/Domain/Issues/IIssueRepository.cs`
- `src/backend/PrIMA.Api/Domain/Issues/Issue.cs`
- `src/backend/PrIMA.Api/Domain/Issues/IssueErrors.cs`
- `src/backend/PrIMA.Api/Domain/Issues/IssueSeverity.cs`
- `src/backend/PrIMA.Api/Domain/Issues/IssueStatus.cs`
- `src/backend/PrIMA.Api/Domain/Issues/Occurrence.cs`
- `src/backend/PrIMA.Api/Domain/Issues/SeverityRank.cs`
- `src/backend/PrIMA.Api/Domain/Issues/TelemetryEvidence.cs`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

### P2-S05

Title: Persistence Layer & Dapper Repositories

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement high-performance direct SQL repositories using Dapper for Detectors, Issues, Inspection Runs, and governance updates.

Depends On: P1-S02, P2-S04  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api/Infrastructure/Persistence/DetectorRepository.cs` is implemented:
   - `GetByIdAsync(Guid detectorId, IDbConnection conn, IDbTransaction tx)`: Queries `dbo.Detectors`.
   - `UpdateLivenessAsync(Guid detectorId, DateTime runTimeUtc, DetectorHealth health, IDbConnection conn, IDbTransaction tx)`: Updates `LastRunAtUtc`, `LastHealthyHeartbeatUtc`, and `HealthStatus = 'Healthy'`.
2. `src/backend/PrIMA.Api/Infrastructure/Persistence/IssueRepository.cs` is implemented:
   - `FindByCorrelationKeyWithLockAsync(string correlationKey, IDbConnection conn, IDbTransaction tx)`: Queries `dbo.Issues` using `SELECT ... WITH (UPDLOCK, HOLDLOCK) WHERE CorrelationKey = @Key`.
   - `InsertIssueAsync(Issue issue, IDbConnection conn, IDbTransaction tx)`: Inserts into `dbo.Issues`.
   - `UpdateRecurringIssueAsync(Issue issue, IDbConnection conn, IDbTransaction tx)`: Updates `OccurrenceCount`, `LastOccurrenceAtUtc` in `dbo.Issues`.
   - `ReopenIssueAsync(Issue issue, IDbConnection conn, IDbTransaction tx)`:
     - Updates `dbo.Issues` with `Status = 'Reopened'`, `ReopenedAtUtc`, `OccurrenceCount`, `LastOccurrenceAtUtc`, and sets `CurrentAssignee = NULL, AcknowledgedAtUtc = NULL, ResolvedAtUtc = NULL, DismissedAtUtc = NULL`.
     - Executes non-destructive invalidation:
       - `UPDATE dbo.RemediationRecords SET IsSuperseded = 1 WHERE IssueId = @Id;`
       - `UPDATE dbo.VerificationRecords SET IsSuperseded = 1 WHERE IssueId = @Id;`
       - `UPDATE dbo.DismissalRecords SET IsSuperseded = 1 WHERE IssueId = @Id;`
   - `InsertOccurrenceAsync(Occurrence occurrence, IDbConnection conn, IDbTransaction tx)`: Inserts into `dbo.Occurrences`.
   - `InsertInspectionRunAsync(InspectionRun run, IDbConnection conn, IDbTransaction tx)`: Inserts into `dbo.InspectionRuns`.
3. Repositories are registered in DI container in `Program.cs`.

Notes: Implements `GAP-004`, `GAP-005`, and concurrency locking (`UPDLOCK, HOLDLOCK`).

Implementation Notes:
- Implemented `DetectorRepository` under `src/backend/PrIMA.Api/Infrastructure/Persistence/`:
  - `GetByIdAsync` querying `dbo.Detectors` with mapping to `Detector` aggregate.
  - `UpdateLivenessAsync` updating `LastRunAtUtc`, `LastHealthyHeartbeatUtc`, and `HealthStatus = 'Healthy'` via parametrized Dapper SQL.
  - `GetByNameAsync`, `GetAllAsync`, `InsertAsync`, `UpdateAsync`, and `InsertInspectionRunAsync`.
- Implemented `IssueRepository` under `src/backend/PrIMA.Api/Infrastructure/Persistence/`:
  - `FindByCorrelationKeyWithLockAsync` utilizing `SELECT ... WITH (UPDLOCK, HOLDLOCK) WHERE CorrelationKey = @Key` for strict concurrency isolation during telemetry ingestion.
  - `InsertIssueAsync` / `InsertAsync` inserting new anomaly records into `dbo.Issues`.
  - `UpdateRecurringIssueAsync` updating `OccurrenceCount` and `LastOccurrenceAtUtc` for active recurring issues.
  - `ReopenIssueAsync` executing atomic update setting `Status = 'Reopened'`, `ReopenedAtUtc`, `OccurrenceCount`, `LastOccurrenceAtUtc`, clearing assignee/acknowledgment/closure timestamps (`NULL`), and invalidating prior governance records (`UPDATE dbo.RemediationRecords/VerificationRecords/DismissalRecords SET IsSuperseded = 1 WHERE IssueId = @Id`).
  - `InsertOccurrenceAsync` inserting observation snapshots into `dbo.Occurrences`.
  - `InsertInspectionRunAsync` inserting telemetry run sweeps into `dbo.InspectionRuns`.
  - `SupersedePriorGovernanceRecordsAsync` and `GetOccurrencesByIssueIdAsync`.
- Updated `IDetectorRepository` and `IIssueRepository` interfaces with overloads accepting `IDbConnection` and `IDbTransaction` to seamlessly support ambient or transaction-scoped execution.
- Registered `IDetectorRepository` and `IIssueRepository` as scoped services in `Program.cs`.
- Verified clean build (`0 Warning(s)`, `0 Error(s)`) via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Changed Files:
- `src/backend/PrIMA.Api/Domain/Detectors/IDetectorRepository.cs`
- `src/backend/PrIMA.Api/Domain/Issues/IIssueRepository.cs`
- `src/backend/PrIMA.Api/Infrastructure/Persistence/DetectorRepository.cs`
- `src/backend/PrIMA.Api/Infrastructure/Persistence/IssueRepository.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

## P3 - Backend API Features & Tooling

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P3-S06

Title: Officer Authentication Feature (S-00)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement officer sign-in endpoint issuing 12-hour HS256 JWT Bearer tokens with PBKDF2 password verification.

Depends On: P1-S02, P2-S05  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. DTOs and validator created under `src/backend/PrIMA.Api/Features/Auth/`:
   - `LoginRequest.cs` (`Username`, `Password`).
   - `LoginResponse.cs` (`Token`, `ExpiresAtUtc`, `Officer: { Id, Username, FullName, Role }`).
   - `LoginValidator.cs`: FluentValidation ensuring non-empty username and password.
2. `LoginEndpoint.cs` / `LoginHandler.cs` is implemented:
   - Queries `dbo.Officers` by `Username`.
   - If user not found, returns `401 Unauthorized` RFC 7807 ProblemDetails ("Invalid username or password.").
   - If `IsActive == false`, returns `401 Unauthorized` RFC 7807 ProblemDetails ("Account is deactivated. Contact EDP Lead.").
   - Verifies password against `dbo.Officers.PasswordHash` using `Microsoft.AspNetCore.Identity.PasswordHasher<object>`. If invalid, returns `401 Unauthorized`.
   - Generates HS256 JWT token with claims: `sub` (Id), `name` (Username), `fullName`, `role`.
   - Token expiration set to configured `Jwt:ExpirationHours` (12 hours).
3. Endpoint mapped at `POST /api/auth/login` with `.AllowAnonymous()`.

Notes: Implements `OQ-002` and `OQ-003`.

Implementation Notes:
- Created `LoginRequest.cs` and `LoginResponse.cs` (with nested `OfficerDto`) under `src/backend/PrIMA.Api/Features/Auth/` matching OpenAPI contract.
- Created `LoginValidator.cs` with FluentValidation rules requiring non-empty username (including non-whitespace) and password.
- Implemented `LoginHandler.cs`:
  - Queries `dbo.Officers` by username using Dapper direct SQL.
  - Returns RFC 7807 `401 Unauthorized` (`INVALID_CREDENTIALS`, "Invalid username or password.") if officer is null or password verification fails.
  - Returns RFC 7807 `401 Unauthorized` (`ACCOUNT_DEACTIVATED`, "Account is deactivated. Contact EDP Lead.") if officer `IsActive == false`.
  - Verifies hashed password using ASP.NET Core `PasswordHasher<object>` (PBKDF2).
  - Emits HS256 JWT Bearer token with claims: `sub` (Id), `name` (Username), `fullName`, `role`.
  - Sets expiration according to `Jwt:ExpirationHours` (defaulting to 12h).
- Implemented `LoginEndpoint.cs` mapping `POST /api/auth/login` with `.AllowAnonymous()` and OpenAPI documentation.
- Registered `LoginHandler` in DI and mapped `app.MapAuthEndpoints()` in `Program.cs`.
- Verified clean build (`0 Warning(s)`, `0 Error(s)`) via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Changed Files:
- `src/backend/PrIMA.Api/Features/Auth/LoginRequest.cs`
- `src/backend/PrIMA.Api/Features/Auth/LoginResponse.cs`
- `src/backend/PrIMA.Api/Features/Auth/LoginValidator.cs`
- `src/backend/PrIMA.Api/Features/Auth/LoginHandler.cs`
- `src/backend/PrIMA.Api/Features/Auth/LoginEndpoint.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

### P3-S07

Title: Telemetry Ingestion Pipeline (POST /api/ingestion/telemetry)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement standardized telemetry ingestion endpoint supporting batch findings, API key authentication, atomic transaction processing, guardrails, and anomaly deduplication/reopening.

Depends On: P2-S05  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Security filter / middleware is implemented:
   - Validates `X-Detector-API-Key` header against `Ingestion:ApiKeys` array using `CryptographicOperations.FixedTimeEquals`.
   - Returns RFC 7807 `401 Unauthorized` on missing or invalid key.
2. DTOs and validator created under `src/backend/PrIMA.Api/Features/Ingestion/`:
   - `IngestTelemetryRequest.cs` (`detectorId`, `executionTimestampUtc`, `triggerSource`, `outcome`, `durationMs`, `failureReason`, `findings[]`).
   - `FindingItem.cs` (`targetEntityReference`, `title`, `baseSeverity`, `evidence: { module, sourceTable, recordKeys, debitTotal, creditTotal, varianceDelta, trace }`).
   - `IngestTelemetryResponse.cs` (`inspectionRunId`, `detectorId`, `outcome`, `processedFindingsCount`, `results[]`).
   - `IngestTelemetryValidator.cs`:
     - `triggerSource` in `Scheduled`, `Manual`, `EventHook`.
     - `outcome` in `Passed`, `FindingDiscovered`, `ExecutionFailed`.
     - If `outcome == "ExecutionFailed"`: rejects with `422 Unprocessable Entity` ("ExecutionFailed outcome is supported in Stage 3 with infrastructure anomaly synthesis.").
     - If `outcome == "Passed"`: `findings` must be empty or null.
     - If `outcome == "FindingDiscovered"`: `findings` must contain `>= 1` items.
     - Enforces intra-batch uniqueness: rejects duplicate `targetEntityReference` (case-insensitive after trim) with `422 Unprocessable Entity`.
     - Validates string lengths (`targetEntityReference <= 213`, `title <= 250`).
3. `IngestTelemetryHandler.cs` & `IngestTelemetryEndpoint.cs` implemented:
   - Detector guardrails:
     - If detector does not exist in `dbo.Detectors`, returns `404 Not Found`.
     - If detector `AdminStatus == Paused` or `Retired`, returns `409 Conflict` ("Detector is not active.").
   - Transaction boundary: opens connection, starts transaction (`ReadCommitted`).
   - Inserts `InspectionRun` record.
   - Updates detector liveness (`LastRunAtUtc`, `LastHealthyHeartbeatUtc`, `HealthStatus = Healthy`).
   - For each finding in `findings[]`:
     - Computes canonical `CorrelationKey`: `$"{detectorId:D}:{targetEntityReference.Trim().ToUpperInvariant()}"`.
     - Queries issue with update lock (`UPDLOCK, HOLDLOCK`).
     - If not found: creates new `Issue` (`Status = Open`, `OccurrenceNumber = 1`), inserts issue and occurrence. Retries lookup once if concurrency error `2601`/`2627` occurs on `UX_Issues_CorrelationKey`. Result action: `Created`.
     - If found and active (`Open`, `Acknowledged`, `Investigating`, `Reopened`): invokes `RecordRecurringOccurrence`, updates issue, inserts occurrence (`OccurrenceNumber = N+1`). Result action: `Appended`.
     - If found and closed (`Resolved`, `Dismissed`): invokes `RecordRecurringOccurrence` (`BR-PRIMA-002`), updates issue (`Reopened`), supersedes prior records (`IsSuperseded = 1`), inserts occurrence (`OccurrenceNumber = N+1`). Result action: `Reopened`.
   - Commits transaction atomically.
   - Returns `200 OK` with `IngestTelemetryResponse`.
4. Endpoint mapped at `POST /api/ingestion/telemetry`.

Notes: Implements `GAP-002`, `GAP-004`, `GAP-005`, `GAP-007`, `OQ-001`, `BR-PRIMA-001`, `BR-PRIMA-002`, and `BR-PRIMA-009`.

Implementation Notes:
- Created `ApiKeyEndpointFilter.cs` validating `X-Detector-API-Key` against `Ingestion:ApiKeys` using constant-time `CryptographicOperations.FixedTimeEquals`, returning RFC 7807 401 Unauthorized ProblemDetails on failure.
- Implemented request/response DTOs: `FindingItem.cs`, `IngestTelemetryRequest.cs`, and `IngestTelemetryResponse.cs`.
- Implemented `IngestTelemetryValidator.cs` enforcing canonical trigger sources, outcomes, Stage 1 rejection of `ExecutionFailed` (422), empty findings for `Passed`, non-empty findings for `FindingDiscovered`, intra-batch case-insensitive duplicate entity reference rejection (422), and string length constraints.
- Implemented `IngestTelemetryHandler.cs` enforcing detector guardrails (404 for not found, 409 for Paused/Retired), atomic `ReadCommitted` transaction boundary, `InspectionRun` insertion, detector heartbeat/liveness update, and finding processing using `UPDLOCK, HOLDLOCK`. Handled new issue creation with concurrency retry (2601/2627), active issue occurrence appending, and BR-PRIMA-002 reopening of resolved/dismissed issues with non-destructive governance record invalidation (`IsSuperseded = 1`).
- Implemented `IngestTelemetryEndpoint.cs` mapping `POST /api/ingestion/telemetry` with endpoint filter and OpenAPI metadata, and wired handler/filter/endpoint into `Program.cs`.
- Verified clean build (`0 Warning(s)`, `0 Error(s)`) via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Changed Files:
- `src/backend/PrIMA.Api/Features/Ingestion/ApiKeyEndpointFilter.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/FindingItem.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryRequest.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryResponse.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryValidator.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryHandler.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryEndpoint.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

### P3-S08

Title: Live Dashboard Feed Query Feature (S-01)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement the live active issue dashboard feed endpoint providing global summary counters and sorted, filtered active issues.

Depends On: P2-S05  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Endpoint authorization: Secured via `.RequireAuthorization()` (valid JWT Bearer token required; flat v1 authorization for all roles).
2. DTOs created under `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/`:
   - `GetDashboardFeedQuery.cs` (`status`, `severity`, `scope`, `search`).
   - `GetDashboardFeedResponse.cs` (`summary: { criticalCount, highCount, openCount, reopenedCount, resolvedTodayCount }`, `items[]`).
   - `IssueFeedItemDto.cs` (`id`, `correlationKey`, `title`, `detectorId`, `detectorName`, `scope`, `currentSeverity`, `severityRank`, `status`, `escalationLevel`, `occurrenceCount`, `currentAssignee`, `createdAtUtc`, `lastOccurrenceAtUtc`).
3. Query validation:
   - If query parameter `status` equals `Resolved` or `Dismissed` (case-insensitive), returns `422 Unprocessable Entity` RFC 7807 ProblemDetails ("Dashboard feed displays active issues only. Closed issues are not accessible via this endpoint.").
4. `GetDashboardFeedHandler.cs` is implemented:
   - Timezone boundary: reads `OperationalTimeZone` (`Asia/Jakarta`), converts current UTC instant to `Asia/Jakarta`, determines start of today (00:00:00 WIB) and start of tomorrow (00:00:00 WIB next day), and converts both back to UTC (`startOfTodayWibUtc`, `startOfTomorrowWibUtc`).
   - Query 1 (Global Unfiltered Summary):
     - Executes aggregation query counting `CriticalCount`, `HighCount`, `OpenCount`, `ReopenedCount`, and `ResolvedTodayCount` (where `Status = 'Resolved' AND ResolvedAtUtc >= @startOfTodayWibUtc AND ResolvedAtUtc < @startOfTomorrowWibUtc`).
   - Query 2 (Filtered Active Feed):
     - Queries `dbo.Issues` where `Status NOT IN ('Resolved', 'Dismissed')`.
     - Applies optional filters: `@Status`, `@Severity`, `@Scope`, and `@Search` (substring matching against `Title`, `DetectorName`, and `CorrelationKey`).
     - Orders results strictly by `ORDER BY SeverityRank DESC, LastOccurrenceAtUtc DESC`.
5. Endpoint mapped at `GET /api/issues/dashboard-feed`.

Notes: Implements `OQ-005` and `RISK-002`.

Implementation Notes:
- Created DTOs under `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/`:
  - `GetDashboardFeedQuery` binding query filters (`status`, `severity`, `scope`, `search`).
  - `IssueFeedItemDto` matching active issue projection columns.
  - `GetDashboardFeedResponse` containing `summary` (`DashboardFeedSummaryDto`) and `items` (`IReadOnlyList<IssueFeedItemDto>`).
- Implemented `GetDashboardFeedHandler`:
  - Enforced query validation returning RFC 7807 `422 Unprocessable Entity` (`IssueErrors.ClosedStatusFeedFilter`) if `status` query filter equals `Resolved` or `Dismissed` (case-insensitive).
  - Calculated operational day boundaries in UTC using configured `OperationalTimeZone` (`Asia/Jakarta`) for accurate today resolution tracking (`[startOfTodayWibUtc, startOfTomorrowWibUtc)`).
  - Executed high-performance multi-query via Dapper:
    - Query 1: Unfiltered global summary aggregation counting `CriticalCount`, `HighCount`, `OpenCount`, `ReopenedCount`, and `ResolvedTodayCount`.
    - Query 2: Active feed query filtering out `Resolved` and `Dismissed` issues, applying optional filters (`@Status`, `@Severity`, `@Scope`, `@Search`), and strictly ordering by `SeverityRank DESC, LastOccurrenceAtUtc DESC`.
- Implemented `GetDashboardFeedEndpoint`:
  - Mapped route at `GET /api/issues/dashboard-feed`.
  - Enforced flat JWT Bearer authorization using `.RequireAuthorization()`.
  - Configured OpenAPI metadata and tags.
- Registered `GetDashboardFeedHandler` in DI container and mapped route in `Program.cs`.
- Verified clean build (`0 Warning(s)`, `0 Error(s)`) via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Changed Files:
- `src/backend/PrIMA.Api/Domain/Issues/IssueErrors.cs`
- `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/GetDashboardFeedEndpoint.cs`
- `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/GetDashboardFeedHandler.cs`
- `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/GetDashboardFeedQuery.cs`
- `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/GetDashboardFeedResponse.cs`
- `src/backend/PrIMA.Api/Features/Issues/GetDashboardFeed/IssueFeedItemDto.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

### P3-S09

Title: Development Demo Data Seeder & Node.js Telemetry Simulator

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement the development-only demo data seeder and zero-dependency Node.js CLI telemetry simulator to exercise all Stage 1 operational scenarios.

Depends On: P3-S07, P3-S08  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api/Seeding/DevelopmentDemoDataSeeder.cs` is implemented:
   - Gated on `IHostEnvironment.IsDevelopment() && Configuration.GetValue<bool>("SeedDemoData")`.
   - Idempotently seeds 4 active detectors across scopes (`Billing Journal Balance Checker`, `Patient Admission Discharge Sync`, `Prescription Stock Deductor`, `EMR Database Replication Monitor`).
   - Idempotently seeds 1 paused detector (`Laboratory Order Sync Prober`, `AdminStatus = Paused`).
   - Idempotently seeds 1 resolved issue with `VerificationRecord` (`CleanPassed = 1`, `IsSuperseded = 0`) and `ResolvedAtUtc` set to today WIB.
   - Idempotently seeds 1 dismissed issue with `DismissalRecord` (`IsSuperseded = 0`).
2. Seeder is invoked during startup in `Program.cs`.
3. `src/tools/telemetry-simulator/simulate.mjs` is created:
   - Zero-dependency Node.js CLI script using native `fetch`.
   - Accepts `--url <endpoint>` (defaults to `http://localhost:5000/api/ingestion/telemetry`) and `--key <key>` (defaults to `prima-dev-key-alpha-99`).
   - Implements CLI scenarios:
     - `passed`: Submits inspection run with 0 findings (`outcome = "Passed"`).
     - `new-finding`: Submits finding with unique `targetEntityReference` creating an `Open` issue.
     - `duplicate-finding`: Submits same finding again verifying `OccurrenceCount` increment and occurrence appending.
     - `recurrence-resolved`: Submits finding matching the seeded `Resolved` issue to verify `BR-PRIMA-002` reopening.
     - `recurrence-dismissed`: Submits finding matching the seeded `Dismissed` issue to verify `BR-PRIMA-002` reopening.
     - `batch-findings`: Submits multiple distinct findings in a single inspection run.
   - Prints formatted JSON responses and HTTP status to console.

Notes: Implements `GAP-001`, `OQ-004`, and simulator tooling.

Implementation Notes:
- Created `DevelopmentDemoDataSeeder.cs` gated strictly on `IHostEnvironment.IsDevelopment() && Configuration.GetValue<bool>("SeedDemoData")`.
  - Idempotently seeds 4 active detectors: `Billing Journal Balance Checker` (`DataIntegrity`), `Patient Admission Discharge Sync` (`ProcessIntegrity`), `Prescription Stock Deductor` (`DataSync`), and `EMR Database Replication Monitor` (`Infrastructure`).
  - Idempotently seeds 1 paused detector: `Laboratory Order Sync Prober` (`AdminStatus = Paused`).
  - Idempotently seeds 1 resolved issue with associated `InspectionRun`, `Occurrence`, and `VerificationRecord` (`CleanPassed = 1`, `IsSuperseded = 0`) with `ResolvedAtUtc` set within current day WIB (`Asia/Jakarta`).
  - Idempotently seeds 1 dismissed issue with associated `InspectionRun`, `Occurrence`, and `DismissalRecord` (`IsSuperseded = 0`).
- Registered `DevelopmentDemoDataSeeder` in DI and invoked it asynchronously in `Program.cs` during application startup post-migration under development environment and `SeedDemoData` configuration gate.
- Created `src/tools/telemetry-simulator/simulate.mjs`:
  - Zero-dependency Node.js CLI utility using native `fetch` and ESM module structure.
  - Implements CLI options `--url` (defaults to `http://localhost:5000/api/ingestion/telemetry`) and `--key` (defaults to `prima-dev-key-alpha-99`).
  - Implements all required scenarios (`passed`, `new-finding`, `duplicate-finding`, `recurrence-resolved`, `recurrence-dismissed`, `batch-findings`), plus `paused-detector` (409 validation) and `all` (batch sequential run).
  - Nicely formats HTTP status and indented JSON responses to stdout.
- Verified backend builds cleanly (`0 Warning(s)`, `0 Error(s)`) via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj` and all 22 unit tests pass.

Changed Files:
- `src/backend/PrIMA.Api/Seeding/DevelopmentDemoDataSeeder.cs`
- `src/backend/PrIMA.Api/Program.cs`
- `src/tools/telemetry-simulator/simulate.mjs`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

## P4 - Frontend UI Features

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P4-S10

Title: Frontend Core Infrastructure, Axios Client, Auth Store & App Shell

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Build the frontend core foundation: API type definitions, authenticated Axios client, Pinia auth store with localStorage session persistence, navigation guards, and App Shell layout enforcing Stage 1 boundaries.

Depends On: P1-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. TypeScript API contracts generated or mapped in `src/frontend/src/shared/types/api.generated.ts` matching Swagger specifications.
2. Axios client implemented in `src/frontend/src/shared/api/http.ts`:
   - Configured with `baseURL: '/api'`.
   - Request interceptor: attaches `Authorization: Bearer <token>` when token is available in auth store.
   - Response interceptor: on `401 Unauthorized`, clears auth store session, redirects to `/login?redirect=<safe-path>`. Translates RFC 7807 `ProblemDetails` to typed `ApiError`.
3. Pinia auth store implemented in `src/frontend/src/app/stores/auth.store.ts`:
   - State: `token`, `officer`, `isAuthenticated`.
   - Actions: `login(username, password)`, `logout()`, `restoreSession()`.
   - Persists token and officer in `localStorage` (`prima_token`, `prima_officer`) for F5 reload persistence.
4. Vue Router configured in `src/frontend/src/app/router/index.ts`:
   - Route `/login` mapped to `LoginView.vue`.
   - Route `/` mapped to `AppLayout.vue` wrapping `DashboardView.vue`.
   - Navigation guard: restricts unauthenticated access to `/login`; redirects authenticated users away from `/login` to `/`. Validates redirect param against open redirects.
5. App Shell layout created in `src/frontend/src/app/layouts/AppLayout.vue`:
   - Header: PrIMA brand title, live WIB clock, logged-in officer name & role pill, Logout button.
   - Sidebar navigation:
     - `Dashboard` (active, styled, displays badge pill with `open + reopened` count).
     - `Detectors` (visibly disabled, `aria-disabled="true"`, tooltip `"Coming in Stage 3"`).
     - `History & Reports` (visibly disabled, `aria-disabled="true"`, tooltip `"Coming in Stage 4"`).
   - Footer: operational environment badge and connection status.

Notes: Successfully implemented frontend core infrastructure adhering to Target Architecture §4.7, §4.10, and §9.1. API contracts strongly mapped to OpenAPI types; Axios HTTP client configured with automatic JWT attachment and typed RFC 7807 `ProblemDetails` (`ApiError`) translation; Pinia auth store implements 12-hour session persistence with `localStorage` (`prima_token`, `prima_officer`); Vue Router configures navigation guards preventing open redirects and protecting private routes; App Shell layout implements desktop-first operational workbench with brand title, live WIB clock, officer role display, active issue count badge pill, and Stage 3/4 boundary disabled links. Built cleanly with `vue-tsc -b && vite build`.

Changed Files:
- `src/frontend/src/shared/types/api.generated.ts`
- `src/frontend/src/shared/api/errors.ts`
- `src/frontend/src/shared/api/http.ts`
- `src/frontend/src/shared/utils/security.ts`
- `src/frontend/src/shared/composables/useAppMetrics.ts`
- `src/frontend/src/app/stores/auth.store.ts`
- `src/frontend/src/app/router/index.ts`
- `src/frontend/src/router/index.ts`
- `src/frontend/src/app/layouts/AppLayout.vue`
- `src/frontend/src/features/auth/views/LoginView.vue`
- `src/frontend/src/features/dashboard/views/DashboardView.vue`
- `src/frontend/src/main.ts`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

### P4-S11

Title: Frontend Officer Login Screen (S-00)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement the responsive officer login view (S-00) with VeeValidate + Zod form validation, inline credential error feedback, and redirect restoration.

Depends On: P3-S06, P4-S10  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Login view implemented in `src/frontend/src/features/auth/views/LoginView.vue`:
   - Professional workbench layout centered on dark-slate background.
   - System title: "PrIMA" — Proactive Inspection Monitoring App.
   - Form fields: Username input, Password input (with toggle visibility), Sign In button.
2. Form validation using VeeValidate and Zod schema:
   - Username: required, trimmed.
   - Password: required.
   - Validation messages displayed inline below inputs.
3. API integration and error handling:
   - On submission, calls `authStore.login(username, password)`.
   - Submitting state: button is disabled with PrimeVue progress spinner.
   - On `401 Unauthorized`: displays inline alert banner ("Invalid username or password.").
   - On unexpected error: displays friendly connection error alert.
   - On success: navigates to `redirect` query parameter (if valid relative path) or `/`.

Notes: Implements Screen `S-00` and Use Case `S-00`.

Implementation Notes:
- Implemented `LoginView.vue` with centered workbench card on a `bg-slate-900` dark-slate background, brand header "PrIMA" — Proactive Inspection Monitoring App, and role badge "EDP / IT Support Sign In".
- Built form schema validation with VeeValidate `useForm` and `@vee-validate/zod` + `zod` (`z.object` enforcing trimmed, non-empty username and non-empty password). Inline error messages are rendered below each input field.
- Integrated PrimeVue form controls: `InputText`, `Password` (with `:feedback="false"` and `toggleMask` visibility toggling), `Message` for inline alert banners, and `Button` disabled with `ProgressSpinner` during `isSubmitting`.
- Handled API integration with `authStore.login(username, password)`: displays RFC 7807 401 Unauthorized inline alert banner ("Invalid username or password.") and friendly connection error alert on unexpected failures.
- Restores original navigation path via `getSafeRedirect(route.query.redirect, '/')` protecting against open redirect vulnerabilities.
- Verified clean build via `npm run build` (`vue-tsc -b && vite build`) in `src/frontend/`.

Changed Files:
- `src/frontend/src/features/auth/views/LoginView.vue`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

### P4-S12

Title: Frontend Live Issue Dashboard (S-01)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement the live active issue dashboard workbench (S-01) with summary KPI tiles, multi-dimension filter bar, high-density active issue feed, and 10-second non-disruptive polling.

Depends On: P3-S08, P4-S10  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. TanStack Query composable implemented in `src/frontend/src/features/dashboard/composables/useDashboardFeed.ts`:
   - Queries `GET /api/issues/dashboard-feed` with reactive filter parameters (`status`, `severity`, `scope`, `search`).
   - Configured with `refetchInterval: 10000` (10s polling).
   - Configured with `placeholderData: keepPreviousData` so background refetches never cause UI flashing, scroll jumps, or input wipes.
   - Skeletons displayed only during initial cold load (`isPending`).
2. Summary counters bar component implemented in `src/frontend/src/features/dashboard/components/SummaryCountersBar.vue`:
   - 5 KPI tiles:
     - `Critical` (crimson badge / red accent).
     - `High` (amber badge / orange accent).
     - `Open` (blue accent).
     - `Reopened` (purple accent).
     - `Resolved Today` (green accent; non-clickable; tooltip `"History view available in Stage 4"`).
   - Clicking active KPI tiles toggles or sets corresponding filter.
3. Filter bar component implemented in `src/frontend/src/features/dashboard/components/DashboardFilterBar.vue`:
   - Status dropdown: `All`, `Open`, `Acknowledged`, `Investigating`, `Reopened` (excludes Resolved/Dismissed).
   - Severity dropdown: `All`, `Critical`, `High`, `Medium`, `Low`.
   - Scope dropdown: `All`, `DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`.
   - Debounced (300ms) text search input for Title, Detector Name, and CorrelationKey.
   - "Reset Filters" button returning filters to default state.
4. Active issue feed component implemented in `src/frontend/src/features/dashboard/components/ActiveIssueFeed.vue`:
   - Dense table/list view optimized for EDP officers.
   - Columns: Severity badge (ranked styling), Issue Title & CorrelationKey, Scope pill, Status badge, Occurrence count pill (`xN`), Recency (`LastOccurrenceAtUtc` formatted in `Asia/Jakarta` WIB `dd MMM yyyy, HH:mm:ss`), Detector Name.
   - Rows are non-navigating (Stage 1 boundary, no dead link to S-02).
   - Empty states:
     - All-clean state: "No active issues detected. All hospital inspection routines running normally."
     - Filtered empty state: "No issues match the selected filters." with a "Reset Filters" button.
5. Dashboard view orchestrator implemented in `src/frontend/src/features/dashboard/views/DashboardView.vue` assembling summary bar, filter bar, and active issue feed.

Notes: Implements Screen `S-01`, Use Cases `A-1` & `A-2`, and `OQ-005` / `OQ-006`.

Implementation Notes:
- Implemented `src/frontend/src/shared/utils/dateUtils.ts` providing Asia/Jakarta (WIB) temporal formatting (`dd MMM yyyy, HH:mm:ss`) and scannable relative time helpers (`formatRelativeTime`).
- Implemented `src/frontend/src/features/dashboard/composables/useDashboardFeed.ts`:
  - Consumes `GET /api/issues/dashboard-feed` with reactive filter params (`status`, `severity`, `scope`, `search`).
  - Configured with `refetchInterval: 10000` (10s polling) and `placeholderData: keepPreviousData` ensuring background refetches never disrupt scroll position or user inputs.
  - Synchronizes active issue counter (`openCount + reopenedCount`) to App Shell navigation badge via `activeIssuesCount`.
- Implemented `src/frontend/src/features/dashboard/components/SummaryCountersBar.vue`:
  - 5 KPI tiles (`Critical`, `High`, `Open`, `Reopened`, and `Resolved Today`).
  - Implements toggle filtering for `Critical`, `High`, `Open`, and `Reopened` tiles with visual ring/border feedback.
  - Enforces Stage 4 boundary on `Resolved Today` with disabled/non-clickable state and tooltip `"History view available in Stage 4"`.
- Implemented `src/frontend/src/features/dashboard/components/DashboardFilterBar.vue`:
  - Status filter dropdown (`All`, `Open`, `Acknowledged`, `Investigating`, `Reopened`), strictly omitting closed statuses.
  - Severity dropdown (`All`, `Critical`, `High`, `Medium`, `Low`) and Scope dropdown (`All`, `DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`).
  - Debounced (300ms) search input with quick-clear button matching title, detector, or correlation key.
  - "Reset Filters" button restoring default filter state.
- Implemented `src/frontend/src/features/dashboard/components/ActiveIssueFeed.vue`:
  - Dense tabular presentation optimized for EDP triage.
  - Severity badges with left accent border and dynamic escalation pill (`↑ +N`).
  - Formatted WIB timestamp (`dd MMM yyyy, HH:mm:ss`) with relative time caption and tooltip.
  - Empty states: All-clean state ("No active issues detected...") and Filtered empty state ("No issues match the selected filters." with Reset Filters button).
  - Shimmering skeleton rows during initial cold load (`isPending`).
  - Non-navigating rows respecting Stage 1 boundaries.
- Implemented `src/frontend/src/features/dashboard/views/DashboardView.vue`:
  - Orchestrates summary counters, filter bar, active feed, live sync polling indicator, manual refresh button, and recurrence alert strip.
- Verified clean build (`npm run build` in `src/frontend/` passed with 0 errors).

Changed Files:
- `src/frontend/src/shared/utils/dateUtils.ts`
- `src/frontend/src/features/dashboard/composables/useDashboardFeed.ts`
- `src/frontend/src/features/dashboard/components/SummaryCountersBar.vue`
- `src/frontend/src/features/dashboard/components/DashboardFilterBar.vue`
- `src/frontend/src/features/dashboard/components/ActiveIssueFeed.vue`
- `src/frontend/src/features/dashboard/views/DashboardView.vue`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

## P5 - Automated Testing & Verification

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P5-S13

Title: Domain Unit Tests & API Integration Test Suite

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement automated xUnit test suite covering domain invariants, authentication, ingestion pipeline, deduplication, auto-reopening, and dashboard feed queries.

Depends On: P3-S06, P3-S07, P3-S08  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj` created with `xunit`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`, and `FluentAssertions`.
2. Domain Unit Tests implemented:
   - `CorrelationKeyTests`: Verifies canonical formula formatting, whitespace trimming, uppercase conversion, length enforcement, null checks.
   - `IssueAggregateTests`:
     - Verifies `Issue.Create` sets status `Open`, `SeverityRank`, `EscalationLevel = 0`, `OccurrenceCount = 1`.
     - Verifies `RecordRecurringOccurrence` on active issue appends occurrence, increments count, keeps title/severity immutable.
     - Verifies `RecordRecurringOccurrence` on closed issue (`Resolved`, `Dismissed`) triggers `BR-PRIMA-002`: status transitions to `Reopened`, timestamps and assignee reset, severity preserved.
3. API Integration Tests implemented using `WebApplicationFactory<Program>`:
   - `AuthEndpointsTests`:
     - Successful login with seeded user returns 200 and valid JWT with expected claims.
     - Invalid password returns 401 ProblemDetails.
     - Inactive officer returns 401 ProblemDetails.
   - `IngestionEndpointsTests`:
     - Missing or invalid `X-Detector-API-Key` returns 401 ProblemDetails.
     - Unknown `detectorId` returns 404 ProblemDetails.
     - Paused detector returns 409 ProblemDetails.
     - `ExecutionFailed` outcome returns 422 ProblemDetails.
     - Duplicate `targetEntityReference` in same payload returns 422 ProblemDetails.
     - `Passed` outcome records inspection run with 0 findings and returns 200 OK.
     - `FindingDiscovered` outcome records run, creates `Open` issue with `OccurrenceNumber = 1`.
     - Recurring finding appends `OccurrenceNumber = 2`, increments `OccurrenceCount`, does not create new issue.
     - Recurring finding on closed issue reopens issue and marks prior governance records `IsSuperseded = 1`.
   - `DashboardFeedTests`:
     - Unauthenticated request returns 401.
     - Querying `status=Resolved` or `status=Dismissed` returns 422 ProblemDetails.
     - Summary counters calculate correctly across WIB midnight boundary.
     - Active feed items strictly ordered by `SeverityRank DESC, LastOccurrenceAtUtc DESC`.
     - Filtering by status, severity, scope, and text search returns accurate matching items.
4. All tests pass cleanly via `dotnet test src/backend/PrIMA.Api.Tests/`.

Notes: Implements automated regression gate for backend and domain rules.

Implementation Notes:
- Created `src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj` targeting `net8.0` with `xunit`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`, and `FluentAssertions`. Added project reference to `PrIMA.Api` and registered test project in `src/backend/PrIMA.slnx`.
- Implemented Domain Unit Tests:
  - `src/backend/PrIMA.Api.Tests/Domain/CorrelationKeyTests.cs`: Verified canonical key formatting (`{detectorId:D}:{targetEntityReference.Trim().ToUpperInvariant()}`), whitespace trimming, uppercase conversion, length boundary enforcement (250 chars max), null/empty rejection, and implicit conversions.
  - `src/backend/PrIMA.Api.Tests/Domain/IssueAggregateTests.cs`: Verified `Issue.Create` sets `Status = Open`, numeric `SeverityRank` (Critical=4, High=3, Medium=2, Low=1), `EscalationLevel = 0`, `OccurrenceCount = 1`, and initial occurrence; verified `RecordRecurringOccurrence` appends occurrence and preserves title/severity immutability; verified `RecordRecurringOccurrence` on closed issues (`Resolved`, `Dismissed`) triggers `BR-PRIMA-002` transitioning status to `Reopened`, updating timestamps, clearing assignee, and preserving severity.
- Implemented Integration Test Infrastructure:
  - `src/backend/PrIMA.Api.Tests/Fixtures/PrIMAApiFactory.cs`: Subclassed `WebApplicationFactory<Program>` pointing to isolated test database `PrIMA_IntegrationTests` with automatic DbUp migration execution, Dapper database helpers, test data reset routines, and token generation.
  - `src/backend/PrIMA.Api.Tests/Fixtures/ApiTestCollection.cs`: Configured shared test collection fixture with per-test database reset.
- Implemented API Integration Tests:
  - `src/backend/PrIMA.Api.Tests/Integration/AuthEndpointsTests.cs`: Tested successful login with seeded officer `ahmad.budi` returning JWT with expected `sub`, `name`, `fullName`, and `role` claims; verified 401 ProblemDetails on invalid password and deactivated officer accounts.
  - `src/backend/PrIMA.Api.Tests/Integration/IngestionEndpointsTests.cs`: Tested API key security (missing/invalid header returns 401); detector guardrails (404 for unknown detector, 409 for paused detector); validation rules (422 for `ExecutionFailed` and duplicate `targetEntityReference`); atomic run logging for `Passed` (200 OK); new issue creation with initial occurrence for `FindingDiscovered` (200 OK); deterministic deduplication on recurring finding (appending occurrence #2 without duplicate issue); and `BR-PRIMA-002` auto-reopening on closed issues with non-destructive governance record invalidation (`IsSuperseded = 1`).
  - `src/backend/PrIMA.Api.Tests/Integration/DashboardFeedTests.cs`: Tested authorization gate (401 for unauthenticated request); 422 ProblemDetails on closed status queries (`status=Resolved`, `status=Dismissed`); Asia/Jakarta (WIB) midnight boundary calculation for `ResolvedTodayCount` alongside active KPI counters; strict feed ordering (`ORDER BY SeverityRank DESC, LastOccurrenceAtUtc DESC`); and filtering across status, severity, scope, and text search (title, detector name, correlation key).
- Verified all 43 tests pass cleanly (`0 Failed`, `43 Passed`, `0 Skipped`) via `dotnet test src/backend/PrIMA.Api.Tests/`.

Changed Files:
- `src/backend/PrIMA.slnx`
- `src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj`
- `src/backend/PrIMA.Api.Tests/Fixtures/PrIMAApiFactory.cs`
- `src/backend/PrIMA.Api.Tests/Fixtures/ApiTestCollection.cs`
- `src/backend/PrIMA.Api.Tests/Domain/CorrelationKeyTests.cs`
- `src/backend/PrIMA.Api.Tests/Domain/IssueAggregateTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/AuthEndpointsTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/IngestionEndpointsTests.cs`
- `src/backend/PrIMA.Api.Tests/Integration/DashboardFeedTests.cs`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

### P5-S14

Title: End-to-End Telemetry Ingestion & Live Dashboard Verification

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Perform end-to-end verification running the backend, database migrations, frontend UI, and telemetry simulator across all Stage 1 scenarios.

Depends On: P3-S09, P4-S11, P4-S12, P5-S13  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Starting `PrIMA.Api` automatically creates database if absent, applies `001_InitialSchema.sql` and `002_SeedInitialUsers.sql` without error, and seeds development fixtures (`DevelopmentDemoDataSeeder`).
2. Officer sign-in at `S-00` (`http://localhost:5173/login`) succeeds with `ahmad.budi` / `Hospital@2026!`, stores token in `localStorage`, survives browser reload (F5), and displays officer details in App Shell.
3. Telemetry simulator (`node src/tools/telemetry-simulator/simulate.mjs`) exercises all scenarios successfully:
   - `node simulate.mjs passed` -> 200 OK.
   - `node simulate.mjs new-finding` -> 200 OK (Created).
   - `node simulate.mjs duplicate-finding` -> 200 OK (Appended).
   - `node simulate.mjs recurrence-resolved` -> 200 OK (Reopened).
   - `node simulate.mjs recurrence-dismissed` -> 200 OK (Reopened).
   - `node simulate.mjs batch-findings` -> 200 OK (Batch processed).
4. `S-01` Dashboard updates live every 10 seconds:
   - Displays accurate KPI counts for Critical, High, Open, Reopened, and Resolved Today.
   - New and recurring findings appear immediately on next polling interval without page flicker or filter loss.
   - Filters (status, severity, scope, search) filter active feed items accurately.
   - Stage 1 navigation boundaries are respected (`Detectors` and `History` links disabled with tooltips; `Resolved Today` tile and feed rows non-navigating).
5. All backend tests pass via `dotnet test` and frontend build succeeds via `npm run build`.

Notes: Verifies all Stage 1 Acceptance Conditions (§11 of Target Architecture).

Implementation Notes:
- Verified backend launch, automatic DbUp migration execution, and idempotent development fixtures seeding via `DevelopmentDemoDataSeeder`:
  - Created `launchSettings.json` with development profile configuring port 5005 (due to local IIS host binding on port 5000) and verified `EnsureDatabase.For.SqlDatabase`, schema migration, and seeding routines run successfully.
- Verified officer sign-in at `S-00` (`http://localhost:5173/login`) and backend auth endpoint through Vite proxy (`/api/auth/login`):
  - Correctly validates `ahmad.budi` / `Hospital@2026!`, returns 200 OK with valid JWT token, officer profile details (`Ahmad Budi`, role `EdpOfficer`), and session storage persistence (`localStorage`).
- Verified telemetry simulator (`simulate.mjs`) across all Stage 1 scenarios with 200 OK responses:
  - `passed`: recorded inspection run with 0 findings (200 OK).
  - `new-finding`: recorded inspection run, created new `Open` issue with `occurrenceNumber = 1` (200 OK).
  - `duplicate-finding`: submitted identical finding twice; initial returned `Created` (occurrence 1) and duplicate returned `Appended` (occurrence 2) incrementing count (200 OK).
  - `recurrence-resolved`: submitted finding matching seeded resolved issue; triggered `BR-PRIMA-002` auto-reopening with action `Reopened` (occurrence 2) (200 OK).
  - `recurrence-dismissed`: submitted finding matching seeded dismissed issue; triggered `BR-PRIMA-002` auto-reopening with action `Reopened` (occurrence 2) (200 OK).
  - `batch-findings`: submitted multi-item payload with 3 distinct entity findings; processed all 3 findings atomically in a single inspection run (200 OK).
- Verified `S-01` Dashboard live query and updates:
  - Real-time KPI summary reflects exact counts (`criticalCount`, `highCount`, `openCount`, `reopenedCount`, `resolvedTodayCount`).
  - Active feed items strictly ordered by `SeverityRank DESC, LastOccurrenceAtUtc DESC`.
  - Multidimensional filtering verified for status, severity, scope, and text search across title, detector, and correlation key.
  - Verified Stage 1 navigation boundaries in `AppLayout.vue`, `SummaryCountersBar.vue`, and `ActiveIssueFeed.vue` (`Detectors` and `History` links disabled with tooltips; `Resolved Today` tile and feed rows non-navigating).
- Verified all 43 backend tests pass cleanly via `dotnet test src/backend/PrIMA.slnx` and frontend build succeeds with 0 errors via `npm run build`.

Changed Files:
- `src/backend/PrIMA.Api/Properties/launchSettings.json`
- `src/frontend/vite.config.ts`
- `src/tools/telemetry-simulator/simulate.mjs`
- `docs/issues/IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- 2026-11-20: Initial implementation plan created and approved for PrIMA Stage 1 (Walking Skeleton & Live Ingestion Pipeline). Plan structure finalized with 5 phases, 14 continuous slices, and explicit DAG dependencies. `Execution Approval: APPROVED` granted.
