# Handoff: A-to-C-emissions-read

- **From**: Dev 1 (Platform & Data Backbone)
- **To**: Dev 3 (Insights, Flags & Reporting), All
- **Date**: 2026-10-08
- **Topic**: Emission Read Model & Emission Calculation APIs Ready

---

## 1. What was built

1. **Emission Query Interface (`IEmissionReadModel`)**:
   - `GetSummaryAsync(Guid orgId, string? period)`: Returns total kg CO2e, verified kg CO2e, estimated kg CO2e, Scope 1/2/3 breakdown, and computed Data Quality Score (0..100%).
   - `GetScopeBreakdownAsync(Guid orgId, string? period)`: Returns categorical breakdown per Scope with percentages and standard quantities.
   - `GetMonthlyTrendAsync(Guid orgId, int months = 12)`: Returns trailing monthly emissions, scope breakdowns, and historical data quality scores.

2. **Emission REST Endpoints**:
   - `GET /api/v1/emissions/summary`
   - `GET /api/v1/emissions/breakdown`
   - `GET /api/v1/emissions/trend`
