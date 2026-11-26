---
Title: PrIMA Stage 5 — Detector Handshake Protocol, Pilot SIMRS Detector Suite & Hospital Production Rollout Implementation Plan
Code: PRIMA-STAGE-5
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-11-22
Status: COMPLETED
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
| P1 - Database Schema, Migration & Sandbox Fixtures | IMPLEMENTED | GO | 1/1 |
| P2 - Backend Handshake, Ingestion Drift, Deep Health & Retention | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P3 - API Contract Synchronization & TypeScript Definitions | NOT-STARTED | NOT-REVIEWED | 0/1 |
| P4 - Frontend Enrollment & Canonical Scope Alignment | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P5 - Standardized .NET 8 Reference Detector SDK & Runner | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P6 - Non-Intrusive SIMRS Safety Guardrails & Pilot Detector Suite | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P7 - Production Deployment Bundle, Operational Runbooks & Quality Gate | NOT-STARTED | NOT-REVIEWED | 0/3 |

---

# 5. Phases

## P1 - Database Schema, Migration & Sandbox Fixtures

Implementation Status: IMPLEMENTED  
Review Status: GO  

### P1-S01

Title: DbUp Migration 005 — Stage 5 Handshake, Per-Detector Credentials & SIMRS Sandbox Tables

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed: Created `005_Stage5HandshakeAndSimrsSandbox.sql` creating `dbo.DetectorEnrollmentTokens` with indexes, extending `dbo.Detectors` with `ApiKeyHash`, `Version`, `LastActualIntervalSeconds`, and `CadenceDriftWarning` (with filtered index), provisioning `simrs` schema with 6 sandbox tables, and seeding baseline clean and representative anomaly test fixtures for all 6 tables.
- Added comprehensive integration tests in `Stage5DatabaseMigrationTests.cs` verifying idempotent execution and table/column/fixture presence. All 5 tests passed.
- Updated `PrIMAApiFactory.ResetDatabaseAsync` to include `dbo.DetectorEnrollmentTokens` before `dbo.Detectors` to respect the FK constraint during test resets.
- Changed Files:
  - `src/backend/PrIMA.Api/Migrations/Scripts/005_Stage5HandshakeAndSimrsSandbox.sql`
  - `src/backend/PrIMA.Api.Tests/Integration/Stage5DatabaseMigrationTests.cs`
  - `src/backend/PrIMA.Api.Tests/Fixtures/PrIMAApiFactory.cs`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

## P2 - Backend Handshake, Ingestion Drift, Deep Health & Retention

Implementation Status: IMPLEMENTED  
Review Status: NOT-REVIEWED  

### P2-S02

