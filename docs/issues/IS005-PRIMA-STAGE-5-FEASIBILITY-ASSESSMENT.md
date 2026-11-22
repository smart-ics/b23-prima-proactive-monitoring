---
Title: PrIMA Stage 5 — Detector Handshake Protocol, Pilot SIMRS Detector Suite & Hospital Production Rollout
Code: PRIMA-STAGE-5
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-11-22
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Assess the feasibility of `IS005-PRIMA-STAGE-5-ISSUE.md` (`ID: PRIMA-STAGE-5`, `Type: CHANGE-REQUEST`): implementing Stage 5 ("Detector Handshake Protocol, Pilot SIMRS Detector Suite & Hospital Production Rollout") of `docs/PRIMA-DEVELOPMENT-ROADMAP.md` to achieve the **Hospital On-Premise Production Go-Live & Automated Detector Enrollment** milestone.

In scope for Stage 5:
1. **Zero-Drift Detector Enrollment & Handshake Protocol:**
   - Backend enrollment token management endpoints (`POST /api/detectors/enrollment-tokens`, `GET /api/detectors/enrollment-tokens`, `DELETE /api/detectors/enrollment-tokens/{id}`) allowing authenticated EDP officers to generate short-lived (24h TTL), single-use enrollment tokens (`PRM-TOKEN-...`) and inspect/revoke unconsumed tokens.
   - Backend automated handshake endpoint (`POST /api/detectors/handshake`) allowing detector runners on initial boot or upgrade to submit their runtime technical manifest (`enrollmentToken`, `name`, `targetModule`, `scope`, `expectedCadenceSeconds`, `webhookUrl`, `description`, `version`), provision a unique permanent `detectorId` and a per-detector API key (`prm_key_...`, stored hashed as SHA-256 on `dbo.Detectors`), and activate or idempotently update the detector without configuration drift.
   - Guardrails enforcing name uniqueness (`BR-PRIMA-008`), bounded execution cadences (`30 <= expectedCadenceSeconds <= 86400`) to protect silence supervision (`BR-PRIMA-010`), single-use token expiration, and non-blocking cadence drift detection (`±50%` deviation on scheduled runs).
   - Frontend integration on `S-03 Detector List` (Quick Enroll modal with ready-to-copy environment variables, plus a collapsible `"Pending Enrollment Tokens (Awaiting Handshake)"` strip with token revocation) and `S-05 Detector Form` (segmented mode switcher between `"Automated Handshake (Recommended)"` and `"Manual Registration"`, plus alignment of the `S-05` scope dropdown to canonical scope values).
