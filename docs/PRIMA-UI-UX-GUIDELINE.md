# PRIMA — UI/UX DESIGN SYSTEM & AGENT IMPLEMENTATION GUIDELINE
**System:** PrIMA (Proactive Inspection Monitoring App)  
**Document:** UI/UX Design System & Agent Implementation Guideline  
**Target Audience:** AI Coding Agents & Frontend Engineers  
**Frontend Stack:** Vue 3.5+ (`<script setup lang="ts">`) · Vite 6 · Tailwind CSS v4 · PrimeVue v4 (Aura Preset) · TanStack Query v5 · Pinia · VeeValidate + Zod  
**Companion Documents:**
- [PrIMA Screen Inventory](./PRIMA-SCREEN-INVENTORY.md) (Functional Screen & Modal Inventory)
- [PrIMA Architecture Specification](./PRIMA-ARCHITECTURE.md) (Technical Stack, Vue 3 Feature Slices, API & State Architecture)
- [PrIMA Domain Specification](./PRIMA-DOMAIN.md) (Canonical Ubiquitous Language, Business Rules `BR-PRIMA-001`–`010`, State Machines)
- [PrIMA Use Case Discovery](./PRIMA-USE-CASE-DISCOVERY.md) (Actor & Use Case Mapping)

---

## 1. UX Philosophy & AI Agent Directives ("The Golden Rules")

PrIMA is an operational monitoring and exception-management workbench built for an **EDP / IT Support Officer** monitoring a Hospital Information System (SIMRS). AI coding agents implementing `src/frontend/` **must** follow these five non-negotiable UX directives:

1. **Desktop-First Operational Workbench:**
   Optimize for an EDP officer working at a desktop monitor (`>= 1024px`) triaging financial journal imbalances, sync failures, and service probes. Prioritize high information density, scannable tabular alignment, and minimal clicks over decorative whitespace, marketing banners, or oversized cards. Support tablet viewports (`< 1024px`) cleanly via a collapsible sidebar drawer.
2. **High-Contrast Light Mode Default:**
   Use a clean, slate-grounded light theme (`bg-slate-50` app canvas, `bg-white` surfaces, `border-slate-200` hairlines, `text-slate-900` primary text). High-contrast light surfaces maximize legibility for dense financial currency figures, SQL table names, record keys, and stack traces under hospital office lighting.
3. **Single-Screen Lifecycle Continuity (`S-02`):**
   The entire issue lifecycle (Triage → Investigation → Remediation → Verification → Resolution) lives inside **S-02 Issue Detail**. Never build multi-step wizard routes or separate screens per lifecycle stage. Use inline updates and the three focused modals (`M-01`, `M-02`, `M-03`).
4. **Proactive Constraint Visibility (No Mystery Errors):**
   Domain invariants—especially **BR-PRIMA-004 (Mandatory Verification Gate)** and **BR-PRIMA-006 (Dismissal Justification)**—must be visually communicated *before* the officer clicks an action:
   - Display a 3-step Verification Gate checklist in `S-02`.
   - Keep `Mark Resolved` visibly locked/disabled with an explicit reason until a `CleanPassed` verification record exists.
   - Show live minimum character validation (`0 / 20 min`) in `M-01`.
5. **Non-Disruptive Real-Time Polling:**
   TanStack Query polls automatically every **10 seconds** (Dashboard & Issue feeds) and **15 seconds** (Detectors). Background polling (`isFetching`) must **never** flash full-page skeletons, reset scroll position, collapse expanded occurrences/run details, clear filter chips, wipe draft investigation notes, or close open modals.

---

## 2. Design Tokens & Semantic Color System (Tailwind CSS v4 + PrimeVue v4 Aura)

AI agents **must** map domain values strictly to the semantic Tailwind CSS v4 tokens below. Never invent ad-hoc colors for statuses, severities, or outcomes.

### 2.1 Surface, Border & Text Hierarchy

| Token Role | Tailwind CSS v4 Classes | Usage |
|---|---|---|
| **App Canvas** | `bg-slate-50 text-slate-900 antialiased` | Root viewport background behind cards and workbench panels |
| **Primary Card / Panel** | `bg-white border border-slate-200 rounded-lg shadow-2xs` | Feed containers, evidence panels, KPI tiles, detail cards |
| **Sticky Header Surface** | `bg-white/95 backdrop-blur-xs border-b border-slate-200` | Sticky `S-02` issue header and sticky filter headers |
| **Nested Evidence Well** | `bg-slate-50 border border-slate-200/80 rounded-md` | Structured key-value evidence cells and financial comparison wells |
| **Code / Trace Block** | `bg-slate-900 text-slate-100 font-mono text-xs rounded-md p-3.5` | `TraceString` and `FailureReason` stack trace blocks |
| **Text — Primary** | `text-slate-900` | Issue titles, detector names, monetary values, headings |
| **Text — Secondary** | `text-slate-600` | Descriptions, timestamps, officer names, table labels |
| **Text — Muted / Caption** | `text-slate-500` | Section eyebrows (`uppercase tracking-wider`), helper captions |

### 2.2 Issue Severity Tokens (`IssueSeverity`)

> Apply a `4px` left border accent (`border-l-4`) on `S-01` feed rows alongside the severity pill so officers can scan criticality peripherally.

| Enum Value | Badge / Pill Classes | Dot Indicator | Feed Row Left Accent |
|---|---|---|---|
| `Critical` | `bg-rose-50 text-rose-700 border border-rose-200 font-semibold` | `bg-rose-600` | `border-l-4 border-l-rose-600` |
| `High` | `bg-orange-50 text-orange-700 border border-orange-200 font-semibold` | `bg-orange-500` | `border-l-4 border-l-orange-500` |
| `Medium` | `bg-amber-50 text-amber-800 border border-amber-200 font-medium` | `bg-amber-500` | `border-l-4 border-l-amber-400` |
| `Low` | `bg-sky-50 text-sky-700 border border-sky-200 font-medium` | `bg-sky-500` | `border-l-4 border-l-sky-400` |

**Dynamic Escalation Indicator (`EscalationLevel > 0`, `BR-PRIMA-003`):**
- Render immediately adjacent to the severity badge:
  - Classes: `inline-flex items-center gap-1 bg-rose-100 text-rose-800 border border-rose-300 font-bold text-[11px] px-1.5 py-0.5 rounded`
  - Label: `↑ Escalated` (or `↑ +${escalationLevel}` in dense feed rows)
  - Tooltip (`v-tooltip`): `"Severity auto-escalated (SLA timeout or ≥5 occurrences)"`