Title: Backend Enrollment Token Lifecycle Endpoints (Generate, List, Revoke)

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed: Created vertical slices in `src/backend/PrIMA.Api/Features/Detectors/Enrollment/` (`GenerateEnrollmentToken`, `GetPendingEnrollmentTokens`, `RevokeEnrollmentToken`, and `EnrollmentEndpoints.cs`).
- Plaintext token secret (`PRM-TOKEN-...`) is generated using cryptographic `RandomNumberGenerator` (32 bytes URL-safe base64) and returned only once at generation time. The database strictly persists the SHA-256 lowercase hex hash `TokenHash`.
- Added FluentValidation validator `GenerateEnrollmentTokenValidator` enforcing max 100 char label constraint.
- Registered endpoints in `Program.cs` under `app.MapEnrollmentEndpoints()`.
- Added 11 integration tests in `src/backend/PrIMA.Api.Tests/Integration/EnrollmentTokenEndpointsTests.cs` verifying authentication requirement, token generation, database SHA-256 hashing, active unexpired pending token list projection, and revocation status updates (including 409 on already-consumed/revoked and 404 on nonexistent). All 11 tests passed.
- Changed Files:
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/EnrollmentTokenErrors.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/GenerateEnrollmentToken/GenerateEnrollmentTokenRequest.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/GenerateEnrollmentToken/GenerateEnrollmentTokenResponse.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/GenerateEnrollmentToken/GenerateEnrollmentTokenValidator.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/GenerateEnrollmentToken/GenerateEnrollmentTokenHandler.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/GetPendingEnrollmentTokens/PendingEnrollmentTokenDto.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/GetPendingEnrollmentTokens/GetPendingEnrollmentTokensHandler.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/RevokeEnrollmentToken/RevokeEnrollmentTokenHandler.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/EnrollmentEndpoints.cs`
  - `src/backend/PrIMA.Api/Program.cs`
  - `src/backend/PrIMA.Api.Tests/Integration/EnrollmentTokenEndpointsTests.cs`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P2-S03

Title: Backend Automated Detector Handshake Endpoint with Idempotent Manifest Sync

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed: Created vertical slice `src/backend/PrIMA.Api/Features/Detectors/Enrollment/DetectorHandshake/` (`DetectorHandshakeRequest`, `DetectorHandshakeResponse`, `DetectorHandshakeValidator`, `DetectorHandshakeHandler`), mapped `POST /api/detectors/handshake` in `EnrollmentEndpoints.cs` with `.AllowAnonymous()`, and registered handler in `Program.cs`.
- Handshake validates incoming enrollment token SHA-256 hash using `UPDLOCK` row locks, rejecting missing, non-pending, or expired tokens with 401 Unauthorized.
- Validates manifest parameters enforcing cadence bounds (30s <= ExpectedCadenceSeconds <= 86400s) and canonical scopes (DataIntegrity, ProcessIntegrity, DataSync, Infrastructure) with 422 Unprocessable Entity.
- Extended `DetectorHealth` enum with `Unknown` to support clean hydration of fresh handshake detector records.
- Provisions per-detector cryptographically secure 32-byte base64url API key prefixed with `prm_key_`, persisting only its SHA-256 lowercase hex hash `ApiKeyHash`.
- Executes atomic transaction: for existing detectors, checks `AdminStatus != 'Retired'` (returns 409 Conflict if retired per BR-PRIMA-008), updates manifest, rotates `ApiKeyHash`, restores `AdminStatus = 'Active'`, and records re-enrollment activity log; for fresh detectors, creates new detector record with `HealthStatus = 'Unknown'`. Consumes enrollment token atomically with `ConsumedByDetectorId = detectorId`.
- Added 15 comprehensive integration tests in `src/backend/PrIMA.Api.Tests/Integration/DetectorHandshakeEndpointsTests.cs` verifying fresh enrollment, token consumption, idempotent manifest sync, key rotation, active status restoration from paused state, retired conflict rejection, invalid/consumed/revoked/expired token rejection, cadence bounds rejection, and non-canonical scope rejection. All 15 tests passed.
- Changed Files:
  - `src/backend/PrIMA.Api/Domain/Detectors/DetectorHealth.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/EnrollmentTokenErrors.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/DetectorHandshake/DetectorHandshakeRequest.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/DetectorHandshake/DetectorHandshakeResponse.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/DetectorHandshake/DetectorHandshakeValidator.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/DetectorHandshake/DetectorHandshakeHandler.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/Enrollment/EnrollmentEndpoints.cs`
  - `src/backend/PrIMA.Api/Program.cs`
  - `src/backend/PrIMA.Api.Tests/Integration/DetectorHandshakeEndpointsTests.cs`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P2-S04

Title: Dual-Mode Hashed API Key Authentication & Ingestion Cadence Drift Detection

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Upgraded `ApiKeyEndpointFilter.cs` to extract `DetectorId` from body, route, query, or header, query `dbo.Detectors.ApiKeyHash`, perform constant-time comparison via `CryptographicOperations.FixedTimeEquals` against SHA-256 hash, and fall back to global `Ingestion:ApiKeys` with RFC 7807 401 error response.
  - Upgraded `IngestTelemetryHandler.cs` to calculate interval divergence on `Scheduled` runs against prior `LastRunAtUtc`. On ±50% cadence drift, generates descriptive warning and populates `CadenceDriftWarning` on `dbo.Detectors` and `IngestTelemetryResponse.Warnings`; clears warning to `NULL` when cadence normalizes. Preserves metrics during non-scheduled runs.
  - Updated `Detector.cs`, `IDetectorRepository.cs`, and `DetectorRepository.cs` to persist and map `ApiKeyHash`, `Version`, `LastActualIntervalSeconds`, and `CadenceDriftWarning`.
  - Added comprehensive test suite `Stage5ApiKeyAndCadenceDriftTests.cs` verifying hashed per-detector key authentication, invalid key rejection, global fallback key compatibility, drift detection on ±50% deviation, normalization clearing, and failed run drift recording (9/9 tests passed).
- Changed Files:
  - `src/backend/PrIMA.Api/Domain/Detectors/Detector.cs`
  - `src/backend/PrIMA.Api/Domain/Detectors/IDetectorRepository.cs`
  - `src/backend/PrIMA.Api/Infrastructure/Persistence/DetectorRepository.cs`
  - `src/backend/PrIMA.Api/Features/Ingestion/ApiKeyEndpointFilter.cs`
  - `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryHandler.cs`
  - `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryResponse.cs`
  - `src/backend/PrIMA.Api.Tests/Fixtures/PrIMAApiFactory.cs`
  - `src/backend/PrIMA.Api.Tests/Integration/Stage5ApiKeyAndCadenceDriftTests.cs`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P2-S05

Title: Deep SQL Database Health Check & Automated 90-Day Retention Pruning

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed: Upgraded `GET /health` and `GET /api/health` endpoints in `Program.cs` to execute asynchronous `SELECT 1` health probe against SQL Server using `ISqlConnectionFactory`, returning `200 OK` `{ status: "Healthy", database: "Connected", timestampUtc }` when reachable and `503 Service Unavailable` `{ status: "Unhealthy", database: "Disconnected", error }` on connection failures.
- Extended `MonitoringSupervisorWorker.cs` with automated 90-day retention pruning pass (`DEC-009`, `BR-PRIMA-009`) executing daily (24-hour evaluation cadence) or on-demand via `ExecuteRetentionPruningAsync`, deleting clean `Passed` `dbo.InspectionRuns` older than `Retention:InspectionRunsDays` (default 90 days) in chunks of 1000 without deleting any occurrence-linked runs, `FindingDiscovered` runs, or `ExecutionFailed` runs.
- Authored integration test suite `DatabaseHealthAndRetentionTests.cs` verifying 200/503 health probe responsiveness, retention pruning preservation rules, chunked batch deletion, and 24-hour cycle timer. All 5 tests passed.
- Changed Files:
  - `src/backend/PrIMA.Api/Program.cs`
  - `src/backend/PrIMA.Api/BackgroundServices/MonitoringSupervisorWorker.cs`
  - `src/backend/PrIMA.Api.Tests/Integration/DatabaseHealthAndRetentionTests.cs`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

## P3 - API Contract Synchronization & TypeScript Definitions

Implementation Status: IMPLEMENTED  
Review Status: NOT-REVIEWED  

### P3-S06

Title: OpenAPI Swagger Document Synchronization & TypeScript API Types Generation

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Updated `DetectorDetailDto.cs` and `DetectorListItemDto.cs` alongside their handlers (`GetDetectorByIdHandler.cs` and `GetDetectorListHandler.cs`) to select and expose `ApiKeyHash`, `Version`, `LastActualIntervalSeconds`, and `CadenceDriftWarning`.
- Updated `SwaggerDocumentTests.cs` asserting all Stage 5 endpoints (`POST /api/detectors/enrollment-tokens`, `GET /api/detectors/enrollment-tokens`, `DELETE /api/detectors/enrollment-tokens/{id}`, `POST /api/detectors/handshake`), Stage 5 request/response schemas, and property enhancements on `DetectorDetailDto`, `DetectorListItemDto`, and `IngestTelemetryResponse.Warnings`.
- Exported synchronized `swagger.json` and regenerated frontend TypeScript contracts via `npm run generate:api` with convenience aliases in `scripts/generate-api.js`.
- Verified typecheck and production build with `vue-tsc -b && vite build` (zero errors) and frontend compliance audit (0 violations).
- Changed Files:
  - `src/backend/PrIMA.Api/Features/Detectors/GetDetectorById/DetectorDetailDto.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/GetDetectorById/GetDetectorByIdHandler.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/GetDetectorList/DetectorListItemDto.cs`
  - `src/backend/PrIMA.Api/Features/Detectors/GetDetectorList/GetDetectorListHandler.cs`
  - `src/backend/PrIMA.Api.Tests/Integration/SwaggerDocumentTests.cs`
  - `src/backend/PrIMA.Api/swagger.json`
  - `src/frontend/scripts/generate-api.js`
  - `src/frontend/src/shared/types/api.generated.ts`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

## P4 - Frontend Enrollment & Canonical Scope Alignment

Implementation Status: IMPLEMENTED  
Review Status: NOT-REVIEWED  

### P4-S07

Title: Frontend Enrollment API Composables & EnrollmentTokenModal Component

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Updated `src/frontend/src/features/detectors/api/detector.keys.ts` with `enrollmentTokenKeys` (`all`, `pending`).
  - Created `src/frontend/src/features/detectors/api/useEnrollmentQueries.ts` providing `usePendingEnrollmentTokensQuery()`, `useGenerateEnrollmentTokenMutation()`, and `useRevokeEnrollmentTokenMutation()`, with automatic cache invalidation of `enrollmentTokenKeys.all`. Re-exported in `useDetectorQueries.ts`.
  - Built `src/frontend/src/features/detectors/components/EnrollmentTokenModal.vue` conforming to PrimeVue `Dialog`, featuring Step 1 token label input with "Generate Enrollment Token" action, and Step 2 single-view plaintext token secret display in a monospace container with 1-click clipboard copy, ready-to-copy `.env` snippet block (`PRIMA_ENROLLMENT_TOKEN` / `PRIMA_SERVER_URL`), and amber warning alert regarding 24h expiration. Sensitive secret state is cleanly purged on dialog close.
  - Exported `EnrollmentTokenModal` in `src/frontend/src/features/detectors/components/index.ts`.
  - Validated with `npm run audit:compliance` (0 violations) and `vue-tsc -b && vite build` (zero errors).
- Changed Files:
  - `src/frontend/src/features/detectors/api/detector.keys.ts`
  - `src/frontend/src/features/detectors/api/useEnrollmentQueries.ts`
  - `src/frontend/src/features/detectors/api/useDetectorQueries.ts`
  - `src/frontend/src/features/detectors/components/EnrollmentTokenModal.vue`
  - `src/frontend/src/features/detectors/components/index.ts`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P4-S08

Title: S-03 Detector List Integration: Quick Enroll CTA, Pending Tokens Strip & Drift Badges

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Created `src/frontend/src/features/detectors/components/PendingEnrollmentsStrip.vue` featuring a collapsible amber banner displayed when unconsumed pending tokens exist, displaying awaiting count pill, and expandable table showing Token Prefix (`...`), optional Label, Created By Officer, Created Date (`formatWibDateTime`), and real-time Expiry Countdown (`Expires in X hours / minutes`).
  - Integrated PrimeVue `Dialog` confirmation modal for token revocation with danger styling, triggering `useRevokeEnrollmentTokenMutation` and displaying success/error toast notifications.
  - Exported `PendingEnrollmentsStrip` in `src/frontend/src/features/detectors/components/index.ts`.
  - Updated `DetectorTable.vue` and `DetectorCard.vue` with amber cadence drift warning badge chips and tooltip showing `cadenceDriftWarning` details when present.
  - Updated `DetectorListView.vue` adding `"Quick Enroll (Handshake)"` primary CTA opening `EnrollmentTokenModal.vue`, embedding `PendingEnrollmentsStrip.vue` at the top of the detector portfolio view, and mounting `EnrollmentTokenModal.vue`.
  - Verified static compliance audit with `npm --prefix src/frontend run audit:compliance` (0 violations: zero paginator props, zero external charts, zero native dialogs, zero cross-slice imports).
  - Verified clean TypeScript compilation and production bundle build with `vue-tsc -b && vite build` (zero errors).
- Changed Files:
  - `src/frontend/src/features/detectors/components/PendingEnrollmentsStrip.vue`
  - `src/frontend/src/features/detectors/components/index.ts`
  - `src/frontend/src/features/detectors/components/DetectorTable.vue`
  - `src/frontend/src/features/detectors/components/DetectorCard.vue`
  - `src/frontend/src/features/detectors/views/DetectorListView.vue`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P4-S09

Title: S-04 Detector Detail Drift Display & S-05 Detector Form Handshake Mode & Canonical Scope Alignment

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Updated `DetectorDetailView.vue` (`S-04`) to surface amber Cadence Drift Warning banner when `cadenceDriftWarning` is non-null, detailing actual vs expected execution interval. Added version chip and cadence drift badge to header, and integrated `Version` and `LastActualIntervalSeconds` cards into the Detector Overview configuration grid with target tolerance metrics.
  - Updated `detectorForm.schema.ts` aligning `scope` to canonical `MonitoringScope` values (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`) and exported `DETECTOR_SCOPE_OPTIONS` with canonical human-readable labels and descriptions. Preserved backward compatibility mapping for legacy scope values (`DataQuality`, `BusinessLogic`, `ProcessFlow`).
  - Updated `DetectorFormView.vue` (`S-05`) adding segmented mode switcher between `"Automated Handshake (Recommended)"` and `"Manual Registration"`. Handshake tab features a 3-step zero-drift enrollment walkthrough, copyable .NET 8 runner environment variable snippet, and CTA opening `EnrollmentTokenModal.vue`. Manual tab binds scope dropdown to canonical `scopeOptions` with legacy fallback support.
  - Verified static compliance audit `npm run audit:compliance` (0 violations) and full TypeScript compilation and Vite production build `npm run build` (0 errors).
