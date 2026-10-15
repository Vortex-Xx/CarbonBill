# Handoff: A-to-B-activity-writer

- **From**: Dev 1 (Platform & Data Backbone)
- **To**: Dev 2 (Capture & Extraction), All
- **Date**: 2026-10-08
- **Topic**: Real IActivityWriter Implementation Ready

---

## 1. What was built

1. **`IActivityWriter` Real Implementation**:
   - `ActivityWriter` in `CarbonBill.Modules.Calculation.Services` is now fully implemented and registered in DI.
   - When `IActivityWriter.RecordConfirmedActivityAsync` is called by the Review confirmation workflow:
     1. Automatically runs canonical unit conversion via `IUnitConverter` (e.g. `MWh` -> `kWh`, `gallons` -> `litres`, `cft` -> `m3`).
     2. Persists `ActivityRecord` in `ActivityUnitsDbContext`.
     3. Resolves the emission factor via `IFactorLookup`, factoring in tenant-specific overrides and precedence rules.
     4. Computes GHG footprint using exact `decimal(18,6)` precision (`kg CO2e = quantity_standard * emission_factor`).
     5. Persists `EmissionResult` in `CalculationDbContext`.
     6. Appends an immutable audit entry via `IAuditLogService`.
     7. Returns `Result.Success(activityRecord.Id)`.

2. **Error Isolation**:
   - The method executes in a resilient transactional flow; on failure, it returns `Result.Failure<Guid>` with a detailed error description so the calling Review endpoint can abort without corrupted state.