### 2.3 Issue Lifecycle Status Tokens (`IssueStatus`)

| Enum Value | Chip Classes | PrimeIcon | Meaning in UI |
|---|---|---|---|
| `Open` | `bg-rose-50 text-rose-700 border border-rose-200` | `pi pi-exclamation-circle` | Unacknowledged; SLA escalation timer running |
| `Acknowledged` | `bg-sky-50 text-sky-700 border border-sky-200` | `pi pi-eye` | Acknowledged; awaiting active assignment/investigation |
| `Investigating` | `bg-indigo-50 text-indigo-700 border border-indigo-200` | `pi pi-search` | Actively owned and under investigation/remediation |
| `Reopened` | `bg-purple-50 text-purple-700 border border-purple-300 font-semibold` | `pi pi-refresh` | Recurred after `Resolved`/`Dismissed` (`BR-PRIMA-002`) |
| `Resolved` | `bg-emerald-50 text-emerald-700 border border-emerald-200` | `pi pi-check-circle` | Passed Verification Gate (`BR-PRIMA-004`); read-only |
| `Dismissed` | `bg-slate-100 text-slate-600 border border-slate-200` | `pi pi-ban` | Closed with `DismissalRecord` (`BR-PRIMA-006`); read-only |

### 2.4 Monitoring Scope Tokens (`MonitoringScope`)

| Domain Key | Display Label | Badge Classes |
|---|---|---|
| `DataIntegrity` | `Data Integrity` | `bg-blue-50 text-blue-700 border border-blue-200` |
| `ProcessIntegrity` | `Process Integrity` | `bg-violet-50 text-violet-700 border border-violet-200` |
| `DataSync` | `Data Synchronization` | `bg-cyan-50 text-cyan-700 border border-cyan-200` |
| `Infrastructure` | `Infrastructure & Service` | `bg-slate-100 text-slate-700 border border-slate-300` |

### 2.5 Detector Health (`DetectorHealth`) & Admin Status (`AdminStatus`) Tokens

| Enum | Value | Dot / Icon Indicator | Badge Classes |
|---|---|---|---|
| `DetectorHealth` | `Healthy` | `w-2 h-2 rounded-full bg-emerald-500` | `bg-emerald-50 text-emerald-700 border border-emerald-200` |
| `DetectorHealth` | `Silent` | `w-2 h-2 rounded-full bg-amber-500 animate-pulse` | `bg-amber-50 text-amber-800 border border-amber-300` |
| `DetectorHealth` | `Failing` | `w-2 h-2 rounded-full bg-rose-600 animate-pulse` | `bg-rose-50 text-rose-700 border border-rose-200` |
| `AdminStatus` | `Active` | `pi pi-play-circle` | `bg-emerald-50 text-emerald-700 border border-emerald-200` |
| `AdminStatus` | `Paused` | `pi pi-pause-circle` | `bg-amber-50 text-amber-700 border border-amber-200` |
| `AdminStatus` | `Retired` | `pi pi-lock` | `bg-slate-100 text-slate-500 border border-slate-200 line-through` |

### 2.6 Inspection Run Outcome & Verification Result Tokens

| Category | Enum Value | Display Label | Badge Classes | Dot |
|---|---|---|---|---|
| `Outcome` | `Passed` | `Passed` | `bg-emerald-50 text-emerald-700 border border-emerald-200` | `bg-emerald-500` |
| `Outcome` | `FindingDiscovered` | `Finding Discovered` | `bg-amber-50 text-amber-800 border border-amber-200` | `bg-amber-500` |
| `Outcome` | `ExecutionFailed` | `Execution Failed` | `bg-rose-50 text-rose-700 border border-rose-200` | `bg-rose-600` |
| `Verification` | `CleanPassed` | `Clean Passed` | `bg-emerald-50 text-emerald-700 border border-emerald-200` | `bg-emerald-500` |
| `Verification` | `AnomalyStillPresent` | `Anomaly Still Present` | `bg-rose-50 text-rose-700 border border-rose-200` | `bg-rose-600` |

---

## 3. Typography, Regional Formatting & Telemetry Data Presentation

### 3.1 Font Pairing & Typographic Scale

- **Primary UI Font (`--font-sans`):** `'Inter', system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif`
- **Technical Monospace Font (`--font-mono`):** `'JetBrains Mono', 'Fira Code', ui-monospace, SFMono-Regular, Menlo, monospace`

| Role | Tailwind Classes | Usage |
|---|---|---|
| **Screen / Issue Title** | `text-lg font-semibold text-slate-900 leading-snug` | `S-01`..`S-07` page titles, `S-02` sticky header title |
| **Section Header (Eyebrow)** | `text-xs font-semibold uppercase tracking-wider text-slate-500` | Card headers inside `S-02`, `S-04`, `S-07` |
| **Feed Row Title** | `text-sm font-medium text-slate-900 line-clamp-2` | Issue title in `S-01` and `S-07` |
| **Body / Notes Prose** | `text-sm text-slate-700 leading-relaxed` | Investigation notes, remediation descriptions, dismissal rationale |
| **Chips / Table Meta** | `text-xs font-medium` | Status chips, detector metadata, relative timestamps |
| **Technical Payload** | `font-mono text-xs` | `CorrelationKey`, `SourceTable`, `RecordKeys`, `TraceString`, `DurationMs` |
| **Financial / Numeric Metrics** | `font-mono tabular-nums` | `DebitTotal`, `CreditTotal`, `VarianceDelta`, KPI counts, durations |

### 3.2 Mandatory Monospace & Copy-to-Clipboard Rules

1. **Always render technical identifiers in `font-mono`:**
   `CorrelationKey`, `SourceTable`, `RecordKeys`, `TraceString`, `FailureReason`, and `DurationMs`.
2. **One-Click Copy Affordance (`<CopyableValue>` shared primitive):**
   Attach an inline copy button (`pi pi-copy` → swaps to `pi pi-check text-emerald-600` for `1500ms` + tooltip `"Copied!"`) beside:
   - `CorrelationKey` (in `S-02` header & metadata card)
   - `RecordKeys` (in `S-02` telemetry evidence & occurrence snapshots)
   - `TraceString` / `FailureReason` (top-right corner of the monospace trace box in `S-02` and `S-06`)
   This enables the EDP officer to paste keys directly into SSMS or SIMRS admin tools without manual text selection.

### 3.3 Language & Indonesian Regional Formatting (`id-ID` + `WIB`)