- Changed Files:
  - `src/frontend/src/features/detectors/schemas/detectorForm.schema.ts`
  - `src/frontend/src/features/detectors/views/DetectorDetailView.vue`
  - `src/frontend/src/features/detectors/views/DetectorFormView.vue`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

## P5 - Standardized .NET 8 Reference Detector SDK & Runner

Implementation Status: IMPLEMENTED  
Review Status: NOT-REVIEWED  

### P5-S10

Title: .NET 8 PrIMA.Detectors Project Scaffolding & Core SDK Components

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Created `src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj` targeting `net8.0` with `Dapper`, `Microsoft.Data.SqlClient`, `Microsoft.Extensions.Hosting`, and `Microsoft.Extensions.Http`.
  - Registered `PrIMA.Detectors` in `src/backend/PrIMA.slnx`.
  - Authored core SDK interfaces and models in `PrIMA.Detectors.Sdk`:
    - `IDetectorProbe`: Standard probe interface with `DetectorManifest` and `ExecuteInspectionAsync`.
    - `DetectorManifest`: Metadata record declaring probe identity, module, canonical scope, cadence, description, version, and webhook endpoint key.
    - `TelemetryReport`: Record supporting `Passed`, `FindingDiscovered`, and `ExecutionFailed` factory methods with duration, error message, and trace string.
    - `FindingItem` & `FindingEvidenceItem`: Anomaly findings payload matching the ingestion schema.
    - `IProbeExecutor` & `ProbeExecutor`: Probe execution orchestrator with stopwatch instrumentation and automated exception trapping.
  - Implemented `TelemetryIngestionClient` in `PrIMA.Detectors.Services`:
    - Posts telemetry to `/api/ingestion/telemetry` with `X-Detector-API-Key: {apiKey}` header.
    - Automatic exponential retry on transient 5xx errors and network exceptions, non-retrying on 4xx client errors.
    - Captures and logs non-blocking cadence drift warnings from server responses.
    - Safely bounds `FailureReason` to 1000 characters when outcome is `ExecutionFailed`.
  - Implemented `DetectorRunner` in `PrIMA.Detectors.Services`:
    - Failsafe execution wrapper that traps any unhandled probe exception, SQL timeout, or cancellation into `Outcome = ExecutionFailed` and submits the failure report to the PrIMA server (`BR-PRIMA-005`).
  - Added DI extensions `DetectorSdkExtensions.cs` (`AddDetectorSdk`).
  - Scaffolded `Program.cs` for `PrIMA.Detectors` WebApplication / Worker Service host.
  - Authored comprehensive test suite `Stage5DetectorSdkCoreTests.cs` (11/11 tests passing) verifying manifest defaults, report factories, probe execution, exception trapping, ingestion submission, truncation, warning processing, and failsafe runner submission.
  - Project builds cleanly with 0 warnings: `dotnet build src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj`.
