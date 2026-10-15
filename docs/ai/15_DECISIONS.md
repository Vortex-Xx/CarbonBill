# 15. Decisions and Architecture Decision Records (ADR)

## 1. Open Decisions to Confirm

The following 6 architectural decisions were identified in the Solution Architecture v1.0 document. Each represents an open question with a concrete recommended path. Developers must log confirmations or updates here as the project progresses.

---

### Decision 1: Frontend Framework Choice
- **Question:** Should the frontend be built as a **React + Vite PWA** or as a **Next.js static export**?
- **Architecture Recommendation:** **React + Vite PWA (TypeScript)**.
- **Rationale:** CarbonBill is an authenticated application with zero requirement for public search engine indexing (SEO). A Vite PWA generates a significantly smaller JavaScript bundle size (<200 KB initial load for floor staff route) and integrates natively with Workbox and IndexedDB for offline background synchronization.
- **Current Status:** Tentatively accepted (implemented in P0-3).

---

### Decision 2: OCR Tier 3 (Vision LLM) Usage Policy and Customer Terms
- **Question:** Is Tier 3 Vision LLM (e.g. Gemini Flash API) permitted for extracting real customer bills, and under what specific data privacy conditions?
- **Architecture Recommendation:** Restrict Tier 3 Vision LLM to customer accounts that have explicitly opted in via an organizational consent toggle.
- **Privacy Warning:** Free API tiers of some cloud AI vendors may retain or use submitted payloads for model improvement. For real customer production data, only paid enterprise zero-data-retention tiers or local self-hosted models may be used. *(VERIFY: Check vendor data retention and training terms before pilot).*
- **Current Status:** Open question pending pilot data protection sign-off. Default configured to OFF.

---

### Decision 3: Initial Sector Onboarding Template
- **Question:** Which manufacturing sector should be templated first during Phase 1 MVP?
- **Architecture Recommendation:** **Ready-Made Garments (RMG)**.
- **Rationale:** RMG represents the largest share of Bangladeshi export manufacturing and was the primary cohort in the initial 17-factory field survey. Subsequent sectors (textile dyeing, food processing, light engineering) will be loaded as JSON template configurations without code changes.
- **Current Status:** Confirmed. RMG template prioritized for deliverable A3.

---

### Decision 4: Domain Expert Reviewer for Measure Library & Factors
- **Question:** Who will formally inspect, validate, and sign off on the emission factor tables and the Measure Library research dataset before release?
- **Architecture Recommendation:** Engage a certified Bangladeshi sustainability consultant (matching the Farhana persona) or a university energy/environmental engineering faculty specialist.
- **Protocol:** Every seed row must link to a published source citation (e.g. IFC PaCT, Cascale Higg FEM, SREDA, IDCOL). No arbitrary values may be committed to production datasets without expert sign-off.
- **Current Status:** Open pending expert appointment for Milestone I10.

---

### Decision 5: Pilot Hosting Strategy
- **Question:** What hosting infrastructure strategy should be adopted transitioning from demo to pilot?
- **Architecture Recommendation:** **Azure for Students credits for Demo/Development**, transitioning to a **Single VPS in Singapore running Docker Compose behind Caddy for Pilot**.
- **Rationale:** Keeps operating costs predictable and ultra-low while ensuring <60 ms round-trip latency to Dhaka networks. Caddy provides automatic HTTPS TLS termination.
- **Current Status:** Confirmed in deployment architecture.

---

### Decision 6: Buyer Intensity Output Metric Standard
- **Question:** Which functional unit should serve as the default denominator for emissions intensity metrics (`kg CO2e / unit`)?
- **Architecture Recommendation:** Make the metric configurable per factory during onboarding, defaulting to **per piece of clothing (pieces)** for RMG garment makers and **per kg of fabric** for textile dyeing mills.
- **Rationale:** Buyers from different export brands request different unit denominators. Onboarding will collect the relevant monthly production quantity via `ProductionMetric`.
- **Current Status:** Confirmed. Configurable per organization template.

---

## 2. Architecture Decision Records (ADR) Log

| ADR # | Date | Title | Status | Summary |
|---|---|---|---|---|
| ADR-001 | 2026-10-08 | Transition from Laravel to ASP.NET Core Modular Monolith | Accepted | Selected ASP.NET Core LTS for performance, type safety in calculations, and single-process deployability. |
| ADR-002 | 2026-10-08 | Contract-First Architecture & Frozen Contracts | Accepted | Enforced zero cross-module project references; all interactions via `CarbonBill.Contracts` frozen at G1. |
| ADR-003 | 2026-10-08 | Dual-Layer Tenant Isolation (EF Core + Postgres RLS) | Accepted | Mandated `org_id` on all tenant tables enforced by global query filters and PostgreSQL RLS. |
| ADR-004 | 2026-10-08 | Exact Decimal Precision for Money and Quantities | Accepted | Prohibited `float`/`double`. Mandated `decimal numeric(18,2)` for BDT and `numeric(18,6)` for quantities/factors. |
| ADR-005 | 2026-10-09 | QuestPDF Report Rendering and Bangla Font Spike | Accepted | Validated QuestPDF Community License (free <$1M revenue; VERIFY annual check). Confirmed Noto Sans Bengali font rendering with regular/bold/medium weights for complex conjuncts (`ক্ষ`, `জ্ঞ`, `স্থ`, `ত্র`). Fallback interface abstraction `IReportRenderer` preserved. |
