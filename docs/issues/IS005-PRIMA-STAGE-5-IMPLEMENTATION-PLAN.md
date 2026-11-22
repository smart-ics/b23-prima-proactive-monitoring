---
Title: PrIMA Stage 5 — Detector Handshake Protocol, Pilot SIMRS Detector Suite & Hospital Production Rollout Implementation Plan
Code: PRIMA-STAGE-5
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-11-22
Status: NOT-STARTED
Execution Approval: APPROVED
---

# 1. Objective

Implement Stage 5 ("Detector Handshake Protocol, Pilot SIMRS Detector Suite & Hospital Production Rollout") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`), establishing automated zero-drift detector manifest enrollment, per-detector hashed API key credentials, cadence drift supervision, the standardized .NET 8 Reference Detector SDK, 6 production pilot SIMRS detectors across all 4 monitoring scopes (`BR-PRIMA-007`), on-premise production deployment bundles, deep database health checking, automated 90-day retention pruning (`BR-PRIMA-009`), and hospital EDP operational runbooks, delivering the **Hospital On-Premise Production Go-Live & Automated Detector Enrollment** milestone.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:
- ISSUE: `docs/issues/IS005-PRIMA-STAGE-5-ISSUE.md`
- FEATURE: `docs/PRIMA-DEVELOPMENT-ROADMAP.md` (Stage 5, lines 215–264; Guardrails #4 & #6, lines 326–332), `docs/PrIMA - Product Definition.md` (§3, §7.7, §9, §10), `docs/PRIMA-USE-CASE-DISCOVERY.md` (`D-2`, `E-1`–`E-7`, Handshake Enrollment), `docs/PRIMA-SCREEN-INVENTORY.md` (`S-03`, `S-04`, `S-05`), `docs/PRIMA-UI-UX-GUIDELINE.md`
- ARCHITECTURE: `docs/issues/IS005-PRIMA-STAGE-5-ARCHITECTURE.md` (and baseline `docs/PRIMA-ARCHITECTURE.md`)
- FEASIBILITY-ASSESSMENT: `docs/issues/IS005-PRIMA-STAGE-5-FEASIBILITY-ASSESSMENT.md` (`Status: READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED  
The target architecture artifact `docs/issues/IS005-PRIMA-STAGE-5-ARCHITECTURE.md` authoritatively defines the technical realization, database schema extensions and `simrs` sandbox tables (`DEC-001`, `DEC-006`), backend enrollment token and handshake endpoints (`DEC-001`, `DEC-002`), dual-mode per-detector hashed API key authentication and cadence drift detection (`DEC-001`, `DEC-003`), frontend Quick Enroll modal, pending tokens strip, and canonical scope alignment (`DEC-004`), standardized .NET 8 Reference Detector SDK and runner template (`DEC-005`, `DEC-006`), deterministic business rules and evidence mappings for all 6 pilot SIMRS detectors (`DEC-007`), programmatic non-intrusive safety guardrails (`DEC-008`), and on-premise production packaging, retention pruning, and operational runbooks (`DEC-009`).

---

# 2. Planning Scope

The scope of this implementation plan encompasses the complete delivery of Stage 5 across database schema migrations, backend minimal APIs, contract synchronization, frontend detector management views, .NET 8 Detector SDK, pilot SIMRS detector suite, non-intrusive safety verification, on-premise deployment assets, and operational runbooks:

1. **Database Schema & Migrations (`005_Stage5HandshakeAndSimrsSandbox.sql`):**
   - Create `dbo.DetectorEnrollmentTokens` with token hash, 24h TTL, single-use status tracking (`Pending`, `Consumed`, `Revoked`), and officer attribution.
   - Alter `dbo.Detectors` adding `ApiKeyHash NVARCHAR(128) NULL`, `Version NVARCHAR(50) NULL`, `LastActualIntervalSeconds INT NULL`, and `CadenceDriftWarning NVARCHAR(500) NULL` with filtered index on `ApiKeyHash`.
   - Create `simrs` sandbox schema and 6 realistic hospital test tables: `simrs.acc_journal_entries`, `simrs.ar_receivables`, `simrs.trx_patient_bills`, `simrs.emr_sync_logs`, `simrs.hfis_doctor_schedules`, and `simrs.infra_print_queues`.
   - Seed baseline clean records and representative anomaly test fixtures for local development and automated testing.

2. **Backend Zero-Drift Enrollment & Handshake Protocol (`Features/Detectors/Enrollment/*`):**
   - `POST /api/detectors/enrollment-tokens`: Generate short-lived 24h enrollment token (`PRM-TOKEN-...`), store SHA-256 hash, and return plaintext secret once.
   - `GET /api/detectors/enrollment-tokens`: List pending unconsumed tokens for EDP officer review.
   - `DELETE /api/detectors/enrollment-tokens/{id}`: Revoke unconsumed enrollment token (`Status = 'Revoked'`).
   - `POST /api/detectors/handshake`: Validate token, enforce cadence bounds (`30 <= expectedCadenceSeconds <= 86400`), validate canonical `MonitoringScope`, provision unique per-detector API key (`prm_key_...`), create or idempotently update detector manifest, rotate key hash, set `AdminStatus = 'Active'`, mark token `Consumed`, and return `{ detectorId, apiKey }`. Reject re-enrollment of `Retired` detectors with `409 Conflict` (`BR-PRIMA-008`).

3. **Hashed API Key Authentication & Ingestion Cadence Drift Detection:**
   - Update `ApiKeyEndpointFilter.cs`: Validate incoming `X-Detector-API-Key` using constant-time byte comparison (`FixedTimeEquals`) against target detector's `ApiKeyHash` (SHA-256) on `dbo.Detectors`, maintaining fallback to global `Ingestion:ApiKeys` for backward compatibility.
   - Update `IngestTelemetryHandler.cs`: When `TriggerSource == Scheduled` and `detector.LastRunAtUtc` exists, compute `actualIntervalSeconds`. If divergent by `> ±50%` from `ExpectedCadenceSeconds`, record `CadenceDriftWarning` on `dbo.Detectors` and return in `IngestTelemetryResponse.Warnings`; otherwise clear warning.
   - Update `Program.cs` Health Check: Upgrade `GET /api/health` and `/health` to execute asynchronous `SELECT 1` against SQL Server, returning `200 OK` (`Database: "Connected"`) or `503 Service Unavailable`.
   - Update `MonitoringSupervisorWorker.cs`: Add daily retention pass deleting clean `Passed` `dbo.InspectionRuns` older than 90 days in batches of 1000 without deleting any `dbo.Occurrences` or `dbo.Issues` (`BR-PRIMA-009`).