2. **Standardized .NET 8 Reference Detector SDK & Pilot SIMRS Detector Suite (`src/backend/PrIMA.Detectors/`):**
   - A compiled .NET 8 C# WebApplication/Worker Service host (`PrIMA.Detectors`) linked in `PrIMA.slnx` (and documented/referenced in `src/tools/detector-runner-template/`) providing the reusable Detector Runner SDK: `IDetectorProbe`, `BootstrapHandshakeService` with local JSON credential cache, `TelemetryIngestionClient`, automatic `ExecutionFailed` exception trapping (`BR-PRIMA-005`), and built-in Kestrel `/webhooks/{detectorKey}` endpoints for synchronous `Trigger Run` (`S-04`) and `Trigger Verification Re-run` (`S-02` / `D-2`).
   - Six production pilot SIMRS Detectors across all four monitoring scopes (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`) executing real Dapper SQL queries against a configurable `ReadOnlySimrsDb` connection (backed by a realistic `simrs` sandbox schema in local/test environments):
     1. *Journal Balance Detector* (`DataIntegrity`, `Accounting`, `simrs.acc_journal_entries`)
     2. *Duplicate Receivable Detector* (`DataIntegrity`, `Billing/AR`, `simrs.ar_receivables`)
     3. *Missing Auto-Journal / Unprocessed Billing Pipeline Detector* (`ProcessIntegrity`, `Billing`, `simrs.trx_patient_bills`)
     4. *EMR Synchronization Detector* (`DataSync`, `EMR`, `simrs.emr_sync_logs`)
     5. *SIMRS–HFIS Doctor Schedule Detector* (`DataSync`, `Outpatient/BPJS`, `simrs.hfis_doctor_schedules`)
     6. *Remote Print Server Queue Detector* (`Infrastructure`, `PrintSpooler`, `simrs.infra_print_queues`)
3. **Non-Intrusive Safety & On-Premise Deployment Hardening (`BR-PRIMA-007`):**
   - Programmatic `ReadOnlySimrsConnectionFactory` enforcing `ApplicationIntent=ReadOnly`, non-blocking isolation (`SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SET LOCK_TIMEOUT 2000;`), strict Dapper `CommandTimeout` (5s default), and a static SQL DML/DDL guard rejecting any mutation keywords (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `EXEC`, `MERGE`).
   - Automated xUnit non-interference verification suite (`SimrsNonInterferenceSafetyTests.cs`) proving sub-500ms query execution, zero blocking locks under concurrent simulated SIMRS writes, and safe fallback to `BR-PRIMA-005` `ExecutionFailed` on timeout.
   - Complete on-premise deployment bundle (`deploy/`) including `appsettings.Production.json`, Nginx/IIS TLS reverse-proxy templates, Docker Compose and Windows Service installation scripts, read-only SIMRS DBA login provisioning script, SQL Server backup/retention scripts, deep DB health check on `GET /api/health`, and daily automated retention pruning of `Passed` `InspectionRuns` older than 90 days in `MonitoringSupervisorWorker`.
4. **Operational Enablement, UAT & Production Hypercare Documentation (`docs/operations/`):**
   - `docs/operations/PRIMA-EDP-REMEDIATION-SOP.md`: Concise EDP Remediation SOP quick-reference sheets mapped to all 6 pilot detectors, their `ActionCategory`, diagnostic SQL queries, and `M-03`/`D-2` verification steps.
   - `docs/operations/PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md`: End-to-end staging UAT scenario checklist (`A-1` through `H-2` + Handshake Enrollment) and 2-week production hypercare tuning guide.

Referenced artifacts:
- `ISSUE`: `docs/issues/IS005-PRIMA-STAGE-5-ISSUE.md`
- `ROADMAP`: `docs/PRIMA-DEVELOPMENT-ROADMAP.md` (Stage 5, lines 215–264; Guardrail #4 & #6, lines 326–332)
- `PRODUCT DEFINITION`: `docs/PrIMA - Product Definition.md` (§3 Scope of Monitoring, §7.7 Operational Safety, §9 Deployment Concept, §10 Success Criteria)
- `DOMAIN`: `docs/PRIMA-DOMAIN.md` (`BR-PRIMA-001` through `BR-PRIMA-010`, specifically `BR-PRIMA-005`, `BR-PRIMA-007`, `BR-PRIMA-008`, `BR-PRIMA-010`)
- `USE-CASES`: `docs/PRIMA-USE-CASE-DISCOVERY.md` (`A-1` through `H-2`, Detector Handshake Enrollment)
- `SCREENS`: `docs/PRIMA-SCREEN-INVENTORY.md` (`S-03`, `S-04`, `S-05`)
- `UI-UX GUIDELINE`: `docs/PRIMA-UI-UX-GUIDELINE.md` (§6.5 `S-03`, §6.6 `S-04`, §6.7 `S-05`, §10 Anti-Patterns Checklist)
- `ARCHITECTURE`: `docs/PRIMA-ARCHITECTURE.md`
- Completed Baseline Artifacts: Stages 1 through 4 issues, feasibility assessments, architectures, implementation plans, reviews, and test packages.

## Objective

Assess Stage 5 feasibility against the current codebase (`src/backend` and `src/frontend`), database schema, and hospital operational constraints. Surface, grill, and resolve every architectural, security, domain, UX, and deployment question so downstream `ica-architect` Architecture Update, Planning, and implementation agents do not have to invent any decision.

---

# 2. Current State

## Existing Behavior

- **Stages 1 through 4 are fully implemented and verified in the repository:**
  - `src/backend/PrIMA.Api/`: ASP.NET Core .NET 8 Minimal API with vertical slices (`Auth`, `Ingestion`, `Issues`, `Officers`, `Detectors`, `History`), DbUp migrations `001` through `004`, `MonitoringSupervisorWorker` (60s silence detection and SLA severity escalation), and `DetectorWebhookClient` (15s synchronous HTTP webhook trigger for `S-04` manual runs and `S-02` `D-2` verification re-runs).
  - `src/frontend/`: Vue 3.5+ SPA with Tailwind CSS v4, PrimeVue v4 (Aura preset), TanStack Query polling, and all 8 screens (`S-00` through `S-07`) and 3 modals (`M-01` through `M-03`).
- **Detector Registration & Authentication Limitations:**
  - Detectors can only be registered manually by filling out all technical metadata in `S-05` (`POST /api/detectors`).
  - No enrollment token table (`dbo.DetectorEnrollmentTokens`) or handshake endpoints (`POST /api/detectors/enrollment-tokens`, `POST /api/detectors/handshake`) exist.
  - `dbo.Detectors` lacks columns for `ApiKeyHash`, `Version`, `LastActualIntervalSeconds`, and `CadenceDriftWarning`.
  - `ApiKeyEndpointFilter.cs` authenticates `POST /api/ingestion/telemetry` exclusively against a static `Ingestion:ApiKeys` array in `appsettings.json` (`prima-dev-key-alpha-99`), with no support for per-detector provisioned API keys.
  - `IngestTelemetryHandler.cs` does not evaluate or warn on cadence drift when a scheduled detector reports outside its registered `ExpectedCadenceSeconds`.
- **Frontend `S-03` & `S-05` Gaps:**
  - `DetectorListView.vue` (`S-03`) has no Quick Enroll / Handshake modal and no UI strip for viewing or revoking pending unconsumed enrollment tokens.
  - `DetectorFormView.vue` (`S-05`) has no Handshake pairing mode tab. Furthermore, `detectorForm.schema.ts` currently lists legacy scope strings (`DataQuality`, `BusinessLogic`, `Infrastructure`, `ProcessFlow`) instead of the canonical domain scopes (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`).
- **Detector Runner SDK & Pilot SIMRS Detectors:**
  - Only a Stage 1 Node.js CLI simulator (`src/tools/telemetry-simulator/simulate.mjs`) exists.
  - There is no .NET 8 Detector Runner SDK project (`PrIMA.Detectors`), no automatic bootstrap handshake client, no read-only SIMRS connection guard (`ReadOnlySimrsConnectionFactory`), no SIMRS sandbox tables, and none of the 6 pilot SIMRS detectors exist.
- **Deployment Hardening & Operational Enablement:**
  - `GET /api/health` returns a static `200 OK` JSON object without verifying SQL Server database connectivity.
  - `MonitoringSupervisorWorker` does not perform automated retention pruning of old `Passed` `InspectionRuns`.
  - No production deployment bundle (`deploy/`) or EDP remediation SOPs / UAT playbooks (`docs/operations/`) exist.

## Existing Constraints

