# Handoff: C-to-ALL-reporting.md

**From:** Dev 3 (Track C - Insights & Reporting)  
**To:** Dev 1 (Track A - Platform & Identity), Dev 2 (Track B - Capture & UI Shell)  
**Date:** 2026-10-09  
**Branch:** `feat/c-c6-reporting-backend`  
**Deliverable:** C6 - Reporting Backend (PDF, Excel, Auditor Link, Snapshots, and Dashboards)

---

## 1. Summary of Changes

Deliverable C6 implements the reporting backend for CarbonBill in `src/CarbonBill.Modules.Reporting`.
It provides:
1. **GHG Protocol Aligned PDF Report Generation** via `QuestPdfReportRenderer` implementing `IReportRenderer`.
   - Bilingual support (`en` and `bn` with native Bangla numerals `১২৩৪৫৬৭৮৯০`).
   - 6-part structure: Cover (with DQS badge & GHG Protocol disclaimer), Executive Summary (Scope 1/2/3 split & intensity), Monthly & Activity Ledger, Factor Registry Appendix, Document Audit Trail Appendix, and Limitations/Boundary statement.
   - Price redaction mode (`RedactPrices == true` masks money amounts as `[Confidential]` / `[গোপনীয়]`).
2. **ClosedXML Multi-Tab Excel Workbook** via `ClosedXmlReportRenderer`.
   - Sheets: `Summary`, `Monthly Emissions`, `Factor Registry`, `Document Provenance`.
   - Exact decimal precision formatting: 2 decimals for BDT currency, 6 decimals for activity factors and emissions.
3. **Cryptographic Snapshots & Immutability**:
   - `ReportSnapshot` records an immutable JSON snapshot with SHA-256 `ContentHashSha256`.
   - Once a report transitions to `Locked`, it is completely immutable. Re-approval or modification throws `InvalidOperationException`.
4. **Secure Auditor Share Links**:
   - Expiring tokenized URLs (`/api/v1/reports/share/{token}`) with optional price redaction and view count auditing.
   - Auditor trace endpoint (`/api/v1/reports/{id}/auditor/trace`) mapping every ledger line item back to verified source document IDs, OCR confidence, and approving users.
5. **Dashboard Endpoints**:
   - `GET /api/v1/dashboard/summary?period={YYYY-MM}`
   - `GET /api/v1/dashboard/trend?start={YYYY-MM}&end={YYYY-MM}` (reports verified vs. estimated portions with `HatchFlag` for estimated shares).
6. **Buyer Template Mappings**:
   - Seeded in `seeds/reporting/buyer_template_mapping.json` for Higg FEM, Inditex, and H&M export alignment.

---

## 2. API Endpoints Available

All endpoints are registered under `/api/v1`:

| Method | Path | Description | Access / Roles |
|---|---|---|---|
| `POST` | `/api/v1/reports` | Create draft report | Org Admin, Compliance Officer |
| `POST` | `/api/v1/reports/{id}/submit-review` | Transition draft to `ReadyForReview` | Org Admin, Compliance Officer |
| `POST` | `/api/v1/reports/{id}/approve` | Approve report, freeze snapshot, lock report | Compliance Officer, Org Admin |
| `GET` | `/api/v1/reports/{id}/pdf?lang=en&redactPrices=false` | Render and stream buyer PDF report | Org Admin, Compliance Officer, Auditor |
| `GET` | `/api/v1/reports/{id}/excel?redactPrices=false` | Render and stream buyer Excel workbook | Org Admin, Compliance Officer, Auditor |
| `POST` | `/api/v1/reports/{id}/share` | Generate expiring share link token | Org Admin, Compliance Officer |
| `GET` | `/api/v1/reports/share/{token}` | Public/auditor access to shared report snapshot | Public with valid unexpired token |
| `GET` | `/api/v1/reports/{id}/auditor/trace` | Get click-to-source provenance tree | Org Admin, Compliance Officer, Auditor |
| `GET` | `/api/v1/dashboard/summary?period=YYYY-MM` | Dashboard summary cards (Scope 1/2/3, DQS, status) | All authenticated org members |
| `GET` | `/api/v1/dashboard/trend?start=YYYY-MM&end=YYYY-MM` | Monthly footprint trend with hatch flags | All authenticated org members |

---

## 3. Integration Points for Dev 1 and Dev 2

### For Dev 1 (Track A - Platform & Contracts)
- `ReportingDbContext` is registered and integrated with the multi-tenant query filter on `TenantId` / `OrgId`.
- Decimal precision complies with specifications: `decimal(18,2)` for financial fields, `decimal(18,6)` for emissions/factors, and `decimal(5,4)` for DQS scores.
- When generating reports, `ReportingService` consumes activity records and calculation models.

### For Dev 2 (Track B - UI Shell & Capture)
- Frontend features for `reports` and `dashboard` can now consume these endpoints directly.
- The Auditor view in the web UI can leverage `GET /api/v1/reports/{id}/auditor/trace` to render interactive "click-to-source" inspector cards linking emission totals to document IDs.
- For share links, frontend can generate links pointing to `/reports/share/{token}`.

---

## 4. Verification & Testing

- Automated tests in `tests/CarbonBill.UnitTests/ReportingBackendTests.cs` cover:
  - SHA-256 snapshot stability and determinism.
  - Locked report state immutability.
  - Expiring share link validation with `FakeTimeProvider`.
  - Price redaction masking (`[Confidential]` / `[গোপনীয়]`) in PDF and Excel.
  - Golden text verification for both English and Bangla PDFs.
  - Auditor trace document mapping.
  - Dashboard summary and trend aggregations.