4. **API Contract Synchronization & Frontend Types:**
   - Synchronize OpenAPI specification via `SwaggerDocumentTests.cs` and regenerate strongly typed TypeScript contracts using `npm run generate:api` into `src/frontend/src/shared/types/api.generated.ts`.

5. **Frontend Enrollment & Canonical Scope Alignment (`src/frontend/src/features/detectors/`):**
   - TanStack Query composables for enrollment tokens (`useEnrollmentTokensQuery`, `useGenerateEnrollmentTokenMutation`, `useRevokeEnrollmentTokenMutation`).
   - `EnrollmentTokenModal.vue`: Modal with optional token label input, single-display plaintext token, and 1-click copy environment variable code block (`PRIMA_ENROLLMENT_TOKEN`, `PRIMA_SERVER_URL`).
   - `PendingEnrollmentsStrip.vue`: Collapsible strip on `S-03` displaying active pending tokens with officer, created date, expiry countdown, and revocation action.
   - `DetectorListView.vue` (`S-03`): Add `"Quick Enroll (Handshake)"` CTA, render pending tokens strip, and display amber cadence drift warning chips.
   - `DetectorDetailView.vue` (`S-04`): Display cadence drift alert banner, actual interval statistics, and detector version.
   - `DetectorFormView.vue` (`S-05`) & `detectorForm.schema.ts`: Segmented mode switcher between `"Automated Handshake (Recommended)"` and `"Manual Registration"`, and align scope dropdown to canonical `MonitoringScope` values (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`).

6. **Standardized .NET 8 Reference Detector SDK (`src/backend/PrIMA.Detectors/`):**
   - Add compiled project `PrIMA.Detectors.csproj` to `PrIMA.slnx` (.NET 8 C# WebApplication/Worker Service).
   - Reusable SDK Core: `IDetectorProbe`, `DetectorManifest`, `BootstrapHandshakeService` (with local `.prima-credentials.json` caching), `TelemetryIngestionClient`, and automatic exception trapping into `Outcome = ExecutionFailed` (`BR-PRIMA-005`).
   - Dual execution modes: continuous daemon loop (`BackgroundService`) and one-shot execution (`--run-once` CLI flag).
   - Embedded Kestrel HTTP listener providing `/webhooks/{detectorKey}` matching `DetectorWebhookClient`'s 15-second synchronous contract.
   - Developer documentation and runner guide in `src/tools/detector-runner-template/README.md`.

7. **Pilot SIMRS Detector Suite & Safety Guardrails (`BR-PRIMA-007`):**
   - Programmatic safety guardrails: `ReadOnlySimrsConnectionFactory` enforcing `ApplicationIntent=ReadOnly`, `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED`, `SET LOCK_TIMEOUT 2000`, 5-second `CommandTimeout`, and static DML/DDL rejection (`SimrsSqlSafetyGuard`).
   - 6 Pilot SIMRS Detectors in `PrIMA.Detectors/Probes/`:
     1. *Journal Balance Detector* (`DataIntegrity`, Accounting, `simrs.acc_journal_entries`)
     2. *Duplicate Receivable Detector* (`DataIntegrity`, Billing/AR, `simrs.ar_receivables`)
     3. *Missing Auto-Journal / Unprocessed Billing Pipeline Detector* (`ProcessIntegrity`, Billing, `simrs.trx_patient_bills`)
     4. *EMR Synchronization Detector* (`DataSync`, EMR, `simrs.emr_sync_logs`)
     5. *SIMRS–HFIS Doctor Schedule Detector* (`DataSync`, Outpatient/BPJS, `simrs.hfis_doctor_schedules`)
     6. *Remote Print Server Queue Detector* (`Infrastructure`, PrintSpooler, `simrs.infra_print_queues`)
   - Automated non-interference test suite `SimrsNonInterferenceSafetyTests.cs` verifying sub-500ms execution, zero blocking locks under concurrent simulated SIMRS writes, DML rejection, and timeout trapping.

8. **On-Premise Deployment Bundle & Operational Runbooks:**
   - Deliver `deploy/` bundle: `appsettings.Production.json`, Nginx/IIS TLS reverse-proxy templates, Docker Compose, Windows Service installation script, DBA read-only provisioning script (`provision-simrs-readonly-login.sql`), and backup maintenance script.
   - Deliver `docs/operations/`: `PRIMA-EDP-REMEDIATION-SOP.md` (remediation playbooks for all 6 pilot detectors) and `PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md` (staging UAT checklist and 2-week hypercare guide).
   - Deliver end-to-end integration quality gate smoke test `Stage5FullStackQualityGateSmokeTests.cs`.

**Explicitly Out of Scope (Non-Goals):**
- Automated write-back or self-healing mutations against hospital SIMRS tables (strictly forbidden; PrIMA is an observation platform per `BR-PRIMA-004`).
- Active Directory / LDAP / OAuth2 enterprise SSO (flat EDP officer accounts with PBKDF2/JWT remain authoritative per `PRIMA-ARCHITECTURE.md §1`).
- Multi-database partitioning or distributed message brokers (SQL Server remains the single authoritative store).

---

# 3. Dependencies

## External Prerequisites
- .NET 8 SDK (`8.0.428` or newer) installed on host.
- Node.js (`v20+` or `v22 LTS`) and npm (`10+`) installed on host.
- Microsoft SQL Server 2022 instance running locally on `localhost` (`MSSQLSERVER`) with `PrIMA_Dev` and `PrIMA_IntegrationTests` databases accessible.
- Stage 1, Stage 2, Stage 3, and Stage 4 platforms verified and operational in codebase.

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
| P1 - Database Schema, Migration & Sandbox Fixtures | NOT-STARTED | NOT-REVIEWED | 0/1 |
| P2 - Backend Handshake, Ingestion Drift, Deep Health & Retention | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P3 - API Contract Synchronization & TypeScript Definitions | NOT-STARTED | NOT-REVIEWED | 0/1 |
| P4 - Frontend Enrollment & Canonical Scope Alignment | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P5 - Standardized .NET 8 Reference Detector SDK & Runner | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P6 - Non-Intrusive SIMRS Safety Guardrails & Pilot Detector Suite | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P7 - Production Deployment Bundle, Operational Runbooks & Quality Gate | NOT-STARTED | NOT-REVIEWED | 0/3 |

---

# 5. Phases

## P1 - Database Schema, Migration & Sandbox Fixtures

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

### P1-S01

Title: DbUp Migration 005 — Stage 5 Handshake, Per-Detector Credentials & SIMRS Sandbox Tables

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Create and register DbUp migration `005_Stage5HandshakeAndSimrsSandbox.sql` creating `dbo.DetectorEnrollmentTokens`, extending `dbo.Detectors` with `ApiKeyHash`, `Version`, `LastActualIntervalSeconds`, and `CadenceDriftWarning`, and provisioning the `simrs` sandbox schema with 6 realistic tables and seeded fixtures (`DEC-001`, `DEC-006`).

Depends On: None  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Migration file `src/backend/PrIMA.Api/Migrations/Scripts/005_Stage5HandshakeAndSimrsSandbox.sql` created as an embedded resource in `PrIMA.Api.csproj`.
2. Creates `dbo.DetectorEnrollmentTokens` table:
   - `Id UNIQUEIDENTIFIER PRIMARY KEY`
   - `TokenPrefix NVARCHAR(20) NOT NULL`
   - `TokenHash NVARCHAR(128) NOT NULL` (SHA-256)
   - `Label NVARCHAR(100) NULL`
   - `Status NVARCHAR(30) NOT NULL` (`Pending`, `Consumed`, `Revoked`)
   - `ExpiresAtUtc DATETIME2 NOT NULL`
   - `CreatedByOfficer NVARCHAR(100) NOT NULL`
   - `CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()`
   - `ConsumedAtUtc DATETIME2 NULL`
   - `ConsumedByDetectorId UNIQUEIDENTIFIER NULL FOREIGN KEY REFERENCES dbo.Detectors(Id)`
   - Indexes: `IX_DetectorEnrollmentTokens_Status_Expires`, unique index `UX_DetectorEnrollmentTokens_TokenHash`.
3. Alters `dbo.Detectors`:
   - Adds `ApiKeyHash NVARCHAR(128) NULL`, `Version NVARCHAR(50) NULL`, `LastActualIntervalSeconds INT NULL`, `CadenceDriftWarning NVARCHAR(500) NULL`.
   - Creates filtered index `IX_Detectors_ApiKeyHash ON dbo.Detectors(ApiKeyHash) WHERE ApiKeyHash IS NOT NULL`.
4. Provisions `simrs` schema and 6 sandbox tables:
   - `simrs.acc_journal_entries` (`journal_batch_no`, `entry_date`, `account_code`, `debit`, `credit`, `posted_status`, etc.)
   - `simrs.ar_receivables` (`source_trx_no`, `invoice_no`, `patient_norm`, `receivable_amount`, `status`, etc.)
   - `simrs.trx_patient_bills` (`bill_invoice_no`, `patient_norm`, `bill_amount`, `settlement_status`, `settled_at`, etc.)
   - `simrs.emr_sync_logs` (`encounter_no`, `sync_type`, `sync_status`, `error_message`, `created_at`, `updated_at`, etc.)
   - `simrs.hfis_doctor_schedules` (`doctor_schedule_code`, `doctor_name`, `clinic_name`, `schedule_date`, `simrs_start_time`, `simrs_end_time`, `simrs_quota`, `hfis_start_time`, `hfis_end_time`, `hfis_quota`, `is_active`, etc.)
   - `simrs.infra_print_queues` (`station_queue_name`, `hospital_unit`, `printer_name`, `pending_jobs`, `oldest_job_age_seconds`, `spooler_status`, `last_heartbeat_at`, etc.)
5. Seeds initial realistic baseline clean records and representative anomaly test fixtures for all 6 tables.
6. Migration executes idempotently and succeeds when running `dotnet run --project src/backend/PrIMA.Api` or running test database initialization.

Notes:
- Follows pure persistence layer principles: idempotent script with `IF NOT EXISTS` guards.
- Foreign key relationship `ConsumedByDetectorId` references `dbo.Detectors(Id)` with `ON DELETE NO ACTION`.

---

## P2 - Backend Handshake, Ingestion Drift, Deep Health & Retention

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

### P2-S02

Title: Backend Enrollment Token Lifecycle Endpoints (Generate, List, Revoke)

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Implement vertical slices in `Features/Detectors/Enrollment/` allowing authenticated EDP officers to generate 24h enrollment tokens (`PRM-TOKEN-...`), view active pending tokens, and revoke unconsumed tokens (`DEC-001`, `DEC-002`).

Depends On: P1-S01  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Vertical slices created in `src/backend/PrIMA.Api/Features/Detectors/Enrollment/`:
   - `GenerateEnrollmentToken/`: Endpoint `POST /api/detectors/enrollment-tokens`, request `{ label? }`, handler using `RandomNumberGenerator` generating `PRM-TOKEN-<32-byte-base64url>`, computing SHA-256 hash, inserting `dbo.DetectorEnrollmentTokens` with 24h TTL, attributing to `HttpContext.User` officer name (`BR-PRIMA-008`), and returning `201 Created` with plaintext `{ token, prefix, expiresAtUtc, label }`.
   - `GetPendingEnrollmentTokens/`: Endpoint `GET /api/detectors/enrollment-tokens`, handler querying `dbo.DetectorEnrollmentTokens` for active `Pending` unexpired tokens (`ExpiresAtUtc > SYSUTCDATETIME()`), returning `200 OK` list.
   - `RevokeEnrollmentToken/`: Endpoint `DELETE /api/detectors/enrollment-tokens/{id}`, handler verifying token exists and is `Pending`, updating `Status = 'Revoked'`, returning `204 NoContent`. Returns `409 Conflict` if token is already `Consumed`, `404 NotFound` if nonexistent.
2. Endpoints mapped in `EnrollmentEndpoints.cs` with `RequireAuthorization()` and registered in `Program.cs`.
3. FluentValidation validators enforce label constraints (`<= 100` chars).
4. Unit and integration tests verify token generation, SHA-256 hashing in database, pending list projection, and revocation.

Notes:
- Plaintext token secret is returned ONLY ONCE at generation time. Database stores strictly `TokenHash`.
- Status values: `Pending`, `Consumed`, `Revoked`.

---

### P2-S03

Title: Backend Automated Detector Handshake Endpoint with Idempotent Manifest Sync

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Implement `POST /api/detectors/handshake` validating incoming enrollment tokens, enforcing cadence bounds, provisioning per-detector API keys (`prm_key_...`), and idempotently registering or updating detector manifests (`DEC-001`, `DEC-002`).

Depends On: P1-S01, P2-S02  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Vertical slice `src/backend/PrIMA.Api/Features/Detectors/Enrollment/DetectorHandshake/` created:
   - Endpoint: `POST /api/detectors/handshake` (anonymous access allowed; authenticated via enrollment token).
   - Request: `DetectorHandshakeRequest { EnrollmentToken, Name, TargetModule, Scope, ExpectedCadenceSeconds, WebhookUrl, Description, Version }`.
2. Validates incoming enrollment token:
   - Computes SHA-256 of `EnrollmentToken`.
   - Queries `dbo.DetectorEnrollmentTokens` with `UPDLOCK`.
   - Returns RFC 7807 `401 Unauthorized` / `400 BadRequest` if token not found, status != `Pending`, or `ExpiresAtUtc <= SYSUTCDATETIME()`.
3. Validates manifest parameters:
   - Validates cadence bounds: `30 <= ExpectedCadenceSeconds <= 86400` (returns `422 Unprocessable Entity` if out of bounds to protect `BR-PRIMA-010`).
   - Validates `Scope` against canonical `MonitoringScope` enum values (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`).
