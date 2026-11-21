---
Title: PrIMA Stage 3 — Detector Portfolio, Automated Supervision & Webhook Verification
Code: PRIMA-STAGE-3
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-11-21
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Assess the feasibility of `IS003-PRIMA-STAGE-3-ISSUE.md` (`ID: PRIMA-STAGE-3`, `Type: CHANGE-REQUEST`): implementing Stage 3 ("Detector Portfolio, Automated Supervision & Webhook Verification") of `docs/PRIMA-DEVELOPMENT-ROADMAP.md` to deliver the **Autonomous Proactive Supervision & Self-Monitoring** milestone.

In scope for Stage 3:
1. **Detector Portfolio Management (`S-03`, Use Case `E-1`):** Complete portfolio overview (`/detectors`, 15s polling) featuring 4-state Health Summary Bar (`Healthy`, `Failing`, `Silent`, `Paused`), multi-attribute filter strips (Admin Status, Health, Scope), instant search, virtualized detector table/cards, `+ Register Detector` CTA, and navigation sidebar indicator alert dot in `AppLayout.vue` when any detector is `Failing` or `Silent`.
2. **Detector Detail & Execution History (`S-04`, `S-06`, Use Cases `E-2`, `F-1`, `F-2`):**
   - Single-detector context view (`/detectors/:id`, 15s polling) featuring sticky header, status-sensitive Action Bar (`Trigger Run`, `Pause`/`Resume`, `Edit`, `Retire` with confirmation), 4-column 30-day reliability stats (`LastSuccessfulRun`, `LastFailure`, `TotalRuns30d`, `PassRate%` with emerald/amber/rose clean pass rate coloring), recent 10 inspection runs with inline detail expansion, and linked active issues navigating directly to `S-02`.
   - Comprehensive historical inspection run log (`/detectors/:id/runs`, `S-06`) with date range picker (default 7 days), outcome & trigger source filter pills, expandable rows displaying full execution metadata, duration, error stack traces, and direct links to discovered findings on `S-02`.
3. **Detector Configuration & Administrative Lifecycle (`S-05`, Use Cases `E-3`, `E-4`, `E-5`, `E-6`):**
   - Registration and edit form (`/detectors/new`, `/detectors/:id/edit`) governed by VeeValidate + Zod, enforcing immutable `Name` in edit mode, split cadence picker (`Minutes`/`Hours`) with real-time calculated 2× silence threshold helper text, and validated optional `WebhookUrl`.
   - Administrative lifecycle state transitions (`Active <-> Paused -> Retired`) per `docs/PRIMA-DOMAIN.md §8.2`: pausing suspends silence detection and rejects incoming runs with `409 Conflict`; retiring requires a PrimeVue `ConfirmDialog`, irreversibly decommissions the detector, and preserves historical runs and active linked issues for normal resolution.
4. **On-Demand Manual Detector Execution (`S-04`, Use Case `E-7`):** Officers trigger immediate inspection on demand via HTTP webhook; PrIMA synchronously executes the check, records an `InspectionRun` with `TriggerSource = "Manual"`, updates detector health, and displays immediate outcome feedback.
5. **Autonomous Silence Detection (`BR-PRIMA-010`, `BR-PRIMA-005`):** `MonitoringSupervisorWorker` background service runs on a 60-second loop, evaluating `Active` detectors where elapsed time since `LastHealthyHeartbeatUtc` (or `CreatedAtUtc` for newly onboarded detectors) exceeds `ExpectedCadenceSeconds * 2.0`. It marks the detector `HealthStatus = Silent`, synthesizes/appends an `Infrastructure` issue with deterministic correlation key `SILENT:{DetectorId}`, and records cadence-multiplied occurrences (at 2×, 3×, 4× cadence) to track prolonged silence without database flooding.
6. **Execution Failure Anomaly Synthesis (`BR-PRIMA-005`):** Extend `IngestTelemetryHandler` to process `outcome == "ExecutionFailed"`: record the failed `InspectionRun` with `FailureReason`, set detector `HealthStatus = Failing`, and auto-synthesize or append to an `Infrastructure` issue (`FAIL:{DetectorId}`) so EDP is immediately alerted to detector crashes.
7. **Dynamic Severity Escalation (`BR-PRIMA-003`, Use Cases `G-1`, `G-2`):**
   - Continuously evaluate unacknowledged `Open` and `Reopened` issues past the configured SLA timeout (default 30 minutes), escalating severity by +1 step per SLA window (`Low` -> `Medium` -> `High` -> `Critical`) and incrementing `EscalationLevel` until capped at `Critical`.
   - Continuously evaluate recurrence across all active states (`Open`, `Acknowledged`, `Investigating`, `Reopened`), escalating severity by +1 step when reaching `>= 5` occurrences.
   - Atomically record escalation events in `dbo.IssueActivityLogs`.
