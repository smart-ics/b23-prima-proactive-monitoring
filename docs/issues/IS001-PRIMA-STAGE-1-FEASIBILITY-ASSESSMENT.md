---
Title: PrIMA Stage 1 — Platform Foundation & Core Telemetry Ingestion
Code: PRIMA-STAGE-1
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-11-20
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Assess the feasibility of `IS001-PRIMA-STAGE-1-ISSUE.md` (`ID: PRIMA-STAGE-1`, `Type: CHANGE-REQUEST`): implementing Stage 1 ("Platform Foundation & Core Telemetry Ingestion") of `docs/PRIMA-DEVELOPMENT-ROADMAP.md` to deliver the **Walking Skeleton & Live Ingestion Pipeline** milestone.

In scope for Stage 1:
1. **Authenticated Officer Access (`S-00`):** Individual officer sign-in issuing JWT Bearer tokens for deterministic action attribution, inline invalid-credential feedback, and post-login redirect restoration.
2. **Standardized Telemetry Ingestion (`POST /api/ingestion/telemetry`):** Authenticated external detectors reporting `Passed` and `FindingDiscovered` inspection outcomes.
3. **Deterministic Anomaly Deduplication (`BR-PRIMA-001`) & Immutable Occurrence Audit Trail (`BR-PRIMA-009`):** Creating `Open` issues with an immutable initial occurrence snapshot, and appending immutable occurrences to matching active issues without creating duplicate issues.
4. **Automatic Recurrence Reopening (`BR-PRIMA-002`):** Transitioning matching `Resolved` or `Dismissed` issues to `Reopened`, appending the new occurrence, and invalidating prior resolution/verification/dismissal state.
5. **Live Active Issue Dashboard (`S-01`, Use Cases `A-1` & `A-2`):** Authenticated officers viewing global summary counters (`Critical`, `High`, `Open`, `Reopened`, `Resolved Today`), browsing active issues ordered by urgency and recency (strictly excluding `Resolved` and `Dismissed`), filtering by status/scope/severity and debounced text search, viewing contextual empty states, and receiving 10-second polling updates without visual disruption or filter reset.
6. **Baseline Seed Data & Telemetry Simulation:** Seeding initial officer accounts and development test detectors/closed-issue fixtures, plus providing a telemetry simulator CLI utility covering clean runs, new findings, duplicate findings, and recurrence-after-close scenarios.

Explicitly out of scope for Stage 1:
- **Stage 2:** `S-02` Issue Detail Workbench, modals `M-01`–`M-03`, `dbo.IssueActivityLogs`, manual triage/remediation/verification/resolve/dismiss endpoints (`BR-PRIMA-004`, `BR-PRIMA-006`, `BR-PRIMA-008`).
- **Stage 3:** Detector portfolio screens (`S-03`–`S-06`), `MonitoringSupervisorWorker` autonomous silence/escalation supervision (`BR-PRIMA-003`, `BR-PRIMA-010`), `ExecutionFailed` anomaly synthesis (`BR-PRIMA-005`), `S-01` Alert Strip (`G-1`, `G-2`), and detector webhooks (`D-2`).
- **Stage 4:** `S-07` History & Reports (`H-1`, `H-2`) and v1.0 full-app quality gate.
- **Stage 5:** Pilot SIMRS Detector Suite (`BR-PRIMA-007`) and hospital production hardening/rollout.

Referenced artifacts:
- `ISSUE`: `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md`
- `DOMAIN`: `docs/PRIMA-DOMAIN.md`
- `FEATURE` (pre-existing outcome specifications): `docs/PRIMA-DEVELOPMENT-ROADMAP.md`, `docs/PRIMA-USE-CASE-DISCOVERY.md`, `docs/PRIMA-SCREEN-INVENTORY.md`, `docs/PRIMA-UI-UX-GUIDELINE.md`, `docs/PrIMA - Product Definition.md`
- Pre-existing Technical Architecture Specification: `docs/PRIMA-ARCHITECTURE.md`

## Objective

Assess Stage 1 feasibility against existing specifications and the current workspace/runtime environment, surface every business, domain, data-contract, schema, UX, and architectural contradiction or ambiguity, and record closed stakeholder decisions so downstream `ica-architect` Architecture Update, Planning, and agentic implementation do not invent decisions.

---

# 2. Current State

## Existing Behavior

- The repository is at initial commit `a27e89a` and contains only specification documents under `docs/` and the intake issue `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md`.
- The `src/` directory is completely empty: there is no `.NET` solution or project, no DbUp SQL migration scripts, no database seed script, no Vue 3 application, no generated OpenAPI TypeScript contract (`api.generated.ts`), no telemetry simulator, and no automated test project.
- Companion specifications (`docs/PrIMA - Product Definition.md`, `docs/PRIMA-DOMAIN.md`, `docs/PRIMA-USE-CASE-DISCOVERY.md`, `docs/PRIMA-SCREEN-INVENTORY.md`, `docs/PRIMA-UI-UX-GUIDELINE.md`, `docs/PRIMA-ARCHITECTURE.md`, and `docs/PRIMA-DEVELOPMENT-ROADMAP.md`) define the full 5-stage system and contain several cross-document contradictions and unspecified Stage 1 mechanics documented in Sections 3 and 4.

## Existing Constraints

- **Verified local development environment:**
  - OS: Windows.
  - .NET SDKs installed: `8.0.428` (plus `5.0.416`, `6.0.428`, `8.0.204`, `9.0.313`, `9.0.321`, `10.0.100-rc.1.25451.107`, `10.0.401`).
  - Node.js / npm installed: `v22.22.3` / `10.9.8`.
  - Database services: Microsoft SQL Server 2022 Developer Edition running locally on `localhost` (`MSSQLSERVER` running; `MSSQL$SQL2012` running; `MSSQLLocalDB` installed).
  - Containerization: `docker` is **not** installed in the local environment.