4. Generates per-detector API key:
   - Cryptographically random 32-byte base64url string with prefix `prm_key_`.
   - Computes SHA-256 `ApiKeyHash`.
5. Executes atomic registration / idempotent synchronization:
   - If detector with `Name` exists:
     - If `AdminStatus == 'Retired'`, return `409 Conflict` (`BR-PRIMA-008`).
     - Otherwise, perform idempotent update of `Description`, `Scope`, `TargetModule`, `ExpectedCadenceSeconds`, `WebhookUrl`, `Version`, rotate `ApiKeyHash`, set `AdminStatus = 'Active'`, and record `dbo.DetectorActivityLogs` (`"Detector re-enrolled via handshake manifest sync"`).
   - If detector with `Name` does not exist:
     - Insert new row into `dbo.Detectors` with new `Id`, manifest fields, `ApiKeyHash`, `AdminStatus = 'Active'`, `HealthStatus = 'Unknown'`.
   - Update `dbo.DetectorEnrollmentTokens`: set `Status = 'Consumed'`, `ConsumedAtUtc = SYSUTCDATETIME()`, `ConsumedByDetectorId = detectorId`.
6. Returns `200 OK` `{ detectorId, apiKey, status: "Active", name, version }`.
7. Integration tests verify fresh enrollment, token consumption, idempotent re-enrollment manifest update, key rotation, cadence rejection, and retired detector conflict.