8. **Webhook-Driven Verification Re-run (`S-02`, Use Case `D-2`):** In the Issue Detail Workbench, officers trigger an automated verification re-run via detector webhook following remediation, recording an `AutomatedReRun` `VerificationRecord` (`CleanPassed` or `AnomalyStillPresent`) to unlock or re-lock the Verification Gate (`BR-PRIMA-004`), with graceful fallback guidance to `M-03 Manual Verification` when no webhook is configured.
9. **Dashboard S-01 & Header Linkage Enhancements (`G-1`, `G-2`):**
   - Upgrade `S-01 Issue Dashboard` with a conditional top Alert Strip (`⚠ N issues escalated · M issues reopened`), `↑ Escalated` indicator badges, and dedicated `escalatedOnly` query filtering.
   - Upgrade `S-02 Issue Detail Workbench` sticky header to turn the detector name badge into an active router link to `S-04` (`/detectors/:detectorId`).

Explicitly out of scope for Stage 3:
- **Stage 4:** Retrospective reporting, period-close KPI reporting (`S-07`, `H-1`, `H-2`), multi-period trend visualizations, and full v1.0 quality gate.
- **Stage 5:** Pilot SIMRS detector suite across hospital modules (`BR-PRIMA-007`) and hospital on-premise production deployment.

Referenced artifacts:
- `ISSUE`: `docs/issues/IS003-PRIMA-STAGE-3-ISSUE.md`
- `DOMAIN`: `docs/PRIMA-DOMAIN.md` (Business Rules `BR-PRIMA-003`, `BR-PRIMA-005`, `BR-PRIMA-010`, and Section 8 State Machines)
- `FEATURE` (pre-existing specifications): `docs/PRIMA-DEVELOPMENT-ROADMAP.md` (Stage 3), `docs/PRIMA-USE-CASE-DISCOVERY.md` (Use Cases `D-2`, `E-1`–`E-7`, `F-1`–`F-2`, `G-1`–`G-2`), `docs/PRIMA-SCREEN-INVENTORY.md` (`S-03`–`S-06`), `docs/PRIMA-UI-UX-GUIDELINE.md`
- Pre-existing Technical Architecture: `docs/PRIMA-ARCHITECTURE.md`
- Baseline Stage 1 & 2 Artifacts: `docs/issues/IS001-PRIMA-STAGE-1-FEASIBILITY-ASSESSMENT.md`, `docs/issues/IS002-PRIMA-STAGE-2-FEASIBILITY-ASSESSMENT.md`

## Objective

Assess Stage 3 feasibility against existing specifications, codebase state (`src/backend` and `src/frontend`), and runtime environment. Surface and close every architectural, domain, UX, and lifecycle decision so downstream `ica-architect` Architecture Update, Planning, and agentic implementation do not invent decisions.

---

# 2. Current State

## Existing Behavior

- **Stage 1 & Stage 2 are fully operational in the repository:**
  - `src/backend/PrIMA.Api/`: Minimal API running on .NET 8 with DbUp migrations (`001_InitialSchema.sql`, `002_SeedInitialUsers.sql`, `003_IssueActivityLogs.sql`), JWT auth, telemetry ingestion with deterministic deduplication (`BR-PRIMA-001`) and recurrence reopening (`BR-PRIMA-002`), issue triage commands (`TakeIt`, `Assign`), append-only notes, remediation logging (`M-02`), manual verification (`M-03`), verification gate enforcement (`BR-PRIMA-004`), dismissal (`M-01`), and comprehensive audit logging (`dbo.IssueActivityLogs`).
  - `src/frontend/`: Vue 3.5+ with Tailwind CSS v4, PrimeVue v4, Pinia auth store, TanStack Query polling, `S-00` Login, `S-01` Issue Dashboard, and `S-02` Issue Detail Workbench.