- **Prescribed technology and architectural constraints (`docs/PRIMA-ARCHITECTURE.md`, `docs/PRIMA-DEVELOPMENT-ROADMAP.md`, `docs/PRIMA-UI-UX-GUIDELINE.md`):**
  - Backend: Single ASP.NET Core `.NET 8+` Minimal API project (`src/backend/PrIMA.Api/`), vertical slices + DDD aggregates (`Detector`, `Issue`) + CQRS read projections, Dapper, SQL Server (`dbo` schema), DbUp startup migrations, FluentValidation, JWT Bearer authentication, RFC 7807 `ProblemDetails`, and OpenAPI (`/swagger/v1/swagger.json`).
  - Frontend: Vue 3.5+ (`<script setup lang="ts">`), Vite 6, Tailwind CSS v4, PrimeVue v4 (Aura preset), Pinia, Vue Router, TanStack Query v5 (`10s` dashboard polling), Axios, VeeValidate + Zod, and `openapi-typescript` generating read-only `src/shared/types/api.generated.ts`.
  - UX & Frontend discipline: Feature-slice isolation (`src/features/{auth,dashboard,issues,detectors,history}/`) with zero cross-feature imports; desktop-first light mode; virtual scrolling with zero pagination on `S-01`; `Asia/Jakarta` (`WIB`) timestamp presentation; `id-ID` IDR currency formatting; non-disruptive polling (`Skeleton` only on initial load, never on background `isFetching`).
- **Operational environment:** Single-hospital on-premise deployment model operating in `Asia/Jakarta` (`WIB`, UTC+7).

---

# 3. Gap Analysis

| ID | Severity | Status | Gap |
|---|---|---|---|
| GAP-001 | CRITICAL | CLOSED | Greenfield implementation gap: `src/` contains no backend API, DbUp SQL migrations, Vue 3 frontend, telemetry simulator, or automated tests; exact `.NET` TFM (`net8.0`), database auto-creation strategy, local dev proxy/CORS setup, simulator language/path, and Stage 1 automated test baseline are unspecified. |
| GAP-002 | CRITICAL | CLOSED | Ingestion payload cardinality conflict: `PRIMA-ARCHITECTURE.md §4.3` specifies a single `finding` object per `POST /api/ingestion/telemetry`, contradicting `PRIMA-DOMAIN.md §9` (`InspectionRunCompleted` `Finding Count`) and `PRIMA-SCREEN-INVENTORY.md` (`S-06` `Finding count + CorrelationKey`) and causing multi-finding detector sweeps to inflate `InspectionRuns` counts and skew `PassRate%`. |
| GAP-003 | CRITICAL | CLOSED | `001_InitialSchema.sql` structural defects and cross-spec enum mismatches: `dbo.Occurrences` omits `InspectionRunId` FK (needed by `PRIMA-DOMAIN.md §5.7`, `S-02`, and `S-06`) and marks `SourceTable`/`RecordKeys` `NOT NULL` (breaking non-DB `Infrastructure` probes); `IX_Issues_Status_Severity` indexes `CurrentSeverity NVARCHAR(20)`, sorting alphabetically (`Medium > Low > High > Critical`); `dbo.Detectors.Name` lacks a `UNIQUE` constraint (`S-05`); governance tables lack cycle-superseded tracking for `BR-PRIMA-002`; `dbo.Issues` omits `OccurrenceCount` and `DismissedAtUtc`; and `ActionCategory` / `DismissalReason` string literals conflict across Domain, Roadmap, and Architecture DDL comments. |
| GAP-004 | CRITICAL | CLOSED | Missing detector guardrails and Stage 1 `ExecutionFailed` boundary in `IngestTelemetryHandler`: `PRIMA-ARCHITECTURE.md §4.3` omits unknown `detectorId` existence validation and `PRIMA-DOMAIN.md §6.1` rejection of `Paused`/`Retired` detectors, and does not specify Stage 1 behavior for `ExecutionFailed` (whose `BR-PRIMA-005` `Infrastructure` issue synthesis is deferred to Stage 3). |
| GAP-005 | CRITICAL | CLOSED | `BR-PRIMA-002` auto-reopen persistence and state-reset ambiguity: `PRIMA-ARCHITECTURE.md §4.2.1` nulls `Dismissal` and `LatestVerification` in memory without defining SQL audit preservation (forbidding `DELETE`), `LatestRemediation` invalidation, or reset semantics for `CurrentAssignee`, `AcknowledgedAtUtc`, `ResolvedAtUtc`, `CurrentSeverity`, and `EscalationLevel`. |
| GAP-006 | MAJOR | CLOSED | `BR-PRIMA-003` Stage 1 vs. Stage 3 contradiction: `PRIMA-ARCHITECTURE.md §4.2.1` calls `CheckRecurrenceEscalation()` inside `Issue.RecordRecurringOccurrence()` (`_occurrences.Count >= 5`), contradicting `IS001` and `PRIMA-DEVELOPMENT-ROADMAP.md` (which defer all `BR-PRIMA-003` escalation and `↑ Escalated` UI cues to Stage 3) and causing repeated severity elevation on every occurrence `>= 5`. |
| GAP-007 | MAJOR | CLOSED | `CorrelationKey` canonicalization and recurring-finding metadata ambiguity: `PRIMA-ARCHITECTURE.md §4.3` uses a GUID prefix (`"{DetectorId}:{TargetEntityReference}"`) whereas `PRIMA-UI-UX-GUIDELINE.md §6.3` illustrates `DET-01:...`; `PRIMA-DOMAIN.md §5.4` requires case-insensitive determinism without defining trimming/casing normalization; `BR-PRIMA-001` parenthetical omits `Reopened`; and handling of changed `Title`/`BaseSeverity` on recurrence or duplicate `targetEntityReference` inside one batch is unspecified. |
| GAP-008 | MAJOR | CLOSED | SDLC Permanent Knowledge alignment: no standalone `PRIMA-STAGE-1-FEATURE.md` artifact exists (Stage 1 user outcomes live across Roadmap, Use Case Discovery, and Screen Inventory), and `docs/PRIMA-ARCHITECTURE.md` predates the Stage 1 gap-closure decisions. |

---

# 4. Open Questions