Notes:
- Atomic database transaction guarantees token is marked consumed if and only if detector is created/updated.
- Plaintext API key is returned ONLY in the handshake response.

---

### P2-S04

Title: Dual-Mode Hashed API Key Authentication & Ingestion Cadence Drift Detection

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Upgrade `ApiKeyEndpointFilter.cs` to authenticate per-detector hashed API keys with constant-time comparison, and upgrade `IngestTelemetryHandler.cs` to detect and flag cadence drift (`±50%` on scheduled runs) (`DEC-001`, `DEC-003`).

Depends On: P1-S01  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api/Features/Ingestion/ApiKeyEndpointFilter.cs` upgraded:
   - Extracts incoming `X-Detector-API-Key` from request headers.
   - Computes SHA-256 byte array of the incoming key.
   - Extracts `DetectorId` from request route or telemetry body.
   - Queries `dbo.Detectors` for `ApiKeyHash` where `Id = @DetectorId`.
   - If found, performs constant-time byte comparison using `CryptographicOperations.FixedTimeEquals`.
   - If matched, allows request to proceed.
   - If not matched or no detector key stored, checks fallback global `Ingestion:ApiKeys` array for backward compatibility.
   - Returns RFC 7807 `401 Unauthorized` with `ProblemDetails` if neither matches.
2. `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryHandler.cs` upgraded:
   - When `TriggerSource == Scheduled` and `detector.LastRunAtUtc` exists:
     - Calculates `actualIntervalSeconds = (request.ExecutionTimestampUtc - detector.LastRunAtUtc.Value).TotalSeconds`.
     - Calculates deviation: `actualIntervalSeconds < detector.ExpectedCadenceSeconds * 0.5` or `actualIntervalSeconds > detector.ExpectedCadenceSeconds * 1.5`.
     - If divergent, sets `CadenceDriftWarning` string (e.g. `"Execution cadence drifted by {deviation}% (actual: {actual}s, expected: {expected}s)"`) and appends warning to `IngestTelemetryResponse.Warnings`.
     - If within bounds, sets `CadenceDriftWarning = null`.
   - Persists `LastActualIntervalSeconds` and `CadenceDriftWarning` to `dbo.Detectors` alongside `LastRunAtUtc`.
   - `IngestTelemetryResponse.cs` includes `List<string> Warnings` in response payload.
3. Unit and integration tests verify valid per-detector hashed key authentication, invalid key rejection, global fallback key authentication, cadence drift warning generation on ±50% deviation, and warning clearing when cadence normalizes.

Notes:
- Timing attack prevention: `FixedTimeEquals` ensures constant-time comparison.
- Cadence drift warning is strictly non-blocking: inspection findings are still ingested and processed normally.

---

### P2-S05

Title: Deep SQL Database Health Check & Automated 90-Day Retention Pruning

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Upgrade `GET /api/health` to execute asynchronous `SELECT 1` against SQL Server, and extend `MonitoringSupervisorWorker.cs` with daily batch retention pruning of clean `Passed` runs older than 90 days (`DEC-009`, `BR-PRIMA-009`).

Depends On: P1-S01  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `GET /api/health` and `/health` in `Program.cs` upgraded:
   - Executes asynchronous `SELECT 1` using `SqlConnection` against configured connection string.
   - Returns `200 OK` `{ status: "Healthy", database: "Connected", timestampUtc: "..." }` when database is reachable.
   - Catches exception and returns `503 Service Unavailable` `{ status: "Unhealthy", database: "Disconnected", error: "..." }` when database is down or unreachable.
2. `src/backend/PrIMA.Api/BackgroundServices/MonitoringSupervisorWorker.cs` extended:
   - In addition to 60-second silence supervision and SLA escalation, maintains a daily execution timer for retention pruning (`Retention:InspectionRunsDays`, default 90 days).
   - Executes batch deletion of `Passed` `dbo.InspectionRuns` older than cutoff:
     ```sql
     DELETE TOP (1000) ir
     FROM dbo.InspectionRuns ir
     WHERE ir.Outcome = 'Passed'
       AND ir.ExecutionTimestampUtc < @CutoffUtc
       AND NOT EXISTS (
           SELECT 1 FROM dbo.Occurrences o WHERE o.InspectionRunId = ir.Id
       );
     ```
   - Loops until 0 rows affected to prevent lock escalation and transaction log spikes.
   - Preserves all runs linked to occurrences, all `FindingDiscovered` runs, and all `ExecutionFailed` runs (`BR-PRIMA-009`).
3. Unit and integration tests verify database health check responsiveness (200 on connected, 503 on disconnected) and retention pruning logic (prunes clean passed runs older than 90 days, preserves occurrence-linked runs and recent passed runs).

Notes:
- Batch size of 1000 prevents lock escalation to table level on high-volume production tables.
- Foreign key `dbo.Occurrences.InspectionRunId` is never violated.

---

## P3 - API Contract Synchronization & TypeScript Definitions

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

### P3-S06

Title: OpenAPI Swagger Document Synchronization & TypeScript API Types Generation

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Synchronize OpenAPI Swagger documentation for all new Stage 5 endpoints and regenerate strongly typed TypeScript definitions in `src/frontend/src/shared/types/api.generated.ts`.

Depends On: P2-S02, P2-S03, P2-S04, P2-S05  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Run `SwaggerDocumentTests.cs` (or equivalent Swagger export mechanism) updating `src/backend/PrIMA.Api/swagger.json` to include:
   - `POST /api/detectors/enrollment-tokens`
   - `GET /api/detectors/enrollment-tokens`
   - `DELETE /api/detectors/enrollment-tokens/{id}`
   - `POST /api/detectors/handshake`
   - Updated `POST /api/ingestion/telemetry` response schema with `warnings` array.
   - Updated `dbo.Detectors` DTOs with `apiKeyHash`, `version`, `lastActualIntervalSeconds`, `cadenceDriftWarning`.
2. Run `npm run generate:api` in `src/frontend/` generating updated TypeScript definitions in `src/frontend/src/shared/types/api.generated.ts`.
3. Verification: `src/frontend/src/shared/types/api.generated.ts` compiles cleanly with zero TypeScript errors.

Notes:
- Ensures strict end-to-end type safety between backend minimal APIs and frontend Vue composables.

---

## P4 - Frontend Enrollment & Canonical Scope Alignment

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

### P4-S07

Title: Frontend Enrollment API Composables & EnrollmentTokenModal Component

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Implement TanStack Query composables for enrollment tokens and build `EnrollmentTokenModal.vue` with 1-click copy environment variable code snippets (`DEC-004`).

Depends On: P3-S06  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/features/detectors/api/` updated:
   - Query keys: `enrollmentTokenKeys.all`, `enrollmentTokenKeys.pending`.
   - Composables in `useDetectorQueries.ts` (or `useEnrollmentQueries.ts` within detectors feature):
     - `usePendingEnrollmentTokensQuery()` calling `GET /api/detectors/enrollment-tokens`.
     - `useGenerateEnrollmentTokenMutation()` calling `POST /api/detectors/enrollment-tokens`.
     - `useRevokeEnrollmentTokenMutation()` calling `DELETE /api/detectors/enrollment-tokens/{id}`.