- **Detector portfolio and supervision capabilities are currently unimplemented or partial:**
  - In `dbo.Detectors`, detectors exist only as initial demo seed data.
  - There are no API endpoints or controllers for detector CRUD, lifecycle transitions (`Pause`, `Resume`, `Retire`), detector detail queries with 30-day reliability stats, or inspection run histories.
  - The domain aggregate `Detector.cs` has basic property stubs but lacks domain methods for cadence validation, health evaluation, configuration updates, and execution run validation.
  - In `IngestTelemetryHandler.cs`, when `outcome == "ExecutionFailed"`, the handler does not update detector health to `Failing`, does not record failure reasons, and does not synthesize or append `FAIL:{DetectorId}` infrastructure issues per `BR-PRIMA-005` (it currently unconditionally updates liveness to `Healthy` and returns empty results).
  - Background supervision is completely absent: there is no `MonitoringSupervisorWorker.cs` background service; detectors that crash or stop sending heartbeats remain undetected indefinitely, and unacknowledged or recurring issues never dynamically escalate.
  - Webhook integration is absent: `DetectorWebhookClient.cs` does not exist; neither `S-04` manual execution (`E-7`) nor `S-02` verification re-run (`D-2`) can call webhooks.
  - In `src/frontend/`:
    - The navigation sidebar in `AppLayout.vue` has `Detectors` disabled with a `"Coming in Stage 3"` tooltip.
    - The feature slice `src/frontend/src/features/detectors/` does not exist; routes `/detectors`, `/detectors/new`, `/detectors/:id`, `/detectors/:id/edit`, and `/detectors/:id/runs` are missing.
    - In `WorkbenchHeader.vue` (`S-02`), the detector name badge is rendered as a plain text badge with a tooltip `"Detector portfolio detail available in Stage 3"` instead of an active router link to `S-04`.
    - In `WorkbenchHeader.vue` (`S-02`), the Action Bar promotes `Manual Verification` (`M-03`) as primary and lacks `Trigger Verification Re-run` webhook invocation.
    - In `DashboardView.vue` (`S-01`), the dashboard feed has no alert strip for dynamically escalated issues, no quick-filter for escalated issues, and no soft highlight cues.

## Existing Constraints

- **Verified local development environment:**
  - OS: Windows (PowerShell shell).
  - .NET SDK: `net8.0` (`8.0.428`).
  - Node.js / npm: `v22.22.3` / `10.9.8`.
  - Database: Microsoft SQL Server 2022 Developer Edition running on `localhost` (`MSSQLSERVER` active, connection string in `appsettings.Development.json`).
- **Architectural & Technology Constraints:**
  - Backend: ASP.NET Core .NET 8 Minimal API, vertical slice architecture, Dapper for data access, FluentValidation, RFC 7807 `ProblemDetails`, DbUp startup migrations, and OpenAPI (`/swagger/v1/swagger.json`).
  - Frontend: Vue 3.5+ (`<script setup lang="ts">`), Vite 6, Tailwind CSS v4, PrimeVue v4 (Aura preset), Pinia, TanStack Query v5, VeeValidate + Zod, and `openapi-typescript` generating `src/shared/types/api.generated.ts`.
  - Frontend feature-slice isolation: `src/features/{auth,dashboard,issues,detectors}/` with zero cross-feature imports.
  - Operational timezone: `Asia/Jakarta` (`WIB`, UTC+7).
  - Currency formatting: Indonesian Rupiah (`id-ID`).
  - Non-disruptive polling: 15s polling on `S-03`/`S-04` and 10s on `S-01`/`S-02` must preserve form draft, open dialogs, and expanded run rows.

---

# 3. Gap Analysis