- **Verified Local & Target Environment:**
  - OS: Windows (PowerShell `pwsh`).
  - .NET SDK: `net8.0` (`8.0.428`).
  - Node.js / npm: `v22.22.3` / `10.9.8`.
  - Database: Microsoft SQL Server 2022 Developer Edition on `localhost` (`MSSQLSERVER`).
- **Architectural & Domain Constraints:**
  - Backend: ASP.NET Core .NET 8 Minimal API, Dapper, FluentValidation, RFC 7807 `ProblemDetails`, DbUp embedded SQL migrations.
  - Frontend: Vue 3.5+, Vite 6, Tailwind CSS v4, PrimeVue v4 (Aura), Pinia, TanStack Query v5, `openapi-typescript` contract generation, and strict feature-slice isolation (`src/features/{auth,dashboard,issues,detectors,history}/` with zero cross-feature imports and zero forbidden anti-patterns per `PRIMA-UI-UX-GUIDELINE.md §10`).
  - Operational Safety (`BR-PRIMA-007`): Pilot detectors must operate strictly as read-only probes with zero exclusive/blocking locks, mandatory command timeouts, and zero data mutation capability against SIMRS tables.

---

# 3. Gap Analysis

| ID | Severity | Status | Gap |
|---|---|---|---|
| GAP-001 | CRITICAL | CLOSED | Missing database schema support for Zero-Drift Handshake Enrollment, per-detector API keys, cadence drift tracking, and SIMRS sandbox tables (`005_Stage5HandshakeAndSimrsSandbox.sql`): `dbo.DetectorEnrollmentTokens` does not exist, and `dbo.Detectors` lacks `ApiKeyHash`, `Version`, `LastActualIntervalSeconds`, and `CadenceDriftWarning` columns. |
| GAP-002 | CRITICAL | CLOSED | Missing backend Enrollment Token & Handshake vertical slices (`Features/Detectors/Enrollment/*`): no endpoints to generate, list, or revoke enrollment tokens, no `POST /api/detectors/handshake` endpoint for manifest auto-registration / idempotent upgrade, and `ApiKeyEndpointFilter` cannot validate per-detector hashed API keys. |
| GAP-003 | MAJOR | CLOSED | Missing telemetry cadence drift detection in `IngestTelemetryHandler`: scheduled runs diverging by `> ±50%` from `ExpectedCadenceSeconds` are not flagged in `dbo.Detectors` or returned in `IngestTelemetryResponse.Warnings`. |
| GAP-004 | CRITICAL | CLOSED | Missing frontend Quick Enroll modal, Pending Enrollment Tokens strip on `S-03`, Handshake mode switcher on `S-05`, and canonical `MonitoringScope` alignment (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`) in `S-05` (`detectorForm.schema.ts` and `DetectorFormView.vue`). |
| GAP-005 | CRITICAL | CLOSED | Missing .NET 8 Reference Detector SDK & Unified Pilot Detector Host (`src/backend/PrIMA.Detectors/`): no bootstrap handshake runner, local credential store, `ExecutionFailed` trap (`BR-PRIMA-005`), or Kestrel `/webhooks/{detectorKey}` listener for on-demand `S-04` and `S-02` (`D-2`) runs. |
| GAP-006 | CRITICAL | CLOSED | Missing the 6 Pilot SIMRS Detectors across the 4 monitoring scopes (`JournalBalanceDetector`, `DuplicateReceivableDetector`, `UnprocessedBillingPipelineDetector`, `EmrSyncDetector`, `HfisScheduleSyncDetector`, `PrintQueueSpoolerDetector`) and their backing `simrs` schema tables for local/automated testing. |
| GAP-007 | CRITICAL | CLOSED | Missing programmatic `BR-PRIMA-007` non-interfering read-only enforcement (`ReadOnlySimrsConnectionFactory` with `ApplicationIntent=ReadOnly`, `READ UNCOMMITTED`, `LOCK_TIMEOUT 2000`, 5s `CommandTimeout`, and DML/DDL keyword guard) and automated non-interference xUnit tests (`SimrsNonInterferenceSafetyTests.cs`). |
| GAP-008 | MAJOR | CLOSED | Missing on-premise deployment bundle (`deploy/`), deep SQL health check (`GET /api/health`), automated 90-day `Passed` `InspectionRuns` retention cleanup in `MonitoringSupervisorWorker`, and EDP operational SOP / UAT & Hypercare documentation (`docs/operations/`). |

---

# 4. Open Questions

| ID | Status | Question | Impact |
|---|---|---|---|
| OQ-001 | CLOSED | How should enrollment tokens and detector API credentials be provisioned, persisted, and authenticated during the `POST /api/detectors/handshake` lifecycle? | Determines schema design for `dbo.DetectorEnrollmentTokens` and `dbo.Detectors`, token hashing, and `ApiKeyEndpointFilter` authentication flow. |
| OQ-002 | CLOSED | How should `POST /api/detectors/handshake` behave when a detector script with an existing `Name` is redeployed or re-enrolled with a new token (e.g., host migration, credential rotation, or manifest version bump)? | Governs idempotent manifest sync vs. `409 Conflict` behavior and unconsumed token revocation in `S-03`. |
| OQ-003 | CLOSED | During `POST /api/ingestion/telemetry`, how should cadence drift (divergence between actual execution interval and registered `ExpectedCadenceSeconds`) be calculated, thresholded, and surfaced? | Defines drift threshold (`±50%` on `Scheduled` runs), detector drift columns, API response warnings, and `S-03`/`S-04` UI indicators. |
| OQ-004 | CLOSED | How should unconsumed enrollment tokens (`PendingEnrollment`) and the Quick Enroll Handshake workflow be structured on `S-03` and `S-05`? | Governs `S-03` pending token strip, Quick Enroll modal UX, `S-05` mode switcher, and canonical scope alignment. |
| OQ-005 | CLOSED | What runtime and execution architecture should be used for the Reference Detector SDK & Runner Template? | Establishes .NET 8 C# Worker/WebApplication architecture over Node.js scripts for hospital type-safety and Windows/Linux service deployment. |
| OQ-006 | CLOSED | How should the .NET 8 Detector Runner SDK and the 6 Pilot SIMRS Detectors be packaged in the solution and tested against SIMRS database schemas? | Defines unified `src/backend/PrIMA.Detectors/` project structure, built-in Kestrel webhook routing, and `simrs` sandbox tables for end-to-end SQL testing. |
| OQ-007 | CLOSED | What are the exact deterministic business rules, correlation keys, base severities, and `TelemetryEvidence` structures for the 6 pilot SIMRS detectors? | Locks in exact SQL predicates, target entities, severities, and financial vs. non-financial evidence mapping for all 6 pilot detectors. |
| OQ-008 | CLOSED | How should the non-intrusive read-only inspection constraints (`BR-PRIMA-007`) be technically enforced in the .NET 8 Detector Runner SDK and verified by automated quality gates? | Establishes `ReadOnlySimrsConnectionFactory` guardrails, DBA read-only login script, and `SimrsNonInterferenceSafetyTests.cs`. |
| OQ-009 | CLOSED | How should the hospital on-premise deployment packaging, database retention routines, health checks, and EDP operational documentation be structured in the repository? | Defines `deploy/` artifacts, deep DB health check, 90-day batch retention pruning of `Passed` runs, and `docs/operations/` runbooks. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | All existing Stage 1–4 database migrations (`001` through `004`) remain immutable; Stage 5 schema extensions and `simrs` sandbox tables are added via a new idempotent DbUp migration `005_Stage5HandshakeAndSimrsSandbox.sql`. |
| ASM-002 | In hospital production, `PrIMA.Detectors` connects to the live SIMRS database (or read replica) via a dedicated read-only SQL login configured in `ConnectionStrings:ReadOnlySimrsDb`, while in local development and automated xUnit tests it defaults to the `simrs` schema in the local SQL Server instance. |
| ASM-003 | The global `Ingestion:ApiKeys` array in `appsettings.json` remains supported alongside per-detector `ApiKeyHash` credentials so existing Stage 1–4 integration tests and `simulate.mjs` continue to pass with zero regressions. |
| ASM-004 | All frontend additions on `S-03`, `S-04`, and `S-05` strictly obey `PRIMA-UI-UX-GUIDELINE.md §10` (zero pagination, zero external chart libraries, zero native browser alerts/confirms, zero cross-slice imports). |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Pilot SIMRS detector queries acquiring shared locks or running long table scans that block peak hospital billing or EMR transactions (`BR-PRIMA-007` breach). | Degraded SIMRS clinical/financial performance during peak hospital hours. | Enforce `ReadOnlySimrsConnectionFactory` with `ApplicationIntent=ReadOnly`, `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED`, `SET LOCK_TIMEOUT 2000`, 5s Dapper `CommandTimeout`, static DML/DDL keyword rejection, and verify via `SimrsNonInterferenceSafetyTests.cs` (DEC-008). |
| RISK-002 | Configuration drift between deployed edge detector scripts and PrIMA server cadence/webhook settings. | False silence alarms (`BR-PRIMA-010`) or broken webhook verification re-runs (`D-2`). | Implement zero-drift `POST /api/detectors/handshake` with idempotent manifest synchronization on detector startup and `±50%` scheduled cadence drift warnings (DEC-001, DEC-002, DEC-003). |
| RISK-003 | Plaintext detector API keys or enrollment tokens leaked in database dumps. | Unauthorized telemetry spoofing if database backups are exposed. | Store both enrollment tokens (`TokenHash`) and provisioned per-detector API keys (`ApiKeyHash`) as SHA-256 hashes in SQL Server, returning the plaintext secret only once at generation/handshake time (DEC-001). |
| RISK-004 | Unbounded growth of `dbo.InspectionRuns` from high-frequency scheduled detectors running every 1–5 minutes over months of hospital operation. | SQL Server disk exhaustion and slower historical pass-rate aggregations. | Add automated daily batch retention pruning in `MonitoringSupervisorWorker` deleting `Passed` `InspectionRuns` older than `Retention:InspectionRunsDays` (default 90 days) while permanently preserving all `Issues`, `Occurrences`, and `FindingDiscovered`/`ExecutionFailed` runs (DEC-009). |
| RISK-005 | Legacy scope names (`DataQuality`, `BusinessLogic`, `ProcessFlow`) in `S-05` creating detectors that do not match canonical scope filters (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`) on `S-01`, `S-03`, and `S-07`. | Filtered views miss newly registered detectors or issues. | Align `detectorForm.schema.ts` and `DetectorFormView.vue` to the 4 canonical `MonitoringScope` enum values while preserving backward compatibility in backend enum parsing (DEC-004). |