- **UI Labels, Navigation & Enum Badges:** Strictly **English**, matching the Ubiquitous Language in [PRIMA-DOMAIN.md](./PRIMA-DOMAIN.md) and [PRIMA-SCREEN-INVENTORY.md](./PRIMA-SCREEN-INVENTORY.md) (e.g., `Take It`, `Record Remediation`, `Trigger Verification Re-run`, `Mark Resolved`, `Clean Passed`).
- **Financial Evidence Formatting (`DebitTotal`, `CreditTotal`, `VarianceDelta`):**
  - Format using `Intl.NumberFormat('id-ID', { style: 'currency', currency: 'IDR', minimumFractionDigits: 2 })` (example output: `Rp 1.500.000,00`).
  - Always apply `font-mono tabular-nums text-right`.
  - When `VarianceDelta != null && VarianceDelta !== 0`, highlight `VarianceDelta` inside a rose alert box (`bg-rose-50 text-rose-700 border border-rose-200 font-semibold px-2.5 py-1 rounded`).
  - **Null Safety:** If `DebitTotal`, `CreditTotal`, and `VarianceDelta` are all `null` (common for `ProcessIntegrity`, `DataSync`, or `Infrastructure` issues), **do not** render `Rp 0,00` or empty dashes—hide the financial comparison strip completely.
- **Timestamp Formatting (`shared/utils/dateUtils.ts` with `date-fns`):**
  - **In Feeds (`S-01`, `S-03`, `S-06`):** Display relative time (`Just now`, `3m ago`, `2h ago`, `2d ago`) with a `v-tooltip` revealing the exact `Asia/Jakarta` (`WIB`) timestamp (`20 Nov 2026, 13:21:05 WIB`).
  - **In Detail Timelines, Notes, Audit Logs & History (`S-02`, `S-07`):** Display explicit `WIB` absolute timestamp (`20 Nov 2026, 13:21 WIB`) alongside subtle relative context `(3h ago)`.
- **Duration & Cadence Formatting:**
  - `DurationMs`: `< 1000` → `${ms} ms` (e.g., `420 ms`); `>= 1000` → `${(ms / 1000).toFixed(2)} s` (e.g., `1.42 s`).
  - `ExpectedCadenceSeconds`: `< 60` → `Every ${s}s`; `< 3600` → `Every ${m} min`; `>= 3600` → `Every ${h} hr`.

---

## 4. App Shell, Desktop Grid & Navigation Architecture

### 4.1 Authenticated Workbench Shell (`AppLayout.vue`)

Desktop-first layout (`>= 1024px`) with a fixed `240px` (`w-60`) left sidebar and fluid operational canvas (`flex-1 min-w-0 bg-slate-50`). Below `1024px` (tablet), the sidebar collapses into a slide-over PrimeVue `Drawer` toggled by a compact top hamburger bar.

```text
+-----------------------------------------------------------------------------------+
| [Sidebar 240px Fixed]     | [Main Content Area — flex-1 bg-slate-50]              |
|                           |  max-w-[1440px] mx-auto px-6 py-5                     |
|  PrIMA                    |                                                       |
|  SIMRS Proactive Monitor  |  (Active Route View: S-01 / S-02 / S-03..S-07)        |
|  -----------------------  |                                                       |
|  > Dashboard        [12]  |                                                       |
|    Detectors         [!]  |                                                       |
|    History & Reports      |                                                       |
|                           |                                                       |
|                           |                                                       |
|  -----------------------  |                                                       |
|  ● Live · Polling 10s     |                                                       |
|  [AB] Ahmad Budi          |                                                       |
|       EdpOfficer  [Exit]  |                                                       |
+-----------------------------------------------------------------------------------+
```

**Sidebar Rules:**
1. **Navigation Items (3 Primary Routes Only):**
   - `Dashboard` (`/`) → `S-01`: Show a right-aligned live counter pill of active `Open + Reopened` issues. Color the pill rose (`bg-rose-600 text-white`) when `criticalCount > 0 || reopenedCount > 0`; otherwise slate (`bg-slate-200 text-slate-700`).
   - `Detectors` (`/detectors`) → `S-03`: Show an amber/rose dot (`bg-rose-500` or `bg-amber-500`) if any detector has `HealthStatus` of `Failing` or `Silent`.
   - `History & Reports` (`/history`) → `S-07`: No badge needed.
2. **Active Link Style:** `bg-slate-900 text-white font-medium rounded-md` (high-contrast active indicator). Inactive link: `text-slate-600 hover:bg-slate-100 hover:text-slate-900 rounded-md`.
3. **Live Sync Status Pill:**
   - Healthy polling: Emerald dot (`bg-emerald-500`) + `"Live · 10s"` (or `"Live · 15s"` on Detectors). Subtle spin icon only when `isFetching` is active.
   - Network failure (`>= 2` consecutive failed polls): Amber dot (`bg-amber-500`) + `"Reconnecting..."`.
4. **Officer Session Footer (`Pinia auth.store.ts`):**
   - Initials avatar (`w-8 h-8 rounded-full bg-slate-800 text-white text-xs font-semibold`), `FullName`, `Role` tag (`EdpOfficer`, `SystemAnalyst`, `Lead`), and `Sign Out` button (`pi pi-sign-out`, tooltip `"Sign Out"`).

### 4.2 Unauthenticated Shell (`AuthLayout.vue` / `S-00 Officer Login`)

Aligned with JWT authentication in [PRIMA-ARCHITECTURE.md](./PRIMA-ARCHITECTURE.md):
- Full-height centered canvas (`min-h-screen bg-slate-50 flex items-center justify-center p-4`).
- Minimalist login card (`w-full max-w-sm bg-white border border-slate-200 rounded-xl shadow-xs p-6`).
- Zero marketing graphics or "Sign Up / Forgot Password" links (officers are seeded via DbUp migration `002_SeedInitialUsers.sql`).

---

## 5. PrimeVue v4 (Aura) Component Mapping Matrix

Configure PrimeVue v4 using the **Aura** preset in `src/app/config/primevue.ts` with Tailwind CSS v4 utilities for layout and custom semantic domain wrappers in `src/shared/components/`.

