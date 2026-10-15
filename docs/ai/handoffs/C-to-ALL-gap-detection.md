# Handoff: C-to-ALL-gap-detection

- **From**: Dev 3 (Insights and Output)
- **To**: Dev 1, Dev 2, All
- **Date**: 2026-10-09
- **Topic**: Gap Detection Engine, Missing Alerts, and Document Uploaded Event Integration

---

## 1. What was built in Deliverable C1

1. **Gap Detection Domain & Persistence**:
   - `MissingAlert` aggregate root (`ITenantScopedEntity`) in `CarbonBill.Modules.GapDetection.Domain`.
   - Dual tenant isolation via EF Core global query filters and PostgreSQL RLS compatibility.
   - Escalation timeline state machine:
     - **Day -7**: Calendar reminder (`DayMinus7`, Severity: `Amber`, Status: `Reminded`).
     - **Day -3**: Push alert to responsible floor/accountant user (`DayMinus3`, Severity: `Amber`, Status: `Reminded`).
     - **Day 0 (Due Date) & Overdue**: Escalated to Compliance Officer / Factory Owner (`DayZero`, Severity: `Red`, Status: `Escalated`).
   - Plain-language bilingual request copy formatted with Bangla digits (`১২৩৪৫৬৭৮৯০`) and asset translation:
     - EN: `"Please send diesel slip for Generator 2"`
     - BN: `"অনুগ্রহ করে জেনারেটর ২-এর ডিজেল স্লিপ পাঠান"`

2. **Integration Contracts & Events**:
   - `IExpectedDocRuleReader` in `CarbonBill.SharedKernel.Contracts` (implemented by `Onboarding` and `FakeExpectedDocRuleReader`).
   - `IDocumentReadModel` in `CarbonBill.SharedKernel.Contracts` (implemented by `FakeDocumentReadModel`).
   - `DocumentUploadedEvent` in `CarbonBill.SharedKernel.Events` (published on upload, consumed by `DocumentUploadedEventHandler` to auto-resolve matching missing alerts).
   - `MissingAlertRaisedEvent` & `MissingAlertResolvedEvent` in `CarbonBill.SharedKernel.Events`.

3. **Background Jobs & Endpoints**:
   - `NightlyGapDetectionJob` registered as recurring Hangfire daily job (`gap-detection-nightly`).
   - Endpoints under `/api/v1/gaps`:
     - `GET /api/v1/gaps?period={period}&status={status}`: List missing alerts for tenant.
     - `POST /api/v1/gaps/{id}/nudge`: Nudge responsible staff, updates nudge count and timestamp.
     - `POST /api/v1/gaps/evaluate?period={period}`: Manually trigger evaluation for active tenant.

4. **Automated Test Suite**:
   - `GapDetectionTests.cs` in `tests/CarbonBill.UnitTests`:
     - Escalation timeline verification with fake clock across Days -15, -7, -3, 0, +5.
     - Auto-resolution on document arrival.
     - Event-driven resolution via `DocumentUploadedEventHandler`.
     - Multi-asset organization tracking (only submitted asset resolves, others stay open).
     - Strict tenant-isolation test verifying cross-tenant query prevention and cross-tenant nudge rejection.
     - Nudge counter and resolved state rejection tests.
