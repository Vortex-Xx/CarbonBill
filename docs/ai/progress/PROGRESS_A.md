# PROGRESS_A: Track A Progress Tracker (Dev 1)

## Deliverables Status

| Prompt | Description | Status | Branch / PR | Notes |
|---|---|---|---|---|
| **P0-1a** | Core context docs + AGENTS.md | done | `docs/p0-1a` | Initialized repository skeleton and foundational guides |
| **P0-2** | .NET solution scaffold + infra | done | `feat/a-p0-2-scaffold` | Solution setup, modular monolith class libraries, NetArchTest guard |
| **P0-5** | Contracts + Fakes | done | `feat/a-p0-5-contracts` | Contracts, DTOs, domain events, and in-memory Fakes |
| **A1** | Building blocks & tenancy RLS | done | `feat/a-a1-tenancy-rls` | Multi-schema SQLite/EF Core, global query filters, interceptors, problem+json |
| **A2** | Identity and Tenancy | done | `feat/a-a2-identity` | JWT tokens, refresh cookies, floor staff QR join with 4-digit PIN, org switching |
| **A3** | Onboarding & expected documents | done | `feat/a-a3-onboarding` | Sites, assets, expected doc rules, facility questionnaire profile, RMG template |
| **A4** | Units & Factor Registry | done | `feat/a-a4-factor-registry`| Canonical unit conversion, immutable factor sets, override hierarchy with justification |
| **A5** | Activity & Calculation | done | `feat/a-a5-calculation` | `IActivityWriter` pipeline, decimal(18,6) GHG formula, audit logging, read models |
| **A6** | Platform Admin & isolation tests | done | `feat/a-a6-admin-isolation`| Dataset versioning, tenant overview metrics, 100% tenant table isolation test suite |
| **A7** | Frontend (auth, onboarding, admin)| done | `feat/a-a7-frontend` | React web features for auth, onboarding wizard, admin dashboard, consultant workspace |
| **I1** | Real module wiring (swap fakes) | done | `feat/a-i1-real-wiring` | Host integration with `UseFakes=false`, full DI abstract contract test suite passing |
| **I4** | Security hardening & audit | done | `feat/a-i4-security-audit` | OWASP ASVS verification, adversarial IDOR sweep, tamper-resistant audit trail |
| **I7** | Consultant workspace | done | `feat/a-i7-consultant-workspace` | Multi-tenant consultant switching, factor override workflow with justifications |
| **I8** | Pilot deployment & backups | done | `feat/a-i8-pilot-backups` | VPS Docker Compose behind Caddy TLS, automated SQLite vacuum backup & restore drill |
| **A-P3-1**| Kiosk PIN Lock security (I9 follow-up) | done | `feat/a-p3-pin-lock` | Single-tap kiosk PIN lock, `/api/v1/auth/pin-lock/set`, `/lock`, `/unlock` and Auth UI overlay |
| **A-P3-2**| Direct Utility Connectors | done | `feat/a-p3-utility-connectors`| Digital intake API for DESCO, DPDC, and Titas Gas into calculation pipeline |

---

**Blocked on:** None. All Track A Phase 0, Phase 1, Phase 2, and Phase 3 pilot enhancement tasks completed!

**Next prompt:** Track A Complete. Ready for deployment and pilot testing.

**Open questions logged in 15_DECISIONS.md:**
- None currently blocking Track A.