---

# 7. Recommendations

The key architectural and business alternatives evaluated during stakeholder grilling are summarized below:

## 1. Enrollment Token & Per-Detector Credential Architecture (`OQ-001`, `OQ-002`)
- **Option A: Per-Detector Provisioned API Key + Token Lifecycle Table + Idempotent Re-Enrollment (Selected - DEC-001, DEC-002):** Create `dbo.DetectorEnrollmentTokens` (24h TTL, single-use, revocable) and add `ApiKeyHash` + `Version` to `dbo.Detectors`. `POST /api/detectors/handshake` validates and consumes the token, provisions a unique per-detector API key (`prm_key_...`), stores its SHA-256 hash, and supports idempotent manifest updates and key rotation when an existing non-retired detector re-enrolls by name.
- **Option B: Shared Global Key Return & Strict 409 on Duplicate Name:** Returns the static key from `appsettings.json` and rejects re-enrollment of existing detectors. Disadvantage: Cannot rotate credentials per detector or auto-update manifests on redeployment.

## 2. Reference Detector SDK Runtime & Packaging (`OQ-005`, `OQ-006`)
- **Option A: Unified .NET 8 C# Detector Host (`PrIMA.Detectors`) + `simrs` Sandbox Schema (Selected - DEC-005, DEC-006):** Build `src/backend/PrIMA.Detectors/` as a compiled .NET 8 C# WebApplication/Worker Service project in `PrIMA.slnx` (with template reference docs in `src/tools/detector-runner-template/`), hosting the reusable SDK (`IDetectorProbe`, `BootstrapHandshakeService`, `ReadOnlySimrsConnectionFactory`, Kestrel `/webhooks/{detectorKey}` listener) and all 6 pilot SIMRS detectors executing real SQL queries against `simrs.*` tables.
- **Option B: Node.js Scripts or 6 Separate .csproj Executables:** Disadvantage: Weak type safety for SQL Server connection pooling/timeouts or excessive process/port proliferation for hospital deployment.