- Changed Files:
  - `src/backend/PrIMA.slnx`
  - `src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj`
  - `src/backend/PrIMA.Detectors/Program.cs`
  - `src/backend/PrIMA.Detectors/Sdk/IDetectorProbe.cs`
  - `src/backend/PrIMA.Detectors/Sdk/DetectorManifest.cs`
  - `src/backend/PrIMA.Detectors/Sdk/FindingItem.cs`
  - `src/backend/PrIMA.Detectors/Sdk/TelemetryOutcome.cs`
  - `src/backend/PrIMA.Detectors/Sdk/TelemetryReport.cs`
  - `src/backend/PrIMA.Detectors/Sdk/ProbeExecutor.cs`
  - `src/backend/PrIMA.Detectors/Services/TelemetryIngestionModels.cs`
  - `src/backend/PrIMA.Detectors/Services/TelemetryIngestionClient.cs`
  - `src/backend/PrIMA.Detectors/Services/DetectorRunner.cs`
  - `src/backend/PrIMA.Detectors/Services/DetectorSdkExtensions.cs`
  - `src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj`
  - `src/backend/PrIMA.Api.Tests/Integration/Stage5DetectorSdkCoreTests.cs`
  - `nuget.config`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P5-S11

Title: Bootstrap Handshake Service, Dual Execution Modes, Webhook Listener & Template Docs

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Authored `BootstrapHandshakeModels.cs` and `BootstrapHandshakeService.cs` in `PrIMA.Detectors.Services`:
    - Checks `.prima-credentials.json` for cached credentials (both single-detector and multi-probe dictionary schema).
    - If missing or un-enrolled, checks `PRIMA_ENROLLMENT_TOKEN` (or options) and server URL.
    - If token present, posts manifest to `POST /api/detectors/handshake`, retrieves issued `{ detectorId, apiKey }`, and writes back to `.prima-credentials.json`.
    - If neither credentials nor token exist, logs structured error with clear EDP guidance.
  - Implemented dual execution modes:
    - Daemon mode: Authored `DetectorDaemonWorker.cs` (`BackgroundService`) executing `PeriodicTimer` intervals based on `ExpectedCadenceSeconds` (bounded >= 30s) with initial startup pass, isolated probe execution, and non-blocking cadence drift warning logging.
    - CLI mode: Authored `DetectorCliRunner.cs` (`IDetectorCliRunner`), triggered when launched with `--run-once`, executing single inspection pass for registered probes and exiting with code 0 (success) or 1 (failure) for Task Scheduler / cron.
  - Implemented Embedded Kestrel Webhook Listener in `DetectorWebhookEndpoints.cs`:
    - Maps `POST /webhooks/{detectorKey}` on configurable port (default 5090).
    - Resolves matching probe by endpoint key or name, applies 14-second strict cancellation budget, executes inspection and telemetry ingestion, and returns `200 OK` matching `DetectorWebhookClient`'s synchronous contract (`Outcome`, `DurationMs`, `FailureReason`, `Findings`).
    - Traps probe exceptions and timeouts into `Outcome = ExecutionFailed`.
  - Updated `Program.cs` and `DetectorSdkExtensions.cs`:
    - Registered `IBootstrapHandshakeService`, `IDetectorCliRunner`, `DetectorDaemonWorker`, and `AddDetectorProbe<TProbe>()`.
    - Mapped webhook and health endpoints (`/`, `/health`).
    - Handled `--run-once` returning process exit code directly.
  - Authored comprehensive reference documentation in `src/tools/detector-runner-template/README.md`:
    - Complete guide covering architecture, environment variables, configuration, bootstrap handshake workflow, daemon vs. CLI deployment, custom probe authoring (`IDetectorProbe`), webhook contract, Windows Service registration, and Task Scheduler setup.
  - Authored test suite `Stage5BootstrapAndRunnerTests.cs` (10/10 tests passing):
    - Disk cache loading, automated handshake enrollment, missing token error reporting, invalid token 401 handling, CLI pass with code 0, CLI failure with code 1, zero probes code 0, webhook listener 200 OK with findings payload, webhook 404 for unknown probe, and webhook exception trapping.
  - Verified runtime execution in both `--run-once` (logged CLI execution and exited code 0) and daemon mode (listening on `http://localhost:5090` responding 200 OK on `/` and `/health`).