2. Component `src/frontend/src/features/detectors/components/EnrollmentTokenModal.vue` created:
   - Modal dialog using PrimeVue `Dialog`.
   - Step 1 (Generation): Optional token label text input, `"Generate Enrollment Token"` CTA.
   - Step 2 (Result): Displays plaintext token secret in monospace box with 1-click copy button and success toast.
   - Provides ready-to-copy environment variable code snippet block:
     ```bash
     PRIMA_ENROLLMENT_TOKEN="PRM-TOKEN-..."
     PRIMA_SERVER_URL="https://prima.hospital.internal"
     ```
   - Warning alert emphasizing that the token secret is shown only once and expires in 24 hours.
3. Obey `PRIMA-UI-UX-GUIDELINE.md §10`: zero native `window.alert()`, PrimeVue `Toast` for copy feedback, clean Tailwind CSS styling.

Notes:
- Modal state cleans up plaintext secret on close.

---

### P4-S08

Title: S-03 Detector List Integration: Quick Enroll CTA, Pending Tokens Strip & Drift Badges

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Integrate `"Quick Enroll (Handshake)"` CTA, `PendingEnrollmentsStrip.vue` with token revocation, and cadence drift warning indicator chips on `DetectorListView.vue` (`S-03`) (`DEC-004`).

Depends On: P4-S07  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Component `src/frontend/src/features/detectors/components/PendingEnrollmentsStrip.vue` created:
   - Collapsible banner rendered at top of `S-03` when pending unconsumed enrollment tokens exist.
   - Displays count of awaiting tokens with badge.
   - Expanded list shows: Token Prefix, Label, Created By Officer, Created Date, Expiry Countdown (`Expires in X hours`).
   - Action: `"Revoke"` button opening PrimeVue confirmation dialog, calling `useRevokeEnrollmentTokenMutation` on confirm.
2. `src/frontend/src/features/detectors/views/DetectorListView.vue` updated:
   - Adds `"Quick Enroll (Handshake)"` primary button next to `"Register Detector"` opening `EnrollmentTokenModal.vue`.
   - Embeds `PendingEnrollmentsStrip.vue` above the detector grid/table.
   - Embeds amber cadence drift badge chip on detector cards/rows where `cadenceDriftWarning` is non-null with tooltip showing drift details.
