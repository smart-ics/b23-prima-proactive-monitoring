---
Title: PrIMA Stage 2 — Issue Triage, Investigation & Verification-Gated Resolution
Code: PRIMA-STAGE-2
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-11-21
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Assess the feasibility of `IS002-PRIMA-STAGE-2-ISSUE.md` (`ID: PRIMA-STAGE-2`, `Type: CHANGE-REQUEST`): implementing Stage 2 ("Issue Triage, Investigation & Verification-Gated Resolution") of `docs/PRIMA-DEVELOPMENT-ROADMAP.md` to deliver the **Complete End-to-End Manual Issue Resolution Loop** milestone.

In scope for Stage 2:
1. **Issue Detail Workbench (`S-02`, Use Case `A-3`):** Single-screen operational workbench (`/issues/:id`) featuring sticky header, context-sensitive Action Bar, Latest Telemetry Evidence inspection (metadata grid, financial variance strip with IDR formatting, dark monospace trace box with copy button), chronological collapsible occurrence timeline (`BR-PRIMA-009`), append-only investigation notes, 3-step Verification Gate status card (`BR-PRIMA-004`), remediation and verification history cards, dismissal card, ownership/timestamps card, and collapsible activity audit log (`S-02` Section 9).
2. **Issue Triage & Single-Action Assignment (`Take It`, Use Cases `B-1`, `B-2`, `BR-PRIMA-008`):** Dedicated `POST /api/issues/{id}/take-it` endpoint combining acknowledge + assign to caller + transition to `Investigating` in a single gesture, plus explicit `POST /api/issues/{id}/assign` to another officer and `GET /api/officers` to populate officer assignment dropdowns.
3. **Append-Only Investigation Notes (`C-3`):** `POST /api/issues/{id}/notes` recording timestamped, officer-attributed diagnostic notes displayed chronologically with `Ctrl+Enter` shortcut and draft preservation across 10-second polling.
4. **Documented Remediation Logging (`D-1`, Modal `M-02`):** `POST /api/issues/{id}/remediation` capturing standard operational corrective actions (`DataCorrection`, `ServiceRestart`, `ConfigurationAdjustment`, `CodeRemediationRequested`) with SOP descriptions while in `Investigating`.
5. **Mandatory Verification Gate (`D-3`, `D-4`, Modal `M-03`, `BR-PRIMA-004`):** `POST /api/issues/{id}/verification/manual` recording manual query confirmation (`CleanPassed` vs. `AnomalyStillPresent`). `POST /api/issues/{id}/resolve` strictly enforces `CleanPassed` verification, returning `409 Conflict` ProblemDetails if attempted without successful verification.
6. **Documented Issue Dismissal (`B-3`, Modal `M-01`, `BR-PRIMA-006`):** `POST /api/issues/{id}/dismiss` supporting dismissal across active states (`Open`, `Acknowledged`, `Investigating`, `Reopened`) with mandatory documented rationale (min 20 characters) and structured dismissal reasons (`FalsePositive`, `AcceptedOperationalException`, `DuplicateManualReport`), returning `422 Unprocessable Entity` if rationale is under 20 characters.
7. **Activity Log & Audit Trail Persistence (`BR-PRIMA-009`):** Migration `003_IssueActivityLogs.sql` creating `dbo.IssueActivityLogs` to atomically capture status transitions, assignment changes, notes, remediations, verifications, and dismissals with actor and UTC timestamp.
8. **Dashboard S-01 Linkage & Real-Time Polling Preservation:** Tapping an issue row on `S-01` navigates to `S-02`; 10-second TanStack Query polling on `S-02` refreshes data silently without closing modals, collapsing expanded occurrences, or clearing in-progress note drafts.

Explicitly out of scope for Stage 2:
- **Stage 3:** Automated detector portfolio management (`S-03`–`S-06`), `MonitoringSupervisorWorker` background silence (`BR-PRIMA-010`) and dynamic escalation (`BR-PRIMA-003`), `ExecutionFailed` infrastructure issue synthesis (`BR-PRIMA-005`), and webhook-driven automated re-verification (`D-2`).
- **Stage 4:** Retrospective reporting & history trends (`S-07`, `H-1`, `H-2`) and v1.0 quality gate.
- **Stage 5:** Pilot SIMRS Detector Suite (`BR-PRIMA-007`) and hospital on-premise production hardening.