## 3. Non-Interference Safety Enforcement (`OQ-008`)
- **Option A: Programmatic `ReadOnlySimrsConnectionFactory` + Automated Lock/Latency Test Suite (Selected - DEC-008):** Programmatically enforce `ApplicationIntent=ReadOnly`, `READ UNCOMMITTED`, `LOCK_TIMEOUT 2000`, 5s `CommandTimeout`, and DML/DDL keyword rejection on every SIMRS probe connection, backed by `SimrsNonInterferenceSafetyTests.cs`.
- **Option B: Documentation-Only Guidelines:** Disadvantage: Leaves `BR-PRIMA-007` vulnerable to accidental developer mistakes or missing query timeouts.

---

# 8. Gap Closure Decisions

| Decision ID | Target GAP / OQ | Decision | Rationale | Impact | Architecture Impact | Resolved By | Resolved Date |
|---|---|---|---|---|---|---|---|
| DEC-001 | GAP-001, GAP-002, OQ-001 | **Per-Detector Provisioned API Key & Enrollment Token Lifecycle Table:** Create `dbo.DetectorEnrollmentTokens` (`Id`, `TokenPrefix`, `TokenHash`, `Label`, `Status` [`Pending`, `Consumed`, `Revoked`], `ExpiresAtUtc` [default 24h], `CreatedByOfficer`, `CreatedAtUtc`, `ConsumedAtUtc`, `ConsumedByDetectorId`) and add `ApiKeyHash NVARCHAR(128) NULL`, `Version NVARCHAR(50) NULL` to `dbo.Detectors`. On `POST /api/detectors/handshake`, validate the unconsumed/unexpired token via SHA-256 hash, generate a per-detector API key (`prm_key_<32-byte-base64url>`), store its SHA-256 `ApiKeyHash` on `dbo.Detectors`, mark the token `Consumed`, and return `{ detectorId, apiKey, status, expectedCadenceSeconds }` once. Update `ApiKeyEndpointFilter` to authenticate `X-Detector-API-Key` against either the target detector's `ApiKeyHash` (constant-time SHA-256 match) or fallback global `Ingestion:ApiKeys`. | Provides cryptographic zero-drift enrollment, per-detector secret isolation, and full backward compatibility with existing Stage 1–4 tests and `simulate.mjs`. | Eliminates manual API key copying and configuration drift while keeping secrets hashed at rest. | Add `005_Stage5HandshakeAndSimrsSandbox.sql`, `Features/Detectors/Enrollment/*` endpoints, and enhance `ApiKeyEndpointFilter.cs`. | Drury / User | 2026-11-22 |
| DEC-002 | GAP-002, OQ-002 | **Idempotent Manifest Sync on Re-Enrollment & Token Revocation:** If `POST /api/detectors/handshake` receives a valid token for a detector `Name` that already exists in `dbo.Detectors`: if `AdminStatus == Retired`, return `409 Conflict`; otherwise perform an idempotent manifest update (`Description`, `Scope`, `TargetModule`, `ExpectedCadenceSeconds`, `WebhookUrl`, `Version`), rotate `ApiKeyHash` to the newly issued key, ensure `AdminStatus = Active`, record an activity/audit log, and return `200 OK` with the existing `detectorId` and new `apiKey` (`isNewRegistration: false`). Enforce cadence bounds (`30 <= expectedCadenceSeconds <= 86400`, else `422`). Allow officers to revoke unconsumed tokens via `DELETE /api/detectors/enrollment-tokens/{id}` (`409` if already consumed). | Supports zero-touch container/service redeployment, manifest upgrades, and credential rotation without manual UI edits, while respecting retired detector immutability (`BR-PRIMA-008`). | Seamless lifecycle management for edge detector upgrades and host migrations. | Implement create-or-update transaction in `DetectorHandshakeHandler.cs` and revocation in `RevokeEnrollmentTokenHandler.cs`. | Drury / User | 2026-11-22 |
| DEC-003 | GAP-001, GAP-003, OQ-003 | **Non-Blocking Cadence Drift Detection (±50% on Scheduled Runs):** Add `LastActualIntervalSeconds INT NULL` and `CadenceDriftWarning NVARCHAR(500) NULL` to `dbo.Detectors`. In `IngestTelemetryHandler`, when `TriggerSource == Scheduled` and `detector.LastRunAtUtc` is present, compute `actualIntervalSeconds = (request.ExecutionTimestampUtc - detector.LastRunAtUtc).TotalSeconds`. If `actualIntervalSeconds < ExpectedCadenceSeconds * 0.5` or `> ExpectedCadenceSeconds * 1.5`, populate `CadenceDriftWarning` and include the warning in `IngestTelemetryResponse.Warnings` (`string[]`). If within `[0.5x, 1.5x]`, clear `CadenceDriftWarning = null`. Ignore `Manual` and `EventHook` runs for drift calculation. Surface non-null `CadenceDriftWarning` as an amber pill/tooltip on `S-03` and `S-04`. | Detects cron/scheduler misconfigurations early per Roadmap §2 without creating noisy false-positive operational Issues on `S-01`. | Immediate visibility into scheduler drift for EDP officers on `S-03`/`S-04`. | Update `IngestTelemetryHandler.cs`, `IngestTelemetryResponse.cs`, `GetDetectorList`, `GetDetectorById`, and `S-03`/`S-04` components. | Drury / User | 2026-11-22 |
| DEC-004 | GAP-004, OQ-004 | **S-03 Pending Tokens Strip & Quick Enroll Modal + S-05 Handshake Mode & Canonical Scope Alignment:** (1) On `S-03`, add a `"Quick Enroll (Handshake)"` button opening `EnrollmentTokenModal.vue` (generates token with optional label, renders copyable `PRIMA_ENROLLMENT_TOKEN` and `PRIMA_SERVER_URL` snippets), and render a collapsible `"Pending Enrollment Tokens (Awaiting Handshake)"` strip above the detector table listing active `Pending` tokens from `GET /api/detectors/enrollment-tokens` with a `Revoke` action. (2) On `S-05` (create mode), add a segmented mode switcher: `"Automated Handshake (Recommended)"` vs. `"Manual Registration"`. (3) Update `detectorForm.schema.ts` and `DetectorFormView.vue` to use the 4 canonical `MonitoringScope` values (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`). | Keeps `dbo.Detectors` clean of half-empty placeholder rows while giving EDP officers full visibility and control over pending enrollment tokens and consistent scope filtering across all screens. | Intuitive zero-drift enrollment UX and 100% scope consistency across `S-01`–`S-07`. | Add `EnrollmentTokenModal.vue`, `PendingEnrollmentsStrip.vue`, update `DetectorListView.vue`, `DetectorFormView.vue`, and `detectorForm.schema.ts`. | Drury / User | 2026-11-22 |
| DEC-005 | GAP-005, OQ-005 | **.NET 8 C# Reference Detector SDK & Dual-Mode Execution:** Implement the Reference Detector SDK in .NET 8 C# (`PrIMA.Detectors`), providing: (1) `IDetectorProbe` contract (`Manifest`, `ExecuteProbeAsync`); (2) `BootstrapHandshakeService` that exchanges `PRIMA_ENROLLMENT_TOKEN` (or per-detector token config) via `POST /api/detectors/handshake` and caches `{ detectorId, apiKey }` locally in `.prima-credentials.json`; (3) dual execution modes supporting continuous background cadence loops (`BackgroundService`) and one-shot CLI runs (`--run-once` for Windows Task Scheduler / cron); (4) automatic `try/catch` wrapping every probe to report `Outcome = ExecutionFailed` on unhandled exceptions or SQL timeouts (`BR-PRIMA-005`); and (5) built-in Kestrel HTTP webhook endpoints (`POST /webhooks/{detectorKey}`) matching `DetectorWebhookClient`'s 15s synchronous contract. | Matches the hospital's .NET 8 stack, provides compile-time safety, native `Microsoft.Data.SqlClient` pooling and timeout control, and runs seamlessly as a Windows Service or Linux container. | Standardized, enterprise-grade C# detector runner architecture. | Create `src/backend/PrIMA.Detectors/` project and document template usage in `src/tools/detector-runner-template/README.md`. | Drury / User | 2026-11-22 |
| DEC-006 | GAP-005, GAP-006, OQ-006 | **Unified `PrIMA.Detectors` Solution Project + `simrs` Sandbox Schema:** Add `src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj` to `src/backend/PrIMA.slnx` hosting the SDK and all 6 pilot SIMRS detectors in a single unified process. Add `simrs` schema and 6 realistic SIMRS tables (`simrs.acc_journal_entries`, `simrs.ar_receivables`, `simrs.trx_patient_bills`, `simrs.emr_sync_logs`, `simrs.hfis_doctor_schedules`, `simrs.infra_print_queues`) in migration `005_Stage5HandshakeAndSimrsSandbox.sql` (with baseline seeded clean + anomaly test fixtures in `DevelopmentDemoDataSeeder` / test fixtures) so all 6 pilot detectors run real Dapper SQL queries end-to-end in local dev, UAT, and xUnit integration tests, while switching to `ConnectionStrings:ReadOnlySimrsDb` in hospital production. | Avoids maintaining 6 separate `.csproj` deployments while guaranteeing every pilot detector executes real SQL queries against realistic hospital table schemas. | Full end-to-end automated testability of all 6 pilot SIMRS detectors inside `PrIMA.Api.Tests`. | Add `PrIMA.Detectors` to `PrIMA.slnx` and create `simrs.*` tables in `005_Stage5HandshakeAndSimrsSandbox.sql`. | Drury / User | 2026-11-22 |
| DEC-007 | GAP-006, OQ-007 | **Deterministic Business Rules & Evidence Specification for the 6 Pilot SIMRS Detectors:** Implement the 6 pilot detectors with exact specifications: (1) *Journal Balance Detector* (`DataIntegrity`, Module: `Accounting`, Table: `simrs.acc_journal_entries`, Key: `JournalBatchNo`, Severity: `High`, flags posted batches where `ABS(SUM(Debit) - SUM(Credit)) > 0.01`, populates `DebitTotal`, `CreditTotal`, `VarianceDelta`); (2) *Duplicate Receivable Detector* (`DataIntegrity`, Module: `Billing`, Table: `simrs.ar_receivables`, Key: `SourceTransactionNo`, Severity: `High`, flags active receivables with `COUNT(*) > 1` per `SourceTransactionNo`, populates `DebitTotal` = duplicate sum, `CreditTotal` = single expected amount, `VarianceDelta` = excess amount); (3) *Missing Auto-Journal / Unprocessed Billing Pipeline Detector* (`ProcessIntegrity`, Module: `Billing`, Table: `simrs.trx_patient_bills`, Key: `BillInvoiceNo`, Severity: `High`, flags settled bills `> 15` mins old missing a journal batch in `simrs.acc_journal_entries`, populates `DebitTotal` = bill amount, `CreditTotal` = `0`, `VarianceDelta` = bill amount); (4) *EMR Synchronization Detector* (`DataSync`, Module: `EMR`, Table: `simrs.emr_sync_logs`, Key: `EncounterNo`, Severity: `Medium`, flags records with `SyncStatus = 'Failed'` or `'Pending'` `> 30` mins, financial fields `null`); (5) *SIMRS–HFIS Doctor Schedule Detector* (`DataSync`, Module: `Outpatient`, Table: `simrs.hfis_doctor_schedules`, Key: `DoctorScheduleCode`, Severity: `Medium`, flags active schedules where local SIMRS hours/quota mismatch BPJS HFIS hours/quota, financial fields `null`); (6) *Remote Print Server Queue Detector* (`Infrastructure`, Module: `PrintSpooler`, Table: `simrs.infra_print_queues`, Key: `StationQueueName`, Severity: `Critical`, flags stations with `PendingJobs > 0` stalled `> 5` mins or `SpoolerStatus = 'Offline'`, financial fields `null`). | Closes all business rule and evidence schema ambiguities across the 4 monitoring scopes and respects `PRIMA-UI-UX-GUIDELINE.md §10` (financial fields populated only on financial anomalies, `null` on non-financial anomalies). | Deterministic correlation deduplication (`BR-PRIMA-001`) and rich diagnostic evidence on `S-02`. | Implement the 6 `IDetectorProbe` classes in `src/backend/PrIMA.Detectors/Probes/`. | Drury / User | 2026-11-22 |
| DEC-008 | GAP-007, OQ-008 | **Programmatic `BR-PRIMA-007` Read-Only Connection Guardrails & Automated Non-Interference Test Suite:** Implement `ReadOnlySimrsConnectionFactory` in `PrIMA.Detectors` that: (1) enforces `ApplicationIntent=ReadOnly` on the SQL connection string; (2) executes `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SET LOCK_TIMEOUT 2000;` on connection open so probe queries never place shared blocking locks on SIMRS OLTP transactions; (3) enforces a strict Dapper `CommandTimeout` (default `5` seconds); and (4) validates every probe SQL string against a static DML/DDL guard (` SimrsSqlSafetyGuard.EnsureReadOnlySelect(sql)`) that throws if any forbidden keyword (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `EXEC`, `EXECUTE`, `MERGE`, `CREATE`, `GRANT`, `REVOKE`) is present. Deliver `deploy/sql/provision-simrs-readonly-login.sql` and xUnit test suite `SimrsNonInterferenceSafetyTests.cs` verifying `< 500ms` execution, zero blocking locks during concurrent writes, DML rejection, and timeout trapping into `BR-PRIMA-005` `ExecutionFailed`. | Makes `BR-PRIMA-007` a mechanically enforced code invariant rather than a manual convention, protecting hospital SIMRS databases from locks or mutations. | Provable, auditable operational safety for hospital DBA sign-off. | Implement `ReadOnlySimrsConnectionFactory.cs`, `SimrsSqlSafetyGuard.cs`, `provision-simrs-readonly-login.sql`, and `SimrsNonInterferenceSafetyTests.cs`. | Drury / User | 2026-11-22 |
| DEC-009 | GAP-008, OQ-009 | **On-Premise Deployment Bundle, Deep DB Health Check, Automated 90-Day Run Retention & EDP Operational Runbooks:** (1) Deliver `deploy/` containing `appsettings.Production.json`, Nginx & IIS reverse-proxy TLS configs (`deploy/reverse-proxy/`), `deploy/docker-compose.yml`, `deploy/install-windows-service.ps1`, and `deploy/sql/backup-and-retention.sql`. (2) Enhance `GET /api/health` (and `/health`) to execute an asynchronous `SELECT 1` against SQL Server and report `{ status, database: "Connected", timestampUtc }` (or `503 Service Unavailable` if DB is unreachable). (3) Extend `MonitoringSupervisorWorker` with a daily retention pass (`Retention:InspectionRunsDays`, default `90`) that deletes `Passed` `dbo.InspectionRuns` older than the cutoff (where not referenced by `dbo.Occurrences`) in batches of `1000`, permanently preserving all `Issues`, `Occurrences`, and `FindingDiscovered`/`ExecutionFailed` runs (`BR-PRIMA-009`). (4) Publish `docs/operations/PRIMA-EDP-REMEDIATION-SOP.md` and `docs/operations/PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md`. | Completes all production readiness, database lifecycle hygiene, and EDP operational enablement deliverables required for Hospital Go-Live. | Turnkey hospital deployment package and clear operational runbooks for EDP officers. | Update `Program.cs` health endpoints, `MonitoringSupervisorWorker.cs`, and create `deploy/*` and `docs/operations/*` artifacts. | Drury / User | 2026-11-22 |

---

# 9. Architecture Applicability

## Decision

`ARCHITECTURE-REQUIRED`

## Rationale

Stage 5 introduces significant new technical components, persistence structures, authentication mechanics, and integration boundaries that require formal target-state definition in `docs/issues/IS005-PRIMA-STAGE-5-ARCHITECTURE.md` (and `docs/PRIMA-ARCHITECTURE.md`):
1. **Database Schema Evolution (`005_Stage5HandshakeAndSimrsSandbox.sql`):** New `dbo.DetectorEnrollmentTokens` table, new columns on `dbo.Detectors` (`ApiKeyHash`, `Version`, `LastActualIntervalSeconds`, `CadenceDriftWarning`), and the `simrs` schema containing 6 sandbox tables for end-to-end detector probing.
2. **New Backend Enrollment & Handshake Vertical Slices (`Features/Detectors/Enrollment/*`):** Token generation, listing, revocation, and zero-drift handshake manifest registration/upgrade endpoints, plus dual-mode per-detector `ApiKeyHash` authentication in `ApiKeyEndpointFilter`.
3. **Ingestion Pipeline & Background Worker Enhancements:** Scheduled cadence drift evaluation (`±50%`) in `IngestTelemetryHandler`, deep SQL Server health check on `GET /api/health`, and automated 90-day batch retention pruning of `Passed` `InspectionRuns` in `MonitoringSupervisorWorker`.
4. **New .NET 8 Detector Runner SDK & Pilot Suite Project (`src/backend/PrIMA.Detectors/`):** Reusable C# runner host with `BootstrapHandshakeService`, `ReadOnlySimrsConnectionFactory`, `SimrsSqlSafetyGuard`, Kestrel `/webhooks/{detectorKey}` endpoints, and the 6 pilot SIMRS detectors across all 4 monitoring scopes.
5. **Frontend `S-03`, `S-04`, and `S-05` Enhancements:** Enrollment Token Modal, Pending Enrollment Tokens strip with revocation, `S-03`/`S-04` cadence drift warning indicators, `S-05` Handshake mode switcher, and canonical `MonitoringScope` alignment.
6. **Production Deployment & Operational Enablement Bundle:** `deploy/` reverse-proxy, service packaging, DBA read-only provisioning scripts, and `docs/operations/` EDP Remediation SOPs and UAT/Hypercare playbooks.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved (`GAP-001` through `GAP-008` marked `CLOSED`)
- [x] All required decisions recorded (`DEC-001` through `DEC-009` with full Decision, Rationale, Impact, Architecture Impact, Resolved By, and Resolved Date)
- [x] All blocking open questions resolved (`OQ-001` through `OQ-009` marked `CLOSED`)
- [x] Architecture can be finalized or updated

## Status

READY-FOR-PLANNING

## Notes

All architectural, security, domain, database, UX, and hospital rollout questions for Stage 5 have been thoroughly grilled with the user and closed. Downstream `ica-architect` Architecture Update and Implementation Planning can proceed with zero ambiguity and without inventing any decisions.

---

# 11. References

Referenced artifacts:

- `ISSUE`: `docs/issues/IS005-PRIMA-STAGE-5-ISSUE.md`
- `DOMAIN`: `docs/PRIMA-DOMAIN.md`
- `PRODUCT DEFINITION`: `docs/PrIMA - Product Definition.md`
- `ROADMAP`: `docs/PRIMA-DEVELOPMENT-ROADMAP.md`
- `USE-CASES`: `docs/PRIMA-USE-CASE-DISCOVERY.md`
- `SCREENS`: `docs/PRIMA-SCREEN-INVENTORY.md`
- `UI-UX GUIDELINE`: `docs/PRIMA-UI-UX-GUIDELINE.md`
- `ARCHITECTURE`: `docs/PRIMA-ARCHITECTURE.md`
- Prior Stage Baselines: `docs/issues/IS001-*` through `docs/issues/IS004-*`

Referenced codebase locations:

- `src/backend/PrIMA.slnx`
- `src/backend/PrIMA.Api/Program.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/Detector.cs`
- `src/backend/PrIMA.Api/Domain/Detectors/MonitoringScope.cs`
- `src/backend/PrIMA.Api/Features/Detectors/DetectorEndpoints.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/ApiKeyEndpointFilter.cs`
- `src/backend/PrIMA.Api/Features/Ingestion/IngestTelemetryHandler.cs`
- `src/backend/PrIMA.Api/Infrastructure/Persistence/DetectorRepository.cs`
- `src/backend/PrIMA.Api/Infrastructure/Webhooks/DetectorWebhookClient.cs`
- `src/backend/PrIMA.Api/BackgroundServices/MonitoringSupervisorWorker.cs`
- `src/backend/PrIMA.Api/Migrations/Scripts/001_InitialSchema.sql`
- `src/frontend/src/features/detectors/views/DetectorListView.vue`
- `src/frontend/src/features/detectors/views/DetectorFormView.vue`
- `src/frontend/src/features/detectors/schemas/detectorForm.schema.ts`