3. Component exports registered in `src/frontend/src/features/detectors/components/index.ts`.
4. Verification: Component renders cleanly without pagination, passes `npm run audit:compliance`.

Notes:
- Collapsible strip defaults to collapsed if EDP officer wants a compact view, but displays count pill.

---

### P4-S09

Title: S-04 Detector Detail Drift Display & S-05 Detector Form Handshake Mode & Canonical Scope Alignment

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Surface cadence drift warnings and version info on `S-04`, add automated handshake mode switcher to `S-05`, and align `detectorForm.schema.ts` to canonical `MonitoringScope` values (`DEC-004`).

Depends On: P3-S06  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/features/detectors/views/DetectorDetailView.vue` (`S-04`) updated:
   - Displays amber warning banner when `cadenceDriftWarning` is present, explaining interval divergence.
   - Displays `Version` and `LastActualIntervalSeconds` in Health Statistics / Metadata cards.
2. `src/frontend/src/features/detectors/schemas/detectorForm.schema.ts` updated:
   - Aligns `scope` field to canonical enum values: `DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`.
   - Updates options map with canonical human-readable labels:
     - `DataIntegrity`: `"Data Integrity (Ledger & Master Data)"`
     - `ProcessIntegrity`: `"Process Integrity (Billing & Workflow Pipeline)"`
     - `DataSync`: `"Data Synchronization (EMR, HFIS, BPJS)"`
     - `Infrastructure`: `"Infrastructure & Platform (Print, DB, Services)"`
3. `src/frontend/src/features/detectors/views/DetectorFormView.vue` (`S-05`) updated:
   - Adds segmented mode switcher at top:
     - Tab 1: `"Automated Handshake (Recommended)"` — explains zero-drift enrollment and provides button to open `EnrollmentTokenModal.vue`.
     - Tab 2: `"Manual Registration"` — reveals traditional manual detector configuration form.
   - Updates scope dropdown binding to canonical enum values.
4. Verification: `npm run audit:compliance` exits with code 0; `npm run build` succeeds cleanly with 0 TypeScript/Vue errors.

Notes:
- Backward compatibility: Existing detectors with legacy scope values continue to render gracefully.

---

## P5 - Standardized .NET 8 Reference Detector SDK & Runner

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

### P5-S10

Title: .NET 8 PrIMA.Detectors Project Scaffolding & Core SDK Components

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Scaffold the `src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj` project in `PrIMA.slnx` and implement the core SDK abstractions: `IDetectorProbe`, `DetectorManifest`, `TelemetryIngestionClient`, and automatic `ExecutionFailed` exception trapping (`DEC-005`, `DEC-006`, `BR-PRIMA-005`).

Depends On: P2-S04  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Create `src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj`:
   - Target framework: `net8.0`.
   - Packages: `Dapper`, `Microsoft.Data.SqlClient`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Http`.
   - Register project in `src/backend/PrIMA.slnx`.
2. Core SDK Interfaces and Models in `PrIMA.Detectors/Sdk/`:
   - `IDetectorProbe`:
     ```csharp
     public interface IDetectorProbe
     {
         DetectorManifest Manifest { get; }
         Task<TelemetryReport> ExecuteInspectionAsync(CancellationToken ct = default);
     }
     ```
   - `DetectorManifest`: Record containing `Name`, `TargetModule`, `Scope`, `ExpectedCadenceSeconds`, `Description`, `Version`, `WebhookEndpointKey`.
   - `TelemetryReport`: Record containing `InspectionTimestampUtc`, `Outcome` (`Passed`, `FindingDiscovered`, `ExecutionFailed`), `Findings` (`FindingItem`), `ExecutionDurationMs`, `TraceString`, `ErrorMessage`.
3. `TelemetryIngestionClient` in `PrIMA.Detectors/Services/`:
   - Ingests `TelemetryReport` by calling `POST /api/ingestion/telemetry` with header `X-Detector-API-Key: {apiKey}`.
   - Handles network retry, logs responses and warnings.
4. Automatic `ExecutionFailed` exception trapping:
   - Runner wraps probe execution in try-catch. Any unhandled exception, SQL timeout, or cancellation converts into `Outcome = ExecutionFailed`, populates `ErrorMessage` and `TraceString`, and submits to PrIMA server (`BR-PRIMA-005`).
5. Project compiles cleanly with zero warnings: `dotnet build src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj`.

Notes:
- Technology stack: .NET 8 C# 12, Dapper, `Microsoft.Data.SqlClient`. Zero Entity Framework.

---

### P5-S11

Title: Bootstrap Handshake Service, Dual Execution Modes, Webhook Listener & Template Docs

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Implement `BootstrapHandshakeService` with local credential caching, continuous daemon vs. `--run-once` CLI execution modes, embedded Kestrel webhook listener, and runner template reference documentation (`DEC-005`).

Depends On: P2-S03, P5-S10  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `BootstrapHandshakeService` in `PrIMA.Detectors/Services/`:
   - Checks for local credential file `.prima-credentials.json` (stores `{ detectorId, apiKey, serverUrl }`).
   - If missing, checks environment variable `PRIMA_ENROLLMENT_TOKEN`.
   - If token present, calls `POST /api/detectors/handshake` submitting probe manifest, receives `{ detectorId, apiKey }`, and persists to `.prima-credentials.json`.
   - If neither credentials nor token exist, logs clear error with instructions.
2. Dual execution modes in `Program.cs` / `Worker.cs`:
   - Daemon mode (default): Hosted as a `BackgroundService` executing periodic cadence timers (`PeriodicTimer`) based on `ExpectedCadenceSeconds`.
   - CLI mode: When launched with `--run-once`, executes one inspection pass immediately, ingests telemetry, and exits with code 0 (or 1 on failure) for Windows Task Scheduler / cron.
3. Embedded Kestrel Webhook Listener:
   - Hosts Kestrel HTTP endpoint on configurable port (e.g. 5090).
   - Maps `POST /webhooks/{detectorKey}` matching `DetectorWebhookClient`'s 15-second synchronous contract.
   - Triggers immediate inspection run for the matched probe, ingests result, and returns `200 OK` within 15 seconds.
4. Reference documentation in `src/tools/detector-runner-template/README.md`:
   - Guide on configuring, deploying, and authoring new custom probes using the SDK.