| ID | Status | Question | Impact |
|---|---|---|---|
| OQ-001 | CLOSED | Should `X-Detector-API-Key` authentication use shared configurable key(s) in `appsettings.json`/environment config or per-detector hashed keys stored on `dbo.Detectors`? | Affects `001_InitialSchema.sql`, seed data, ingestion authentication middleware, and Stage 3 `S-05` detector registration. |
| OQ-002 | CLOSED | Should officer roles (`EdpOfficer`, `SystemAnalyst`, `Lead`) enforce role-based API/UI authorization (`PRIMA-DOMAIN.md §4`) or use flat v1 operational access (`PRIMA-USE-CASE-DISCOVERY.md`)? | Determines backend authorization attributes/policies, JWT claims, and UI gating across Stages 1–4. |
| OQ-003 | CLOSED | Which password hashing algorithm, JWT lifetime/refresh strategy, and browser token-storage policy should Stage 1 implement? | Determines `002_SeedInitialUsers.sql` hash format, `LoginEndpoint` verification, shift-duration session stability, and F5 reload behavior. |
| OQ-004 | CLOSED | How should baseline officer seeding (`002_SeedInitialUsers.sql`) be separated from development test detectors and closed-issue fixtures needed to verify `BR-PRIMA-002` in Stage 1? | Prevents synthetic test data from entering production hospital databases while enabling Stage 1 exit-criteria verification. |
| OQ-005 | CLOSED | On `S-01` and `GET /api/issues/dashboard-feed`, what timezone boundary defines `Resolved Today`, do Summary Bar counts remain global when filters apply, which filter controls belong in Stage 1 (`A-2` vs. `S-01`), and is the top Alert Strip deferred to Stage 3? | Defines exact SQL projection, query contract, `WIB` midnight calculation, and `S-01` layout/filter behavior. |
| OQ-006 | CLOSED | How should `AppLayout.vue` sidebar links (`Detectors`, `History & Reports`), the `Resolved Today` summary tile, and `S-01` issue row clicks behave in Stage 1 before `S-02`, `S-03`, and `S-07` are implemented? | Prevents broken 404 routes, console errors, throwaway placeholder views, or premature Stage 2–4 scope creep. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | PrIMA targets a single-hospital on-premise Microsoft SQL Server database (`dbo` schema); local development runs against SQL Server 2022 Developer Edition on `localhost` using configurable connection strings (Windows Integrated Auth default in dev), with no Docker requirement. |
| ASM-002 | Hospital operational timezone defaults to `Asia/Jakarta` (`WIB`, UTC+7), configured via `OperationalTimeZone` in `appsettings.json` and resolved via .NET 8 `TimeZoneInfo.FindSystemTimeZoneById` (supporting both IANA `"Asia/Jakarta"` and Windows `"SE Asia Standard Time"`). |
| ASM-003 | `MonitoringSupervisorWorker` (`BackgroundServices/`) is **not** registered or started in Stage 1; autonomous silence detection (`BR-PRIMA-010`) and SLA/recurrence escalation (`BR-PRIMA-003`) begin in Stage 3 per `docs/PRIMA-DEVELOPMENT-ROADMAP.md`. |
| ASM-004 | Post-login `?redirect=` handling on `S-00` validates that the redirect target is a relative in-app path (starts with `/`, does not start with `//`, and carries no scheme) to prevent open redirects, and visiting `/login` while already authenticated redirects to `/`. |
| ASM-005 | Concurrent `POST /api/ingestion/telemetry` requests sharing a `CorrelationKey` are protected inside the ingestion database transaction (e.g., `UPDLOCK, HOLDLOCK` on lookup or bounded retry on SQL unique-index error `2601`/`2627`) so `UX_Issues_CorrelationKey` never raises HTTP `500`. |
| ASM-006 | Frontend styling uses Tailwind CSS v4 CSS-first configuration (`@tailwindcss/vite` + `@import "tailwindcss";`) rather than a legacy Tailwind v3 `tailwind.config.ts` file (correcting the filename shown in `docs/PRIMA-ARCHITECTURE.md §5.2`). |
| ASM-007 | On `S-01`, Summary Bar `criticalCount` and `highCount` count active issues (`Status NOT IN ('Resolved', 'Dismissed')`) with `CurrentSeverity = 'Critical'` / `'High'`; `openCount` counts `Status = 'Open'`; `reopenedCount` counts `Status = 'Reopened'`; sidebar `Dashboard` badge displays `openCount + reopenedCount` (rose when `criticalCount > 0 \|\| reopenedCount > 0`, else slate); `GET /api/issues/dashboard-feed` rejects closed status filter values (`Resolved`, `Dismissed`) with `422 Unprocessable Entity`. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Concurrent ingestion race on `UX_Issues_CorrelationKey` when overlapping detector sweeps report the same entity simultaneously. | HTTP `500` unique constraint violation and dropped telemetry. | Acquire update/range lock (`UPDLOCK, HOLDLOCK`) or execute bounded retry on SQL `2601`/`2627` within the ingestion transaction, backed by integration tests. |
| RISK-002 | Alphabetical sorting on `CurrentSeverity NVARCHAR(20)` (`Medium > Low > High > Critical`) orders dashboard issues incorrectly. | `Critical` and `High` anomalies buried below `Medium`/`Low` items on `S-01`. | Persist `SeverityRank TINYINT` (`Critical=4`, `High=3`, `Medium=2`, `Low=1`) on `dbo.Issues` with `IX_Issues_Status_SeverityRank` and verify feed order in tests. |
| RISK-003 | Accidental enabling of demo data seeding (`SeedDemoData=true`) in a hospital production deployment. | Fake detectors and synthetic closed issues pollute production operational queues and KPIs. | Default `SeedDemoData: false` in `appsettings.json`, set `true` only in `appsettings.Development.json`, and guard demo seeding on `IHostEnvironment.IsDevelopment() && SeedDemoData`. |
| RISK-004 | Contract drift between Minimal API DTOs and `src/frontend/src/shared/types/api.generated.ts`. | Runtime serialization mismatches and broken TypeScript builds. | Generate `api.generated.ts` strictly from `/swagger/v1/swagger.json` via `openapi-typescript` npm script with zero manual edits. |
| RISK-005 | UTC vs. `WIB` midnight boundary error on `Resolved Today` between `00:00` and `06:59 WIB`. | Yesterday's or today's resolved issues miscounted during morning hospital handover. | Convert current UTC instant to `OperationalTimeZone`, compute `[startOfTodayWibUtc, startOfTomorrowWibUtc)` in UTC, and cover boundary timestamps in unit/API tests. |
| RISK-006 | Password hash mismatch between `002_SeedInitialUsers.sql` and ASP.NET Core `PasswordHasher`. | Seeded officer accounts fail `S-00` login with `401`. | Generate `002_SeedInitialUsers.sql` password hashes directly via `Microsoft.AspNetCore.Identity.PasswordHasher<object>` and test seeded credentials in `PrIMA.Api.Tests`. |
| RISK-007 | Tailwind CSS v4 cascade conflicts with PrimeVue v4 Aura styles. | Broken button/dialog padding, borders, or focus rings. | Configure PrimeVue v4 `cssLayer` ordering explicitly with Tailwind v4 `@layer` directives in the frontend stylesheet entry. |

---

# 7. Recommendations

