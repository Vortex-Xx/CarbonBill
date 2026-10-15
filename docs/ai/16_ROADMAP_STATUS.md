# 16. Roadmap & Implementation Status: CarbonBill

This document tracks execution progress across all phases defined in the CarbonBill Solution Architecture v1.0.

---

## Phase 0: Spikes, Context & Scaffolding (Target: Weeks 1–2)

- [ ] **Technical Spikes:**
  - [ ] **Bangla OCR Accuracy Spike:** Evaluate extraction on 50 sample physical bills using Tesseract `ben+eng`.
  - [ ] **QuestPDF Bangla Font Spike:** Verify vector PDF rendering with `Noto Sans Bengali` glyph shaping and font fallbacks; record outcome. *(Fallback: Playwright HTML-to-PDF)*.
  - [ ] **Draft Datasets Ingestion Spike:** Load initial unit conversion seed and draft Measure Library.
  - [ ] **Quota & Terms Verification:** *(VERIFY)* Validate free-tier limits for Cloudflare R2, Azure Document Intelligence F0, and AI provider data retention terms.
- [ ] **Phase 0 Deliverables:**
  - [x] **P0-1a:** Core context documentation, architectural baselines, and `AGENTS.md` (Gate G0).
  - [ ] **P0-1b:** Domain glossary, database schema, API contracts, and core flow diagrams.
  - [ ] **P0-1c:** Calculation rules, Carbon Flags spec, Recommendations spec, reporting, and security specs.
  - [ ] **P0-2:** ASP.NET Core solution scaffold, module projects, Docker development environment, and NetArchTest.
  - [ ] **P0-3:** React + Vite PWA shell, role-based route splitting (<200 KB for floor route), i18n formatters.
  - [ ] **P0-4:** Seed schemas, unit conversions, template CSV/JSON files, and realistic fake development dataset.
  - [ ] **P0-5:** `CarbonBill.Contracts` interfaces, domain events, DTOs, and in-memory Fakes (Gate G1).

---

## Phase 1: MVP Core Tracks (Concurrent Build)

### Track A: Platform and Data Backbone (Dev 1)
- [ ] **A1:** Core building blocks (EF Core multi-schema, dual tenancy RLS, outbox dispatcher, problem+json errors).
- [ ] **A2:** Identity and tenancy (ASP.NET Core Identity, JWT, QR-code + PIN join flow, roles matrix).
- [ ] **A3:** Onboarding and expected-document calendar (sites, meters, gensets, vehicles, facility profile questionnaire).
- [ ] **A4:** Units conversion and Factor Registry (canonical conversions, immutable FactorSets, factor overrides).
- [ ] **A5:** Activity records, calculation engine (Scope 1 and Scope 2 location-based, verified vs estimated rollups).
- [ ] **A6:** Platform Admin dataset management endpoints and tenant isolation automated scanning tests.
- [ ] **A7:** Frontend authentication, onboarding wizard, and admin dataset views.

### Track B: Capture and Extraction (Dev 2)
- [ ] **B1:** Documents backend (upload hardening, magic bytes, SHA-256 deduplication, Cloudflare R2 storage).
- [ ] **B2:** Extraction pipeline Tier 1 (Hangfire job, Tesseract `ben+eng`, normalization, utility templates, SignalR).
- [ ] **B3:** Extraction pipeline Tiers 2 & 3 (Azure Document Intelligence, consented Vision LLM, golden test harness).
- [ ] **B4:** Review backend (queue sorted by confidence, corrections, confirmation transaction, sampling auto-confirm).
- [ ] **B5:** Floor staff PWA capture (2-tap Bangla UI, IndexedDB offline queue, receipt confirmation, manual fallback).
- [ ] **B6:** Review UI workspace (side-by-side viewer, bounding box highlights, keyboard shortcuts `Tab`/`Enter`/`N`).

### Track C: Insights and Output (Dev 3)
- [x] **C1:** Gap detection engine (nightly job, comparison against expected calendar, days -7, -3, 0 escalation).
- [x] **C2:** Notifications system (in-app, Web Push VAPID, transactional email, quiet hours, anti-fatigue).
- [x] **C3:** Carbon Flags engine (FlagRule schema, data-quality rules, lifecycle Open/Acknowledged/Resolved/Dismissed).
- [x] **C4:** Footprint flags, buyer-readiness checks, monthly production metrics, intensity denominators (`kg CO2e/unit`).
- [x] **C5:** Measure Library and Recommendations engine (8-step pipeline, BDT savings & payback, realism filters).
- [x] **C6:** Reporting backend (GHG Protocol aligned QuestPDF, ClosedXML Excel sheets, expiring auditor share links).
- [x] **C7:** Frontend role dashboards, Carbon Flags cards, recommendation action views, and auditor verification page.

---

## Phase 2: Integration & Differentiators

- [x] **I1:** Wire real module implementations in host (`UseFakes=false`), execute full abstract contract test suite.
- [ ] **I2:** Automated end-to-end integration test (Playwright & C# test runner: upload -> OCR -> review -> calculate -> report).
- [x] **I3:** Role dashboards and buyer PDF verification with real data and reconciliation tests.
- [x] **I4:** Security hardening, OWASP ASVS checklist pass, adversarial tenant-isolation penetration test.
- [ ] **I5:** Golden OCR benchmark report on 150 physical bills; offline torture tests (airplane mode, network degradation).
- [x] **I6:** Observability metrics via OpenTelemetry into Grafana Cloud; flag volume tuning against pilot datasets.
- [x] **I7:** Consultant multi-tenant workspace with cross-factory flag inspection and factor override workflows.

---

## Phase 3: Pilot & Growth

- [x] **I8:** Pilot VPS deployment (Singapore region) running Docker Compose behind Caddy with automated backups.
- [ ] **I9:** Formal usability testing with 5 participants per persona (floor staff submission task < 20 seconds).
- [x] **I10:** Pilot data review and domain expert sign-off on factor tables and Measure Library citations.
- [ ] **Future Enhancements:**
  - [ ] Peer benchmark distributions from opt-in anonymised pilot cohorts.
  - [ ] Closed-loop realised savings verification (calibrating Measure Library against post-intervention utility bills).
  - [x] Direct utility API integrations (DESCO, DPDC, Titas Gas) and digital intake connectors.
  - [ ] SMS and WhatsApp document intake channels.
  - [ ] Broader Scope 3 supply chain raw-material footprinting.