5. Verification: Project builds and runs in both `--run-once` and daemon modes.

Notes:
- Supports running all 6 pilot detectors in a single unified process or selectively enabling probes via configuration.

---

## P6 - Non-Intrusive SIMRS Safety Guardrails & Pilot Detector Suite

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

### P6-S12

Title: Programmatic SIMRS Safety Guardrails & Automated Non-Interference Test Suite

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Implement `ReadOnlySimrsConnectionFactory` and `SimrsSqlSafetyGuard` enforcing `BR-PRIMA-007` non-intrusive safety guardrails, verified by automated xUnit non-interference tests (`DEC-008`).

Depends On: P1-S01, P5-S10  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Safety guardrail classes in `PrIMA.Detectors/Services/`:
   - `ReadOnlySimrsConnectionFactory`: Creates `SqlConnection` with `ApplicationIntent=ReadOnly`, executes `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SET LOCK_TIMEOUT 2000;` on open, and configures default 5-second `CommandTimeout`.
   - `SimrsSqlSafetyGuard.EnsureReadOnlySelect(string sql)`: Regex guard rejecting `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `EXEC`, `EXECUTE`, `MERGE`, `CREATE`, `GRANT`, `REVOKE`, throwing `InvalidOperationException`.
2. Automated test suite `src/backend/PrIMA.Api.Tests/Integration/SimrsNonInterferenceSafetyTests.cs`:
   - Test 1 (Sub-500ms latency): Executes probe queries against seeded `simrs` tables asserting sub-500ms execution.
   - Test 2 (Zero blocking locks): Simulates concurrent hospital OLTP writes (holding uncommitted table locks) and verifies probe queries execute with `READ UNCOMMITTED` without blocking or waiting behind write locks.
   - Test 3 (DML rejection): Asserts `SimrsSqlSafetyGuard` throws exception when query contains mutation keywords.
   - Test 4 (Timeout trapping): Simulates a query taking > 5 seconds and asserts probe terminates and traps into `ExecutionFailed` run (`BR-PRIMA-005`).
3. Verification: `SimrsNonInterferenceSafetyTests` executes and passes 100% green.

Notes:
- Guaranteed hospital safety: probe queries can never acquire shared locks or block hospital OLTP transactions.

---

### P6-S13

Title: Pilot SIMRS Detector Suite: Financial & Process Probes (Journal, Receivable, Billing)

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Implement the first 3 pilot SIMRS detectors in `PrIMA.Detectors/Probes/` with exact deterministic SQL queries, entity references, and financial evidence mappings (`DEC-007`).

Depends On: P1-S01, P5-S10, P6-S12  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `JournalBalanceDetector.cs` implemented:
   - Scope: `DataIntegrity`, Module: `Accounting`, Base Severity: `High`.
   - Query: `simrs.acc_journal_entries` checking `ABS(SUM(debit) - SUM(credit)) > 0.01` over past 3 days.
   - Target Entity Reference: `batch_no` (`JournalBatchNo`).
   - Correlation Key: `{DetectorId}:{JournalBatchNo}`.
   - Populates financial evidence: `DebitTotal`, `CreditTotal`, `VarianceDelta`.
2. `DuplicateReceivableDetector.cs` implemented:
   - Scope: `DataIntegrity`, Module: `Billing`, Base Severity: `High`.
   - Query: `simrs.ar_receivables` checking `COUNT(*) > 1` on `source_trx_no` with `status = 'Active'` over past 7 days.
   - Target Entity Reference: `source_trx_no` (`SourceTransactionNo`).
   - Correlation Key: `{DetectorId}:{SourceTransactionNo}`.
   - Populates financial evidence: `DebitTotal` (duplicate sum), `CreditTotal` (expected amount), `VarianceDelta`.
3. `UnprocessedBillingPipelineDetector.cs` implemented:
   - Scope: `ProcessIntegrity`, Module: `Billing`, Base Severity: `High`.
   - Query: `simrs.trx_patient_bills` settled > 15 minutes ago missing matching `simrs.acc_journal_entries` batch.
   - Target Entity Reference: `bill_invoice_no` (`BillInvoiceNo`).
   - Correlation Key: `{DetectorId}:{BillInvoiceNo}`.
   - Populates financial evidence: `DebitTotal` = bill amount, `CreditTotal` = 0.00, `VarianceDelta` = bill amount.
4. Unit tests verify inspection execution, anomaly detection against sandbox fixtures, clean run reporting, and evidence payload construction.

Notes:
- All queries pass through `SimrsSqlSafetyGuard` and execute via `ReadOnlySimrsConnectionFactory`.

---

### P6-S14

Title: Pilot SIMRS Detector Suite: Sync & Infrastructure Probes (EMR, HFIS, Print Queue)

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Implement the remaining 3 pilot SIMRS detectors in `PrIMA.Detectors/Probes/` covering EMR sync, BPJS HFIS doctor schedules, and print spooler queues (`DEC-007`).

Depends On: P1-S01, P5-S10, P6-S12  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `EmrSyncDetector.cs` implemented:
   - Scope: `DataSync`, Module: `EMR`, Base Severity: `Medium`.
   - Query: `simrs.emr_sync_logs` with `sync_status = 'Failed'` or `Pending` > 30 minutes.
   - Target Entity Reference: `encounter_no` (`EncounterNo`).
   - Correlation Key: `{DetectorId}:{EncounterNo}`.
   - Non-financial: `DebitTotal`, `CreditTotal`, `VarianceDelta` = `null`. `TraceString` details error message.
2. `HfisScheduleSyncDetector.cs` implemented:
   - Scope: `DataSync`, Module: `Outpatient`, Base Severity: `Medium`.
   - Query: `simrs.hfis_doctor_schedules` comparing start/end times and quotas between SIMRS and BPJS HFIS.
   - Target Entity Reference: `doctor_schedule_code` (`DoctorScheduleCode`).
   - Correlation Key: `{DetectorId}:{DoctorScheduleCode}`.
   - Non-financial: `DebitTotal`, `CreditTotal`, `VarianceDelta` = `null`. `TraceString` details schedule mismatch.
3. `PrintQueueSpoolerDetector.cs` implemented:
   - Scope: `Infrastructure`, Module: `PrintSpooler`, Base Severity: `Critical`.
   - Query: `simrs.infra_print_queues` where `spooler_status <> 'Online'` or `pending_jobs > 0 AND oldest_job_age_seconds >= 300`.
   - Target Entity Reference: `station_queue_name` (`StationQueueName`).
   - Correlation Key: `{DetectorId}:{StationQueueName}`.
   - Non-financial: `DebitTotal`, `CreditTotal`, `VarianceDelta` = `null`. `TraceString` details printer and backlog.
4. Unit tests verify inspection execution, anomaly detection against sandbox fixtures, clean run reporting, and evidence payload construction.

Notes:
- Non-financial probes strictly set financial evidence fields to `null` to comply with `PRIMA-DOMAIN.md §TelemetryEvidence`.

---

## P7 - Production Deployment Bundle, Operational Runbooks & Quality Gate

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

### P7-S15

Title: On-Premise Production Deployment Packaging Bundle

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Deliver the complete hospital on-premise production packaging bundle in `deploy/` including reverse proxy TLS configs, Windows Service scripts, Docker Compose, and DBA read-only provisioning scripts (`DEC-008`, `DEC-009`).

Depends On: P5-S11, P6-S12  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `deploy/` directory populated with:
   - `deploy/appsettings.Production.json`: Production configuration template with SQL Server connection strings, JWT settings, ingestion key placeholders, and retention days (`Retention:InspectionRunsDays: 90`).
   - `deploy/nginx/prima.conf`: Reverse-proxy configuration for Nginx terminating hospital TLS/HTTPS, routing `/api/*` to Kestrel (port 5000) and serving SPA static files with security headers.
   - `deploy/iis/web.config`: IIS URL Rewrite and reverse-proxy template for Windows Server environments.
   - `deploy/docker-compose.prod.yml`: Production multi-container composition for Linux server deployment.
   - `deploy/windows-service/install-prima-service.ps1`: PowerShell script for installing PrIMA API and PrIMA Detectors as managed Windows Services with recovery actions.
   - `deploy/sql/provision-simrs-readonly-login.sql`: SQL script provisioning dedicated `prima_readonly` database login and user, granting `SELECT` on required SIMRS tables with explicit `DENY INSERT, UPDATE, DELETE, EXECUTE`.
   - `deploy/sql/maintenance-retention-pruning.sql`: Standalone SQL Server Agent job script for daily retention pruning.
2. Verification: All scripts and configuration files are syntactically valid and verified against production standards.

Notes:
- Follows hospital on-premise IT requirements: supports both Windows Server (IIS / Windows Service) and Linux (Docker / Nginx).

---

### P7-S16

Title: EDP Operational Remediation SOP & Staging UAT / Hypercare Playbooks

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Author and publish comprehensive hospital operational runbooks in `docs/operations/`: EDP remediation standard operating procedures and staging UAT / hypercare playbooks (`DEC-009`).

Depends On: P6-S13, P6-S14  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `docs/operations/PRIMA-EDP-REMEDIATION-SOP.md` authored:
   - Standalone operational reference sheets for each of the 6 pilot detectors.
   - For each detector: Business Impact, Diagnostic SQL queries for EDP, Root Cause analysis steps, Hospital SIMRS correction procedure (e.g. balancing journal, voiding duplicate AR), and Verification procedure (`M-03` manual verification vs. `D-2` automated webhook re-run).
   - Escalation matrix for EDP team leads and hospital division heads.
2. `docs/operations/PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md` authored:
   - Staging UAT scenario checklist covering all use cases: `A-1` through `H-2` plus Handshake Enrollment.
   - Step-by-step test scripts, expected outcomes, and sign-off criteria.
   - 2-week production hypercare operational guide: daily EDP checklist, threshold tuning guidelines, and rollback procedures.
3. Verification: Documents adhere strictly to technical writing standards and reference exact schema tables and UI workflows.

Notes:
- Empowers EDP officers to act autonomously without requiring developer intervention for routine operational anomalies.

---

### P7-S17

Title: Full-Stack Stage 5 Quality Gate & End-to-End Handshake Ingestion Smoke Verification

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective: Implement an end-to-end integration smoke test `Stage5FullStackQualityGateSmokeTests.cs` and verify the entire solution across backend tests, frontend compliance audit, and frontend production build (`DEC-009`).

Depends On: P4-S08, P4-S09, P5-S11, P6-S13, P6-S14, P7-S15, P7-S16  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Integration test `src/backend/PrIMA.Api.Tests/Integration/Stage5FullStackQualityGateSmokeTests.cs` created:
   - Generates enrollment token via `POST /api/detectors/enrollment-tokens`.
   - Executes bootstrap handshake via `POST /api/detectors/handshake` registering a pilot detector manifest.
   - Verifies per-detector API key is provisioned and token marked `Consumed`.
   - Submits telemetry findings using provisioned key via `POST /api/ingestion/telemetry`.
   - Verifies issue and occurrence creation (`BR-PRIMA-001`).
   - Verifies cadence drift detection when interval diverges by `> ±50%`.
   - Triggers on-demand webhook run via `/webhooks/{detectorKey}`.
   - Verifies deep database health check on `GET /api/health`.
   - Verifies daily retention pruning execution.
2. Full solution quality gates:
   - `dotnet test src/backend/PrIMA.slnx`: 100% tests pass (zero failed, zero skipped).
   - `npm run audit:compliance` (in `src/frontend`): 0 violations (exit code 0).
   - `npm run build` (in `src/frontend`): S-03, S-04, and S-05 compile cleanly with 0 TypeScript/Vue errors.

Notes:
- Proves end-to-end operational readiness for hospital production rollout.

---

# 6. Change Log

- 2026-11-22: Initial creation of Stage 5 Implementation Plan covering DbUp 005 migration and SIMRS sandbox fixtures, backend enrollment and handshake minimal APIs, per-detector hashed key validation, cadence drift supervision, frontend Quick Enroll modal, pending tokens strip, and canonical scope alignment, standardized .NET 8 Reference Detector SDK, 6 pilot SIMRS detectors, programmatic non-intrusive safety guardrails, on-premise deployment bundle, deep health checks, automated 90-day retention pruning, and hospital EDP operational runbooks. Plan structure released for execution (`Execution Approval: APPROVED`).