The major cross-cutting trade-offs evaluated during feasibility analysis are summarized below as solution alternatives (approved selections are recorded exclusively in Section 8):

## Ingestion Payload Cardinality (`GAP-002`)

### Option A: `findings[]` batch array per `InspectionRun`
Accept `findings[]` (`0..N` items) on `POST /api/ingestion/telemetry`, recording one `InspectionRun` and atomically ingesting all findings in one transaction.
- **Advantages:** Aligns `InspectionRuns` counts, `PassRate%`, and `S-06` `Finding count` with `PRIMA-DOMAIN.md §9` and `S-06`; matches real detector sweeps; avoids inflating run telemetry.
- **Disadvantages:** Requires array validation and atomic per-batch response mapping (`results[]`).

### Option B: Single `finding` object per `POST`
Retain the single `finding` schema from `PRIMA-ARCHITECTURE.md §4.3`.
- **Advantages:** Marginally simpler request DTO.
- **Disadvantages:** Corrupts `TotalRuns30d` / `PassRate%` when one sweep discovers multiple anomalies, and leaves `Finding count` permanently `0` or `1`.

## Detector API Key Governance (`OQ-001`)

### Option A: Shared rotatable keys in configuration (`appsettings.json` / env)
Authenticate `X-Detector-API-Key` against a configured key array using constant-time comparison.
- **Advantages:** Zero changes to `dbo.Detectors` or Stage 3 `S-05` UX; supports zero-downtime key rotation; simple local/hospital setup.
- **Disadvantages:** Does not cryptographically distinguish which detector sent a request if a shared key leaks.

### Option B: Per-detector hashed keys stored in `dbo.Detectors`
Add `ApiKeyHash` to `dbo.Detectors` from Stage 1 and bind `X-Detector-API-Key` to `detectorId`.
- **Advantages:** Stronger per-detector attribution and revocation.
- **Disadvantages:** Modifies `dbo.Detectors` schema, requires plaintext key return/rotation UX in Stage 3 `S-05`, and complicates Stage 1 seed setup.

## Schema Defect Remediation Timing (`GAP-003`)

### Option A: Fix `001_InitialSchema.sql` upfront in Stage 1
Incorporate `InspectionRunId`, nullable `SourceTable`/`RecordKeys`, `SeverityRank`, `OccurrenceCount`, `DismissedAtUtc`, `IsSuperseded`, `UNIQUE` `Detectors.Name`, and canonical `PRIMA-DOMAIN.md` enum strings into `001_InitialSchema.sql` immediately.
- **Advantages:** Avoids broken alphabetical severity sorting, preserves `InspectionRunId` traceability on Stage 1 occurrences (`BR-PRIMA-009`), supports non-DB probes, and prevents rework in Stages 2–4.
- **Disadvantages:** Requires updating DDL and enum snippets in `docs/PRIMA-ARCHITECTURE.md`.

### Option B: Defer schema fixes to Stages 2–3 migrations
Keep `001_InitialSchema.sql` verbatim as written in `docs/PRIMA-ARCHITECTURE.md §6` and patch via later DbUp scripts.
- **Advantages:** Zero upfront change to Section 6 DDL text.
- **Disadvantages:** Stage 1 `S-01` feed sorts `Medium > Low > High > Critical`, Stage 1 `Occurrences` lose `InspectionRunId` links, and Stage 1 non-DB simulator payloads fail `NOT NULL` checks.

## Seed Data Hygiene (`OQ-004`)

### Option A: Split production DbUp user seed from Development demo seeder
Keep `002_SeedInitialUsers.sql` strictly limited to baseline officer accounts, and place test detectors plus `Resolved`/`Dismissed` sample issues in a gated `Development`-only seeder (`SeedDemoData=true` in `appsettings.Development.json`).
- **Advantages:** Clean hospital production databases while giving devs and Stage 1 verification immediate fixtures for `BR-PRIMA-001` and `BR-PRIMA-002`.
- **Disadvantages:** Requires an explicit config gate (`IsDevelopment() && SeedDemoData`).

### Option B: Unified unconditional DbUp seed script
Place officer accounts, sample detectors, and fake closed issues directly inside `002_SeedInitialUsers.sql`.
- **Advantages:** Single migration pipeline for all environments.
- **Disadvantages:** Pollutes production hospital databases with synthetic detectors and closed issues.

---

# 8. Gap Closure

## GAP-001

Status: CLOSED

### Decision
Target `.NET 8` (`net8.0`) with `Swashbuckle.AspNetCore` exposing `/swagger/v1/swagger.json` inside `src/backend/PrIMA.Api/`. Bootstrap SQL Server automatically via DbUp `EnsureDatabase.For.SqlDatabase(connectionString)` using a configurable connection string (local development default pointing to `localhost` SQL Server 2022 with Windows Integrated Auth). Configure Vite `/api` dev proxy and backend development CORS policy. Implement the telemetry simulator as a zero-dependency Node.js CLI script at `src/tools/telemetry-simulator/simulate.mjs` covering `Passed`, new `FindingDiscovered`, duplicate `FindingDiscovered`, and recurrence-after-close (`Resolved` and `Dismissed`) scenarios. Include an xUnit test project `src/backend/PrIMA.Api.Tests/` in Stage 1 covering domain invariants (`BR-PRIMA-001`, `BR-PRIMA-002`, `BR-PRIMA-009`, `CorrelationKey`) and API contracts (`Auth`, `Ingestion`, `DashboardFeed`).

### Rationale
`.NET 8` SDK (`8.0.428`), Node `v22.22.3`, and SQL Server 2022 Developer Edition (`localhost`) are all verified available locally without Docker. Zero-dependency Node CLI leverages Node 22 directly and avoids extra build steps, while an early xUnit suite prevents domain regressions across Stages 2–4.

