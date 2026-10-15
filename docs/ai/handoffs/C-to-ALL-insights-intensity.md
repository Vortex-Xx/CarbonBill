# Handoff: C-to-ALL-insights-intensity

- **From**: Dev 3 (Insights, Flags & Output)
- **To**: Dev 1 (Platform & Data Backbone), Dev 2 (Capture & Frontend Shell), All
- **Date**: 2026-10-09
- **Topic**: Insights Module, Emission Intensity, Benchmark Honesty Gating & Footprint Evaluators Ready

---

## 1. What Was Built

1. **Insights Module (`CarbonBill.Modules.Insights`)**:
   - `ProductionMetric` domain entity: Tenant-scoped monthly production quantity (`pieces`, `kg_fabric`, `tonnes`) with dual tenant isolation.
   - `BenchmarkSet` domain entity: Sector and size-band peer distribution (`P25`, `P50`, `P75`, `P90`, sample size `N`, source, publication year).
   - `InsightsDbContext`: Configured with EF Core query filters and table mappings.
   - `InsightsSeedLoader`: Seeded benchmark distributions for RMG (Medium $N=32$, Large $N=24$, Small $N=18$) and Textile Dyeing ($N=6$).

2. **Intensity Calculation Engine**:
   - Dual variant calculation:
     $$\text{Verified-Only Intensity} = \frac{\text{Verified kg CO}_2\text{e}}{\text{Production Units}}$$
     $$\text{Including-Estimate Intensity} = \frac{\text{Total kg CO}_2\text{e}}{\text{Production Units}}$$
   - Monthly trend calculation joining trailing emissions with historical monthly production metrics.

3. **Strict Benchmark Honesty Gating**:
   - If peer reference sample $N < 10$:
     - `HasBenchmark: false`
     - `Message: "no benchmark yet"`
     - `MessageBn: "প্রতুল তথ্য এখনও পাওয়া যায়নি"`
     - `P25`, `P50`, `P75`, `P90` are returned as `null` (never synthesized or invented).
   - If $N \ge 10$: Returns full peer distribution for benchmarking.

4. **Carbon Flag Evaluators (Footprint, Readiness, Bill Savings)**:
   All rule thresholds are loaded dynamically from `FlagRule.TriggerParametersJson`:
   - `SPIKE_MOM`: Month-on-month spike vs trailing 3-month median ($>1.3\times$ Amber, $>1.6\times$ Red). Requires $\ge 3$ months history; produces no flag when history $< 3$ months.
   - `HOTSPOT_DETECTED`: Single source accounts for $> 50\%$ of factory's footprint (Info).
   - `GENSET_RELIANCE`: Diesel generator electricity $> 20\%$ of monthly electricity (Amber).
   - `INTENSITY_ABOVE_PEERS`: Current intensity $>$ P75 (Amber) or $>$ P90 (Red), gated by $N \ge 10$.
   - `TARGET_DRIFT`: Cumulative footprint drifts $> 5\%$ above trajectory target (Amber).
   - `REPORT_NOT_READY`: Unconfirmed slips, missing months, or Data Quality Score $< 80\%$ (Red).
   - `FACTOR_OUTDATED`: Active factor set reference year is older than reporting period year (Amber).
   - `OVERRIDE_UNAPPROVED`: Factor override without approved compliance sign-off or justification (Red).
   - `POWER_FACTOR_PENALTY`: Power factor $< 0.90$ or penalty line detected on utility bill (Info).

5. **REST Endpoints**:
   - `GET /api/v1/dashboard/intensity?period=YYYY-MM&sector=RMG&sizeBand=Medium`: Intensity summary, peer distribution, and historical monthly trend.
   - `POST /api/v1/insights/production-metrics`: Captures monthly production volume (validated $> 0$, `YYYY-MM`).
   - `GET /api/v1/insights/production-metrics`: Lists tenant production metrics.

6. **Inter-Module Contracts in `CarbonBill.SharedKernel.Contracts`**:
   - `IEmissionReadModel`: Summary, scope breakdown, and monthly trend queries.
   - `IProductionMetricReader`: Production volume lookup by org, site, and period.
   - `IBenchmarkReader`: Peer distribution lookup by sector, size band, and metric.
