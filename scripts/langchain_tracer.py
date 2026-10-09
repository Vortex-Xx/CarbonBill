#!/usr/bin/env python3
"""
CarbonBill - LangChain & LangSmith Tracing Client
==================================================
Traces utility bill extraction and carbon suggestions using LangSmith and LLM providers.
Exports and respects:
- LANGSMITH_TRACING=true
- LANGSMITH_ENDPOINT=https://api.smith.langchain.com
- LANGSMITH_API_KEY=<your-langsmith-api-key>
- LANGSMITH_PROJECT=CarbonBill
- ANTHROPIC_API_KEY
- GROQ_API_KEY
"""

import os
import sys
import json
import uuid
import datetime
import urllib.request
import urllib.error
from pathlib import Path

# Load from .env file if present in parent directories
env_path = Path(__file__).resolve().parent.parent / ".env"
if env_path.exists():
    with open(env_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, v = line.split("=", 1)
                os.environ.setdefault(k.strip(), v.strip())

# Default configuration
os.environ.setdefault("LANGSMITH_TRACING", "true")
os.environ.setdefault("LANGSMITH_ENDPOINT", "https://api.smith.langchain.com")
os.environ.setdefault("LANGSMITH_PROJECT", "CarbonBill")

def send_langsmith_trace(run_name, run_type, inputs, outputs, metadata=None):
    endpoint = os.environ.get("LANGSMITH_ENDPOINT", "https://api.smith.langchain.com")
    api_key = os.environ.get("LANGSMITH_API_KEY", "")
    project = os.environ.get("LANGSMITH_PROJECT", "CarbonBill")

    if not api_key:
        print("[LangSmith] API key missing. Skipping trace.")
        return None

    run_id = str(uuid.uuid4())
    now_iso = datetime.datetime.now(datetime.timezone.utc).isoformat()

    payload = {
        "id": run_id,
        "name": run_name,
        "run_type": run_type,
        "inputs": inputs,
        "outputs": outputs,
        "session_name": project,
        "start_time": now_iso,
        "end_time": now_iso,
        "extra": {
            "metadata": metadata or {}
        }
    }

    req = urllib.request.Request(
        f"{endpoint.rstrip('/')}/runs",
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "x-api-key": api_key
        },
        method="POST"
    )

    try:
        with urllib.request.urlopen(req) as resp:
            data = resp.read().decode("utf-8")
            print(f"[LangSmith] Trace logged successfully. Run ID: {run_id}")
            print(f"            Project: {project}")
            print(f"            Dashboard: https://smith.langchain.com/o/default/projects/p/{project}")
            return run_id
    except urllib.error.HTTPError as e:
        print(f"[LangSmith] HTTP error: {e.code} - {e.read().decode('utf-8')}")
        return None
    except Exception as e:
        print(f"[LangSmith] Error sending trace: {e}")
        return None

def test_trace_extraction():
    sample_doc = {
        "fileName": "desco_mirpur_11_june_2026.pdf",
        "ocr_text": "ঢাকা ইলেকট্রিক সাপ্লাই কোম্পানি লিমিটেড (DESCO)\nবিল নং: DESCO-98124\nমাস: জুন ২০২৬\nব্যবহৃত বিদ্যুৎ: ৫৪,২০০ kWh\nমোট বিল: ৩,৭৯,৪০০ টাকা\nপাওয়ার ফ্যাক্টর জরিমানা: ৩,৪৫০ টাকা",
    }
    extracted_fields = {
        "vendor": "DESCO",
        "billNumber": "DESCO-98124",
        "billingPeriod": "2026-06",
        "quantity": 54200.0,
        "unit": "kWh",
        "amountBdt": 379400.0,
        "powerFactorPenaltyBdt": 3450.0,
        "confidence": 0.96
    }
    metadata = {
        "model": "openai/gpt-oss-120b",
        "provider": "Groq",
        "tier": 3,
        "tenant": "AUST-Apparels-01"
    }

    print("Sending test trace to LangSmith...")
    return send_langsmith_trace(
        run_name="GroqBillExtractionPipeline",
        run_type="chain",
        inputs=sample_doc,
        outputs=extracted_fields,
        metadata=metadata
    )

if __name__ == "__main__":
    test_trace_extraction()