| ID | Severity | Status | Gap |
|---|---|---|---|
| GAP-001 | CRITICAL | CLOSED | Missing `MonitoringSupervisorWorker` background service: `src/backend/PrIMA.Api/` has no autonomous background service running a 60-second evaluation loop for silence detection (`BR-PRIMA-010`), infrastructure issue synthesis (`BR-PRIMA-005`), or dynamic severity escalation (`BR-PRIMA-003`). |
| GAP-002 | CRITICAL | CLOSED | Incomplete `IngestTelemetryHandler`: currently hardcodes `DetectorHealth.Healthy`, ignores `outcome == "ExecutionFailed"`, does not record failure reasons, and does not synthesize or append `FAIL:{DetectorId}` infrastructure issues per `BR-PRIMA-005`. |
| GAP-003 | CRITICAL | CLOSED | Missing `DetectorWebhookClient` and webhook vertical slices: no HTTP client to execute synchronous on-demand detector checks, no `POST /api/detectors/{id}/trigger-run` (`E-7`), and no `POST /api/issues/{id}/verification/trigger-run` (`D-2`). |
| GAP-004 | CRITICAL | CLOSED | Missing Detector portfolio management API endpoints: no queries or commands for `GetDetectorList` (`S-03`), `GetDetectorById` (`S-04`), `RegisterDetector` (`S-05`), `UpdateDetector` (`S-05`), `ToggleDetectorStatus` (`S-04`), or `GetInspectionRuns` (`S-06`). |
| GAP-005 | CRITICAL | CLOSED | Missing frontend Detector feature slice and screens: `src/features/detectors/` does not exist; routes `/detectors` (`S-03`), `/detectors/:id` (`S-04`), `/detectors/new` & `/detectors/:id/edit` (`S-05`), and `/detectors/:id/runs` (`S-06`) are missing; sidebar `Detectors` menu item is disabled in `AppLayout.vue`. |
| GAP-006 | MAJOR | CLOSED | Incomplete `Detector` domain aggregate: `Detector.cs` lacks domain methods for updating mutable configuration, tracking 30-day health metrics, evaluating silence thresholds, and enforcing administrative state rules (`Active <-> Paused -> Retired`). |
| GAP-007 | MAJOR | CLOSED | Missing S-01 / S-02 upgrades & sidebar alert integration: S-01 lacks top Alert Strip for escalated issues (`G-1`), `escalatedOnly` query filtering, and newly surfaced row highlights; S-02 header detector badge lacks route navigation to `S-04`; S-02 Action Bar lacks webhook-driven verification re-run trigger with graceful fallback to `M-03`. |

---

# 4. Open Questions

