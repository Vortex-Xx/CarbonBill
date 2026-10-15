# Handoff: C-to-ALL-recommendations

- **From**: Dev 3 (Insights and Output)
- **To**: Dev 1 (Platform Backbone), Dev 2 (Capture and Extraction), All
- **Date**: 2026-10-09
- **Topic**: Recommendations Engine, Measure Library, Versioned Dataset Loader, and Shared Facility Profile

---

## 1. What was built in Deliverable C5

1. **Domain Entities & Dual Isolation**:
   - `MeasureSource`: Research source metadata (`SourceCode`, `Title`, `Institution`, `Year`, `LicenseTerms`, `CandidateStatus`).
   - `Measure`: Energy-efficiency initiative (`MeasureCode`, `NameBn`, `NameEn`, `Category`, `ApplicableSectorsJson`, `ApplicabilityRulesJson`, `SavingLow/Typical/High`, `CapexLow/High`, `LifetimeYears`, `EvidenceGrade` [A/B/C], `SourceId`).
   - `FacilityProfile`: Facility questionnaire and energy baseline activities per tenant (`BaselineMonthlyKwh`, `BaselineMonthlyDieselLitres`, `BaselineMonthlyGasM3`, `ElectricityTariffBdtPerKwh`, `DieselTariffBdtPerLitre`, `GasTariffBdtPerM3`).
   - `Recommendation`: Ranked initiative per tenant (`SavingRangeCo2eJson`, `SavingRangeBdtJson`, `CapexRangeBdtJson`, `PaybackYears`, `CostPerTco2e`, `CompositeScore`, `Rank`, `Status` [Suggested/Planned/Done/NotFeasible], `StatusReason`, `NeedsEnergyAudit`, `RealisedSavingBdt`, `RealisedTco2eAvoided`, `EvidenceUpgradeRecorded`).

2. **Versioned Dataset Loader (`IDatasetLoader<Measure>`)**:
   - `MeasureDatasetLoader`:
     - Mandatory source verification: Fails validation with `InvalidOperationException` if any numeric field is provided without a verifiable `MeasureSource` row.
     - Strict range ordering validation: Enforces `SavingLow <= SavingTypical <= SavingHigh` and `CapexLow <= CapexHigh`.
     - Evidence grade validation: Accepts only standard grades `'A'`, `'B'`, or `'C'`.
     - Seeds loaded from `seeds/measures/measure_sources_TEMPLATE.json` and `seeds/measures/measure_library_SAMPLE.json` (strictly using `FAKE_` measures for tests/dev).

3. **8-Step Deterministic Engineering Pipeline**:
   - **Step 1 (`ProfileIngestionStep`)**: Combines questionnaire with baseline consumption and calculates factory's own effective tariffs directly from bills (BDT/kWh and BDT/litre).
   - **Step 2 (`EligibilityFilterStep`)**: Machine-readable rule evaluation (`has_boiler`, `has_genset`, `monthly_kwh_gt`). Strict rule: Rooftop solar is eliminated if roof area is unknown/zero or building ownership is rented.
   - **Step 3 (`CarbonImpactStep`)**: Computes avoided GHG emissions ($\text{tCO}_2\text{e} = \text{baseline} \times \text{saving\%} \times \text{factor} / 1000$) across Low, Typical, and High ranges.
   - **Step 4 (`FinancialModelingStep`)**: Annual savings in BDT ($= \text{avoided units} \times \text{tariff}$), simple payback ($= \text{capex} / \text{annual saving}$), and cost per avoided $\text{tCO}_2\text{e}$ ($= (\text{annualised capex} - \text{annual saving}) / \text{avoided tCO}_2\text{e}$). Accurately preserves negative cost-per-tonne indicating net-profitable abatement.
   - **Step 5 (`RealismFilterStep`)**: Evaluates budget feasibility against factory's stated budget band. Flags `NeedsEnergyAudit = true` when capex exceeds threshold ($> 1,000,000$ BDT).
   - **Step 6 (`RankingStep`)**: Multi-factor composite scoring ($w_1 \cdot \text{Payback} + w_2 \cdot \text{Impact} + w_3 \cdot \text{Grade} - w_4 \cdot \text{Capex}$) with configurable weights from appsettings. Ranks top 5 for quick cards and entire portfolio for Marginal Abatement Cost Curve (MACC).
   - **Step 7 (`ExplanationStep`)**: Generates bilingual explanation cards in English and Bangla with Bengali numerals, explicit Low/Typical/High ranges, assumptions, next steps, source citations, and IDCOL / Bangladesh Bank green refinancing notes.
   - **Step 8 (`CloseTheLoopStep`)**: State transitions (`Planned`, `Done`, `NotFeasible`). Mandatory explanation reason required for `NotFeasible`. On `Done`, compares pre- and post-intervention utility bills to store empirical realised savings in BDT and logs evidence upgrade candidate data.

4. **REST Endpoints**:
   - `GET /api/v1/recommendations`: Returns `{ top5, all, summary }` for the tenant.
   - `PUT /api/v1/recommendations/{id}/status`: Transitions state (`Planned`, `Done`, `NotFeasible` with reason).
   - `POST /api/v1/profile/facility`: Shared facility questionnaire and baseline capture endpoint, recalculates recommendations.
   - `POST /api/v1/recommendations/recalculate`: Triggers immediate re-evaluation of pipeline.

5. **Coordination Note for Dev 1 (Onboarding)**:
   - `POST /api/v1/profile/facility` updates the questionnaire parameters used by both Onboarding (facility profile) and Recommendations (baseline energy and tariff calculations).
