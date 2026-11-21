# ISSUE

## Metadata

ID: PRIMA-STAGE-2
Type: CHANGE-REQUEST
Status: OPEN
Title: Implement PrIMA Stage 2 — Issue Triage, Investigation & Verification-Gated Resolution

## Source

Reported By: User
Reported Date: 2026-11-21

## Description

Implement Stage 2 ("Issue Triage, Investigation & Verification-Gated Resolution") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`).

This change request covers delivering the complete operational issue resolution capabilities of PrIMA (Proactive Inspection Monitoring App), centering on the single-screen Issue Detail Workbench (`S-02`) and its workflow modals (`M-01`, `M-02`, `M-03`). This enables EDP / IT Support officers to transition active issues discovered in Stage 1 through explicit triage, structured evidence investigation, append-only notes, documented remediation, mandatory manual verification, and definitive resolution or dismissal with comprehensive audit logging.

## Desired Outcome

From a business and operational perspective, completing Stage 2 delivers the "Complete End-to-End Manual Issue Resolution Loop" milestone:

1. **Issue Detail Workbench (`S-02`, Use Case `A-3`):** EDP officers can view a comprehensive single-screen workbench for any active, resolved, or dismissed issue, featuring sticky header navigation, copyable correlation keys, occurrence counter, detector link, severity/status/scope badges, context-sensitive action bar driven by issue state, structured telemetry evidence inspection, chronological occurrence history, and audit history.
2. **Issue Triage & Accountability (`B-1`, `B-2`, `BR-PRIMA-008`):** Officers can acknowledge issues (`Open`/`Reopened` -> `Acknowledged`), self-assign ownership via a single-action "Take It" flow (`Acknowledged` + self-assignment -> `Investigating`), or assign/reassign issues to specific seeded EDP officers with explicit attribution and accountability.
3. **Structured Evidence & Occurrence Review (`C-1`, `C-2`):** Officers can inspect the latest telemetry evidence (metadata grid, financial variance comparison with IDR formatting, trace details) and review chronological occurrence snapshots without loss of interactive state during background dashboard polling.
4. **Append-Only Investigation Notes (`C-3`):** Officers can record timestamped, officer-attributed diagnostic notes to maintain a collaborative investigation history.
5. **Documented Remediation Logging (`D-1`, Modal `M-02`):** Officers can record standard operational remediation actions (categorized by action type with SOP descriptions) while in the `Investigating` state.
6. **Mandatory Verification Gate (`D-3`, `D-4`, Modal `M-03`, `BR-PRIMA-004`):** Officers can record manual verification outcomes (`CleanPassed` vs. `AnomalyStillPresent`). The system strictly prevents marking an issue as `Resolved` unless a successful verification (`CleanPassed`) has been recorded following remediation. Unsuccessful verification keeps the issue in `Investigating` and allows subsequent remediation.
7. **Documented Issue Dismissal (`B-3`, Modal `M-01`, `BR-PRIMA-006`):** Officers can dismiss an issue across valid active states (`Open`, `Acknowledged`, `Investigating`, `Reopened`) with mandatory documented justification rationale meeting minimum length requirements (at least 20 characters) and structured dismissal reasons.
8. **Activity Log & Audit Trail Persistence (`BR-PRIMA-009`):** All status transitions, assignment changes, remediation entries, verification milestones, and dismissal actions are immutably persisted and viewable in the issue activity log.

## Current Situation

- Stage 1 ("Platform Foundation & Core Telemetry Ingestion") is complete and operational in the repository (`src/backend` and `src/frontend`), providing the Minimal API host, initial database schema, JWT officer authentication (`S-00`), telemetry ingestion with deduplication/reopening, and the active issue dashboard (`S-01`).
- The operational resolution loop is currently incomplete: clicking on an issue on `S-01` cannot navigate to an Issue Detail Workbench (`S-02`), and officers have no mechanism in the application to acknowledge, assign, investigate, remediate, verify, resolve, or dismiss issues.
- The backend lacks commands and domain logic for issue state transitions beyond ingestion creation/reopening (`Acknowledge`, `Assign`, `AddNote`, `RecordRemediation`, `RecordVerification`, `Resolve`, `Dismiss`), and database storage for issue activity logging (`dbo.IssueActivityLogs`) is not yet present.

## Evidence

- `docs/PRIMA-DEVELOPMENT-ROADMAP.md` — Section 1 Delivery Strategy (line 22), Section 2 Stage 2 Details (lines 87–130), and Traceability Matrix (lines 252–299)
- `docs/PRIMA-DOMAIN.md` — Issue lifecycle state machine, Capabilities, and Business Rules `BR-PRIMA-004`, `BR-PRIMA-006`, `BR-PRIMA-008`, `BR-PRIMA-009`
- `docs/PRIMA-USE-CASE-DISCOVERY.md` — Use Cases `A-3` (View Issue Detail), `B-1` (Acknowledge Issue), `B-2` (Assign Issue / Take It), `B-3` (Dismiss Issue), `C-1` (Review Telemetry Evidence), `C-2` (Review Occurrence History), `C-3` (Add Investigation Note), `D-1` (Record Remediation Action), `D-3` (Record Manual Verification), `D-4` (Mark Issue Resolved)
- `docs/PRIMA-SCREEN-INVENTORY.md` — Screen `S-02` (Issue Detail Workbench) and Modals `M-01` (Dismiss Issue), `M-02` (Record Remediation), `M-03` (Manual Verification)
- `docs/PRIMA-UI-UX-GUIDELINE.md` — Section 4 (`S-02` Component Hierarchy & Layout), Section 5 (Modals `M-01`–`M-03`), Section 7 (Interactive State Machine & Action Bar Rules)
- `docs/PRIMA-ARCHITECTURE.md` — Core issue aggregate design, API contracts, and CQRS patterns
- `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md` & `IS001-PRIMA-STAGE-1-IMPLEMENTATION-PLAN.md` — Completed Stage 1 baseline artifacts and implementation commit `b2b3126`

## Notes

- **Scope Boundary:** This ISSUE covers Stage 2 deliverables only. Automated detector portfolio management (`S-03`–`S-06`), autonomous supervisor worker / background silence & escalation (`BR-PRIMA-003`, `BR-PRIMA-005`, `BR-PRIMA-010`), and webhook-driven automated re-verification (`D-2`) belong to Stage 3. Retrospective reporting (`S-07`) belongs to Stage 4. Real SIMRS detector integration belongs to Stage 5.
- **Specification Alignment:** As noted in `docs/PRIMA-DEVELOPMENT-ROADMAP.md` §4 item 1, Stage 2 incorporates `003_IssueActivityLogs.sql` to support the Section 9 activity log requirement on `S-02`.
- **Intake Boundary:** In accordance with `ics-issue-creation` and SDLC role separation, this ISSUE artifact documents the formal change request and desired business outcomes without prescribing architecture solutions, implementation steps, or test plans.