| UI Pattern | PrimeVue v4 Component | Required Configuration / Props | Wrapper / Usage Rule |
|---|---|---|---|
| **Virtualized Feeds** (`S-01`, `S-03`, `S-06`, `S-07`) | `DataTable` or `VirtualScroller` | `scrollable`, `scrollHeight="flex"` (or calculated viewport height), `virtualScrollerOptions="{ itemSize: 64 }"`, **no `paginator`** on `S-01`, `S-03`, `S-06` | Use clickable row styling (`cursor-pointer hover:bg-slate-50 transition-colors`). |
| **Domain Badges & Chips** | `Tag` (or styled `<span>`) | `rounded-full` for scopes/counts; `rounded-md` for status/severity | Always wrap in `shared/components/`: `<IssueSeverityBadge>`, `<IssueStatusChip>`, `<ScopeTag>`, `<DetectorHealthBadge>`. |
| **Filter Chip Strips** | Custom button group / `SelectButton` | Compact `px-2.5 py-1 text-xs rounded-md border` | Selected chip: `bg-slate-900 text-white border-slate-900`. Unselected: `bg-white text-slate-600 border-slate-200 hover:bg-slate-50`. |
| **Search Input** | `IconField` + `InputIcon` + `InputText` | `placeholder="Search title, detector, or key..."`, `size="small"` | Debounce input changes by `300ms` before updating query filter ref. Include clear `×` button when non-empty. |
| **Workflow Modals** (`M-01`, `M-02`, `M-03`) | `Dialog` | `:modal="true"`, `:draggable="false"`, `:closable="true"`, `class="w-full max-w-lg"` | Prevent closing via outside click while mutation `isPending`. Focus first input automatically. |
| **Destructive Confirmation** (`Retire Detector`) | `ConfirmDialog` (`useConfirm()`) | `acceptProps="{ severity: 'danger', label: 'Retire Detector' }"`, `rejectProps="{ severity: 'secondary', outlined: true }"` | Never use native `window.confirm()` or `window.alert()`. |
| **Collapsible Panels** (`S-02` Occurrences, Activity Log) | `Panel` or `Accordion` | `toggleable`, `:collapsed="true"` (by default for Activity Log) | Keep expanded state tracked by entity ID so polling does not collapse panels. |
| **Form Controls** (`S-05`, `M-01`–`M-03`) | `InputText`, `Textarea`, `Select`, `InputNumber`, `Password`, `RadioButton` | `size="small"` or standard compact density; `Textarea` uses `autoResize` | Always bind through **VeeValidate** (`useForm` + `@vee-validate/zod`) and display inline `<small class="text-rose-600">`. |
| **Date Range Picker** (`S-06`, `S-07`) | `DatePicker` + Preset Buttons | `selectionMode="range"`, `dateFormat="dd M yy"`, `:manualInput="false"` | Pair with single-click presets (`Today`, `This Week`, `This Month`, `Last Month`). |
| **Action Feedback** | `Toast` (`useToast()`) | `position="bottom-right"`, `life: 3500` | Fire ONLY on explicit user actions/mutations (`Take It`, `Save`, `Resolve`), **never** on background polling. |
| **Initial Loading** | `Skeleton` | Match exact row/card height of target screen | Render ONLY when `isLoading` (no cached data yet), never during background `isFetching`. |

---

## 6. Screen-by-Screen Spatial Blueprints (`S-00`–`S-07` & `M-01`–`M-03`)

### 6.1 `S-00` — Officer Login (`AuthLayout.vue`)
- **Route:** `/login`
- **Layout:** Centered `max-w-sm` card containing:
  1. App Header: `PrIMA` badge + `"Proactive Inspection Monitoring App"` + `"EDP / IT Support Sign In"`
  2. Inline Error Banner (`Message severity="error"`): Shown on `401 Unauthorized` (`"Invalid username or password."`)
  3. `Username` (`InputText`, required, autofocus)
  4. `Password` (`Password`, `:feedback="false"`, `toggleMask`, required)
  5. `Sign In` (`Button`, full-width, `:loading="isPending"`)
- **Post-Action:** Stores JWT + Officer profile in `auth.store.ts` and redirects to `/` (`S-01`) or original `?redirect=` target.

---

### 6.2 `S-01` — Issue Dashboard
- **Route:** `/` · **Polling:** `10_000ms`
- **Vertical Layout Stack (Top to Bottom):**

```text
+-----------------------------------------------------------------------------------+
| [1. Conditional Alert Strip] (Visible ONLY if escalatedCount > 0 || reopened > 0) |
| ⚠ 2 issues escalated to Critical · 1 issue reopened            [View Affected →]  |
+-----------------------------------------------------------------------------------+
| [2. Summary Bar — 5 Compact KPI Filter Tiles (grid-cols-2 sm:grid-cols-5 gap-3)]   |
| [🔴 Critical: 3] [🟠 High: 5] [🟡 Open: 8] [🔁 Reopened: 1] [✅ Resolved Today: 4] |
+-----------------------------------------------------------------------------------+
| [3. Filter Bar]                                                                   |
| Status: [All][Open][Acknowledged][Investigating][Reopened]                        |
| Scope:  [All][Data Integrity][Process][Sync][Infrastructure]   [🔍 Search...] [×] |
+-----------------------------------------------------------------------------------+
| [4. Issue Feed — Virtualized Scrollable List (sorted: Severity DESC, LastSeen DESC)]
| |┃ [Critical][↑ +1]  Unbalanced Journal: Billing Invoice INV-2026-11-0042         |
| |┃                   Journal Balance Detector · [Data Integrity] · ×4 · 3m ago    |
| |┃                   Status: [Open] · Unassigned                                  |
| |---------------------------------------------------------------------------------|
| |┃ [High]            EMR Encounter Sync Timeout for Ward B                        |
| |┃                   EMR Sync Detector · [Data Synchronization] · ×2 · 14m ago    |
| |┃                   Status: [Investigating] · Assigned: Ahmad Budi               |
+-----------------------------------------------------------------------------------+
```

**Interactive & Visual Rules:**
- **Alert Strip:** Use `bg-rose-50 border border-rose-300 text-rose-900 rounded-lg px-4 py-2.5 flex items-center justify-between`. Clicking `"View Affected →"` applies filter (`status=Reopened` or `escalatedOnly=true`).
- **Summary Bar Tiles:** Clicking a tile (`Critical`, `High`, `Open`, `Reopened`) toggles that filter and highlights the tile with `ring-2 ring-slate-900 bg-slate-50`. Clicking `Resolved Today` navigates to `S-07` pre-filtered to `Today + Resolved`.
- **Feed Row Anatomy (`min-h-[68px] px-4 py-3`):**
  - Left `4px` severity color accent (`border-l-4`).
  - Left/Main zone: Severity badge + optional `↑` Escalation pill + Title (`line-clamp-2`, max 2 lines) + metadata line (`DetectorName · ScopeTag · Occurrence count ×N`). Highlight occurrence pill in amber/rose if `count >= 5` (`BR-PRIMA-003`).
  - Right zone (`shrink-0 flex items-center gap-4`): `IssueStatusChip`, Assignee chip (or `"Unassigned"` in `text-slate-400 italic`), relative `LastOccurrenceAtUtc` (`v-tooltip` shows `WIB` timestamp).
