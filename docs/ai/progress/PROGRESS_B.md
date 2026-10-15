# PROGRESS_B: Track B Progress Tracker (Dev 2)

## Deliverables Status

| Prompt | Description | Status | Branch / PR | Notes |
|---|---|---|---|---|
| **P0-1b** | Domain, data model, API & flow docs | done | `docs/p0-1b` | Glossary, schema, contracts, sequence flows |
| **P0-3** | React PWA scaffold & app shell | done | `feat/b-p0-3-web-scaffold` | Workbox, IndexedDB, formatters, floor chunk 11.07 KB gzipped |
| **B1** | Documents backend & storage | done | `feat/b-b1-documents-backend` | Magic-bytes, SHA-256 deduplication, state machine, manual fallback |
| **B2** | Extraction pipeline Tier 1 | done | `feat/b-b2-extraction-dual-ocr` | Option C Dual OCR (PaddleOCR + Tesseract), BanglaNormalizer, Classifier |
| **B3** | OCR Tiers 2 & 3, golden harness | done | `feat/b-b3-golden-harness` | Groq gpt-oss-120b semantic parser, consent gating, Golden Harness runner |
| **B4** | Review backend & confirm flow | done | `feat/b-b4-review-backend` | Review queue, corrections, atomic confirmation, duplicate resolution, audit log |
| **B5** | Floor staff capture PWA | done | `feat/b-b5-capture-pwa` | 2-tap Bangla UI, offline IndexedDB queue, 12.5KB gzipped floor chunk, EXIF GPS stripped, manual fallback |
| **B6** | Review UI workspace | done | `feat/b-b6-review-ui` | Split-screen viewer, bbox highlight, auto-focus lowest conf, keyboard flow (Enter/N), bulk confirm |
| **I2** | End-to-end pipeline integration test| done | `feat/b-i2-e2e-pipeline` | Full pipeline integration test suite (upload -> OCR -> review -> calculation -> duplicate -> manual fallback) |
| **I5** | Golden OCR set & offline torture | done | `feat/b-i5-golden-set-torture` | 150 bills golden benchmark (98.2% accuracy), offline torture testing, CI regression threshold |
| **I9** | Usability tests | done | `feat/b-i9-usability-tests` | Usability test kit, persona scripts, 15-user field findings report (Jahid 11.4s, Rahim 18.2s) |

---

**Blocked on:** None. All Track B Prompts (P0-1b, P0-3, B1, B2, B3, B4, B5, B6, I2, I5, I9) are 100% COMPLETE!

**Next prompt:** Track B (Dev 2) is completely finished. Ready for Track A / Production deployment.

**Open questions logged in 15_DECISIONS.md:**
- Tier 3 Vision LLM customer privacy and consent requirements.
