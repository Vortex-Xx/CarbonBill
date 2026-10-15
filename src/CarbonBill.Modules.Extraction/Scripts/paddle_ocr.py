#!/usr/bin/env python3
"""
PaddleOCR / RapidOCR Engine Runner for CarbonBill
Provides high-accuracy OCR fallback for complex, skewed, or low-contrast utility bills and challans.
Supports both images (PNG, JPG, WebP) and PDFs (multi-page rendering).
"""

import sys
import os
import json
import numpy as np

def run_ocr(file_path: str):
    if not os.path.exists(file_path):
        return {
            "success": False,
            "error": f"File not found: {file_path}",
            "text": "",
            "overall_confidence": 0.0,
            "line_count": 0,
            "engine": "PaddleOCR"
        }

    try:
        from rapidocr_onnxruntime import RapidOCR
        engine = RapidOCR()
    except Exception as ex:
        return {
            "success": False,
            "error": f"Failed to initialize RapidOCR/PaddleOCR engine: {ex}",
            "text": "",
            "overall_confidence": 0.0,
            "line_count": 0,
            "engine": "PaddleOCR"
        }

    ext = os.path.splitext(file_path)[1].lower()
    extracted_lines = []
    confidences = []

    try:
        if ext == ".pdf":
            import pypdfium2 as pdfium
            pdf = pdfium.PdfDocument(file_path)
            num_pages = min(len(pdf), 3)  # Process up to first 3 pages

            for page_idx in range(num_pages):
                page = pdf[page_idx]
                pil_img = page.render(scale=2.0).to_pil()
                img_np = np.array(pil_img)
                results, _ = engine(img_np)
                if results:
                    for item in results:
                        text = item[1].strip()
                        conf = float(item[2])
                        if text:
                            extracted_lines.append(text)
                            confidences.append(conf)
        else:
            from PIL import Image
            img = Image.open(file_path).convert("RGB")
            img_np = np.array(img)
            results, _ = engine(img_np)
            if results:
                for item in results:
                    text = item[1].strip()
                    conf = float(item[2])
                    if text:
                        extracted_lines.append(text)
                        confidences.append(conf)

        overall_conf = (sum(confidences) / len(confidences)) if confidences else 0.0
        full_text = "\n".join(extracted_lines)

        return {
            "success": len(extracted_lines) > 0,
            "text": full_text,
            "overall_confidence": round(overall_conf, 4),
            "line_count": len(extracted_lines),
            "engine": "PaddleOCR",
            "error": None
        }
    except Exception as ex:
        return {
            "success": False,
            "error": str(ex),
            "text": "",
            "overall_confidence": 0.0,
            "line_count": 0,
            "engine": "PaddleOCR"
        }

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"success": False, "error": "Missing file_path argument"}))
        sys.exit(1)

    file_to_process = sys.argv[1]
    result = run_ocr(file_to_process)
    print(json.dumps(result, ensure_ascii=False))