| ID | Status | Question | Impact |
|---|---|---|---|
| OQ-001 | CLOSED | For on-demand detector execution (Manual Run on `S-04` and Verification Re-run on `S-02`), what communication protocol should PrIMA use to interact with the detector's `WebhookUrl`? | Defines `DetectorWebhookClient` architecture, endpoint contracts, network timeouts, and synchronous UI feedback. |
| OQ-002 | CLOSED | If a detector's `WebhookUrl` times out (>15s), is unreachable, or returns an HTTP 5xx error during a Manual Run (`S-04`) or Verification Re-run (`S-02`), how should PrIMA record and handle the failure? | Governs error handling, `InspectionRun` recording, detector health transitions, `BR-PRIMA-005` issue synthesis, and RFC 7807 ProblemDetails responses. |
| OQ-003 | CLOSED | For newly registered active detectors that have never reported telemetry (`LastHealthyHeartbeatUtc` is null), what baseline should `MonitoringSupervisorWorker` use to compute silence cadence? | Prevents silence false-positives or permanent blind spots on un-onboarded detectors. |
| OQ-004 | CLOSED | When a previously `Silent` or `Failing` detector recovers by reporting a successful clean run, how should the existing open `Infrastructure` issue (`SILENT:{id}` or `FAIL:{id}`) be handled? | Preserves `BR-PRIMA-004` Verification Gate and `BR-PRIMA-005` accountability without premature automated closure. |
| OQ-005 | CLOSED | While a detector remains continuously silent across multiple evaluation cycles, how frequently should `MonitoringSupervisorWorker` append new `Occurrence` records to `SILENT:{DetectorId}`? | Prevents database bloat from 60s supervisor ticks while preserving temporal audit trail for chronic outages. |
| OQ-006 | CLOSED | How should Dynamic Severity Escalation (`BR-PRIMA-003`) progress when an `Open`/`Reopened` issue remains unacknowledged across multiple SLA timeout windows? | Defines multi-window escalation stepping, severity ceiling, and `EscalationLevel` tracking. |
| OQ-007 | CLOSED | Which lifecycle statuses are eligible for SLA timeout escalation and recurrence threshold escalation? | Governs domain aggregate escalation methods and background supervisor query filtering. |
| OQ-008 | CLOSED | How should the 30-day "Pass Rate %" metric on `S-04` (Detector Detail) and `S-03` (Detector List) be calculated? | Standardizes reliability reporting calculation, data projection, and UI color thresholds (emerald/amber/rose). |
| OQ-009 | CLOSED | When an officer retires a detector (`E-6`), how should any currently active issues (`Open`, `Acknowledged`, `Investigating`) associated with that detector be treated? | Defines relational integrity, issue lifecycle autonomy, and retirement validation rules. |
| OQ-010 | CLOSED | How should `S-01` handle the "Filter Escalated" action from the Alert Strip and how should the sidebar `Detectors` alert dot obtain detector health metrics? | Governs `GetDashboardFeed` API extension, `AppLayout.vue` polling strategy, and UX navigation consistency. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | Stage 3 builds directly upon the established Stage 1 and Stage 2 architectures in `src/backend/PrIMA.Api` and `src/frontend`, reusing existing Dapper connection factories, RFC 7807 problem details handling, and TanStack Query polling patterns. |
| ASM-002 | All operational timestamps are stored in UTC (`DATETIME2`) and formatted in `Asia/Jakarta` (`WIB`, UTC+7) on the frontend via existing `formatWibDateTime`. |
| ASM-003 | Detectors run externally and post telemetry to `/api/ingestion/telemetry` using API Key authentication (`X-Api-Key`), while all user-facing detector management endpoints require JWT authentication (`[Authorize]`). |
| ASM-004 | `MonitoringSupervisorWorker` runs as a hosted `BackgroundService` within `PrIMA.Api`, using a scoped service provider to execute evaluations every 60 seconds with configurable thresholds in `appsettings.json`. |
| ASM-005 | Detector `Name` and `Id` are strictly immutable once created to guarantee deterministic correlation key and telemetry foreign key integrity. |
| ASM-006 | Detector retirement is permanent and irreversible (soft-retire via `AdminStatus = Retired`); hard deletion from `dbo.Detectors` is prohibited to maintain audit trails. |
| ASM-007 | When calling detector webhooks, PrIMA enforces a strict 15-second HTTP request timeout to guarantee responsive UI feedback and prevent backend worker thread starvation. |
| ASM-008 | Retrospective period-close KPI reporting (`S-07`) and real SIMRS pilot detectors remain strictly deferred to Stages 4 and 5. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Webhook hang or external detector outage freezing UI during on-demand execution. | Officer triggers manual run or verification re-run and browser hangs waiting for response. | Configure `HttpClient` in `DetectorWebhookClient` with a strict 15-second timeout, cancellation tokens, and explicit error handling returning `504 Gateway Timeout` or `502 Bad Gateway` ProblemDetails. |
| RISK-002 | Supervisor loop flooding `dbo.Occurrences` during prolonged detector silence. | An inactive detector receives 60 occurrences per hour, exhausting storage and cluttering occurrence timelines. | Enforce cadence-multiplied occurrences (DEC-005): record Occurrence #1 on 2× cadence breach, and append new occurrences only when silence crosses subsequent integer cadence multiples (3×, 4×, 5×). |
| RISK-003 | Race condition between background silence evaluation and incoming recovery telemetry. | A detector reports healthy telemetry simultaneously with the supervisor marking it silent. | Wrap detector status transitions and issue synthesis inside a database transaction with `UPDLOCK` / `ROWLOCK` on `dbo.Detectors`, and verify `LastHealthyHeartbeatUtc` under lock before transitioning to `Silent`. |
| RISK-004 | Inadvertent bypass of the Verification Gate during automated verification re-run. | Webhook returns `FindingDiscovered` but issue is erroneously unlocked for resolution. | Strictly inspect webhook telemetry outcome: only create a `VerificationRecord` with `CleanPassed = true` if `Outcome == "Passed"` with 0 findings; if `FindingDiscovered` or `ExecutionFailed`, record `CleanPassed = false`, keeping the gate locked. |
| RISK-005 | Contract drift between new detector Minimal API endpoints and frontend TypeScript types. | Runtime serialization bugs and broken Vue 3 builds. | Automatically regenerate `api.generated.ts` strictly from `/swagger/v1/swagger.json` via `npm run codegen:api` with zero manual edits. |
| RISK-006 | Active issues orphaned or permanently locked upon detector retirement. | An open issue for a retired detector cannot be resolved or dismissed. | Maintain issue lifecycle autonomy (DEC-009): retirement only blocks new runs and silence checks; active issues remain fully addressable through normal investigation, remediation, verification, and resolution. |
| RISK-007 | Unbounded escalation loop elevating issues indefinitely. | Dynamic escalation increments severity or escalation levels endlessly every 60 seconds. | Enforce multi-step escalation rules (DEC-006): cap severity at `Critical`, record last escalation timestamp in issue state, and require a full SLA window (e.g. 30m) between successive escalation steps. |