### Impact
Establishes exact Stage 1 workspace layout (`src/backend/PrIMA.Api/`, `src/backend/PrIMA.Api.Tests/`, `src/frontend/`, `src/tools/telemetry-simulator/simulate.mjs`), local database bootstrap behavior, and Stage 1 automated verification criteria.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md` §§ 2, 4.1, 5.2, and 7 to specify `net8.0` + Swashbuckle, DbUp `EnsureDatabase`, Vite `/api` proxy / CORS, `src/tools/telemetry-simulator/simulate.mjs`, and `src/backend/PrIMA.Api.Tests/` (plus correcting `tailwind.config.ts` to Tailwind v4 CSS-first `@tailwindcss/vite` setup).

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## GAP-002

Status: CLOSED

### Decision
Update `POST /api/ingestion/telemetry` to accept a `findings[]` array per `InspectionRun`. One POST records exactly 1 `InspectionRun` and atomically ingests `0..N` findings inside a single database transaction: `findings` must be empty or omitted when `outcome == "Passed"`, and must contain `>= 1` items when `outcome == "FindingDiscovered"`. Return `200 OK` with `inspectionRunId`, `detectorId`, `outcome`, `processedFindingsCount`, and per-finding `results[]` (`correlationKey`, `issueId`, `action`: `Created` | `Appended` | `Reopened`, `occurrenceId`, `occurrenceNumber`).

### Rationale
Matches `PRIMA-DOMAIN.md §9` (`InspectionRunCompleted` `Finding Count`) and `S-06` (`Finding count + CorrelationKey`), prevents multi-finding detector sweeps from inflating `InspectionRuns` counts and skewing `PassRate%`, and guarantees atomic run-plus-finding consistency.

### Impact
Eliminates single-finding payload restriction; updates ingestion request/response DTOs, FluentValidation rules, transactional repository/handler logic, OpenAPI schema, and telemetry simulator payloads.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §4.3` (Request Payload JSON, Ingestion Processing Pipeline, and Response Schema) to replace the single `finding` object with `findings[]` and document atomic transactional processing and batch response structure.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## GAP-003

Status: CLOSED

### Decision
Apply all structural schema corrections upfront in `001_InitialSchema.sql` and enforce a single canonical enum vocabulary across SQL, C#, OpenAPI, and TypeScript:
- `dbo.Detectors`: add `UNIQUE` constraint on `Name` (`UQ_Detectors_Name`).
- `dbo.Occurrences`: add `InspectionRunId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.InspectionRuns(Id)` with index `IX_Occurrences_InspectionRunId`; change `SourceTable` to `NVARCHAR(100) NULL` and `RecordKeys` to `NVARCHAR(500) NULL`.
- `dbo.Issues`: add `SeverityRank TINYINT NOT NULL` (`Critical=4`, `High=3`, `Medium=2`, `Low=1`), replacing alphabetical severity indexing with `IX_Issues_Status_SeverityRank (Status, SeverityRank DESC, LastOccurrenceAtUtc DESC)`; add `OccurrenceCount INT NOT NULL DEFAULT 1`; add `DismissedAtUtc DATETIME2 NULL`.
- `dbo.RemediationRecords`, `dbo.VerificationRecords`, `dbo.DismissalRecords`: add `IsSuperseded BIT NOT NULL DEFAULT 0`.
- Canonical enum strings (aligned to `PRIMA-DOMAIN.md`):
  - `MonitoringScope`: `DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`
  - `ActionCategory`: `DataCorrection`, `ServiceRestart`, `ConfigurationAdjustment`, `CodeRemediationRequested`
  - `DismissalReason`: `FalsePositive`, `AcceptedOperationalException`, `DuplicateManualReport`
  - `TriggerSource`: `Scheduled`, `Manual`, `EventHook`
  - `InspectionOutcome`: `Passed`, `FindingDiscovered`, `ExecutionFailed`
  - `IssueStatus`: `Open`, `Acknowledged`, `Investigating`, `Resolved`, `Dismissed`, `Reopened`
  - `IssueSeverity`: `Low`, `Medium`, `High`, `Critical`
  - `AdminStatus`: `Active`, `Paused`, `Retired`
  - `HealthStatus`: `Healthy`, `Silent`, `Failing`
  - `VerificationMethod`: `AutomatedReRun`, `ManualQueryConfirmed`
  - `OfficerRole`: `EdpOfficer`, `SystemAnalyst`, `Lead`

### Rationale
Correcting the baseline schema upfront prevents broken alphabetical severity sorting on `S-01`, preserves `InspectionRunId` audit linkage required by `BR-PRIMA-009` and Stage 3 `S-06`, enables non-database probes (`Infrastructure` / `DataSync`) without dummy `SourceTable`/`RecordKeys`, supports non-destructive reopen history via `IsSuperseded`, and eliminates enum string mismatches across layers.

### Impact
Defines exact DDL for `001_InitialSchema.sql`, Dapper column mappings, `Occurrence` constructor/entity properties (`InspectionRunId`), `Issue` projection fields (`SeverityRank`, `OccurrenceCount`, `DismissedAtUtc`), and OpenAPI string enum schemas.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §4.2.1` (`Issue` and `Occurrence` properties), `§5` (domain/UI enum alignment), and `§6` (`001_InitialSchema.sql` DDL and indexes) to incorporate all schema fixes and canonical enum names.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## GAP-004

Status: CLOSED

### Decision
Enforce explicit detector validation and Stage 1 outcome boundaries on `POST /api/ingestion/telemetry`:
1. If `detectorId` does not exist in `dbo.Detectors`, return `404 Not Found` (RFC 7807 `ProblemDetails`) and record nothing.
2. If the detector's `AdminStatus` is `Paused` or `Retired`, return `409 Conflict` (RFC 7807 `ProblemDetails`) and record no `InspectionRun` (`PRIMA-DOMAIN.md §6.1`).
3. Reject `outcome == "ExecutionFailed"` in Stage 1 with `422 Unprocessable Entity` (RFC 7807 `ProblemDetails`) until Stage 3 activates `BR-PRIMA-005` (`Infrastructure` issue synthesis) end-to-end.
4. For accepted `Passed` and `FindingDiscovered` runs on an `Active` detector, update `LastRunAtUtc`, `LastHealthyHeartbeatUtc`, and `HealthStatus = Healthy`.

### Rationale
Enforces `PRIMA-DOMAIN.md §6.1` invariant immediately, prevents foreign-key `500` errors on unknown detectors, and avoids partial `ExecutionFailed` ingestion leaving a detector `Failing` without an `Infrastructure` issue before Stage 3.

### Impact
Adds `IDetectorRepository` lookup and `Detector.CanAcceptTelemetry()` / `RecordHealthyRun()` domain behavior, explicit `404`/`409`/`422` ProblemDetails paths in `IngestTelemetryValidator`/`IngestTelemetryHandler`, and `Paused` detector rejection test coverage.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §4.3` (Ingestion Processing Pipeline) and `§4.2` (`Detector` aggregate methods) to require detector existence (`404`), `Active` status check (`409`), and Stage 1 `ExecutionFailed` rejection (`422`, deferred to Stage 3).

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## GAP-005

Status: CLOSED

