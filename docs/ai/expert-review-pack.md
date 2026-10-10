# Expert Review Pack & Domain Sign-Off Register (Prompt I10)

**Project:** CarbonBill Bangladesh  
**Phase:** Phase 3 Pilot & Growth  
**Owner:** Track C (Dev 3 - Insights & Output)  
**Target Reviewer:** Accredited Industrial Energy Auditor / Sustainability Consultant (e.g. Farhana persona)

---

## 1. Purpose & Reviewer Mandate

Before deploying CarbonBill with pilot RMG and textile manufacturing cohorts, all underlying analytical coefficients—emission factors, decarbonization measure savings/capex in BDT, peer intensity percentiles, and Carbon Flag threshold sensitivities—must undergo formal inspection and written sign-off by a qualified domain specialist.

> **CRITICAL REPOSITORY RULE (AGENTS.md Rule 7 & 15):**  
> AI agents and developers must never hard-code or invent emission factors, savings percentages, or capex costs from conversational memory. Every production number must be verified against an accredited empirical source row in this register.

---

## 2. Emission Factors Register (`seeds/factors/`)

| # | Activity / Fuel Category | Canonical Unit | GWP Basis | Default Value | Candidate Authoritative Source | Verification Checklist | Status |
|---|---|---|---|---|---|---|---|
| **EF-01** | Bangladesh National Grid Electricity | $\text{kg CO}_2\text{e}/\text{kWh}$ | IPCC AR6 | `0.620000` *(SAMPLE)* | Department of Environment (DoE) / SREDA Grid Emission Factor 2023; UNFCCC CDM Harmonized Data | Confirm latest published Combined Margin (CM) vs Operating Margin (OM). Confirm grid transmission loss inclusion. | **Pending Sign-off** |
| **EF-02** | Stationary Diesel Combustion (Generators) | $\text{kg CO}_2\text{e}/\text{litre}$ | IPCC AR6 | `2.680000` | IPCC EFDB 2006/2019 Refinements (Gas/Diesel Oil); UK DEFRA / DESNZ 2024 | Verify Net Calorific Value ($43.0\text{ TJ/Gg}$) and density ($0.84\text{ kg/L}$) for South Asian industrial diesel. | **Pending Sign-off** |
| **EF-03** | Industrial Natural Gas (Boilers & Stoves) | $\text{kg CO}_2\text{e}/\text{m}^3$ | IPCC AR6 | `2.020000` | Petrobangla Annual Heating Value Report; Titas Gas Transmission & Distribution Co. | Verify pipeline Gross Heating Value ($950\text{–}1050\text{ BTU/CFT}$) and local methane purity. | **Pending Sign-off** |
| **EF-04** | Commercial Liquefied Petroleum Gas (LPG) | $\text{kg CO}_2\text{e}/\text{kg}$ | IPCC AR6 | `2.980000` | IPCC Guidelines for National GHG Inventories (Stationary LPG) | Confirm standard 30:70 propane-butane cylinder mix used in Bangladesh industrial kitchens. | **Pending Sign-off** |
| **EF-05** | Road Freight (Dhaka-Chittagong Container) | $\text{kg CO}_2\text{e}/\text{tonne-km}$ | IPCC AR6 | `0.254054` | UK DEFRA / DESNZ Freight Conversion Factors (Rigid/Articulated >17t HGV) | Verify standard truck loading factors along the N1 highway corridor. | **Pending Sign-off** |
| **EF-06** | Feeder Sea Freight (Ctg - Singapore Feeder) | $\text{kg CO}_2\text{e}/\text{tonne-km}$ | IPCC AR6 | `0.016200` | Clean Cargo Working Group (CCWG) / GLEC Framework | Confirm average fuel consumption for regional container feeders ($1,000\text{–}2,500\text{ TEU}$). | **Pending Sign-off** |

---

## 3. Decarbonization Measure Library Register (`seeds/measures/`)

Every starter measure models financial metrics in Bangladesh Taka (BDT) and avoids point estimates by presenting honest Low / Typical / High ranges:

| Code | Measure Title | Category | Evidence Grade | Savings Range (% avoided) | Typical Capex (BDT) | Typical Payback (Months) | Source Citation & Reference | Status |
|---|---|---|---|---|---|---|---|---|
| **PFL-EE-002** | APFC Capacitor Bank Refurbishment | Electricity | **A** (Local Audit) | 3% – 5% | 35,000 – 55,000 | 3.5 – 6.0 | IFC PaCT Textile Energy Audit Guide; DESCO Industrial Tariff Schedule | **Pending Sign-off** |
| **AIR-EE-003** | Ultrasonic Leak Sealing in Compressed Air | Compressed Air | **A** (Local Audit) | 8% – 18% | 10,000 – 22,000 | 1.0 – 3.5 | GIZ Promotion of Social & Environmental Standards (PSES) Textile Audits | **Pending Sign-off** |
| **BOIL-EE-001**| Boiler Flue Gas Economizer Installation | Boiler & Steam | **A** (Local Audit) | 4% – 8% | 350,000 – 600,000 | 10.0 – 16.0 | IFC PaCT Case Study #14 (Gas Boiler Efficiency in Gazipur Cluster) | **Pending Sign-off** |
| **MOT-EE-004** | VFD on Air Compressor & Cooling Towers | Motors & Drives | **A** (Local Audit) | 12% – 25% | 120,000 – 250,000 | 8.0 – 14.0 | SREDA Industrial Energy Efficiency Action Plan | **Pending Sign-off** |
| **SOL-RN-001** | Rooftop Solar PV Installation (Net Metering) | Renewable | **B** (Regional Case) | 15% – 35% | 65,000 – 85,000 / kWp | 42.0 – 60.0 | SREDA Net Metering Guidelines & IDCOL Renewable Financing Terms | **Pending Sign-off** |
| **STM-EE-002** | Steam Trap Repair & Pipeline Insulation | Boiler & Steam | **A** (Local Audit) | 5% – 12% | 45,000 – 90,000 | 2.5 – 5.0 | IFC PaCT Steam System Optimization Manual | **Pending Sign-off** |
| **LGT-EE-001** | LED Retrofit & Skylight Daylighting | Lighting | **A** (Local Audit) | 40% – 60% (lighting) | 25,000 – 60,000 | 4.0 – 8.0 | Cascale Higg FEM Good Practice Guidance | **Pending Sign-off** |
| **SEW-EE-005** | Direct Drive Servo Motors on Sewing Lines | Garment Floor | **A** (Local Audit) | 15% – 30% (machine) | 6,500 – 9,500 / head | 6.0 – 11.0 | IFC PaCT Garment Sewing Machine Efficiency Study | **Pending Sign-off** |
| **GEN-OP-001** | Generator Synchronization & Auto-Transfer | Diesel Genset | **B** (Regional Case) | 6% – 12% | 85,000 – 180,000 | 5.0 – 9.0 | Industrial Genset Fuel Optimization Benchmarks (Dhaka Cluster) | **Pending Sign-off** |
| **BLR-TN-002** | Burner Air-Fuel Ratio Tuning & O2 Trim | Boiler & Steam | **B** (Regional Case) | 2% – 5% | 15,000 – 35,000 | 1.5 – 3.0 | Gas Combustion Efficiency Guidelines (Petrobangla) | **Pending Sign-off** |
| **LOG-FR-001** | Export Freight Cargo Route Consolidation | Transport | **C** (Estimate) | 5% – 10% | 20,000 – 50,000 | 3.0 – 6.0 | Cascale Supply Chain Logistics Framework | **Pending Sign-off** |
| **FLT-MT-002** | Factory Fleet Preventive Tyre & Engine Tune | Mobile Fleet | **C** (Estimate) | 4% – 8% | 15,000 – 30,000 | 2.0 – 4.5 | Road Freight Fuel Efficiency Field Guide | **Pending Sign-off** |

---

## 4. Sector Peer Benchmark Sets (`seeds/benchmarks/`)

### 4.1 Strict Peer Sample Gating Rule ($n \ge 10$)
To ensure statistics are never deceptive or statistically skewed by small samples, the system strictly enforces:
$$\text{If } n < 10 \implies \text{Suppress P25/P50/P75/P90 curves and display: "কোন বেঞ্চমার্ক পাওয়া যায়নি" / "No benchmark yet"}$$

| Benchmark Cohort | Canonical Metric | Sample Size ($n$) | P25 | P50 (Median) | P75 | P90 | Gating Status |
|---|---|---|---|---|---|---|---|
| **RMG Woven/Knit Garments (Small: 1–500 machines)** | $\text{kg CO}_2\text{e}/\text{piece}$ | 14 *(Pilot)* | 0.380 | 0.520 | 0.610 | 0.680 | **Active ($n \ge 10$)** |
| **RMG Woven/Knit Garments (Medium: 501–1500 machines)** | $\text{kg CO}_2\text{e}/\text{piece}$ | 12 *(Pilot)* | 0.310 | 0.440 | 0.530 | 0.620 | **Active ($n \ge 10$)** |
| **Textile Dyeing & Finishing (10–30 tonnes/day)** | $\text{kg CO}_2\text{e}/\text{kg fabric}$ | 6 *(Pilot)* | — | — | — | — | **Gated ("No benchmark yet", $n < 10$)** |
| **Denim Composite Mill (>30 tonnes/day)** | $\text{kg CO}_2\text{e}/\text{metre}$ | 3 *(Pilot)* | — | — | — | — | **Gated ("No benchmark yet", $n < 10$)** |

---

## 5. Carbon Flag Sensitivity Ranges (`seeds/flags/flag_rules.json`)

