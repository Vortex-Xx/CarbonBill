# CarbonBill: LangSmith Tracing & Practical Testing Guide

This guide documents the **LangSmith / LangChain Tracing integration**, how all implemented Track B features practically work, and how to test and verify them locally.

---

## 1. LangSmith & AI Configuration

The application is configured to trace all LLM and extraction invocations directly to [LangSmith](https://smith.langchain.com/).

### Environment Variables

The project loads the following environment variables (defined in `.env` and `src/CarbonBill.Api/appsettings.Development.json`):

```bash
export LANGSMITH_TRACING=true
export LANGSMITH_ENDPOINT=https://api.smith.langchain.com
export LANGSMITH_API_KEY=<your-langsmith-api-key>
export LANGSMITH_PROJECT="CarbonBill"

export ANTHROPIC_API_KEY=<your-anthropic-api-key>
export GROQ_API_KEY=<your-groq-api-key>
```

### LangSmith Tracing Architecture

```
[Uploaded Document / Bill]
        │
        ▼
[DualOcrEngine (Tesseract 5 + PaddleOCR)]
        │
        ▼
[GroqLlmExtractor (model: openai/gpt-oss-120b)]
        ├─── 1. Sends prompt + OCR text to Groq API
        ├─── 2. Intercepts inputs, outputs, latency & metadata
        │
        ▼
[LangSmithTracer (ILangSmithTracer)]
        │
        └───► POST https://api.smith.langchain.com/runs
              Headers: x-api-key, Content-Type: application/json
              Session / Project: "CarbonBill"
```

* **Non-blocking Resiliency**: If LangSmith is unreachable or times out, the extraction pipeline logs a warning and proceeds uninterrupted.
* **Consent Gated**: If tenant consent is not granted, Tier 3 AI extraction is skipped in compliance with GHG Protocol / GDPR rules.

---

## 2. Running the Live Application

Both the ASP.NET Core backend and Vite React frontend are currently running live:

| Component | URL | Technology | Status |
|---|---|---|:---:|
| **Backend API** | `http://localhost:5120` | ASP.NET Core 8 LTS, SQLite, Hangfire, Serilog | **Running** |
| **Swagger UI** | `http://localhost:5120/swagger` | OpenAPI Specification | **Running** |
| **Hangfire Dashboard** | `http://localhost:5120/hangfire` | Background jobs & SQLite vacuum | **Running** |
| **Frontend PWA** | `http://localhost:5173` | React 19, Vite, Tailwind CSS, Workbox PWA | **Running** |

---

## 3. How the Features Practically Work

### A. Floor Staff Capture PWA (Persona: Jahid)
* **URL**: `http://localhost:5173/?mode=floor` (or login as `floor@apex.local` / `Pass1234!`)
* **Features**:
  1. **4 Large Touch Cards**: ডিজেল (Diesel), গ্যাস (Gas), বিদ্যুৎ (Electricity), চালান (Shipping Challan).
  2. **2-Tap Capture Flow**: Tap bill category $\rightarrow$ camera opens $\rightarrow$ capture $\rightarrow$ compressed & uploaded.
  3. **Client-Side Compression**: Resizes to max 1600px, WebP/JPEG under 400 KB, strips EXIF GPS metadata.
  4. **Offline Queue (IndexedDB)**: If internet drops on factory floor, items queue safely in IndexedDB and automatically retry with client GUID idempotency keys when online.
  5. **Manual Fallback Entry**: When camera or lighting is poor, staff can enter Litres and Slip Number directly.
  6. **Ultra-Light Bundle**: 12.53 KB gzipped initial chunk for fast loading on low-end Android phones.

### B. Ingestion & Extraction Backend
* **Endpoint**: `POST /api/v1/documents` and `POST /api/v1/extraction/test-ocr`
* **Features**:
  1. **Magic-Byte Header Validation**: Rejects disguised malicious executables or scripts.
  2. **SHA-256 Deduplication**: Identical bills uploaded within the same tenant are rejected as duplicates (`DocumentDuplicate`).
  3. **Option C Dual OCR**: Extracts raw Bangla and English text.
  4. **Bangla Normalizer**: Converts Bangla digits (`১২৩৪`) to standard Latin digits (`1234`), normalizes date formats and canonical units (`kWh`, `litre`, `m3`, `tonne-km`).
  5. **Utility Classifier**: Automatically classifies DESCO, DPDC, BPDB, Padma Oil, Meghna, Jamuna, Titas Gas.
  6. **Groq LLM Semantic Extraction**: Strict JSON extraction with `openai/gpt-oss-120b`.
  7. **LangSmith Trace Emission**: Automatically posts run traces with start/end time, prompts, outputs, and metadata.

### C. Review UI Workspace (Persona: Rahim - Accountant)
* **URL**: `http://localhost:5173/?mode=review` (or login as `accountant@apex.local` / `Pass1234!`)
* **Features**:
  1. **Dual Split-Screen Layout**:
     - **Left Column**: Queue sidebar sorted by **lowest confidence first** (items needing attention are prioritized).
     - **Center Column**: Original document viewer with zoom in/out, rotate 90°, and interactive bounding-box highlights.
     - **Right Column**: Fields editor with Bangla numerals and BDT currency formatting.
  2. **Auto-Focus Lowest Confidence Field**: First low-confidence field is highlighted in amber and focused immediately.
  3. **Keyboard Shortcuts**:
     - `Tab` / `Shift+Tab`: Navigate across fields.
     - `Enter`: Confirm current document.
     - `N`: Skip / advance to next document.
  4. **Duplicate Resolution**: "Keep One" dropdown to resolve duplicate detections.
  5. **Estimated Data Toggle**: Flag missing month bills as estimated for buyer audit compliance.
  6. **Floor Retake Request**: Request floor staff to retake photo with one-click reason selection.
  7. **Bulk Confirm**: One-click bulk confirmation for all documents with confidence $\ge 90\%$ (governed by org settings).

---

## 4. How to Test the System

### Test 1: Python LangSmith Trace Test
Run the standalone Python LangSmith tracing script:
```powershell
python scripts/langchain_tracer.py
```
**Expected Output**:
```
Sending test trace to LangSmith...
[LangSmith] Trace logged successfully. Run ID: <uuid>
            Project: CarbonBill
            Dashboard: https://smith.langchain.com/o/default/projects/p/CarbonBill
```

### Test 2: Authenticated API Login & Review Settings
Test authentication and review queue using PowerShell:
```powershell
$loginPayload = @{ email = "accountant@apex.local"; password = "Pass1234!" } | ConvertTo-Json
$res = Invoke-RestMethod -Uri "http://localhost:5120/api/v1/auth/login" -Method Post -ContentType "application/json" -Body $loginPayload
$token = $res.accessToken

$headers = @{ "Authorization" = "Bearer $token" }
$settings = Invoke-RestMethod -Uri "http://localhost:5120/api/v1/review/settings" -Method Get -Headers $headers
$settings | ConvertTo-Json
```

### Test 3: Test OCR Extraction with Live LangSmith Tracing
Upload a bill to the extraction endpoint and watch the trace log:
```powershell
$filePath = "E:\Hackathons\Hack for Humanity\7899d104-4e29-4809-9a2a-ef7578e51925.jfif"
$url = "http://localhost:5120/api/v1/extraction/test-ocr"

$boundary = [System.Guid]::NewGuid().ToString()
$fileBytes = [System.IO.File]::ReadAllBytes($filePath)
$fileName = [System.IO.Path]::GetFileName($filePath)

$body = (
    "--$boundary`r`n" +
    "Content-Disposition: form-data; name=`"file`"; filename=`"$fileName`"`r`n" +
    "Content-Type: image/jpeg`r`n`r`n" +
    [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes) + "`r`n" +
    "--$boundary--`r`n"
)

