# PROGRESS_C: Track C Progress Tracker (Dev 3)

## Deliverables Status

| Prompt | Description | Status | Branch / PR | Notes |
|---|---|---|---|---|
| **P0-1c** | Rules, flags, recommendations, reports & security docs | done | `docs/p0-1c` | Specs 07 through 12, Track C and Progress C |
| **P0-4** | Seeds, fixtures & dev dataset | done | `feat/c-p0-4-seeds` | Seed schemas, templates, samples, and 14-flag dev dataset |
| **C1** | Gap detection engine | done | `feat/c-c1-gap-detection` | MissingAlert entity, nightly Hangfire job & DocumentUploaded handler, Day -7/-3/0 escalation, bilingual plain requests, auto-resolve |
| **C2** | Notifications subsystem | done | `feat/c-c2-notifications` | In-app, Web Push VAPID, Brevo/Resend/Logging email with Polly backoff, bilingual bn/en templates, Bangla numerals, top 5 weekly digest, alert collapse, snooze with reason, event subscriptions |
| **C3** | Flags engine & data-quality rules | done | `feat/c-c3-flags` | FlagRule (runtime tunable, seed loader), Flag entity, IFlagRuleEvaluator for 5 data-quality rules, auto-resolve lifecycle, 30-day dismissal expiry, IFlagRaiser & IFlagReader, top 5 dashboard query, nightly Hangfire job, DocumentConfirmed & EmissionCalculated event handlers |
| **C4** | Footprint flags, intensity & benchmarks | done | `feat/c-c4-insights-footprint-flags` | Insights module (ProductionMetric, BenchmarkSet), verified vs incl-estimate intensity, honest peer gating (N >= 10), 9 evaluators (MoM spike, hotspot, genset reliance, intensity above peers, target drift, report not ready, factor outdated, override unapproved, power factor penalty), GET /dashboard/intensity & POST/GET /insights/production-metrics |
| **C5** | Measure Library & recommendations engine | done | `feat/c-c5-recommendations-engine` | Measure & MeasureSource entities, versioned dataset loader with strict source validation & range ordering, 8-step pipeline (profile, eligibility, impact, financial BDT & negative cost/t, realism & audit filter, ranking composite, bilingual explanation cards, closed-loop status update & bill delta), GET /recommendations, PUT /recommendations/{id}/status, POST /profile/facility |
| **C6** | Reporting backend (PDF, Excel, Auditor link) | done | `feat/c-c6-reporting-backend` | QuestPDF bilingual GHG report (en/bn), ClosedXML multi-sheet export, SHA-256 ReportSnapshot hash & immutability, expiring share links, price redaction masking, click-to-source auditor trace, dashboard summary and trend endpoints |
| **C7** | Frontend (dashboard, flags, recommendations, reports, auditor) | done | `feat/c-c7-frontend` | Role dashboards (Owner, Accountant, Compliance, Consultant), Chart.js trend with hatched estimated segments, Scope breakdown, intensity vs benchmark with honest n<10 gating, Carbon Flags feed with snooze/dismiss modals, Measure Library cards with Low/Typical/High ranges in BDT, MACC chart, reports readiness checklist, sign-off approval, share link modal with price redaction, auditor read-only portal with click-to-source traceability |
| **I3** | Role dashboards & buyer PDF on real data | done | `feat/c-phase2-phase3-integration` | Real data reconciliation and golden PDF verification with seeds/dev dataset, auditor trace fix, bilingual QuestPDF & ClosedXML tests |
| **I6** | Observability & flag tuning | done | `feat/c-phase2-phase3-integration` | OpenTelemetry business metrics (System.Diagnostics.Metrics), GET /api/v1/metrics/business endpoint, Grafana Cloud dashboard JSON, Sentry wiring and docs/ai/flag-tuning.md |
| **I10**| Pilot data & expert review | done | `feat/c-phase2-phase3-integration` | Complete expert review pack, source register, n>=10 benchmark gating, and pilot interview validation plan in docs/ai/expert-review-pack.md |

---

**Blocked on:** None. All Track C deliverables across Phase 0, Phase 1, Phase 2, and Phase 3 (P0-1c, P0-4, C1–C7, I3, I6, I10) are COMPLETE!

**Next prompt:** Phase 2/3 Integration & Gate G4 sign-off with other tracks.

**Open questions logged in 15_DECISIONS.md:**
- QuestPDF Bangla font glyph rendering spike validated under ADR-005.
- Expert reviewer appointment for Measure Library and emission factors.