Referenced artifacts:
- `ISSUE`: `docs/issues/IS002-PRIMA-STAGE-2-ISSUE.md`
- `DOMAIN`: `docs/PRIMA-DOMAIN.md`
- `FEATURE` (pre-existing specifications): `docs/PRIMA-DEVELOPMENT-ROADMAP.md`, `docs/PRIMA-USE-CASE-DISCOVERY.md`, `docs/PRIMA-SCREEN-INVENTORY.md`, `docs/PRIMA-UI-UX-GUIDELINE.md`, `docs/PrIMA - Product Definition.md`
- Pre-existing Technical Architecture: `docs/PRIMA-ARCHITECTURE.md`
- Baseline Stage 1 Artifacts: `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md`, `IS001-PRIMA-STAGE-1-FEASIBILITY-ASSESSMENT.md`, `IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

## Objective

Assess Stage 2 feasibility against existing specifications, codebase state (`src/backend` and `src/frontend`), and runtime environment. Surface every technical, domain, UX, and lifecycle contradiction or ambiguity, and record closed stakeholder decisions so downstream `ica-architect` Architecture Update, Planning, and agentic implementation do not invent decisions.

---

# 2. Current State

## Existing Behavior

- Stage 1 is fully operational in the repository:
  - `src/backend/PrIMA.Api/`: Minimal API running on .NET 8, DbUp migrations (`001_InitialSchema.sql`, `002_SeedInitialUsers.sql`), JWT officer login (`/api/auth/login`), telemetry ingestion (`POST /api/ingestion/telemetry`) with deterministic deduplication (`BR-PRIMA-001`) and recurrence reopening (`BR-PRIMA-002`), and dashboard feed query (`GET /api/issues/dashboard-feed`).
  - `src/frontend/`: Vue 3.5+ Vite application with Tailwind CSS v4, PrimeVue v4 Aura preset, Pinia auth store, TanStack Query 10s polling, `S-00` Login view, and `S-01` Dashboard view.
- The operational resolution loop is currently unimplemented:
  - Clicking an issue row on `S-01` (`ActiveIssueFeed.vue`) has no navigation handler and cannot open `S-02`.
  - There is no `/issues/:id` route in `src/frontend/src/app/router/index.ts`, and the `src/frontend/src/features/issues/` feature slice directory does not exist.
  - The backend lacks issue command endpoints for triage, notes, remediation, verification, resolve, and dismiss.
  - The domain aggregate `Issue.cs` contains only basic stubs for `Acknowledge`, `Assign`, `StartInvestigation`, `Resolve`, and `Dismiss`, missing domain validation, verification enforcement (`BR-PRIMA-004`), and collections for notes, remediations, verifications, and dismissals.
  - `dbo.IssueActivityLogs` table does not exist in SQL Server; migration `003_IssueActivityLogs.sql` is missing.
  - There is no endpoint to retrieve the list of active officers (`dbo.Officers`) for assignment dropdowns.

## Existing Constraints

- **Verified local development environment:**
  - OS: Windows.
  - .NET SDK: `net8.0` (`8.0.428`).
  - Node.js / npm: `v22.22.3` / `10.9.8`.
  - Database: Microsoft SQL Server 2022 Developer Edition running on `localhost` (`MSSQLSERVER` active, connection string in `appsettings.Development.json`).
  - Containerization: `docker` is not installed; on-premise local SQL Server instance is used.
- **Architectural & Technology Constraints:**
  - Backend: ASP.NET Core .NET 8 Minimal API, vertical slice architecture, Dapper for data access, FluentValidation, RFC 7807 `ProblemDetails`, DbUp startup migrations, and OpenAPI (`/swagger/v1/swagger.json`).
  - Frontend: Vue 3.5+ (`<script setup lang="ts">`), Vite 6, Tailwind CSS v4, PrimeVue v4 (Aura preset), Pinia, TanStack Query v5, VeeValidate + Zod, and `openapi-typescript` generating `src/shared/types/api.generated.ts`.
  - Frontend feature-slice isolation: `src/features/{auth,dashboard,issues}/` with zero cross-feature imports.
  - Operational timezone: `Asia/Jakarta` (`WIB`, UTC+7).
  - Currency formatting: Indonesian Rupiah (`id-ID`, e.g. `Rp 1.500.000`).
  - Non-disruptive polling: 10s polling on `S-02` must preserve form draft, open dialogs, and expanded occurrence IDs.

---

# 3. Gap Analysis

| ID | Severity | Status | Gap |
|---|---|---|---|
| GAP-001 | CRITICAL | CLOSED | Missing Issue lifecycle command endpoints and domain enforcement in `src/backend/PrIMA.Api/`: `Issue.cs` aggregate lacks verification enforcement on `Resolve` (`BR-PRIMA-004`), rationale validation on `Dismiss` (`BR-PRIMA-006`), and child collections for notes, remediations, and verifications; endpoints for `TakeIt`, `Assign`, `AddNote`, `RecordRemediation`, `RecordVerification`, `Resolve`, and `Dismiss` do not exist. |
| GAP-002 | CRITICAL | CLOSED | Missing `dbo.IssueActivityLogs` database schema and migration: `PRIMA-DEVELOPMENT-ROADMAP.md` §2 and `S-02` Section 9 require an immutable activity audit trail, but migration `003_IssueActivityLogs.sql` is missing from `src/backend/PrIMA.Api/Migrations/Scripts/`. |
| GAP-003 | CRITICAL | CLOSED | Missing `S-02` Issue Detail Workbench and workflow modals in `src/frontend/`: `src/features/issues/` does not exist; routes `/issues/:id` and workflow modals `M-01` (Dismiss), `M-02` (Remediation), and `M-03` (Verification) are not implemented. |
| GAP-004 | MAJOR | CLOSED | Missing officer directory endpoint: `S-02` secondary actions (`Assign to…`, `Reassign`) require selecting an officer from active EDP team accounts (`dbo.Officers`), but no `GET /api/officers` endpoint exists. |
| GAP-005 | MAJOR | CLOSED | Invalidation of active verification records upon recurring anomaly ingestion or remediation retry: if an issue in `Investigating` has a recorded verification and a recurring finding arrives via `POST /api/ingestion/telemetry`, or if an officer logs a new remediation retry, active verification records must be superseded (`IsSuperseded = 1`) to re-lock the Verification Gate (`BR-PRIMA-004`). |
| GAP-006 | MAJOR | CLOSED | Missing `S-01` dashboard feed row navigation and `S-02` back-navigation state preservation: rows in `ActiveIssueFeed.vue` are not clickable to navigate to `/issues/:id`, and `S-02` back navigation must preserve filter state and history origins. |

---

# 4. Open Questions

| ID | Status | Question | Impact |
|---|---|---|---|
| OQ-001 | CLOSED | In S-02 Action Bar during `Investigating` after remediation is recorded, automated detector re-run (`D-2`) is deferred to Stage 3. How should the Action Bar present verification in Stage 2? | Affects `S-02` Action Bar button layout, state machine transitions, and user actions. |
| OQ-002 | CLOSED | When an officer uses `Assign to…` on an `Open` or `Reopened` issue to assign another officer, which lifecycle transition should occur? | Defines state machine transition, SLA timer handling, and primary action for assigned officer. |
| OQ-003 | CLOSED | How should the backend API structure officer assignment and the `Take It` single-tap action? | Defines endpoint contracts, request DTOs, and validation against `dbo.Officers`. |
| OQ-004 | CLOSED | What is the exact schema and persistence strategy for `dbo.IssueActivityLogs` (`003_IssueActivityLogs.sql`)? | Determines database migration, indexing, audit trail format, and transactional integrity. |
| OQ-005 | CLOSED | How should verification invalidation behave when a new occurrence is ingested on an active `Investigating` issue, or when remediation is retried? | Enforces `BR-PRIMA-004` integrity against stale or invalidated verifications. |
| OQ-006 | CLOSED | In S-02 sticky header, the detector name is specified to link to `S-04` (`/detectors/:detectorId`), which is deferred to Stage 3. How should this detector reference behave in Stage 2? | Prevents broken router navigation or premature Stage 3 scope creep. |
| OQ-007 | CLOSED | Can an issue currently in `Investigating` status be reassigned to another EDP officer? | Accommodates hospital operational shift handovers without disrupting issue lifecycle. |
| OQ-008 | CLOSED | How should the Issue Detail Workbench (`S-02`) fetch the issue and its associated governance records and audit trail? | Determines backend query performance, network round-trips, and data projection. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | Stage 2 builds directly upon the established Stage 1 architecture in `src/backend/PrIMA.Api` and `src/frontend`, reusing existing Dapper connection factories, RFC 7807 problem details handling, and TanStack Query polling patterns. |
| ASM-002 | All operational timestamps are stored in UTC (`DATETIME2`) and formatted in `Asia/Jakarta` (`WIB`, UTC+7) on the frontend via existing `formatWibDateTime`. |
| ASM-003 | Webhook-driven automated re-verification (`D-2`) and background supervisor worker (`MonitoringSupervisorWorker`, `BR-PRIMA-003`, `BR-PRIMA-010`) remain deferred to Stage 3. |
| ASM-004 | Officer identity for self-actions (`Take It`, notes, remediation, verification, dismissal) is extracted from the authenticated JWT claims (`CurrentOfficer` via `HttpContext.User`), ensuring deterministic accountability (`BR-PRIMA-008`). |
| ASM-005 | All mutating issue endpoints require authentication (`[Authorize]`) with flat v1 officer access (`EdpOfficer`, `SystemAnalyst`, `Lead` all have operational access). |
| ASM-006 | Modals `M-01`, `M-02`, and `M-03` are governed by VeeValidate + Zod, strictly enforcing client-side validation (`BR-PRIMA-006` min 20 chars for dismissal rationale) backed by server-side FluentValidation returning `422 Unprocessable Entity`. |
| ASM-007 | 10-second TanStack Query polling on `S-02` preserves active modal visibility, form draft inputs, and expanded occurrence IDs across background refetch cycles. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Resolution bypass without clean verification (`BR-PRIMA-004`). | Issue marked `Resolved` while anomaly remains active in SIMRS. | Enforce verification check both in `Issue` domain entity and `ResolveIssueHandler`, returning `409 Conflict` ProblemDetails if `CleanPassed` verification does not exist; disable `Mark Resolved` button on `S-02` until unlocked. |
| RISK-002 | Stale or invalidated verification used to resolve issue after recurrence. | Anomaly recurred during investigation but officer resolves based on prior outdated verification. | Automatically mark active `VerificationRecords` as `IsSuperseded = 1` inside ingestion transaction when a recurring finding matches an active `Investigating` issue. |
| RISK-003 | Incomplete audit trail due to partial transaction failures. | Activity log missing despite successful issue state change. | Persist `dbo.IssueActivityLogs` within the exact same database transaction as the issue status change or governance record insertion. |
| RISK-004 | Frontend form draft or open modal wiped during 10-second background polling. | Officer typing investigation note or remediation details loses work when background poll completes. | Store draft states in local component reactive state decoupled from query response, and ensure modals operate as isolated dialog components that do not re-mount on parent refetch. |
| RISK-005 | Contract drift between Minimal API endpoints and frontend TypeScript types. | Runtime serialization bugs and broken Vue 3 builds. | Automatically regenerate `api.generated.ts` strictly from `/swagger/v1/swagger.json` via `npm run codegen:api` with zero manual edits. |
| RISK-006 | Broken navigation on detector reference in S-02 header. | Officer clicks detector name and encounters 404 or blank view because `S-04` is deferred to Stage 3. | Render detector name as a styled text badge with an informational tooltip (`'Detector portfolio detail available in Stage 3'`) and no router link. |

---

# 7. Recommendations

The major cross-cutting trade-offs evaluated during feasibility analysis are summarized below as solution alternatives (approved selections are recorded exclusively in Section 8):

## Verification Presentation on S-02 Action Bar (`OQ-001`)

### Option A: Promote `Manual Verification` (`M-03`) to Primary Button
Promote `Manual Verification` to the primary action button on `S-02` when remediation is recorded, and omit `Trigger Verification Re-run` until Stage 3 webhook support is implemented.
- **Advantages:** Clean, zero-confusion operational UX; officers immediately see actionable manual verification flow; avoids disabled dead buttons.
- **Disadvantages:** None. Aligns with Stage 2 manual resolution loop scope.

### Option B: Keep `Trigger Verification Re-run` Primary but Disabled
Keep `Trigger Verification Re-run` visible as primary but disabled with a tooltip indicating automated re-run is scheduled for Stage 3, keeping `Manual Verification` as secondary.
- **Advantages:** Highlights future automation capability.
- **Disadvantages:** Creates visual friction and extra clicks for officers performing daily manual triage.

## Officer Assignment Architecture (`OQ-002`, `OQ-003`)

### Option A: Dedicated `Take It` + `Assign` Endpoints with Active Officer Validation
Implement dedicated `POST /api/issues/{id}/take-it` (no payload, sets `CurrentOfficer` as assignee -> `Investigating`) alongside `POST /api/issues/{id}/assign` (payload: `officerId`, validates against active `dbo.Officers` -> `Acknowledged`), supported by `GET /api/officers`.
- **Advantages:** Minimal network payload for 90% single-tap action; strict relational integrity against active officers; clear lifecycle separation.
- **Disadvantages:** Two endpoints instead of one overloaded endpoint.

### Option B: Overloaded `Assign` Endpoint
Implement a single `POST /api/issues/{id}/assign` accepting `{ isSelfAssign: boolean, officerId?: Guid }`.
- **Advantages:** Single endpoint route.
- **Disadvantages:** More complex request validation and less explicit intent.

## Detail Read Model Projection (`OQ-008`)

### Option A: Composite `GET /api/issues/{id}` via Dapper Multi-Query
Fetch issue metadata, occurrences, notes, remediations, verifications, dismissal, and activity logs in a single SQL round-trip using Dapper `QueryMultipleAsync`.
- **Advantages:** Optimal latency; single network request for S-02; atomic snapshot across child collections.
- **Disadvantages:** Composite DTO payload.

### Option B: Normalized Sub-Endpoints
Expose individual endpoints for `/occurrences`, `/notes`, `/remediations`, `/activity-logs`.
- **Advantages:** Granular payloads.
- **Disadvantages:** 5+ HTTP round-trips on initial load and every 10s polling tick; risk of inconsistent partial state.

---

# 8. Gap Closure Decisions

## GAP-001: Missing Issue Lifecycle Command Endpoints & Domain Enforcement
- **Status:** CLOSED
- **Decision:** Implement complete domain methods in `Issue.cs` (`TakeIt`, `Assign`, `StartInvestigation`, `AddNote`, `RecordRemediation`, `RecordVerification`, `Resolve`, `Dismiss`) and expose corresponding vertical slice Minimal API endpoints in `src/backend/PrIMA.Api/Features/Issues/`:
  - `POST /api/issues/{id}/take-it`
  - `POST /api/issues/{id}/assign`
  - `POST /api/issues/{id}/acknowledge`
  - `POST /api/issues/{id}/start-investigation`
  - `POST /api/issues/{id}/notes`
  - `POST /api/issues/{id}/remediation`
  - `POST /api/issues/{id}/verification/manual`
  - `POST /api/issues/{id}/resolve`
  - `POST /api/issues/{id}/dismiss`
- **Rationale:** Encapsulates business invariant enforcement (`BR-PRIMA-004`, `BR-PRIMA-006`, `BR-PRIMA-008`) directly within the domain aggregate and vertical slices, returning RFC 7807 `ProblemDetails` on validation or invariant failures.
- **Impact:** Backend `Features/Issues/` slices created; `Issue.cs` domain aggregate updated with complete lifecycle logic.
- **Architecture Impact:** Updates `docs/PRIMA-ARCHITECTURE.md §4.2` and Minimal API endpoints table.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## GAP-002: Missing `dbo.IssueActivityLogs` Database Schema & Migration
- **Status:** CLOSED
- **Decision:** Add DbUp migration `003_IssueActivityLogs.sql` creating table `dbo.IssueActivityLogs` with columns:
  - `Id UNIQUEIDENTIFIER PRIMARY KEY`
  - `IssueId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Issues(Id)`
  - `ActivityType NVARCHAR(50) NOT NULL` (`Created`, `Acknowledged`, `Assigned`, `Reassigned`, `InvestigationStarted`, `NoteAdded`, `RemediationRecorded`, `VerificationRecorded`, `Resolved`, `Dismissed`, `Reopened`)
  - `ActorName NVARCHAR(100) NOT NULL`
  - `TimestampUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()`
  - `Summary NVARCHAR(500) NOT NULL`
  - `DetailsJson NVARCHAR(MAX) NULL`
  - Index: `CREATE INDEX IX_IssueActivityLogs_IssueId_Time ON dbo.IssueActivityLogs(IssueId, TimestampUtc DESC);`
  All lifecycle commands and ingestion reopen events atomically write to this table within their respective SQL transactions.
- **Rationale:** Satisfies `PRIMA-DEVELOPMENT-ROADMAP.md` §2 and `S-02` Section 9 immutable audit trail requirement with high query performance and full attribution.
- **Impact:** Migration script added; Dapper persistence methods updated.
- **Architecture Impact:** Updates `docs/PRIMA-ARCHITECTURE.md §8.1` database schema.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## GAP-003: Missing S-02 Issue Detail Workbench & Workflow Modals
- **Status:** CLOSED
- **Decision:** Implement `src/frontend/src/features/issues/` feature slice containing:
  - `views/IssueDetailView.vue` (`/issues/:id`, registered in router)
  - `components/WorkbenchHeader.vue` (sticky header, badges, copyable correlation key, context-sensitive Action Bar)
  - `components/LatestEvidenceCard.vue` (metadata grid, null-safe IDR financial strip, monospace trace box with copy button)
  - `components/OccurrencesTimeline.vue` (collapsible list, newest-first, inline evidence expansion preserving expanded IDs across polls)
  - `components/InvestigationNotesStream.vue` (append-only chronological list, `Ctrl+Enter` composer, draft preservation)
  - `components/VerificationGateCard.vue` (3-step checklist, locks `Mark Resolved` until `CleanPassed`, solid emerald banner when Resolved)
  - `components/RemediationHistoryCard.vue`
  - `components/VerificationRecordCard.vue`
  - `components/DismissalRecordCard.vue`
  - `components/OwnershipTimestampsCard.vue`
  - `components/ActivityLogAccordion.vue` (collapsed by default, audit history)
  - `components/modals/DismissIssueModal.vue` (`M-01`, min 20 chars counter)
  - `components/modals/RecordRemediationModal.vue` (`M-02`, category select + SOP description)
  - `components/modals/ManualVerificationModal.vue` (`M-03`, radio cards for CleanPassed vs. AnomalyStillPresent)
  - `composables/useIssueDetail.ts` (TanStack Query 10s polling, mutation invalidations, modal state management)
- **Rationale:** Complete realization of `docs/PRIMA-SCREEN-INVENTORY.md` (`S-02`, `M-01`–`M-03`) and `docs/PRIMA-UI-UX-GUIDELINE.md §6.3` layout blueprints.
- **Impact:** Frontend issue feature slice fully implemented.
- **Architecture Impact:** Updates `docs/PRIMA-ARCHITECTURE.md §5` frontend layout specifications.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## GAP-004: Missing Officer Lookup Endpoint
- **Status:** CLOSED
- **Decision:** Implement `GET /api/officers` in `Features/Officers/GetOfficersEndpoint.cs`, returning active officers (`Id`, `Username`, `FullName`, `Role`) from `dbo.Officers` for authenticated users.
- **Rationale:** Allows `S-02` `Assign to…` and `Reassign` dropdowns to display real active officer names with validated GUID selection.
- **Impact:** Backend endpoint and frontend officer query composable added.
- **Architecture Impact:** Adds `GET /api/officers` to API contract inventory.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## GAP-005: Verification Invalidation on Active Finding Recurrence & Remediation Retry
- **Status:** CLOSED
- **Decision:** Inside `POST /api/ingestion/telemetry` (`IngestTelemetryHandler`), when a recurring finding matches an active `Investigating` issue, mark any active `dbo.VerificationRecords` as `IsSuperseded = 1` inside the transaction. Furthermore, when an officer records a new remediation (`M-02`), mark any prior un-superseded failed or stale `dbo.VerificationRecords` as `IsSuperseded = 1`.
- **Rationale:** Guarantees `BR-PRIMA-004` integrity: an issue cannot be resolved based on a prior clean verification if telemetry proves the anomaly recurred before resolution, or based on an old verification if a new remediation attempt has begun.
- **Impact:** Ingestion handler and remediation command update verification supersession state.
- **Architecture Impact:** Updates `docs/PRIMA-ARCHITECTURE.md §4.3` and domain invariants.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## GAP-006: S-01 Dashboard Row Navigation & S-02 Back-Navigation
- **Status:** CLOSED
- **Decision:** In `ActiveIssueFeed.vue`, attach click handler to table rows (ignoring clicks on copyable correlation key buttons) navigating to `/issues/${item.id}` via `router.push()`. On `S-02`, the top header back link navigates to `/` (Dashboard), preserving dashboard state upon return.
- **Rationale:** Connects Stage 1 dashboard triage directly to Stage 2 operational resolution workbench (`A-3`).
- **Impact:** `ActiveIssueFeed.vue` and `WorkbenchHeader.vue` navigation wired.
- **Architecture Impact:** Fully aligns router navigation map with Screen Inventory.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

---

## OQ-001: Action Bar Verification Presentation in Stage 2
- **Status:** CLOSED
- **Decision:** Promote `Manual Verification` (`M-03`) to the Primary Action button on `S-02` during `Investigating` after remediation is recorded (`LatestRemediation != null && LatestVerification == null`), and omit `Trigger Verification Re-run` until Stage 3 webhook support is implemented.
- **Rationale:** Webhook-driven automated re-run (`D-2`) is explicitly deferred to Stage 3; promoting `Manual Verification` provides a clear, actionable primary action for EDP officers during Stage 2 without dead buttons.
- **Impact:** `WorkbenchHeader.vue` action bar renders `Manual Verification` as primary.
- **Architecture Impact:** Aligns `docs/PRIMA-UI-UX-GUIDELINE.md §7` with Stage 2 roadmap boundary.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## OQ-002: Officer Assignment Lifecycle Transitions
- **Status:** CLOSED
- **Decision:** When an officer uses `Assign to…` on an `Open` or `Reopened` issue, the issue transitions to `Acknowledged` with the selected officer as `CurrentAssignee`, setting `AcknowledgedAtUtc = now` (which halts unacknowledged SLA escalation timers). When the assigned officer opens the issue, their primary action is `Start Investigating` to move the status to `Investigating`.
- **Rationale:** Respects the domain state machine in `docs/PRIMA-DOMAIN.md §8.1` (`Open -> Acknowledged -> Investigating`), halting unacknowledged SLA timers while requiring the assigned officer to explicitly start their investigation.
- **Impact:** `AssignIssueHandler` sets status to `Acknowledged` for open/reopened issues and sets `CurrentAssignee`.
- **Architecture Impact:** Formally documents state transition semantics in architecture.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## OQ-003: Backend API Structure for Assignment & `Take It`
- **Status:** CLOSED
- **Decision:** Expose dedicated endpoint `POST /api/issues/{id}/take-it` (requiring no payload, setting `CurrentOfficer` as assignee and moving directly to `Investigating`) alongside `POST /api/issues/{id}/assign` (accepting `{ officerId: Guid }` validated against active accounts in `dbo.Officers`) and `GET /api/officers`.
- **Rationale:** Optimizes the 90% common single-tap operational path (`Take It`) while enforcing strict relational integrity for explicit multi-user assignment.
- **Impact:** Slices created in `Features/Issues/TakeIt/` and `Features/Issues/AssignIssue/`.
- **Architecture Impact:** Standardizes API routes and request contracts.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## OQ-004: `dbo.IssueActivityLogs` Schema & Persistence Strategy
- **Status:** CLOSED
- **Decision:** Table schema defined as:
  `dbo.IssueActivityLogs (Id UNIQUEIDENTIFIER PK, IssueId UNIQUEIDENTIFIER FK, ActivityType NVARCHAR(50), ActorName NVARCHAR(100), TimestampUtc DATETIME2, Summary NVARCHAR(500), DetailsJson NVARCHAR(MAX) NULL)`
  with index on `(IssueId, TimestampUtc DESC)`. Every command handler and ingestion reopen handler writes an activity record within its primary SQL transaction.
- **Rationale:** Ensures complete, immutable, atomic audit logging (`BR-PRIMA-009`) with zero risk of partial persistence.
- **Impact:** Migration `003_IssueActivityLogs.sql` and repository helper `InsertActivityLogAsync` created.
- **Architecture Impact:** Updates `docs/PRIMA-ARCHITECTURE.md §8.1`.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## OQ-005: Verification Invalidation Rules on Recurrence & Remediation Retry
- **Status:** CLOSED
- **Decision:** Any new occurrence ingested on an active `Investigating` issue marks active `VerificationRecords` as `IsSuperseded = 1` (re-locking resolution). Logging a new `RemediationRecord` (`M-02`) also supersedes prior failed verification records.
- **Rationale:** Strictly upholds `BR-PRIMA-004`: resolution requires fresh empirical proof (`CleanPassed`) following the most recent state of the anomaly and remediation.
- **Impact:** `IngestTelemetryHandler` and `RecordRemediationHandler` execute supersession updates.
- **Architecture Impact:** Encapsulated in domain state machine and persistence contracts.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## OQ-006: Header Detector Reference Behavior in Stage 2
- **Status:** CLOSED
- **Decision:** In `S-02` sticky header, render the detector name as a styled text badge with an informational tooltip (`'Detector portfolio detail available in Stage 3'`) with no active router link in Stage 2.
- **Rationale:** Screen `S-04` (`/detectors/:detectorId`) belongs to Stage 3; disabling navigation prevents broken routes or throwaway placeholder screens.
- **Impact:** `WorkbenchHeader.vue` detector reference rendered as styled badge with tooltip.
- **Architecture Impact:** Avoids premature Stage 3 route bindings.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## OQ-007: Reassignment in `Investigating` Status
- **Status:** CLOSED
- **Decision:** Allow `Reassign` on issues in `Investigating` status (via a secondary action dropdown and within the Ownership & Timestamps card), updating `CurrentAssignee` while keeping status as `Investigating` and recording an activity log entry (`ActivityType: 'Reassigned'`).
- **Rationale:** Accommodates real hospital EDP shift handovers without requiring issue dismissal or artificial state resets.
- **Impact:** `AssignIssueHandler` permits reassignment during `Investigating`; `OwnershipTimestampsCard.vue` provides reassign button.
- **Architecture Impact:** Expands assignment domain rules.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

## OQ-008: Composite `GetIssueById` Read Projection
- **Status:** CLOSED
- **Decision:** Implement `GET /api/issues/{id}` returning a single composite `IssueDetailDto` (containing issue metadata, occurrences, notes, remediations, verifications, dismissal, and activity logs) using Dapper `QueryMultipleAsync` in one SQL round-trip.
- **Rationale:** Maximizes load performance, simplifies frontend state management in `useIssueDetail.ts`, and guarantees atomic snapshot consistency on each 10s poll tick.
- **Impact:** `GetIssueByIdHandler` executes single multi-result SQL command.
- **Architecture Impact:** Updates CQRS query contract in `docs/PRIMA-ARCHITECTURE.md §4.4`.
- **Resolved By:** User Architecture Interview (`/grill-me`)
- **Resolved Date:** 2026-11-21

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

Stage 2 introduces significant new architectural components, persistence schemas, and API contracts that must be formally recorded in `docs/PRIMA-ARCHITECTURE.md` before implementation planning:
1. **New Database Schema:** Migration `003_IssueActivityLogs.sql` adding `dbo.IssueActivityLogs` and indexing.
2. **Issue Aggregate Expansion:** Full lifecycle domain logic in `Issue.cs` enforcing `BR-PRIMA-004` (Verification Gate), `BR-PRIMA-006` (Dismissal Rationale), `BR-PRIMA-008` (Accountability), and non-destructive governance invalidation on active recurrence.
3. **New API Slices:** 9 new Minimal API command/query endpoints under `Features/Issues/` and `Features/Officers/`.
4. **Composite Query Projection:** Single-trip multi-result Dapper query for `GetIssueById`.
5. **Frontend Feature Architecture:** New `src/features/issues/` feature slice with 11 components, 3 modals, and TanStack Query cache invalidation matrix.

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

All 6 gaps (`GAP-001`–`GAP-006`) and 8 open questions (`OQ-001`–`OQ-008`) are `CLOSED` with full resolution metadata (`0 OPEN`). Every operational, domain, UX, and architectural trade-off has been resolved with explicit stakeholder decisions. Downstream agentic development (`ica-architect`, `ica-implementer`, `ica-reviewer`) has complete, unambiguous specifications to finalize architecture and execute implementation without inventing decisions.

---

# 11. References

Referenced artifacts:
- `ISSUE`: `docs/issues/IS002-PRIMA-STAGE-2-ISSUE.md`
- `DOMAIN`: `docs/PRIMA-DOMAIN.md`
- `FEATURE` (pre-existing specifications): `docs/PRIMA-DEVELOPMENT-ROADMAP.md`, `docs/PRIMA-USE-CASE-DISCOVERY.md`, `docs/PRIMA-SCREEN-INVENTORY.md`, `docs/PRIMA-UI-UX-GUIDELINE.md`, `docs/PrIMA - Product Definition.md`
- Pre-existing Technical Architecture: `docs/PRIMA-ARCHITECTURE.md`
- Baseline Stage 1 Artifacts: `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md`, `IS001-PRIMA-STAGE-1-FEASIBILITY-ASSESSMENT.md`, `IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md`

Referenced codebase locations:
- `src/backend/PrIMA.Api/Domain/Issues/`
- `src/backend/PrIMA.Api/Features/Issues/`
- `src/backend/PrIMA.Api/Features/Officers/`
- `src/backend/PrIMA.Api/Infrastructure/Persistence/`
- `src/backend/PrIMA.Api/Migrations/Scripts/`
- `src/frontend/src/features/issues/`
- `src/frontend/src/features/dashboard/components/ActiveIssueFeed.vue`
- `src/frontend/src/app/router/index.ts`
