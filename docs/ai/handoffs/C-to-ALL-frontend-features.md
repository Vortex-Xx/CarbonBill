# Handoff: C-to-ALL-frontend-features.md

**From:** Dev 3 (Track C - Insights & Reporting)  
**To:** Dev 1 (Track A - Platform & Identity), Dev 2 (Track B - Capture & UI Shell)  
**Date:** 2026-10-09  
**Branch:** `feat/c-c7-frontend`  
**Deliverable:** C7 - Frontend: Dashboard, Flags, Recommendations, Reports, Auditor

---

## 1. Summary of Changes

Deliverable C7 completes the user-facing web analytics and reporting frontend for Track C within `web/src/features/` and integrates them seamlessly into `web/src/routes/main/DashboardView.tsx`.

### Core Capabilities Implemented:

1. **Role Dashboards (`web/src/features/dashboard`)**:
   - **Owner**: Total footprint in tCO₂e, trailing quarterly trend indicator, top 3 priority flags, top 3 energy & cost saving actions in BDT.
   - **Accountant**: Review queue pending count with direct deep link, missing expected documents checklist (Days -7/-3/0 escalation status), invoice reconciliation.
   - **Compliance**: Report sign-off readiness index, Data Quality Score (DQS) breakdown (Grade A/B/C), IPCC factor basis verification.
   - **Consultant**: Multi-tenant client portfolio overview table (Apex, Square, Ha-Meem, Beximco) with emissions, DQS grade, open flags, and pending overrides count.

2. **Analytical Charts (`Chart.js`)**:
   - **Trend Chart**: 6-month monthly emissions trend featuring solid verified activity bars and **hatched diagonal stripe segments** for proxy estimates; flags months where estimated share exceeds 10%.
   - **Scope Breakdown Chart**: Interactive donut chart with Scope 1, 2, 3 shares, percentage distribution, and center total readout.
   - **Intensity vs Peer Benchmark Chart**: Computes emissions intensity (kg CO₂e / unit) and benchmarks against industry peer percentiles (P25, P50, P75, P90).
   - **Honest Peer Gating**: When peer sample size $n < 10$, strictly suppresses misleading benchmarks and displays the honest banner: *"যথেষ্ট পিয়ার ডেটা নেই (No benchmark yet — n < 10)"*.

3. **Carbon Flags Interactive Feed (`web/src/features/flags`)**:
   - Top 5 priority cards with severity badges: Critical (Red), Warning (Amber), Info (Blue).
   - Plain-language explanation in both Bangla and English.
   - "কী করতে হবে" / "What to do" action drawer deep-linking to original bills in Review Queue.
   - User actions:
     - **Acknowledge**: Marks flag as acknowledged.
     - **Dismiss**: Opens modal with **mandatory reason**; enforces **30-day expiry notice** before auto-re-evaluation.
     - **Snooze**: Snooze modal with 7, 14, 30 days interval and optional reason.
   - Fatigue control banner explaining the top-5 prioritization algorithm.

4. **Evidence-Based Recommendations Engine (`web/src/features/recommendations`)**:
   - Top 5 starter measures from Measure Library seeds (APFC capacitor bank, air leaks, boiler economizer, high-bay LEDs, rooftop solar).
   - **Range Display Rule**: Strictly presents calibrated ranges `Low – High (Typical)` for Annual BDT Savings, Capex, Payback months, and tCO₂e avoided. Never outputs deceptive single-point figures.
   - Evidence grade badges (`Grade A`, `Grade B`, `Grade C`) with source links (IFC PaCT, SREDA, IDCOL).
   - "Why this?" / "কেন এই পদক্ষেপ?" dropdown showing factory baseline tariff assumptions.
   - Financing note pills (IDCOL 6% green refinancing, SREDA low-interest credit).
   - Lifecycle status transitions (`Suggested`, `Planned`, `Done`, `NotFeasible` with mandatory reason modal).
   - **Marginal Abatement Cost Curve (MACC)**: Interactive Chart.js bar chart displaying BDT / tCO₂e (including negative-cost profit-yielding measures below the zero line) vs reduction potential.

5. **Buyer Reports Manager (`web/src/features/reports`)**:
   - Audit readiness checklist (checks expected bills, critical flags, DQS >= 80%, factor versions).
   - Report preview summary with period, totals, and mandatory GHG Protocol disclaimer.
   - Sign-off & approval action: locks report snapshot and binds SHA-256 cryptographic seal.
   - **Share-Link Dialog**: Configurable expiry (7, 14, 30, 90 days), **Redact Prices** toggle, copyable URL, and active link management.
   - Download links for QuestPDF (Bangla & English) and ClosedXML Excel workbooks.

6. **Auditor Read-Only Portal (`web/src/features/auditor`)**:
   - Read-only verification interface for brand buyers (H&M, Inditex) or third-party auditors.
   - Cryptographic SHA-256 seal stamp (`SHA-256: 9f86d...`).
   - Price redaction mode: when active, masks financial amounts as `[Confidential]` / `[গোপনীয়]`, protecting commercial sensitivity while preserving physical activity and emission metrics.
   - **Click-to-Source Traceability Tree**: Clicking any activity line item opens the Provenance Inspector Modal showing original document ID, OCR confidence score, confirming reviewer, and applied factor citation.

---

## 2. Integration Points for Dev 1 and Dev 2

### For Dev 1 (Track A - Identity & Onboarding)
- Role switcher supports `'Owner' | 'Accountant' | 'Compliance' | 'Consultant'`.
- Consultant portfolio view connects to multi-tenant organization context.

### For Dev 2 (Track B - Capture, Review & Shell)
- `DashboardView.tsx` exposes top-level tab navigation across `Dashboard`, `Flags`, `Recommendations`, `Reports`, and `Auditor`.
- Deep-links from flags and accountant checklists gracefully invoke `onGoToReview()` and `onGoToFloor()`.
- Floor route bundle size remains strictly verified at **9.92 KB gzipped** (well within the 200 KB budget limit).

---

## 3. Automated Tests

All 29 Vitest tests pass cleanly:
- `web/src/features/recommendations/__tests__/rangeDisplay.test.ts` (Range ordering `low <= typical <= high`, BDT formatting, negative cost/t).
- `web/src/features/dashboard/__tests__/dashboardGating.test.ts` (Peer gating $n < 10$, quartile ordering, hatched estimated flag > 10%).
- `web/src/features/flags/__tests__/flagsInteractions.test.ts` (Acknowledge, snooze, dismiss with mandatory reason & 30-day expiry).
- `web/src/features/reports/__tests__/reportsApproval.test.ts` (Readiness checklist, approval locking with SHA-256 seal, share links with price redaction).
- `web/src/features/auditor/__tests__/auditorTrace.test.ts` (Click-to-source provenance tree, OCR score, confirming user, price redaction).
- `web/src/shared/formatters/__tests__/banglaFormatters.test.ts`
- `web/src/features/capture/__tests__/capture.test.ts`
- `web/src/features/review/__tests__/reviewQueue.test.ts`