---

# 7. Recommendations

The major cross-cutting trade-offs evaluated during feasibility analysis are summarized below as solution alternatives (approved selections are recorded exclusively in Section 8):

## Webhook Invocation Protocol (`OQ-001`)

### Option A: Synchronous HTTP Execution with 15s Timeout (Selected)
PrIMA sends `POST {WebhookUrl}` with `{ detectorId, triggerSource, issueId? }` and awaits the standard telemetry result JSON within a 15-second timeout, immediately recording the `InspectionRun` and updating health/verification status.
- **Advantages:** Instantaneous feedback for officers on `S-02` and `S-04`; eliminates callback race conditions and complex polling state machines; aligns perfectly with `PRIMA-UI-UX-GUIDELINE.md` inline spinner specifications.
- **Disadvantages:** External detector check must complete within 15 seconds (consistent with `BR-PRIMA-007` non-intrusive lightweight probe requirement).

### Option B: Asynchronous Trigger with Webhook Callback
PrIMA calls `POST {WebhookUrl}`, receives `202 Accepted`, and external detector posts back to `/api/ingestion/telemetry`.
- **Advantages:** Decouples execution duration from HTTP request lifespan.
- **Disadvantages:** Adds latency and UI polling friction for interactive triage on `S-02`.

---

# 8. Gap Closure Decisions