- **Exclusion Rule:** Never show `Resolved` or `Dismissed` issues in `S-01`.

---

### 6.3 `S-02` — Issue Detail Workbench *(Core Operational Screen)*
- **Route:** `/issues/:id` · **Polling:** `10_000ms` (paused/background-safe; never overwrites open modal/note draft)
- **Layout:** Sticky Top Header + Desktop 2-Column Asymmetric Grid (`grid-cols-1 lg:grid-cols-12 gap-6`):

```text
+===================================================================================+
| [1. STICKY WORKBENCH HEADER — sticky top-0 z-20 bg-white/95 border-b]             |
| ← Back to Dashboard   [DET-01:INV-2026-11-0042 📋]   Occurrence #4   [Detector ↗] |
| Unbalanced Journal: Billing Invoice INV-2026-11-0042                              |
| [🔴 Critical] [↑ Escalated] [🟣 Investigating] [Data Integrity]                   |
|                                  ACTION BAR: [ Record Remediation ] [Add Note][Dismiss]
+===================================================================================+
| [LEFT PRIMARY COLUMN: lg:col-span-8]          | [RIGHT GOVERNANCE: lg:col-span-4] |
|                                                |                                   |
| 2. LATEST TELEMETRY EVIDENCE (Always Open)     | 5. VERIFICATION GATE (BR-PRIMA-004)
| +--------------------------------------------+ | [✓] 1. Investigating (Ahmad Budi) |
| | Module: Billing | Table: acc_journal...    | | [○] 2. Remediation Recorded       |
| | Keys: invoice_id=984321, batch=B2026... 📋 | | [○] 3. Verification CleanPassed   |
| |--------------------------------------------| | 🔒 Mark Resolved locked until #3  |
| | Debit Total    Credit Total    Variance    | |                                   |
| | Rp 1.500.000   Rp 1.450.000    Rp 50.000 ! | | 6. VERIFICATION RECORD (if any)   |
| |--------------------------------------------| | 7. REMEDIATION HISTORY (if any)   |
| | TRACE [Copy]:                              | | 8. DISMISSAL RECORD (if Dismissed)|
| | > Debit lines sum to 1,500,000 but credit  | |                                   |
| |   lines sum to 1,450,000...                | | 9. OWNERSHIP & TIMESTAMPS         |
| +--------------------------------------------+ |   Assignee · Created · Ack · Last |
|                                                |                                   |
| 3. OCCURRENCES & TIMELINE (Collapsible)        | 10. ACTIVITY LOG (Collapsed by    |
| [▾] Occurrences (4)                            |     default — audit trail)        |
|   #4 · 20 Nov 13:15 · Scheduled [View Evid ▾]  | [▸] Activity Log (6 events)       |
|   #3 · 20 Nov 12:45 · Scheduled [View Evid ▾]  |                                   |
|                                                |                                   |
| 4. INVESTIGATION NOTES (Append-only)           |                                   |
|   [AB] Ahmad Budi · 20 Nov 13:18 WIB           |                                   |
|   Checked acc_journal_entries, missing tax...  |                                   |
|   +------------------------------------------+ |                                   |
|   | Add investigation note...                | |                                   |
|   +------------------------------------------+ |                                   |
|                                  [+ Add Note]  |                                   |
+================================================+===================================+
```

**Detailed Rules for `S-02` Sections:**
- **Sticky Header & Action Bar:** Back link respects navigation origin (`Back to History` if navigated from `S-07`, otherwise `Back to Dashboard`). Detector name is a clickable router link to `/detectors/:detectorId` (`S-04`).
- **Latest Telemetry Evidence Card (Always Visible):**
  - Top 2×2 key-value metadata well (`Module`, `Source Table`, `Record Keys` with copy button, `Captured At WIB`).
  - Financial sub-grid (3 columns, shown only when `DebitTotal !== null || CreditTotal !== null || VarianceDelta !== null`).
  - Dark monospace `TraceString` box with copy button in the top-right corner.
- **Occurrences & Timeline (`BR-PRIMA-009` Immutable):**
  - Sorted newest-first (`#N` down to `#1`). Collapsed when only 1 occurrence exists; expandable header shows `Occurrences (N)`. Clicking `View Evidence` expands that specific occurrence's `TelemetryEvidence` snapshot inline.
- **Investigation Notes:**
  - Append-only chronological stream. Composer at bottom supports `Ctrl+Enter` to submit. Hide composer when `Status` is `Resolved` or `Dismissed`.
- **Verification Gate Card (Top of Right Column):**
  - Always visible during active lifecycle (`Open`, `Acknowledged`, `Investigating`, `Reopened`) so the officer clearly understands `BR-PRIMA-004` progress.
  - When `Status === 'Resolved'`, show a solid emerald banner: `"Verified Clean & Resolved"`.
  - When `Status === 'Dismissed'`, show the `DismissalRecord` card prominently at the top of the right column instead.
- **Activity Log:** Collapsed by default (`:collapsed="true"`) at the bottom of the right column to reduce visual noise while preserving full auditability.

---

### 6.4 Modals (`M-01`, `M-02`, `M-03`)

All modals open over `S-02` using PrimeVue `Dialog` (`modal`, `:draggable="false"`, `max-w-lg`) and are governed by **VeeValidate + Zod**.

| Modal | Fields & Controls | Validation Rules (Zod) | Primary / Secondary Actions |
|---|---|---|---|
| **M-01: Dismiss Issue** | 1. `Dismissal Reason` (`Select`: `False Positive`, `Accepted Operational Exception`, `Duplicate Manual Report`)<br>2. `Justification Rationale` (`Textarea`, 4 rows, live counter `0 / 20 min chars`) | `reason`: required<br>`rationale`: required, trimmed length `>= 20` (`BR-PRIMA-006`) | Primary: `Confirm Dismiss` (`severity="danger"`)<br>Secondary: `Cancel` |
| **M-02: Record Remediation** | 1. `Action Category` (`Select`: `Data Correction`, `Service Restart`, `Configuration Adjustment`, `Code Remediation Requested`)<br>2. `Description` (`Textarea`, 4 rows, placeholder describing fix per SOP) | `actionCategory`: required<br>`description`: required, non-empty | Primary: `Save Remediation` (`severity="primary"`)<br>Secondary: `Cancel` |
| **M-03: Manual Verification** | 1. `Verification Evidence / Notes` (`Textarea`, 4 rows, placeholder describing SQL query or manual check performed)<br>2. `Result` (2 visual selectable radio cards:<br>— Green card: `Anomaly Gone (Clean)` → `CleanPassed`<br>— Rose card: `Anomaly Still Present` → `AnomalyStillPresent`) | `evidenceNotes`: required, non-empty<br>`cleanPassed`: boolean required | Primary: `Submit Verification`<br>Secondary: `Cancel` |