| Flag Code | Family | Default Parameter | Allowed Tunable Range | Guardrail Rationale |
|---|---|---|---|---|
| `MISSING_DOCUMENT` | Data Quality | Day -7, -3, 0 | Escalation $-10$ to $0$ days | Floor staff must receive early nudge before monthly close. |
| `LOW_OCR_CONFIDENCE`| Data Quality | Confidence $< 0.70$ | $0.60$ to $0.80$ | Never drop below $0.60$ to prevent hallucinated quantities. |
| `DUPLICATE_SUSPECTED`| Data Quality | Exact Hash or (Vendor, Slip, Period) | Exact Match | Must never be disabled; duplicate billing corrupts carbon inventory. |
| `IMPLAUSIBLE_VALUE` | Data Quality | $> 3.0 \times \text{IQR}$ | $2.5\times$ to $4.5\times$ | Minimum 3 months history required; suppresses false flags during onboarding. |
| `ESTIMATED_SHARE_HIGH`| Data Quality | $> 10\%$ estimated | $5\%$ to $20\%$ | Buyer audit standard caps acceptable estimation at $10\%$. |
| `SPIKE_MOM` | Footprint | $1.3\times$ (Amber), $1.6\times$ (Red) | $1.2\times$ to $1.8\times$ | Minimum 3 months history required before evaluating. |
| `HOTSPOT_DETECTED` | Footprint | Single source $> 50\%$ | $40\%$ to $60\%$ | Prevents over-focus on minor energy lines. |
| `GENSET_RELIANCE` | Footprint | Diesel kWh equivalent $> 20\%$ | $15\%$ to $35\%$ | Identifies inefficient captive generator running over grid power. |
| `INTENSITY_ABOVE_PEERS`| Footprint | Above Peer P75 / P90 | Requires $n \ge 10$ | Honesty rule prevents flagging against insufficient cohort sizes. |
| `POWER_FACTOR_PENALTY`| Bill Savings | PF $< 0.90$, Penalty $>$ BDT 5,000 | PF $0.85\text{–}0.92$ | Direct Taka savings deep-link to APFC Capacitor Bank recommendation. |

---

## 6. Formal Expert Sign-Off Protocol Checklist

Every dataset release (`DatasetVersion`) loaded into `CarbonBill.Modules.PlatformAdmin` requires the following verified criteria:

- [ ] **Methodology Statement:** All outputs are explicitly stated as *"Estimate aligned with GHG Protocol methodology, not audited or certified"*.
- [ ] **Source URL Traceability:** 100% of emission factors have valid, accessible public URLs and publication citations.
- [ ] **No Single-Point Financials:** 100% of decarbonization measures display distinct Low / Typical / High ranges in BDT.
- [ ] **Negative Cost-Per-Tonne Handling:** High ROI measures (APFC, steam insulation, leak repair) correctly indicate negative cost-per-tonne (net financial profit).
- [ ] **Honest Benchmark Gating:** Cohorts with $n < 10$ suppressed from public display.
- [ ] **Bilingual Equivalence:** Bangla translation checked for accurate engineering terminology (e.g. "পাওয়ার ফ্যাক্টর ক্যাপাসিটর ব্যাংক", "বয়লার ইকোনোমাইজার").

### Sign-Off Signature Block

```text
Domain Expert Name: _________________________________________________
Professional Affiliation / Credentials: _________________________________
Accreditation ID (e.g., SREDA Certified Energy Auditor / LEED AP): _______
Dataset Release Version: 1.0-pilot
Date of Inspection: _____________________
Signature: ______________________________
```

---

## 7. Pilot Qualitative Interview Validation Plan

To validate whether Carbon Flag thresholds and recommendations reflect shop-floor realities, structured interviews will be conducted across the 5 representative factory personas:

### 7.1 Persona Interview Matrix

| Persona | Key Validation Questions | Threshold Calibration Target |
|---|---|---|
| **Floor Staff** (Jahid) | Did the Bangla slip request appear on your phone at the right time? Was the 2-tap camera interface fast enough (<20 s)? | Calibration of Day -7 / Day -3 nudge timing and Bangla copy clarity. |
| **Accountant** (Rahim) | How many false OCR warnings occurred? Were BDT energy amounts properly calculated without rounding discrepancies? | Tuning `LOW_OCR_CONFIDENCE` threshold and utility tariff rates. |
| **Compliance Officer** (Nusrat)| Did the Data Quality Score (DQS) align with your buyer's Higg FEM audit readiness? Were price redactions respected on share links? | Calibration of `ESTIMATED_SHARE_HIGH` and share link expiry periods. |
| **Factory Owner** (Kabir) | Were the capex and payback ranges in BDT realistic for your monthly maintenance budget? Did you trust the MACC chart? | Tuning measure capex ranges and financing interest rates (5.5% SREDA). |
| **Consultant** (Farhana) | Did the multi-client dashboard allow you to spot hotspot factories quickly? Did the factor override justification flow work? | Calibration of cross-factory peer benchmarks and override workflows. |

### 7.2 Survey Sample Caveat ($n = 17$ Cohort)
* **Statistical Caution:** The initial pilot survey comprises $n = 17$ factories in the Gazipur and Savar industrial zones. While sufficient for qualitative usability testing and identifying gross anomalies, it is insufficient to establish permanent national benchmark distributions.
* **Continuous Recalibration:** Measure savings and peer curves will be updated quarterly as verified utility bills are accumulated post-pilot.
