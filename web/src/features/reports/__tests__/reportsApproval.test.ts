import { describe, it, expect } from 'vitest';
import {
  fetchReports,
  submitReportForReview,
  approveReport,
  createShareLink,
  getPdfDownloadUrl,
  getExcelDownloadUrl,
} from '../reportsApi';

describe('Reports Approval Workflow & Immutability', () => {
  it('loads report list with readiness checklist and scope breakdowns', async () => {
    const reports = await fetchReports();
    expect(reports.length).toBeGreaterThan(0);

    const rep = reports[0];
    expect(rep.scope1Emissions).toBeGreaterThan(0);
    expect(rep.scope2Emissions).toBeGreaterThan(0);
    expect(rep.readinessChecklist).toBeDefined();
  });

  it('approves report, transitions to Locked state, and attaches SHA-256 seal', async () => {
    const targetId = 'rep-2026-09';
    const result = await approveReport(targetId, 'Nusrat Jahan (Compliance Officer)');

    expect(result.success).toBe(true);
    expect(result.hash.length).toBe(64); // Valid SHA-256 hex string

    const updatedList = await fetchReports();
    const approved = updatedList.find((r) => r.id === targetId);
    expect(approved?.status).toBe('Locked');
    expect(approved?.contentHashSha256).toBe(result.hash);
    expect(approved?.approvedBy).toBe('Nusrat Jahan (Compliance Officer)');
  });

  it('creates expiring share link with configurable price redaction', async () => {
    const targetId = 'rep-2026-08';
    const shareLink = await createShareLink(targetId, 30, true);

    expect(shareLink.token).toContain('share-');
    expect(shareLink.redactPrices).toBe(true);
    expect(shareLink.shareUrl).toContain(shareLink.token);

    const expiryDate = new Date(shareLink.expiresAt);
    const now = new Date();
    const diffDays = Math.round((expiryDate.getTime() - now.getTime()) / (1000 * 3600 * 24));
    expect(diffDays).toBeGreaterThanOrEqual(29);
    expect(diffDays).toBeLessThanOrEqual(31);
  });

  it('generates compliant PDF and Excel download URLs with language and redaction options', () => {
    const pdfBn = getPdfDownloadUrl('rep-2026-08', 'bn', false);
    const pdfEnRedacted = getPdfDownloadUrl('rep-2026-08', 'en', true);
    const excelUrl = getExcelDownloadUrl('rep-2026-08', true);

    expect(pdfBn).toContain('/api/v1/reports/rep-2026-08/pdf?lang=bn');
    expect(pdfEnRedacted).toContain('redactPrices=true');
    expect(excelUrl).toContain('/api/v1/reports/rep-2026-08/excel?redactPrices=true');
  });
});