---

### 6.5 `S-03` — Detector List
- **Route:** `/detectors` · **Polling:** `15_000ms`
- **Layout:**
  1. **Page Header:** Title `"Detector Portfolio"` + right-aligned primary button `+ Register Detector` (navigates to `S-05` create mode).
  2. **Health Summary Bar (4 clickable tiles):** `🟢 Healthy`, `🔴 Failing`, `🟡 Silent`, `⏸ Paused`.
  3. **Filter Bar:** Admin Status pills (`All · Active · Paused · Retired`), Health pills (`All · Healthy · Silent · Failing`), Scope pills (`All · Data Integrity · Process · Sync · Infrastructure`), plus instant search input.
  4. **Detector Feed Table (Virtual scroll, no pagination):**
     - Columns: Health Dot & Badge · Detector Name & Description snippet · Scope Tag · Target Module · Admin Status Chip · Last Run (relative + `WIB` tooltip) · Expected Cadence (`Every 30 min`).
     - Row click navigates to `S-04 Detector Detail`.

---

### 6.6 `S-04` — Detector Detail
- **Route:** `/detectors/:id` · **Polling:** `15_000ms`
- **Layout:**
  1. **Sticky Header:** Back link (`← Back to Detectors`), Detector Name, Health Badge (`Healthy`/`Silent`/`Failing`), Admin Status Chip (`Active`/`Paused`/`Retired`), Scope Tag, Target Module, Cadence.
  2. **Action Bar (governed by `AdminStatus`):**
     - `Active`: `Trigger Run` (primary, inline loading spinner while executing), `Pause` (secondary), `Edit` (secondary → `S-05`), `Retire` (danger outlined → triggers `ConfirmDialog`).
     - `Paused`: `Resume` (primary), `Edit` (secondary), `Retire` (danger outlined).
     - `Retired`: Read-only banner (`"This detector is permanently retired. Historical runs and issues remain preserved for audit."`); action buttons hidden.
  3. **4-Column Stats Row:** `Last Successful Run`, `Last Failure`, `Total Runs (30d)`, `Pass Rate %` (monospace `tabular-nums`, colored emerald `>= 95%`, amber `80–94%`, rose `< 80%`).
  4. **2-Column Lower Split (`lg:grid-cols-12 gap-6`):**
     - **Left (`lg:col-span-7`): Recent Inspection Runs (last 10):** Header with `"View All Runs →"` link to `S-06`. Each row shows Outcome dot, Trigger chip (`Scheduled`/`Manual`/`EventHook`), Timestamp (`WIB`), Duration (`ms`/`s`), and expands inline on click to reveal failure reason or finding summary.
     - **Right (`lg:col-span-5`): Active Linked Issues:** Open/Investigating/Reopened issues tied to this detector. Clicking any row navigates to `S-02`.

---

### 6.7 `S-05` — Detector Form (Register / Edit)
- **Route:** `/detectors/new` (create) and `/detectors/:id/edit` (edit)
- **Layout:** Centered single-column form card (`max-w-2xl mx-auto bg-white border border-slate-200 rounded-lg p-6`).
- **Fields:**
  1. `Name` (`InputText`): Required in Register mode; **strictly read-only / disabled** (`:disabled="isEditMode"`) with helper caption `"Detector identity cannot be changed after registration."` in Edit mode.
  2. `Description` (`Textarea`, 3 rows): Required.
  3. `Monitoring Scope` (`Select`): `Data Integrity`, `Process Integrity`, `Data Synchronization`, `Infrastructure & Service`.
  4. `Target Module` (`InputText`): e.g., `Accounting`, `EMR`, `Billing`.
  5. `Expected Cadence` (Split input: `InputNumber` + unit `Select` `Minutes` / `Hours` converted cleanly to `ExpectedCadenceSeconds` on submit): Helper text displays calculated silence threshold (`"Marked Silent if no heartbeat for 2× cadence"` per `BR-PRIMA-010`).
  6. `Webhook URL` *(optional field if supported by backend `RegisterDetector`)*: Validated URL input for on-demand re-runs.
- **Footer Actions:** `Cancel` (secondary, discards dirty form back to `S-03`/`S-04`) and `Save Detector` (primary, `:loading="isPending"`). In Register mode, saves immediately as `Active`.

---

### 6.8 `S-06` — Inspection Run Log
- **Route:** `/detectors/:id/runs`
- **Layout:**
  1. **Context Header:** Back link (`← Back to Detector`), Detector Name, Health Badge, Current Cadence, and Date Range Picker (default: last 7 days).
  2. **Filter Bar:** Outcome pills (`All · Passed · Finding Discovered · Execution Failed`) + Trigger pills (`All · Scheduled · Manual · EventHook`).
  3. **Virtualized Run List (no pagination):**
     - Row: Expand chevron · Outcome dot & badge · Timestamp (`WIB`) · Trigger chip · Duration (`font-mono`) · Inline summary (`Finding →` clickable link to `S-02` if `FindingDiscovered`, or truncated red error reason if `ExecutionFailed`).
     - Expanded Drawer (inline below row): Full `Outcome` metadata, full monospace `FailureReason` / stack trace block (if failed), or `Finding` count + copyable `CorrelationKey` + link to `S-02` (if finding discovered).

---

### 6.9 `S-07` — History & Reports
- **Route:** `/history` (Read-only — zero mutation buttons)
- **Layout:**
  1. **Header + Date Range Controls:** Title `"History & Reliability Reports"` + Preset buttons (`Today`, `This Week`, `This Month`, `Last Month`, `Custom`) + `DatePicker` range selector.
  2. **6-Tile KPI Grid (`grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3`):**
     `Total Issues` · `Resolution Rate %` · `Avg. Time to Acknowledge (h)` · `Avg. Time to Resolve (h)` · `Detector Pass Rate %` · `Reopened Issues`.
  3. **2-Column Scope & Severity Breakdown (`grid-cols-1 md:grid-cols-2 gap-4`):**
     Compact count tables with horizontal proportion bars: Left card = Issues by `MonitoringScope`; Right card = Issues by `IssueSeverity`.
  4. **Filterable Issue History Table:**
     - Filters: Status (`All · Resolved · Dismissed · Reopened`), Scope, Detector (`Select`), Severity (`Select`).
     - Columns: Status Chip · Title · Detector · Scope · Severity · Created (`WIB`) · Closed/Resolved Date (`WIB`).
     - Clicking any row navigates to `S-02` (read-only view for `Resolved`/`Dismissed`).

