# Flag Tuning & Observability Guide (Prompt I6)

This document specifies the operational procedure for tuning Carbon Flag sensitivity thresholds, managing user alert fatigue, and configuring OpenTelemetry and Sentry across CarbonBill pilot deployments.

---

## 1. Alert Fatigue Controls

Carbon Flags are designed to highlight operational waste and audit risks without overwhelming factory personnel. CarbonBill enforces a three-layer fatigue control model:

### Layer 1: Top 5 Active Flag Restriction
* The Factory Owner and Compliance dashboards (`/dashboard`) only display the **top 5 prioritized flags** ordered by:
  1. **Severity:** `Critical` > `Warning` > `Info`.
  2. **Recency:** Latest period / unaddressed first.
* All additional open flags remain accessible under the full Carbon Flags feed (`/flags`), but do not clutter the executive home screen.

### Layer 2: Consolidated Weekly Digest
* Rather than triggering immediate notifications for every minor anomaly, low-severity and non-blocking flags are batched into a single weekly email digest delivered every Sunday morning.
* Only critical audit blockers (e.g., missing bill on Day 0 or severe MoM diesel spike > 1.6x) send real-time Web Push / SMS alerts.

### Layer 3: 30-Day Snooze & Dismissal Controls
* Users can snooze a persistent flag for 7, 14, or 30 days.
* Dismissing a flag requires entering a mandatory written reason (e.g., *"Production volume doubled in August due to Ramadan order surge; diesel usage legitimate"*).
* Dismissals automatically expire after 30 days to prevent permanent suppression of unresolved structural anomalies.

---

## 2. Pilot Threshold Tuning Procedure

All 14 Carbon Flags are runtime-tunable via the `flags.flag_rules` database table without recompilation or redeployment.

### 2.1 Tuning Cadence During Pilot
1. **Week 1 Baseline:** Run pilot factories with default thresholds (`seeds/flags/flag_rules.json`).
2. **Weekly Volume Review:** In Grafana, inspect panel **"Top 5 Flags by Trigger Frequency"** (`topk(5, sum by (rule_code, severity) (carbonbill_flags_raised_total))`).
3. **Fatigue Gate:** If any single rule triggers more than 3 times per month for a single factory, schedule a tuning review with the designated consultant (Farhana).

### 2.2 Tunable Rules & Sensitivity Calibration Matrix

| Flag Code | Default Threshold | Tunable Parameter | Calibration Direction | Pilot Rationale |
|---|---|---|---|---|
| `SPIKE_MOM` | 1.3x (Amber), 1.6x (Red) | `amber_ratio`, `red_ratio` | Increase to 1.4x / 1.75x if seasonal batch orders cause benign spikes | RMG dyeing batches fluctuate heavily month-to-month. |
| `LOW_OCR_CONFIDENCE` | 0.70 (Warning) | `min_confidence` | Lower to 0.65 for crumpled carbon-copy slips; raise to 0.80 for digital utility PDFs | Avoid false alarms on physical challans where numbers are still legible. |
| `IMPLAUSIBLE_VALUE` | $3.0 \times \text{IQR}$ | `iqr_multiplier` | Adjust between $2.5\times$ and $4.0\times$ based on historical variance | Factories with fewer than 6 months of historical data should use $3.5\times$. |
| `ESTIMATED_SHARE_HIGH`| 10% (Warning) | `max_estimated_share` | Increase to 15% during month 1 of onboarding | Early onboarding months may lack receipts before staff habit forms. |
| `GENSET_RELIANCE` | 20% (Warning) | `max_genset_kwh_share` | Adjust upwards to 25% during summer load-shedding peaks (April–June) | Grid instability in Gazipur/Savar requires heavy diesel backup. |
| `POWER_FACTOR_PENALTY`| 0.90 PF threshold | `pf_threshold`, `min_penalty_bdt` | Default penalty threshold BDT 5,000 | Suppresses trivial micro-penalties under BDT 1,000. |

### 2.3 Procedure to Update a Threshold in Production
```sql
-- Example: Adjusting MoM Spike sensitivity for an energetic pilot factory
UPDATE flags.flag_rules
SET trigger_parameters = jsonb_set(
    trigger_parameters, 
    '{amber_ratio}', 
    '1.45'
),
updated_at = NOW()
WHERE flag_code = 'SPIKE_MOM';
```

---

## 3. Observability Architecture (OpenTelemetry & Sentry)

### 3.1 OpenTelemetry Business Metrics
CarbonBill exposes native runtime business meters via `CarbonBill.Modules.Reporting.Observability.CarbonBillMetrics`:

* **Metrics Endpoint:** `GET /api/v1/metrics/business` (anonymous, internal/scraper access).
* **Metric Names:**
  - `carbonbill_documents_ingested_total`
  - `carbonbill_review_duration_seconds`
  - `carbonbill_ocr_tier_invocations_total`
  - `carbonbill_field_corrections_total` / `carbonbill_fields_confirmed_total`
  - `carbonbill_flags_raised_total` / `carbonbill_flags_resolved_total`
  - `carbonbill_recommendations_completed_total`

### 3.2 Grafana Cloud Integration
* Dashboard-as-code definition committed in [infra/grafana/carbonbill_insights_dashboard.json](file:///home/rakibul/Projects/CarbonBill/infra/grafana/carbonbill_insights_dashboard.json).
* Import directly into Grafana Cloud or local Prometheus/Grafana instance.

### 3.3 Sentry Exception Monitoring Setup

#### Server Configuration (`src/CarbonBill.Api/Program.cs`):
```csharp
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.TracesSampleRate = 0.2; // 20% in staging/production
    options.SendDefaultPii = false; // Never transmit customer utility bill contents
    options.Environment = builder.Environment.EnvironmentName;
});
```

#### Client Configuration (`web/src/main.tsx`):
```typescript
import * as Sentry from '@sentry/react';

if (import.meta.env.VITE_SENTRY_DSN) {
  Sentry.init({
    dsn: import.meta.env.VITE_SENTRY_DSN,
    integrations: [Sentry.browserTracingIntegration()],
    tracesSampleRate: 0.1,
    environment: import.meta.env.MODE,
    beforeSend(event) {
      // Strip any sensitive invoice amounts or PII
      return event;
    },
  });
}
```