| Decision ID | Target GAP / OQ | Decision | Rationale | Impact | Architecture Impact | Resolved By | Resolved Date |
|---|---|---|---|---|---|---|---|
| DEC-001 | GAP-003, OQ-001 | **Synchronous HTTP Webhook Execution:** PrIMA sends `POST {WebhookUrl}` with `{ detectorId, triggerSource, issueId? }` and waits up to 15s for the detector to return standard telemetry JSON (`outcome`, `durationMs`, `findings[]`, `failureReason`). PrIMA records the `InspectionRun` and updates verification/health immediately in the HTTP response. | Delivers instantaneous feedback on `S-02` and `S-04` without callback latency or polling race conditions; fits non-intrusive probe model (`BR-PRIMA-007`). | Direct synchronous feedback in UI; fast verification gate resolution. | Implement `DetectorWebhookClient.cs` using `IHttpClientFactory` with 15s timeout and endpoint handlers for `TriggerManualRun` and `TriggerVerificationRun`. | Drury / User | 2026-11-21 |
| DEC-002 | GAP-003, OQ-002 | **Comprehensive Webhook Failure Treatment:** If a webhook times out (>15s), is unreachable, or returns HTTP 5xx, PrIMA records an `InspectionRun` with `Outcome = ExecutionFailed`, marks detector `HealthStatus = Failing`, synthesizes/appends `FAIL:{DetectorId}` infrastructure issue per `BR-PRIMA-005`, records a failed `VerificationRecord` (if triggered from `D-2`), and returns `502 Bad Gateway` / `504 Gateway Timeout` ProblemDetails. | Guarantees unified treatment of detector health anomalies (`BR-PRIMA-005`) even during on-demand execution, while alerting EDP officers to network or host failures. | Ensures failed on-demand checks are audited and visible in detector history. | Update `DetectorWebhookClient` error handling to persist failed runs and synthesize failure issues within the same transaction. | Drury / User | 2026-11-21 |
| DEC-003 | GAP-001, OQ-003 | **CreatedAtUtc Silence Baseline for Unrun Detectors:** For active detectors with `LastHealthyHeartbeatUtc == null`, elapsed silence duration is computed as `UtcNow - CreatedAtUtc`. If no heartbeat arrives within `ExpectedCadenceSeconds * 2.0`, the detector transitions to `Silent`. | Eliminates monitoring blind spots where newly deployed detectors that fail to start remain indefinitely unflagged. | Detects dead-on-arrival or misconfigured detectors automatically after 2× cadence. | In `MonitoringSupervisorWorker`, query silence condition as `COALESCE(LastHealthyHeartbeatUtc, CreatedAtUtc) < @SilenceCutoff`. | Drury / User | 2026-11-21 |
| DEC-004 | GAP-001, OQ-004 | **Detector Health Reset with EDP Issue Governance on Recovery:** When a `Silent` or `Failing` detector reports a successful clean run, its health status resets to `Healthy` and `LastHealthyHeartbeatUtc` is updated. However, the open `Infrastructure` issue (`SILENT:{id}` or `FAIL:{id}`) remains active, appending a system activity log noting recovery so EDP can verify and formally close the issue per `BR-PRIMA-004` and `BR-PRIMA-005`. | Upholds the domain invariant that infrastructure issues follow the standard issue lifecycle requiring human accountability and verification. | Prevents silent auto-closure of infrastructure incidents without EDP awareness. | `IngestTelemetryHandler` detects health recovery, resets detector health, and inserts an activity log on any matching open infrastructure issue. | Drury / User | 2026-11-21 |
| DEC-005 | GAP-001, OQ-005 | **Cadence-Multiplied Occurrence Generation for Silence:** `MonitoringSupervisorWorker` records Occurrence #1 when crossing 2× cadence (transitioning to `Silent`), and appends a new occurrence only when silence crosses subsequent integer cadence multiples (3×, 4×, 5×, etc.). | Prevents database bloat from 60-second polling while accurately tracking prolonged silence and incrementing recurrence for escalation. | Clean, bounded occurrence growth in `dbo.Occurrences`. | In supervisor worker, calculate `multiples = (int)(elapsed / cadence)` and compare against `issue.OccurrenceCount`. | Drury / User | 2026-11-21 |
| DEC-006 | GAP-001, OQ-006 | **Multi-Step SLA Escalation Capped at Critical:** Each elapsed SLA timeout window (default 30 mins) while unacknowledged elevates severity by +1 step (`Low` -> `Medium` -> `High` -> `Critical`) and increments `EscalationLevel`. Once `Critical` is reached, further SLA escalation ceases. Recurrence (`>= 5` occurrences) independently elevates severity by +1 step. | Provides progressive operational urgency for neglected issues without exceeding maximum severity boundaries. | Predictable, bounded escalation progression visible on `S-01`. | In `Issue.cs`, add `EscalateSeverity(reason, utcNow)` method enforcing severity progression and capping at `Critical`. | Drury / User | 2026-11-21 |
| DEC-007 | GAP-001, OQ-007 | **Lifecycle Eligibility for Dynamic Escalation:** SLA timeout escalation applies strictly to unacknowledged states (`Open` and `Reopened`), halting once `Acknowledged`. Recurrence escalation (`>= 5` occurrences) applies across all active states (`Open`, `Acknowledged`, `Investigating`, `Reopened`). | Reflects business reality: acknowledgment halts unacknowledged SLA timers, but chronic recurring anomalies remain high-risk even during investigation. | Immediate escalation alerts on chronic bugs during triage or investigation. | Update supervisor worker query to evaluate SLA timeouts on `Status IN ('Open', 'Reopened')`, and recurrence on `Status IN ('Open', 'Acknowledged', 'Investigating', 'Reopened')`. | Drury / User | 2026-11-21 |
| DEC-008 | GAP-004, OQ-008 | **Standardized Clean Pass Rate Calculation:** 30-day Pass Rate % is calculated strictly as `Count(Outcome == 'Passed') / TotalRuns * 100`. UI colors apply thresholds: Emerald for `>= 95%`, Amber for `85%–94%`, Rose for `< 85%`. | Directly indicates data health to EDP officers (the proportion of inspection runs where zero data integrity defects were found). | Clear, unambiguous reliability KPI on `S-03` and `S-04`. | Query calculates `COUNT(CASE WHEN Outcome = 'Passed' THEN 1 END) * 100.0 / NULLIF(COUNT(*), 0)`. | Drury / User | 2026-11-21 |
| DEC-009 | GAP-004, OQ-009 | **Issue Autonomy on Detector Retirement:** Retiring a detector (`E-6`) transitions `AdminStatus = Retired`, prevents future runs (`409 Conflict`), and suppresses silence checks. All existing active issues (`Open`, `Acknowledged`, `Investigating`) remain active and addressable through normal investigation, remediation, and resolution. | Ensures retirement does not leave unresolved hospital data anomalies unhandled or unverified. | Clean lifecycle separation between monitoring probe configuration and clinical/financial issue resolution. | `ToggleDetectorStatusHandler` updates `AdminStatus = Retired` without modifying linked `dbo.Issues` records. | Drury / User | 2026-11-21 |
| DEC-010 | GAP-007, OQ-010 | **Dedicated Escalated Feed Filter & Sidebar Health Polling:** Add `EscalatedCount` to `DashboardFeedSummaryDto` and `bool? EscalatedOnly` filter parameter to `GetDashboardFeedQuery`; clicking "Filter Escalated →" on the `S-01` Alert Strip activates this filter. Navigation sidebar indicator dot reads detector health counts from `GET /api/detectors/summary` (or dashboard feed summary), rendering an alert dot when `(FailingCount + SilentCount) > 0`. | Seamless single-click navigation for escalation awareness (`G-1`, `G-2`) and global detector health visibility across the entire app. | Cohesive, real-time awareness of platform health without full-table polling overhead. | Extend `GetDashboardFeedEndpoint`, `DashboardFilterBar.vue`, and `AppLayout.vue`. | Drury / User | 2026-11-21 |