---

## 7. Interactive State Machine & Context-Sensitive Action Bar Rules (`S-02`)

The `S-02` sticky header Action Bar **must** deterministically render buttons based on `Issue.Status`, `LatestRemediation`, and `LatestVerification` in accordance with [PRIMA-DOMAIN.md §8.1](./PRIMA-DOMAIN.md#81-issue-lifecycle-state-machine):

| `Issue.Status` | Condition | Primary Action Button | Secondary Action Buttons | Verification Gate / Disabled State Rule |
|---|---|---|---|---|
| `Open` | Initial discovery | **`Take It`** (`bg-slate-900 text-white`) — Acknowledges + assigns to current officer + moves directly to `Investigating` | `Assign to…` · `Dismiss` (`M-01`) | Gate Step 1 pending; `Mark Resolved` not shown yet |
| `Reopened` | Recurred after close | **`Take It`** (`bg-purple-700 text-white`) — Re-takes ownership → `Investigating` | `Assign to…` · `Dismiss` (`M-01`) | Prior verification invalidated (`BR-PRIMA-002`) |
| `Acknowledged` | Acknowledged without assignee | **`Start Investigating`** (`bg-indigo-600 text-white`) — Self-assigns → `Investigating` | `Reassign` · `Dismiss` (`M-01`) | Gate Step 1 requires assignment (`BR-PRIMA-008`) |
| `Investigating` | `LatestRemediation == null` | **`Record Remediation`** (`bg-slate-900 text-white` → opens `M-02`) | `Add Note` (scrolls/focuses composer) · `Dismiss` (`M-01`) | `Mark Resolved` visible as **disabled** with lock icon (`🔒 Remediation & verification required`) |
| `Investigating` | `LatestRemediation != null` AND `LatestVerification == null` | **`Trigger Verification Re-run`** (`bg-indigo-600 text-white`, shows inline spinner while calling webhook) | `Manual Verification` (opens `M-03`) · `Dismiss` (`M-01`) | `Mark Resolved` visible as **disabled** (`🔒 Clean verification required`) |
| `Investigating` | `LatestVerification.CleanPassed == false` (`AnomalyStillPresent`) | **`Record Remediation`** (Retry — opens `M-02`) | `Trigger Verification Re-run` · `Manual Verification` · `Dismiss` | Alert banner on Verification card (`"Anomaly still present — retry remediation"`); `Mark Resolved` remains **disabled** |
| `Investigating` | `LatestVerification.CleanPassed == true` (`CleanPassed`) | **`Mark Resolved`** (`bg-emerald-600 hover:bg-emerald-700 text-white` with `pi pi-check-circle`) | `Add Note` | **Gate Unlocked (`BR-PRIMA-004`)**: Clicking transitions immediately to `Resolved` |
| `Resolved` | Closed (Verified Clean) | *(None — read-only badge `"Resolved"`)* | *(None)* | All mutating buttons & note composer hidden |
| `Dismissed` | Closed (Dismissed) | *(None — read-only badge `"Dismissed"`)* | *(None)* | All mutating buttons & note composer hidden |

> **Important Edge Case:** If a `VerificationRecord` exists (`CleanPassed == true`) and a new finding arrives under the same `CorrelationKey`, the backend clears `LatestVerification` and moves status to `Reopened` (`BR-PRIMA-002`). The next 10s poll on `S-02` automatically updates the Action Bar back to the `Reopened` state and locks resolution again.

---

## 8. Real-Time Polling UX & Non-Disruptive Refresh Rules (TanStack Query)

Per [PRIMA-ARCHITECTURE.md §5.3](./PRIMA-ARCHITECTURE.md#53-state-management--polling-conventions), the frontend relies on TanStack Query polling (`refetchInterval: 10_000` for dashboard/issues; `15_000` for detectors) with `refetchOnWindowFocus: true`.

### 8.1 Non-Disruptive Rendering Rules
1. **Initial Load vs. Background Refetch:**
   - Render `<Skeleton>` loaders **exclusively** when `isLoading.value === true` (first load with no cached data).
   - When `isFetching.value === true && data.value !== undefined`, keep all existing DOM rendered and interactive. Only show a subtle syncing indicator in the sidebar footer.
2. **State Preservation Across Poll Ticks:**
   - Track expanded accordion/row IDs (`expandedOccurrenceIds: Set<string>`, `expandedRunIds: Set<string>`) using stable entity IDs, never array indices.
   - Never reset `filterState`, `searchInput`, `scrollTop`, or `noteDraft` inside a `watch(data)` callback.
   - When a modal (`M-01`, `M-02`, `M-03`) is open on `S-02`, allow background query refetching to update read-only telemetry/occurrence count silently, **without** closing the dialog or resetting VeeValidate form fields.
3. **Subtle New-Issue Attention Cue:**
   - On `S-01`, when a poll introduces a newly created `Critical` or `Reopened` row ID that was not in the previous snapshot, apply a 2-second soft amber/rose highlight transition (`transition-colors duration-1000`) to that row without jumping the user's scroll position.

### 8.2 Mutation Cache Invalidation Matrix

Every TanStack `useMutation` hook **must** invalidate the exact query keys affected so the UI reflects state transitions immediately without waiting for the next 10–15s poll:

| Mutation Action | Origin Screen / Modal | Query Keys to Invalidate (`queryClient.invalidateQueries`) | UI Feedback |
|---|---|---|---|
| `TakeIt` / `AssignIssue` / `Acknowledge` | `S-02` Header | `['issues', 'detail', issueId]`, `['dashboard', 'feed']` | Success Toast (`"Issue assigned to you"`) |
| `AddInvestigationNote` | `S-02` Notes | `['issues', 'detail', issueId]` | Clear textarea; scroll note list to latest entry |
| `RecordRemediation` | `M-02` | `['issues', 'detail', issueId]` | Close `M-02`; Success Toast; Action Bar shifts to Verification options |
| `TriggerVerificationRun` | `S-02` Header | `['issues', 'detail', issueId]`, `['detectors']` | Toast showing `CleanPassed` (emerald) or `AnomalyStillPresent` (warn) |
| `RecordManualVerification` | `M-03` | `['issues', 'detail', issueId]` | Close `M-03`; Toast reflecting result; unlocks `Mark Resolved` if Clean |
| `ResolveIssue` | `S-02` Header | `['issues', 'detail', issueId]`, `['dashboard', 'feed']`, `['history']` | Success Toast (`"Issue marked as Resolved"`); screen switches to read-only |
| `DismissIssue` | `M-01` | `['issues', 'detail', issueId]`, `['dashboard', 'feed']`, `['history']` | Close `M-01`; Success Toast (`"Issue dismissed"`); screen switches to read-only |
| `RegisterDetector` / `UpdateDetector` | `S-05` | `['detectors', 'list']`, `['detectors', 'detail', detectorId]` | Success Toast; navigate back to `S-03` or `S-04` |
| `ToggleDetectorStatus` (`Pause`/`Resume`/`Retire`) | `S-04` | `['detectors', 'detail', detectorId]`, `['detectors', 'list']` | Success Toast reflecting new `AdminStatus` |
| `TriggerManualRun` | `S-04` | `['detectors', 'detail', detectorId]`, `['detectors', 'runs', detectorId]`, `['dashboard', 'feed']` | Toast with run outcome (`Passed` / `FindingDiscovered` / `ExecutionFailed`) |

---

## 9. Empty, Loading, Error & RFC 7807 Feedback Patterns

### 9.1 Contextual Empty States

Never show a generic `"No data"` string. Always render a centered empty-state panel tailored to context:

| Screen | Condition | Empty State Copy & Action |
|---|---|---|
| **`S-01` Dashboard** | 0 active issues & **no filters active** | Emerald check icon · Heading: `"All Clear — No Active Operational Issues"` · Subtitle: `"All active SIMRS detectors are operating normally."` |
| **`S-01` / `S-03` / `S-06` / `S-07`** | 0 results & **filters/search active** | Search icon · Heading: `"No matching records found"` · Action button: `"Reset All Filters"` |
| **`S-02` Notes** | 0 investigation notes | Subtle inline text: `"No investigation notes recorded yet. Add diagnostic findings below."` |
| **`S-03` Detectors** | 0 detectors registered | Heading: `"No Detectors Registered"` · Primary CTA: `"+ Register First Detector"` (`→ S-05`) |
| **`S-04` Active Issues** | 0 linked open issues | Compact emerald row: `"No active issues for this detector."` |

### 9.2 RFC 7807 `ProblemDetails` Error Mapping

The centralized Axios interceptor (`src/shared/services/http.ts`) maps backend RFC 7807 `ProblemDetails` into a typed `ApiError`. Components **must** handle each HTTP status consistently:

| HTTP Status | Backend Meaning | Required UI Behavior |
|---|---|---|
| **`422 Unprocessable Entity`** | Validation failure (e.g., dismissal rationale `< 20` chars, invalid cadence) | Map field errors directly to VeeValidate (`form.setErrors(apiError.fieldErrors)`) so messages appear inline directly below the invalid field. |
| **`409 Conflict`** | Domain invariant / business rule conflict (e.g., `Mark Resolved` called without `CleanPassed` verification, or stale concurrent transition) | Display a PrimeVue `Toast` (`severity="warn"`, summary `"Action Blocked"`, detail `apiError.detail`) AND immediately invalidate the current entity query (`['issues', 'detail', id]`) to resync UI state. |
| **`404 Not Found`** | Issue or Detector ID does not exist | Render a clean inline not-found state with a `"Back to Dashboard"` / `"Back to Detectors"` button. |
| **`401 Unauthorized`** | Missing or expired JWT token | Clear Pinia `auth.store.ts` session and redirect to `/login` (`S-00`) preserving `?redirect=` query param. On `S-00` itself, show inline `<Message severity="error">`. |
| **Network / `5xx` during Polling** | Temporary backend disconnect during 10s/15s background poll | **Do NOT show a Toast every 10 seconds.** After `>= 2` consecutive failed background polls (`failureCount >= 2`), switch the sidebar live indicator to amber `"Reconnecting..."` and display a single non-blocking top banner until a poll succeeds. |

---

## 10. AI Agent Implementation Checklist & Anti-Patterns ("Do Not Do")

Before finalizing any frontend PR or feature slice, the AI agent **must** verify against these guardrails:

### Mandatory Do's
- [ ] **Feature Boundary Compliance:** Keep all API calls, query key factories, Zod schemas, and feature views isolated inside `src/features/{auth,dashboard,issues,detectors,history}/` with zero cross-feature imports (`PRIMA-ARCHITECTURE.md §5.1`).
- [ ] **Shared Domain Primitives:** Put `<IssueSeverityBadge>`, `<IssueStatusChip>`, `<ScopeTag>`, `<DetectorHealthBadge>`, `<CopyableValue>`, and `dateUtils.ts` / `currencyUtils.ts` inside `src/shared/` so all screens share identical visual tokens.
- [ ] **Generated Contract Usage:** Type all query responses and mutation payloads from `src/shared/types/api.generated.ts` (generated via `openapi-typescript`).
- [ ] **Accessibility & Keyboard Efficiency:** Support `Esc` to close modals (`M-01`–`M-03`), `Ctrl+Enter` to submit investigation notes (`S-02`), visible focus rings (`focus-visible:ring-2 focus-visible:ring-slate-900`), and semantic `<button>` elements for all interactive chips and rows.

### Forbidden Anti-Patterns ("Do Not Do")
- ❌ **Do NOT add pagination** to `S-01 Issue Dashboard`, `S-03 Detector List`, or `S-06 Inspection Run Log` — use virtual scroll (`VirtualScroller` / virtualized `DataTable`) so active operational anomalies are never hidden on page 2.
- ❌ **Do NOT build multi-step wizards** for `S-02` issue resolution or `S-05` detector creation.
- ❌ **Do NOT create a separate "Detector Health Alerts" screen** — silent or failing detectors surface as `Infrastructure` issues in `S-01` (`BR-PRIMA-005`) and as health badges in `S-03`/`S-04`.
- ❌ **Do NOT enable `Mark Resolved` optimistically** without a `CleanPassed` `VerificationRecord` — the UI must enforce `BR-PRIMA-004` visually before calling the API.
- ❌ **Do NOT trigger full-screen loading spinners or skeletons on background polling ticks** (`isFetching`).
- ❌ **Do NOT render `null` financial fields as `Rp 0,00`** on non-financial issues; omit the financial sub-grid unless `DebitTotal`, `CreditTotal`, or `VarianceDelta` is present.
- ❌ **Do NOT install external charting libraries** (`Chart.js`, `ECharts`, etc.) for v1 — `S-07` uses KPI tiles and simple CSS horizontal proportion bars in the Scope/Severity breakdown table.
- ❌ **Do NOT use native browser `alert()` or `confirm()`** — use PrimeVue `Toast` and `ConfirmDialog`.