### Decision
When `BR-PRIMA-002` automatically reopens a `Resolved` or `Dismissed` issue upon recurrence:
1. Preserve all historical rows in `dbo.VerificationRecords`, `dbo.RemediationRecords`, and `dbo.DismissalRecords` (never execute `DELETE`); mark prior-cycle records `IsSuperseded = 1` so the reopened investigation cycle requires new remediation and verification (`BR-PRIMA-004`).
2. Transition `Status` to `Reopened` and set `ReopenedAtUtc = occurrenceTimeUtc`.
3. Clear `CurrentAssignee = null`, `AcknowledgedAtUtc = null`, `ResolvedAtUtc = null`, and `DismissedAtUtc = null`.
4. Preserve `CurrentSeverity` and `EscalationLevel` (do not downgrade a recurring defect).

### Rationale
Preserves full historical auditability while ensuring reopened issues enter the active triage queue cleanly (`Take It` / `Assign to…` on `S-02`), cease counting toward `Resolved Today`, require fresh verification before re-resolution, and retain prior urgency.

### Impact
Updates `Issue.RecordRecurringOccurrence` domain method and `IIssueRepository` SQL persistence to clear `CurrentAssignee`/`AcknowledgedAtUtc`/`ResolvedAtUtc`/`DismissedAtUtc` and mark prior governance rows `IsSuperseded = 1`.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §4.2.1` (`Issue.RecordRecurringOccurrence`) and `Infrastructure/Persistence/IssueRepository.cs` expectations to specify `IsSuperseded = 1` SQL updates, clearing of assignee/acknowledgment/closure timestamps, and preservation of `CurrentSeverity` and `EscalationLevel`.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## GAP-006

Status: CLOSED

### Decision
Strictly defer all `BR-PRIMA-003` dynamic severity escalation (both unacknowledged SLA timeout and `>= 5` recurrence threshold) to Stage 3. In Stage 1, `Issue.RecordRecurringOccurrence` only appends the immutable `Occurrence`, updates `LastOccurrenceAtUtc` and `OccurrenceCount`, and performs `BR-PRIMA-002` reopening when `Status` is `Resolved` or `Dismissed`; `EscalationLevel` remains `0` throughout Stage 1.

### Rationale
Aligns code behavior cleanly with `IS001-PRIMA-STAGE-1-ISSUE.md` and `docs/PRIMA-DEVELOPMENT-ROADMAP.md` (which assign `BR-PRIMA-003`, `dbo.IssueActivityLogs`, and `↑ Escalated` dashboard cues to Stage 3) and prevents undocumented Stage 1 severity escalation without audit logs or UI indicators.

### Impact
Removes `CheckRecurrenceEscalation()` from Stage 1 `Issue.RecordRecurringOccurrence` and postpones all `EscalationLevel` increments to Stage 3.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §4.2.1` (`Issue.RecordRecurringOccurrence`) to explicitly mark `CheckRecurrenceEscalation()` as a Stage 3 capability alongside `MonitoringSupervisorWorker` (`§4.4`).

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## GAP-007

Status: CLOSED

### Decision
1. Canonicalize `CorrelationKey` using `CorrelationKey.Create(detectorId, targetEntityReference)` formatted as `$"{detectorId:D}:{targetEntityReference.Trim().ToUpperInvariant()}"` (lowercase hyphenated GUID + trimmed uppercase entity reference; treat `[DET-01:...]` in `PRIMA-UI-UX-GUIDELINE.md` as illustrative; enforce max `targetEntityReference` length `213` characters so total length stays within `NVARCHAR(250)`).
2. Treat `Reopened` alongside `Open`, `Acknowledged`, and `Investigating` as active statuses for `BR-PRIMA-001` deduplication.
3. Keep the existing `Issue.Title` and `Issue.BaseSeverity` unchanged when a recurring finding matches an existing issue.
4. Reject duplicate `targetEntityReference` values (case-insensitive after trimming) within the same `findings[]` batch with `422 Unprocessable Entity`.

### Rationale
Guarantees case-insensitive deterministic deduplication (`PRIMA-DOMAIN.md §5.4`) without introducing extra `Code` fields onto `Detector`, clarifies `BR-PRIMA-001` behavior for `Reopened` issues, keeps issue identity stable across recurrences, and enforces clean batch input validation.

### Impact
Defines `CorrelationKey` value-object invariants, `IngestTelemetryValidator` string length and within-batch uniqueness checks, and `RecordRecurringOccurrence` update boundaries.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §4.2` (`CorrelationKey`) and `§4.3` (`CorrelationKey` normalization formula, active-status dedup set including `Reopened`, stable title/severity on recurrence, and batch uniqueness rule).

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## GAP-008

Status: CLOSED

### Decision
Treat `docs/PRIMA-DEVELOPMENT-ROADMAP.md`, `docs/PRIMA-USE-CASE-DISCOVERY.md`, and `docs/PRIMA-SCREEN-INVENTORY.md` as the authoritative Stage 1 business outcome specifications referenced by this `FEASIBILITY-ASSESSMENT` (no separate `PRIMA-STAGE-1-FEATURE.md` file required). Classify Stage 1 `Architecture Applicability` as `ARCHITECTURE-REQUIRED` so `ica-architect` updates `docs/PRIMA-ARCHITECTURE.md` in place with all approved Stage 1 decisions before creating `IMPLEMENTATION-PLAN`. Retain `Status: READY-FOR-PLANNING` on this artifact until `ica-architect` verifies and grants `READY-FOR-PLANNING`.

### Rationale
Avoids duplicating already-thorough outcome specifications across `docs/`, enforces Knowledge-Centric SDLC single-owner update of `docs/PRIMA-ARCHITECTURE.md`, and respects strict Analyst vs. Architect gate authority.

### Impact
Unblocks `ica-architect` to immediately review/grant `READY-FOR-PLANNING`, update `docs/PRIMA-ARCHITECTURE.md` in place, and author the Stage 1 `IMPLEMENTATION-PLAN`.

### Architecture Impact
`ica-architect` updates `docs/PRIMA-ARCHITECTURE.md` in place to reconcile all closed Stage 1 decisions (`GAP-001`–`GAP-008`, `OQ-001`–`OQ-006`) before Planning begins.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## OQ-001

Status: CLOSED

### Decision
Authenticate `POST /api/ingestion/telemetry` via shared ingestion API key(s) configured in `appsettings.json` / environment variables (`Ingestion:ApiKeys` string array supporting multiple valid keys for zero-downtime rotation), validated using constant-time byte comparison (`CryptographicOperations.FixedTimeEquals`) against the `X-Detector-API-Key` header. Return `401 Unauthorized` (RFC 7807) when missing or invalid. Keep `dbo.Detectors` and Stage 3 `S-05` free of API-key storage/management in v1 (defer per-detector keys to Stage 5 security hardening if needed).

### Rationale
Matches `docs/PRIMA-ARCHITECTURE.md §4.1` & `§6`, keeps Stage 1 and Stage 3 `S-05` simple, and supports safe key rotation via array config.

### Impact
Requires `Ingestion:ApiKeys` configuration section, constant-time API-key endpoint filter/middleware, and `401` test coverage without schema changes to `dbo.Detectors`.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §4.1` and `§4.3` to document `Ingestion:ApiKeys` array configuration, constant-time `X-Detector-API-Key` validation, and `401` RFC 7807 failure response.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## OQ-002

