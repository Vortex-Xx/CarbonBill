# Handoff: A-to-C-expected-docs

- **From**: Dev 1 (Platform & Data Backbone)
- **To**: Dev 3 (Gap Detection & Insights), All
- **Date**: 2026-10-08
- **Topic**: Expected Document Rules and Facility Setup for Gap Detection Engine

---

## 1. What was built

1. **Onboarding Endpoints**:
   - `POST /api/v1/onboarding/sites`: Create sites.
   - `GET /api/v1/onboarding/sites`: List sites and associated assets.
   - `POST /api/v1/onboarding/assets`: Create meters, gensets, boilers, vehicles.
   - `GET /api/v1/onboarding/assets`: List assets.
   - `POST /api/v1/onboarding/rules`: Configure expected document submissions (frequency, due day of month, responsible user).
   - `GET /api/v1/onboarding/rules`: List active rules.
   - `POST /api/v1/onboarding/profile/facility` & `GET /api/v1/onboarding/profile/facility`: 2-minute facility questionnaire profile storage (boilers, gensets, roof area sq ft, budget band, production metrics).
   - `POST /api/v1/onboarding/templates/rmg`: 1-tap template provisioning standard RMG site, grid meter, diesel genset, and boiler.

2. **Integration Interface for Dev 3 (Gap Detection Engine)**:
   - `IExpectedDocRuleReader` in `CarbonBill.Modules.Onboarding.Services`:
     - `GetRulesForOrgAsync(Guid orgId, CancellationToken ct)`
     - `GetAllActiveRulesAsync(CancellationToken ct)`
   - Returns `ExpectedDocRuleDto` records with `OrgId`, `AssetId`, `AssetName`, `AssetType`, `DocType`, `Frequency`, `DueDayOfMonth`, `ResponsibleUserId`.