---

# 9. Architecture Applicability

**Decision:** `ARCHITECTURE-REQUIRED`

**Rationale:**
Stage 3 introduces significant structural, behavioral, and cross-cutting components:
1. **New Background Worker Component:** Introduction of `BackgroundServices/MonitoringSupervisorWorker.cs` to run recurring silence evaluation and SLA/recurrence escalation loops.
2. **New Integration Boundary:** Creation of `Infrastructure/Webhooks/DetectorWebhookClient.cs` to manage outbound HTTP communication with external hospital detectors.
3. **New Vertical Slices & Aggregate Expansion:** Addition of complete `Features/Detectors/*` vertical slices (`GetDetectorList`, `GetDetectorById`, `RegisterDetector`, `UpdateDetector`, `ToggleDetectorStatus`, `TriggerManualRun`, `GetInspectionRuns`) and domain methods on `Detector.cs` and `Issue.cs`.
4. **Extended Ingestion Pipeline:** Refactoring `IngestTelemetryHandler` to support `ExecutionFailed` processing and automated infrastructure issue synthesis (`BR-PRIMA-005`).
5. **New Frontend Feature Slice:** Implementation of `src/features/detectors/` (`S-03`, `S-04`, `S-05`, `S-06`) and cross-cutting upgrades to `S-01`, `S-02`, and `AppLayout.vue`.

A formal Target Architecture Update (`ica-architect`) is required before implementation planning can proceed.

---

# 10. Planning Readiness

### Readiness Checklist

- [x] All 7 critical and major architectural gaps identified and resolved.
- [x] All 10 open questions grilled, analyzed, and closed with recorded stakeholder decisions.
- [x] Webhook invocation protocol, timeout, and failure treatment formally specified (`DEC-001`, `DEC-002`).
- [x] Silence detection baseline, recurrence cadence, and recovery handling formally defined (`DEC-003`, `DEC-004`, `DEC-005`).
- [x] Dynamic severity escalation progression, ceiling, and status eligibility finalized (`DEC-006`, `DEC-007`).
- [x] 30-day Pass Rate % calculation and coloring thresholds standardized (`DEC-008`).
- [x] Detector retirement issue autonomy and lifecycle transitions aligned (`DEC-009`).
- [x] Frontend alert strip interactions, escalated filtering, and sidebar health alerts defined (`DEC-010`).
- [x] Architecture Applicability established as `ARCHITECTURE-REQUIRED`.

### Artifact Status

```text
Status: READY-FOR-PLANNING
```

The feasibility assessment is fully resolved and complete. All architectural and business questions are closed; downstream architecture and planning can proceed without inventing decisions.