Status: CLOSED

### Decision
Implement flat v1 operational authorization: any active (`IsActive = 1`) authenticated officer (`EdpOfficer`, `SystemAnalyst`, or `Lead`) has full operational access to all authenticated officer API endpoints and UI screens. Include `Role` in the JWT claims (`role` + standard claims) and display it in the `AppLayout.vue` officer session footer for audit attribution only. Reject inactive accounts (`IsActive = 0`) at login with `401 Unauthorized` (`"Invalid username or password."`).

### Rationale
Aligns with `docs/PRIMA-USE-CASE-DISCOVERY.md` ("one active human actor... no separate role-based UI required") while preserving role visibility and attribution defined in `PRIMA-DOMAIN.md §4` and `PRIMA-UI-UX-GUIDELINE.md §4.1`.

### Impact
Simplifies Stage 1–4 authorization to standard authenticated-user policies (`RequireAuthorization()`), validates `IsActive` during login, and avoids unneeded role-based UI hiding.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §1`, `§4.1` (`Features/Auth`), and `§5` to specify flat v1 authorization for active officers, `IsActive = 1` check on login, and attribution-only `Role` JWT claim.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## OQ-003

Status: CLOSED

### Decision
1. **Password hashing:** Use ASP.NET Core `Microsoft.AspNetCore.Identity.PasswordHasher<object>` (PBKDF2, built-in, zero extra packages) for password verification and `002_SeedInitialUsers.sql` hashes.
2. **JWT lifecycle:** Issue HS256 JWT Bearer tokens signed by configured symmetric secret (`Jwt:SigningKey`, minimum 32 bytes) with configurable `Jwt:ExpirationHours` defaulting to `12` hours and no refresh-token flow in v1. Include Officer ID (`sub`), `Username`, `FullName`, and `Role` claims. Return `{ accessToken, expiresAtUtc, officer: { id, username, fullName, role } }` from `POST /api/auth/login`.
3. **Browser session storage:** Persist the JWT and officer profile in `localStorage` via Pinia `auth.store.ts` so sessions survive F5 reloads and multi-tab inspection; Axios response `401` clears `auth.store.ts` and redirects to `/login?redirect=<validated-relative-path>`.

### Rationale
Zero external crypto dependencies, covers full 8–12h hospital EDP operational shifts without spurious mid-shift logouts, preserves F5 reload state, and relies on clean `401` redirect restoration without refresh-token complexity.

### Impact
Defines exact `LoginEndpoint` request/response DTO contract, `Program.cs` JWT Bearer configuration, `auth.store.ts` persistence key/behavior, and `http.ts` interceptor redirect logic.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §2`, `§4.1` (`Features/Auth`), and `§5.2` (`auth.store.ts`, `http.ts`) to record `PasswordHasher`, 12h configurable JWT expiry, `LoginResponse` structure, and `localStorage` session persistence.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## OQ-004

Status: CLOSED

