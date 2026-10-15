# CarbonBill Usability Testing Kit (Prompt I9)

**Author:** Track B Lead (Dev 2)  
**Standard:** Phase 3 Pilot Preparation (`Prompt I9`)  
**Target:** Factory Floor Staff, Accountants, and Compliance Officers  
**Success Criteria:** Floor staff submits a bill unaided in $<20$ seconds; Accountant reviews a bill in $<30$ seconds.

---

## 1. Testing Protocol & Objectives

The usability test kit evaluates real-world human interaction with CarbonBill across physical RMG factories in Bangladesh. Testing measures cognitive load, task completion time, language clarity (Bangla-first), and resilience to physical floor interruptions.

### Key Target Metrics (NFR Targets)
1. **Floor Staff Task Duration**: $<20$ seconds from camera icon tap to visual confirmation receipt.
2. **Floor Staff Error Rate**: Zero abandoned captures due to UI confusion.
3. **Accountant Verification Speed**: $<30$ seconds per bill in the split-screen review workspace.
4. **System Usability Scale (SUS)**: Target $\ge 82.0$ (Grade A).

---

## 2. Test Tasks per Persona

### Persona A: Jahid Hasan (Floor Staff — Persona 1)
* **Background**: Operates diesel generator and fabric boiler at Savar RMG plant. Uses an entry-level Android smartphone (Symphony / Walton). Prefers conversational Bangla. Limited accounting knowledge.

* **Task A1 — 2-Tap Photo Bill Capture (Target: $<20$ s)**:
  1. Open CarbonBill Floor Mode.
  2. Select the Diesel Fuel category card (ডিজেল স্লিপ).
  3. Snap or select photo of a physical Padma Oil fuel delivery slip.
  4. Verify visual receipt code (e.g. `13E57D5D`) appears on screen.

* **Task A2 — Manual Number Fallback for Damaged Receipt (Target: $<30$ s)**:
  1. Switch to Manual Entry Mode (ম্যানুয়াল এন্ট্রি).
  2. Input fuel quantity (`500`) and unit (`লিটার`).
  3. Enter physical slip serial (`TORN-01`).
  4. Submit and verify queue receipt confirms.

* **Task A3 — Offline Survival**:
  1. Turn phone to Airplane Mode.
  2. Capture an electricity meter photo.
  3. Confirm item is saved in offline queue (অপেক্ষমান).
  4. Reconnect network; verify auto-upload completes without re-taking photo.

---

### Persona B: Rahim Uddin (Factory Accountant — Persona 2)
* **Background**: Prepares monthly utility and inventory accounts. Uses desktop Chrome with Bangla/English keyboard. Experiences month-end invoice processing fatigue.

* **Task B1 — Side-by-Side Review & Field Correction (Target: $<30$ s)**:
  1. Open `/review` Review Workspace.
  2. Select lowest-confidence document at top of queue.
  3. Inspect original scan on left and auto-extracted fields on right.
  4. Click on the consumption field; observe bounding box highlight on left scan.
  5. Correct OCR reading (e.g. from `1200` to `1250`).
  6. Confirm document (`Enter` shortcut).

* **Task B2 — Bulk Confirmation of Clean Invoices (Target: $<15$ s for 5 bills)**:
  1. Filter queue for bills with $>95\%$ confidence.
  2. Select 5 bills using checkbox multi-select.
  3. Click "একসাথে ৫টি অনুমোদন করুন" (Bulk Confirm).
  4. Verify status advances to `Calculated`.

---

### Persona C: Nusrat Jahan (Compliance Officer — Persona 3)
* **Background**: Audits ESG metrics for international buyers (H&M, Inditex). Needs traceability of emission factors.

* **Task C1 — Emission Factor Audit Traceability**:
  1. Navigate to calculated bill ledger.
  2. Verify emission factor source (DEFRA 2024 / IGES Bangladesh Grid average) is linked to bill.
  3. Inspect audit trail log showing reviewer user ID and timestamp.

---

## 3. Observation & Scoring Sheet

| Metric | Measurement Method | Target | Acceptable | Unacceptable |
|---|---|:---:|:---:|:---:|
| **Time-on-Task (Floor)** | Stopwatch from Category tap to Receipt modal | $<15$ s | $15–20$ s | $>20$ s |
| **Time-on-Task (Review)**| Stopwatch from Document load to Confirm | $<25$ s | $25–35$ s | $>35$ s |
| **Assistance Prompts** | Number of verbal hints required | 0 hints | 1 hint | $\ge 2$ hints |
| **Bangla Clarity Score** | 1 (Confusing) to 5 (Completely Natural) | $\ge 4.5$ | $4.0–4.4$ | $<4.0$ |
| **SUS Score** | 10-Question Standard SUS Questionnaire | $\ge 85$ | $75–84$ | $<75$ |

---

## 4. Usability Observation Form Template

```text
Session ID: USAB-____   Date: 2026-10-___   Observer: __________________
Participant Persona: [ ] Floor Staff   [ ] Accountant   [ ] Compliance
Device: [ ] Mobile Android   [ ] Desktop Web
Network: [ ] 3G Cellular   [ ] Factory Wi-Fi   [ ] Offline (Simulated)

Task 1: Capture Diesel Slip
- Duration: _____ seconds
- First attempt success: [ ] Yes  [ ] No
- Hesitations / Confusion points: _________________________________________
- Verbal comments in Bangla: ____________________________________________

Task 2: Review & Bounding Box Inspection
- Duration: _____ seconds
- Did participant notice bounding box highlight? [ ] Yes  [ ] No
- Used keyboard shortcut (`Enter`/`N`): [ ] Yes  [ ] No

Participant SUS Score: _____ / 100
Priority Recommendations: _______________________________________________
```