$headers = @{ "Authorization" = "Bearer $token" }
$response = Invoke-RestMethod -Uri $url -Method Post -ContentType "multipart/form-data; boundary=$boundary" -Body $body -Headers $headers
$response | ConvertTo-Json -Depth 5
```

Check the backend server output to observe the LangSmith confirmation:
```
[INF] Start processing HTTP request POST https://api.smith.langchain.com/runs
[INF] Received HTTP response headers after 641ms - 202
[INF] LangSmith trace successfully logged to project 'CarbonBill' (RunId: ...)
```

### Test 4: Run Complete Solution Unit & Architecture Tests
Run all 61 automated backend tests:
```powershell
dotnet test CarbonBill.sln
```
**Result**: 61 passed, 0 failed, 0 warnings.

Run all frontend unit tests:
```powershell
cd web
npm test
```
**Result**: 11 passed, 0 failed.

---

## 5. Seeded Test Credentials

The database is automatically pre-seeded with factory personas:

| Persona | Email | Password | Role | Access Route |
|---|---|---|---|---|
| **Accountant (Rahim)** | `accountant@apex.local` | `Pass1234!` | `Accountant` | `http://localhost:5173/?mode=review` |
| **Floor Staff (Jahid)** | `floor@apex.local` | `Pass1234!` | `FloorStaff` | `http://localhost:5173/?mode=floor` |
| **Factory Owner (Kabir)** | `owner@apex.local` | `Pass1234!` | `Owner` | `http://localhost:5173/` |
| **Compliance Officer (Nusrat)** | `compliance@apex.local` | `Pass1234!` | `Compliance` | `http://localhost:5173/` |
| **Platform Administrator** | `admin@carbonbill.local` | `Admin1234!` | `PlatformAdmin` | `http://localhost:5120/hangfire` |