### Decision
Separate production seeding from development test/demo fixtures:
1. `002_SeedInitialUsers.sql` (DbUp) seeds **only** idempotent baseline officer accounts (`EdpOfficer`, `SystemAnalyst`, `Lead` — e.g., `ahmad.budi`, `siti.rahma`, `edp.lead`) safe for any environment.
2. Implement a separate `Development`-only idempotent seeder (run at startup only when `IHostEnvironment.IsDevelopment()` AND `SeedDemoData == true`; configured as `false` in `appsettings.json` and `true` in `appsettings.Development.json`) that seeds baseline detectors across all 4 monitoring scopes (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`), at least 1 `Paused` detector (for `409` verification), and sample `Resolved` (with `CleanPassed` `VerificationRecord`) and `Dismissed` (with `DismissalRecord`) issues for end-to-end simulator and `BR-PRIMA-002` testing.

### Rationale
Prevents synthetic test detectors and pre-closed demo issues from ever entering a real hospital database while ensuring Stage 1 developers and exit-criteria verification have immediate deterministic fixtures.

### Impact
Defines `002_SeedInitialUsers.sql` scope, `SeedDemoData` configuration flag, development-only demo data seeder module, and simulator target detector IDs.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §4.1` (`Migrations/` & Dev Seeder) and `§7` to document the split between `002_SeedInitialUsers.sql` (officers only) and the `Development`-gated demo detector/fixture seeder.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## OQ-005

Status: CLOSED

### Decision
1. **`Resolved Today` day boundary:** Compute `Resolved Today` (`Status == 'Resolved'`) using `ResolvedAtUtc >= startOfTodayWibUtc AND ResolvedAtUtc < startOfTomorrowWibUtc` derived from configurable `OperationalTimeZone` defaulting to `Asia/Jakarta` (`WIB`, UTC+7).
2. **Summary Bar counts:** All 5 Summary Bar counters (`criticalCount`, `highCount`, `openCount`, `reopenedCount`, `resolvedTodayCount`) remain globally unfiltered across all active issues / today's resolved issues regardless of active UI filters.
3. **`S-01` filter & search surface:** Support server-side filtering on `GET /api/issues/dashboard-feed` by `status` (`Open`, `Acknowledged`, `Investigating`, `Reopened`), `severity` (`Critical`, `High`, `Medium`, `Low`), `scope` (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`), and `search` (case-insensitive substring match against `Title`, `DetectorName`, and `CorrelationKey`), combined with `AND` semantics. Detector and date-range filters remain deferred to `S-07` (Stage 4). Active feed is strictly sorted by `SeverityRank DESC, LastOccurrenceAtUtc DESC`, never returning `Resolved` or `Dismissed` items.
4. **Top Alert Strip:** Strictly defer the `S-01` conditional Alert Strip (`G-1`/`G-2`) to Stage 3 per `docs/PRIMA-DEVELOPMENT-ROADMAP.md`; the `Reopened` Summary Tile surfaces reopened issues in Stage 1.

### Rationale
Prevents `00:00–06:59 WIB` operational midnight miscounts, keeps situational awareness counters accurate while filtering, matches `S-01` Screen Inventory and UI/UX Guideline density without premature `S-07` controls, and preserves clean Stage 1 vs. Stage 3 scope boundaries.

### Impact
Fixes the exact SQL query, parameters, and response DTO for `GetDashboardFeed`, plus `S-01` filter-bar behavior and `Resolved Today` tile calculation.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §4.1` (`GetDashboardFeed`), `§5.3`, and `§6` query assumptions to document `OperationalTimeZone` (`WIB`), global summary counters, server-side `status`/`severity`/`scope`/`search` filter parameters, `SeverityRank DESC, LastOccurrenceAtUtc DESC` sort, and Stage 3 deferral of the Alert Strip.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

## OQ-006

Status: CLOSED

### Decision
Render all 3 primary sidebar navigation items (`Dashboard`, `Detectors`, `History & Reports`) in `AppLayout.vue` per `PRIMA-UI-UX-GUIDELINE.md §4.1`, with:
- `Dashboard` (`/`) active and showing the live `Open + Reopened` counter pill driven by `GetDashboardFeed` summary counters.
- `Detectors` and `History & Reports` visibly disabled (`aria-disabled="true"`, non-navigating) with informative tooltips (`"Coming in Stage 3"` and `"Coming in Stage 4"`), omitting the `Detectors` health dot until Stage 3.
- `Resolved Today` summary tile on `S-01` rendered as an informational counter tile (non-navigating in Stage 1, with tooltip `"History view available in Stage 4"`).
- `S-01` issue feed rows rendered non-navigating in Stage 1 (no `cursor-pointer` broken navigation or throwaway `/issues/:id`, `/detectors`, `/history` stub pages; router fallback safely redirects unknown authenticated paths to `/`).

### Rationale
Preserves the authentic `AppLayout.vue` visual shell while maintaining zero dead routes, zero console/router errors, and zero throwaway placeholder code before Stage 2–4 screens are delivered.

### Impact
Clarifies Stage 1 `router/index.ts` routes, `AppLayout.vue` sidebar item states, and `S-01` tile/row interactivity boundaries.

### Architecture Impact
Update `docs/PRIMA-ARCHITECTURE.md §5.2` (`app/router/index.ts`, `AppLayout.vue`) and `§7` to reflect Stage 1 route registration (`/login`, `/`) and disabled Stage 3–4 navigation affordances.

### Resolved By
User (Stakeholder Decision via /grill-me)

### Resolved Date
2026-11-20

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

Stage 1 is a greenfield platform bootstrap and the closed feasibility decisions (`GAP-001`–`GAP-008`, `OQ-001`–`OQ-006`) materially amend the pre-existing technical specification in `docs/PRIMA-ARCHITECTURE.md` — specifically:
1. Replacing the single-`finding` ingestion payload with `findings[]` atomic batch ingestion and response schema (`§4.3`).
2. Fixing `001_InitialSchema.sql` (`InspectionRunId`, nullable `SourceTable`/`RecordKeys`, `SeverityRank`, `OccurrenceCount`, `DismissedAtUtc`, `IsSuperseded`, `UNIQUE` `Detectors.Name`) and canonicalizing enum strings (`§4.2`, `§5`, `§6`).
3. Adding `404`/`409`/`422` detector and Stage 1 `ExecutionFailed` ingestion guardrails (`§4.3`).
4. Specifying `BR-PRIMA-002` non-destructive reopen SQL/aggregate resets and deferring `BR-PRIMA-003` recurrence escalation to Stage 3 (`§4.2.1`).
5. Formalizing `CorrelationKey` canonicalization (`"{detectorId:D}:{TRIM_UPPER}"`) and within-batch duplicate rejection (`§4.2`, `§4.3`).
6. Locking down shared `Ingestion:ApiKeys` array auth, flat v1 officer authorization, `PasswordHasher` + 12h JWT + `localStorage` session persistence, `002_SeedInitialUsers.sql` vs. Dev demo seeder split, `WIB` `Resolved Today` query semantics, and Stage 1 route/test/simulator boundaries (`§§ 2, 4, 5, 7`).

`ica-architect` must update `docs/PRIMA-ARCHITECTURE.md` in place before `IMPLEMENTATION-PLAN` creation.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

READY-FOR-PLANNING

## Notes

All 8 gaps (`GAP-001`–`GAP-008`) and 6 open questions (`OQ-001`–`OQ-006`) are `CLOSED` with full resolution metadata (`0 OPEN`). In accordance with Knowledge-Centric SDLC gate ownership, the Analyst leaves `Status` as `NOT-READY`; `ica-architect` is the sole role authorized to verify the closed entries, set `Status: READY-FOR-PLANNING`, update `docs/PRIMA-ARCHITECTURE.md` in place, and produce the Stage 1 `IMPLEMENTATION-PLAN`.

---

# 11. References

Referenced artifacts:
- `ISSUE`: `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md`
- `DOMAIN`: `docs/PRIMA-DOMAIN.md`
- `FEATURE` (pre-existing outcome specs): `docs/PRIMA-DEVELOPMENT-ROADMAP.md`, `docs/PRIMA-USE-CASE-DISCOVERY.md`, `docs/PRIMA-SCREEN-INVENTORY.md`, `docs/PRIMA-UI-UX-GUIDELINE.md`, `docs/PrIMA - Product Definition.md`
- Pre-existing Technical Architecture Specification: `docs/PRIMA-ARCHITECTURE.md`

Referenced codebase locations:
- `src/` (currently empty; greenfield target for `src/backend/PrIMA.Api/`, `src/backend/PrIMA.Api.Tests/`, `src/frontend/`, and `src/tools/telemetry-simulator/simulate.mjs`)

Referenced documents:
- `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md`
- `docs/PRIMA-DEVELOPMENT-ROADMAP.md`
- `docs/PRIMA-DOMAIN.md`
- `docs/PRIMA-ARCHITECTURE.md`
- `docs/PRIMA-SCREEN-INVENTORY.md`
- `docs/PRIMA-UI-UX-GUIDELINE.md`
- `docs/PRIMA-USE-CASE-DISCOVERY.md`
- `docs/PrIMA - Product Definition.md`
