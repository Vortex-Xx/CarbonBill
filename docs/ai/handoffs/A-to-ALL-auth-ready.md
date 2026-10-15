# Handoff: A-to-ALL-auth-ready

- **From**: Dev 1 (Platform & Data Backbone)
- **To**: Dev 2 (Capture & Extraction), Dev 3 (Insights & Output), All
- **Date**: 2026-10-08
- **Topic**: Authentication, Tenant Resolution, Roles Matrix & Org Switching

---

## 1. What was built

1. **Identity and Authentication System**:
   - `POST /api/v1/auth/login`: Email/password authentication returning scoped JWT and secure refresh cookie.
   - `POST /api/v1/auth/refresh`: Refresh token exchange via HTTP-only cookie or body.
   - `POST /api/v1/auth/join`: QR-code join flow for floor staff with 4-digit PIN (no email or password required).
   - `POST /api/v1/auth/invite`: Multi-use or single-use invitation generation with QR payloads.
   - `POST /api/v1/auth/switch-org`: Organization switching for consultants and multi-org administrators.
   - `GET /api/v1/auth/me`: Current user profile, active organization, and roles.

2. **Tenant Context & Middleware**:
   - `TenantResolutionMiddleware`: Automatically extracts `org_id`, `sub`, and `role` claims from the JWT and injects them into scoped `ITenantContext`.
   - `TenantSaveChangesInterceptor`: Enforces tenant isolation on all `ITenantScopedEntity` saves.

3. **Roles Matrix**:
   - `Roles.FloorStaff`: Capture bills and view own submissions.
   - `Roles.Accountant`: Review OCR queue, verify/edit fields, confirm bills, and view financial summaries.
   - `Roles.Compliance`: Onboarding, emission factors, flags, gap alerts, and reporting.
   - `Roles.Owner`: Full tenant access, facility profiles, and factor overrides.
   - `Roles.Consultant`: Multi-tenant access across assigned client organizations.
   - `Roles.AuditorLink`: Read-only scoped access to verified report evidence and audit trails.
   - `Roles.PlatformAdmin`: Platform administration, seed dataset updates, and global metrics.
