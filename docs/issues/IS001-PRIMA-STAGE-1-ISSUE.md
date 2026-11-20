# ISSUE

## Metadata

ID: PRIMA-STAGE-1
Type: CHANGE-REQUEST
Status: OPEN
Title: Implement PrIMA Stage 1 — Platform Foundation & Core Telemetry Ingestion

## Source

Reported By: User
Reported Date: 2026-11-20

## Description

Implement Stage 1 ("Platform Foundation & Core Telemetry Ingestion") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`).

This change request covers establishing the initial operational foundation of PrIMA (Proactive Inspection Monitoring App) so that external automated detectors can submit standardized inspection telemetry into the platform and EDP / IT Support officers can authenticate and monitor active operational issues in near real time.

## Desired Outcome

From a business and operational perspective, completing Stage 1 delivers the "Walking Skeleton & Live Ingestion Pipeline" milestone:

1. **Authenticated Officer Access (`S-00`):** EDP / IT Support officers can sign in with individual accounts so operational sessions are authenticated and subsequent actions are attributable to an identified officer, with clear invalid-credential feedback and post-login redirect restoration.
2. **Standardized Telemetry Ingestion:** Authenticated external detectors can report `Passed` and `FindingDiscovered` inspection outcomes into PrIMA.
3. **Deterministic Anomaly Deduplication (`BR-PRIMA-001`) & Immutable Occurrence Audit Trail (`BR-PRIMA-009`):** New findings create an `Open` issue with an immutable initial occurrence snapshot; recurring findings matching an existing active issue's correlation identity append a new immutable occurrence to that issue without creating duplicate issues.
4. **Automatic Recurrence Reopening (`BR-PRIMA-002`):** Findings matching a previously `Resolved` or `Dismissed` issue automatically transition that issue to `Reopened`, record the new occurrence, and clear prior verification/dismissal state.
5. **Live Active Issue Dashboard (`S-01`, Use Cases `A-1` & `A-2`):** Authenticated officers can view active issue summary counters (`Critical`, `High`, `Open`, `Reopened`, `Resolved Today`), browse active issues ordered by urgency and recency (strictly excluding `Resolved` and `Dismissed` issues), filter by status/scope and search by text, see contextual empty states, and receive automatic periodic dashboard updates without visual disruption or resetting active filters.
6. **Baseline Seed Data & Telemetry Simulation:** Initial officer accounts, baseline detector entries, and a telemetry simulation utility are available to exercise clean runs, new findings, duplicate findings, and recurrence-after-close scenarios end-to-end.

## Current Situation

- The repository currently contains only product, domain, use-case, screen inventory, UI/UX, architecture, and staged development roadmap specifications under `docs/` (initial commit `a27e89a`).
- The `src/` directory is empty; no backend service, database setup, officer authentication, telemetry ingestion capability, frontend application, or telemetry simulator is implemented yet.

## Evidence

- `docs/PRIMA-DEVELOPMENT-ROADMAP.md` — Stage Overview Summary (line 39), Stage 1: Platform Foundation & Core Telemetry Ingestion (lines 49–84), and Traceability Matrix (lines 252–299)
- `docs/PrIMA - Product Definition.md` — Product Overview, Product Objectives, and Scope of Monitoring
- `docs/PRIMA-DOMAIN.md` — Ubiquitous Language, Capabilities, and Business Rules `BR-PRIMA-001`, `BR-PRIMA-002`, and `BR-PRIMA-009`
- `docs/PRIMA-USE-CASE-DISCOVERY.md` — Use Cases `A-1` (View Issue Dashboard) and `A-2` (Filter & Search Issues)
- `docs/PRIMA-SCREEN-INVENTORY.md` — Screens `S-00` (Officer Login) and `S-01` (Issue Dashboard)
- `docs/PRIMA-UI-UX-GUIDELINE.md` — UI/UX guidelines applicable to `S-00` and `S-01`
- `docs/PRIMA-ARCHITECTURE.md` — Pre-existing architectural specification referenced by the roadmap
- Workspace inspection confirming `src/` contains no implementation files and `docs/issues/` contains no prior ISSUE artifacts

## Notes

- **Scope Boundary:** This ISSUE covers Stage 1 only. Stage 2 (`S-02` Issue Detail Workbench, triage, investigation notes, remediation, verification gate, resolution, and dismissal modals `M-01`–`M-03`), Stage 3 (Detector portfolio `S-03`–`S-06`, autonomous silence/escalation supervision, `ExecutionFailed` synthesis, and webhook triggers), Stage 4 (`S-07` History & Reports and v1.0 quality gate), and Stage 5 (Pilot SIMRS Detector Suite and hospital rollout) are outside the scope of this ISSUE.
- **Pre-existing Specifications:** Companion specifications (including `docs/PRIMA-DOMAIN.md` and `docs/PRIMA-ARCHITECTURE.md`) already exist in `docs/`; downstream Discovery and Feasibility Assessment will determine how they align with formal SDLC Permanent Knowledge artifacts and implementation planning.
- **Intake Boundary:** In accordance with `ics-issue-creation`, this artifact captures what is requested and does not define feasibility analysis, architecture decisions, implementation details, planning, or testing strategy.