- Changed Files:
  - `src/backend/PrIMA.Detectors/Services/BootstrapHandshakeModels.cs`
  - `src/backend/PrIMA.Detectors/Services/BootstrapHandshakeService.cs`
  - `src/backend/PrIMA.Detectors/Services/DetectorDaemonWorker.cs`
  - `src/backend/PrIMA.Detectors/Services/DetectorCliRunner.cs`
  - `src/backend/PrIMA.Detectors/Services/DetectorWebhookEndpoints.cs`
  - `src/backend/PrIMA.Detectors/Services/TelemetryIngestionModels.cs`
  - `src/backend/PrIMA.Detectors/Services/DetectorRunner.cs`
  - `src/backend/PrIMA.Detectors/Services/DetectorSdkExtensions.cs`
  - `src/backend/PrIMA.Detectors/Program.cs`
  - `src/tools/detector-runner-template/README.md`
  - `src/backend/PrIMA.Api.Tests/Integration/Stage5BootstrapAndRunnerTests.cs`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

## P6 - Non-Intrusive SIMRS Safety Guardrails & Pilot Detector Suite

Implementation Status: IMPLEMENTED  
Review Status: NOT-REVIEWED  

### P6-S12

Title: Programmatic SIMRS Safety Guardrails & Automated Non-Interference Test Suite

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Authored `SimrsSqlSafetyGuard.cs` in `PrIMA.Detectors.Services`:
    - Regex guard rejecting `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `EXEC`, `EXECUTE`, `MERGE`, `CREATE`, `GRANT`, `REVOKE` with case-insensitive word boundaries.
    - Throws `InvalidOperationException` with explicit `BR-PRIMA-007` diagnostic message on mutation statements or empty queries.
    - Provides `EnsureReadOnlySelect(string sql)` and `IsReadOnlySelect(string sql)` helper APIs.
  - Authored `IReadOnlySimrsConnectionFactory.cs` and `ReadOnlySimrsConnectionFactory.cs` in `PrIMA.Detectors.Services`:
    - Creates `SqlConnection` with `ApplicationIntent=ReadOnly`.
    - Automatically sets `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SET LOCK_TIMEOUT 2000;` on connection open.
    - Configures default 5-second `CommandTimeout`.
    - Provides helper query methods (`QueryAsync<T>`, `QuerySingleOrDefaultAsync<T>`, `QueryFirstOrDefaultAsync<T>`) pre-validating queries via `SimrsSqlSafetyGuard`.
    - Registered `IReadOnlySimrsConnectionFactory` in `DetectorSdkExtensions.cs`.
  - Authored comprehensive automated non-interference test suite `SimrsNonInterferenceSafetyTests.cs` (22/22 tests passing):
    - Test 1 (Sub-500ms latency): Executes all 6 pilot SIMRS detector queries against seeded tables, verifying sub-500ms execution and finding seeded anomaly fixtures.
    - Test 2 (Zero blocking locks): Simulates concurrent hospital OLTP writes holding exclusive uncommitted locks and verifies probe queries execute with `READ UNCOMMITTED` without waiting behind write locks (< 500ms vs 2000ms lock timeout).
    - Test 3 (DML rejection): Asserts `SimrsSqlSafetyGuard` rejects mutation keywords across multiple DML/DDL operations and allows valid `SELECT` statements (including CTEs and joins).
    - Test 4 (Timeout trapping): Simulates long-running query exceeding 5-second timeout, asserting probe terminates, traps into `Outcome = ExecutionFailed`, and correctly posts failure telemetry to PrIMA server (`BR-PRIMA-005`).
  - Changed Files:
    - `src/backend/PrIMA.Detectors/Services/IReadOnlySimrsConnectionFactory.cs`
    - `src/backend/PrIMA.Detectors/Services/ReadOnlySimrsConnectionFactory.cs`
    - `src/backend/PrIMA.Detectors/Services/SimrsSqlSafetyGuard.cs`
    - `src/backend/PrIMA.Detectors/Services/DetectorSdkExtensions.cs`
    - `src/backend/PrIMA.Detectors/Services/DetectorRunner.cs`
    - `src/backend/PrIMA.Detectors/Services/DetectorWebhookEndpoints.cs`
    - `src/backend/PrIMA.Api.Tests/Integration/SimrsNonInterferenceSafetyTests.cs`
    - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P6-S13

Title: Pilot SIMRS Detector Suite: Financial & Process Probes (Journal, Receivable, Billing)

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Authored `JournalBalanceDetector.cs` in `PrIMA.Detectors.Probes`:
    - Scope: `DataIntegrity`, Module: `Accounting`, Base Severity: `High`, Cadence: 300s, WebhookEndpointKey: `journal-balance`.
    - Queries `simrs.acc_journal_entries` for unbalanced batches (`ABS(SUM(debit) - SUM(credit)) > 0.01`) over past 3 days.
    - Maps target entity reference `batch_no`, populates financial evidence (`DebitTotal`, `CreditTotal`, `VarianceDelta`), and formats trace description.
  - Authored `DuplicateReceivableDetector.cs` in `PrIMA.Detectors.Probes`:
    - Scope: `DataIntegrity`, Module: `Billing`, Base Severity: `High`, Cadence: 300s, WebhookEndpointKey: `duplicate-receivable`.
    - Queries `simrs.ar_receivables` for duplicate active records (`COUNT(*) > 1`) over past 7 days.
    - Maps target entity reference `source_trx_no`, populates financial evidence (`DebitTotal` duplicate sum, `CreditTotal` max expected, `VarianceDelta`), and formats trace description.
  - Authored `UnprocessedBillingPipelineDetector.cs` in `PrIMA.Detectors.Probes`:
    - Scope: `ProcessIntegrity`, Module: `Billing`, Base Severity: `High`, Cadence: 300s, WebhookEndpointKey: `unprocessed-billing`.
    - Queries `simrs.trx_patient_bills` settled > 15 minutes ago missing matching auto-journal entries in `simrs.acc_journal_entries`.
    - Maps target entity reference `bill_invoice_no`, populates financial evidence (`DebitTotal` = bill amount, `CreditTotal` = 0.00, `VarianceDelta` = bill amount), and formats trace description.
  - Updated `DetectorSdkExtensions.cs`, `Program.cs`, and `ReadOnlySimrsConnectionFactory.cs`:
    - Added `AddPilotFinancialProbes()` DI helper registering the 3 probes.
    - Updated `ReadOnlySimrsConnectionFactory` with configuration and environment variable fallbacks (`PRIMA_SIMRS_CONNECTION_STRING`).
    - Added `appsettings.json` for `PrIMA.Detectors`.
  - Authored integration test suite `PilotSimrsFinancialProbesTests.cs` (15/15 tests passing):
    - Manifest declarations for all 3 probes.
    - Anomaly detection asserting exact financial evidence against seeded sandbox fixtures (`JRN-202611-ERR01`, `TRX-AR-202611-DUP01`, `BILL-202611-NOJRN01`).
    - Clean run reporting (`Passed`) when zero anomalies exist.
    - End-to-end telemetry ingestion creating `dbo.Issues` and `dbo.Occurrences` with exact correlation keys and financial evidence fields.
    - Embedded Kestrel webhook execution returning synchronous findings payloads within the 15-second contract.
- Changed Files:
  - `src/backend/PrIMA.Detectors/Probes/JournalBalanceDetector.cs`
  - `src/backend/PrIMA.Detectors/Probes/DuplicateReceivableDetector.cs`
  - `src/backend/PrIMA.Detectors/Probes/UnprocessedBillingPipelineDetector.cs`
  - `src/backend/PrIMA.Detectors/Services/ReadOnlySimrsConnectionFactory.cs`
  - `src/backend/PrIMA.Detectors/Services/DetectorSdkExtensions.cs`
  - `src/backend/PrIMA.Detectors/Program.cs`
  - `src/backend/PrIMA.Detectors/appsettings.json`
  - `src/backend/PrIMA.Api.Tests/Integration/PilotSimrsFinancialProbesTests.cs`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P6-S14

Title: Pilot SIMRS Detector Suite: Sync & Infrastructure Probes (EMR, HFIS, Print Queue)

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Authored `EmrSyncDetector.cs` in `src/backend/PrIMA.Detectors/Probes/`:
    - Scope: `DataSync`, Module: `EMR`, Base Severity: `Medium`, Cadence: 300s, WebhookEndpointKey: `emr-sync`.
    - Queries `simrs.emr_sync_logs` for failed sync logs or pending transactions older than 30 minutes.
    - Maps target entity reference `encounter_no`, sets financial evidence fields to `null`, and formats trace details.
  - Authored `HfisScheduleSyncDetector.cs` in `src/backend/PrIMA.Detectors/Probes/`:
    - Scope: `DataSync`, Module: `Outpatient`, Base Severity: `Medium`, Cadence: 300s, WebhookEndpointKey: `hfis-schedule-sync`.
    - Queries `simrs.hfis_doctor_schedules` comparing practice times (`SimrsStart`-`SimrsEnd` vs `HfisStart`-`HfisEnd`) and quotas between SIMRS and BPJS HFIS.
    - Maps target entity reference `doctor_schedule_code`, sets financial evidence fields to `null`, and formats schedule discrepancy trace.
  - Authored `PrintQueueSpoolerDetector.cs` in `src/backend/PrIMA.Detectors/Probes/`:
    - Scope: `Infrastructure`, Module: `PrintSpooler`, Base Severity: `Critical`, Cadence: 60s, WebhookEndpointKey: `print-queue-spooler`.
    - Queries `simrs.infra_print_queues` where `spooler_status <> 'Online'` or `pending_jobs > 0 AND oldest_job_age_seconds >= 300`.
    - Maps target entity reference `station_queue_name`, sets financial evidence fields to `null`, and formats printer backlog trace.
  - Updated `DetectorSdkExtensions.cs`:
    - Added `AddPilotSyncAndInfraProbes()` and `AddAllPilotSimrsProbes()` extension methods registering probes in DI container.
  - Authored comprehensive test suite `PilotSimrsSyncAndInfraProbesTests.cs` (15/15 tests passing):
    - Manifest declarations for all 3 sync/infrastructure probes.
    - Anomaly detection asserting exact non-financial evidence against seeded sandbox fixtures (`ENC-202611-FAIL01`, `ENC-202611-STALL01`, `SCH-OBG-002`, `PRN-KASIR-02`, `PRN-IGD-01`).
    - Clean run reporting (`Passed`) when zero anomalies exist.
    - End-to-end telemetry ingestion creating `dbo.Issues` and `dbo.Occurrences` with exact correlation keys and strictly null financial evidence fields per `PRIMA-DOMAIN.md §TelemetryEvidence`.
    - Embedded Kestrel webhook execution returning synchronous findings payloads within the 15-second contract.
- Changed Files:
  - `src/backend/PrIMA.Detectors/Probes/EmrSyncDetector.cs`
  - `src/backend/PrIMA.Detectors/Probes/HfisScheduleSyncDetector.cs`
  - `src/backend/PrIMA.Detectors/Probes/PrintQueueSpoolerDetector.cs`
  - `src/backend/PrIMA.Detectors/Services/DetectorSdkExtensions.cs`
  - `src/backend/PrIMA.Api.Tests/Integration/PilotSimrsSyncAndInfraProbesTests.cs`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

## P7 - Production Deployment Bundle, Operational Runbooks & Quality Gate

Implementation Status: IMPLEMENTED  
Review Status: NOT-REVIEWED  

### P7-S15

Title: On-Premise Production Deployment Packaging Bundle

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Created `deploy/appsettings.Production.json` configuring production connection strings for `PrIMADb` and read-only `SimrsDb`, JWT settings, 60s supervisor polling, 30m SLA escalation, and 90-day retention pruning.
  - Created `deploy/nginx/prima.conf` with modern TLS 1.2/1.3 ciphers, HSTS, security headers, Gzip compression, SPA client fallback, and `/api/*` and `/health` reverse proxying to Kestrel.
  - Created `deploy/iis/web.config` providing IIS URL Rewrite rules for HTTP-to-HTTPS redirect, reverse proxying `/api/*` and `/health` to Kestrel port 5000 via ARR, Vue SPA client routing fallback, static MIME mappings, and security HTTP headers.
  - Created `deploy/docker-compose.prod.yml` and accompanying multi-stage production Dockerfiles (`deploy/docker/Dockerfile.api`, `deploy/docker/Dockerfile.detectors`, `deploy/docker/Dockerfile.frontend`) coordinating SQL Server 2022, PrIMA API, Detector Runner daemon, and Nginx frontend.
  - Created `deploy/windows-service/install-prima-service.ps1` PowerShell script supporting elevated installation, automatic crash recovery (60s service restarts), Windows Firewall rule generation (ports 5000 and 5090), and service lifecycle management.
  - Created `deploy/sql/provision-simrs-readonly-login.sql` provisioning server login `prima_readonly` and SIMRS database user with explicit `GRANT SELECT` restricted to the 6 pilot tables and explicit `DENY` on all DML/DDL mutations (`DEC-008`, `BR-PRIMA-007`).
  - Created `deploy/sql/maintenance-retention-pruning.sql` defining SQL Server Agent job `PrIMA_Daily_Retention_Pruning` scheduled daily at 02:00 AM off-peak, deleting clean `Passed` runs older than 90 days in 1,000-row chunks while preserving occurrence-linked runs (`DEC-009`, `BR-PRIMA-009`).
  - Created `deploy/README.md` providing end-to-end hospital EDP deployment and operations guide.
  - Validated syntax of JSON (`ConvertFrom-Json`), XML (`[xml]`), and PowerShell (`[System.Management.Automation.Language.Parser]`).
- Changed Files:
  - `deploy/appsettings.Production.json`
  - `deploy/nginx/prima.conf`
  - `deploy/iis/web.config`
  - `deploy/docker-compose.prod.yml`
  - `deploy/docker/Dockerfile.api`
  - `deploy/docker/Dockerfile.detectors`
  - `deploy/docker/Dockerfile.frontend`
  - `deploy/windows-service/install-prima-service.ps1`
  - `deploy/sql/provision-simrs-readonly-login.sql`
  - `deploy/sql/maintenance-retention-pruning.sql`
  - `deploy/README.md`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P7-S16

Title: EDP Operational Remediation SOP & Staging UAT / Hypercare Playbooks

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Created `docs/operations/PRIMA-EDP-REMEDIATION-SOP.md`:
    - Detailed the universal incident lifecycle and remediation action categories (Modal `M-02`: `DataCorrection`, `ServiceRestart`, `ConfigAdjustment`, `CodeRemediation`).
    - Authored standalone operational reference sheets for all 6 pilot detectors:
      1. `JournalBalanceDetector` (`journal-balance`, Accounting, `DataIntegrity`)
      2. `DuplicateReceivableDetector` (`duplicate-receivable`, Billing, `DataIntegrity`)
      3. `UnprocessedBillingPipelineDetector` (`unprocessed-billing`, Billing, `ProcessIntegrity`)
      4. `EmrSyncDetector` (`emr-sync`, EMR, `DataSync`)
      5. `HfisScheduleSyncDetector` (`hfis-schedule-sync`, Outpatient, `DataSync`)
      6. `PrintQueueSpoolerDetector` (`print-queue-spooler`, PrintSpooler, `Infrastructure`)
    - Provided exact diagnostic SQL queries using `READ UNCOMMITTED` and `SET LOCK_TIMEOUT 2000`, root cause analysis, application correction procedures in SIMRS, and verification methods (`D-2` automated webhook re-run vs. `D-3` manual verification modal `M-03`) satisfying the Verification Gate.
    - Defined escalation governance with automated 30-minute SLA rules (`BR-PRIMA-004`), 4 response tiers, and departmental contact directory.
    - Documented false positive dismissal governance with mandatory >=20 character rationale (`BR-PRIMA-006`, Modal `M-01`).
  - Created `docs/operations/PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md`:
    - Structured full staging UAT scenario battery covering use cases `A-1` through `H-2` plus Zero-Drift Handshake Enrollment (`DEC-004`), including Quick Enroll modal, pending token revocation, manifest handshake API, per-detector key provisioning, and cadence drift detection (`BR-PRIMA-010`).
    - Authored step-by-step test scripts with pass/fail criteria and sign-off table.
    - Provided 2-week production hypercare guide including 3-shift daily EDP checklists (morning, midday peak, evening close), threshold tuning rules (cadence drift ±50%, silence alert multiplier, 5s query timeouts), and emergency rollback runbooks (Windows service, IIS, database).
- Changed Files:
  - `docs/operations/PRIMA-EDP-REMEDIATION-SOP.md`
  - `docs/operations/PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

### P7-S17

Title: Full-Stack Stage 5 Quality Gate & End-to-End Handshake Ingestion Smoke Verification

Implementation Status: IMPLEMENTED  
Review Status: GO  

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
- Implementation completed:
  - Authored `Stage5FullStackQualityGateSmokeTests.cs` covering the complete end-to-end operational workflow:
    1. Authenticates EDP officer and issues 24-hour enrollment token (`PRM-TOKEN-...`) via `POST /api/detectors/enrollment-tokens`.
    2. Performs anonymous bootstrap handshake via `POST /api/detectors/handshake` submitting pilot `JournalBalanceDetector` manifest, verifying 200 OK, per-detector API key (`prm_key_...`), and initial `Unknown` health status.
    3. Verifies database state: token marked `Consumed` with `ConsumedByDetectorId` and timestamp, and detector stores hashed API key (`ApiKeyHash`).
    4. Ingests telemetry findings using provisioned per-detector API key via `POST /api/ingestion/telemetry`, verifying `dbo.Issues` and `dbo.Occurrences` creation with financial evidence (`DebitTotal`, `CreditTotal`, `VarianceDelta`) per `BR-PRIMA-001`.
    5. Submits subsequent scheduled run with interval diverging +100% (600s actual vs 300s expected), verifying non-blocking warning response and database persistence of `LastActualIntervalSeconds` and `CadenceDriftWarning` per `BR-PRIMA-010`.
    6. Hosts embedded Kestrel webhook listener via `MapDetectorWebhooks` and dispatches `POST /webhooks/journal-balance`, returning 200 OK with synchronous findings payload within the 15-second contract.
    7. Verifies deep database health check via `GET /api/health` returning 200 OK with `Status: Healthy` and `Database: Connected`.
    8. Verifies daily retention pruning via `MonitoringSupervisorWorker.ExecuteRetentionPruningAsync()`, confirming deletion of clean `Passed` runs older than 90 days while preserving findings, occurrences, and recent runs (`BR-PRIMA-009`).
  - Added focused unit/integration tests in the suite verifying single-use token consumption (401 on reuse), invalid detector key rejection (401), negative cadence drift (-66.7%), and health check connectivity.
  - Executed full backend solution test suite: `dotnet test src/backend/PrIMA.slnx` -> 100% passed (447/447 passed, 0 failed, 0 skipped).
  - Executed frontend static compliance audit: `npm run audit:compliance` -> 0 violations.
  - Executed frontend production bundle build: `npm run build` (`vue-tsc -b && vite build`) -> succeeded with 0 errors.
- Changed Files:
  - `src/backend/PrIMA.Api.Tests/Integration/Stage5FullStackQualityGateSmokeTests.cs`
  - `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- 2026-11-22: Initial creation of Stage 5 Implementation Plan covering DbUp 005 migration and SIMRS sandbox fixtures, backend enrollment and handshake minimal APIs, per-detector hashed key validation, cadence drift supervision, frontend Quick Enroll modal, pending tokens strip, and canonical scope alignment, standardized .NET 8 Reference Detector SDK, 6 pilot SIMRS detectors, programmatic non-intrusive safety guardrails, on-premise deployment bundle, deep health checks, automated 90-day retention pruning, and hospital EDP operational runbooks. Plan structure released for execution (`Execution Approval: APPROVED`).
