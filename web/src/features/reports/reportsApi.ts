import { ReportItem, ReportStatus, ShareLinkInfo } from './types';
import { apiFetch } from '../../lib/apiClient';

export const mockReportsList: ReportItem[] = [
  {
    id: 'rep-2026-09',
    period: '2026-09',
    status: 'ReadyForReview',
    version: 1,
    scope1Emissions: 12.45,
    scope2Emissions: 48.20,
    scope3Emissions: 5.10,
    totalEmissions: 65.75,
    dataQualityScore: 0.9125,
    dqsGrade: 'Grade A',
    readinessChecklist: {
      allExpectedDocsReceived: false,
      missingDocsCount: 2,
      criticalFlagsResolved: true,
      openCriticalFlagsCount: 0,
      dqsAboveThreshold: true,
      dqsScore: 0.9125,
      factorsUpToDate: true,
      isReadyForApproval: true,
    },
    activeShareLinks: [],
  },
  {
    id: 'rep-2026-08',
    period: '2026-08',
    status: 'Locked',
    version: 1,
    scope1Emissions: 14.10,
    scope2Emissions: 44.50,
    scope3Emissions: 4.70,
    totalEmissions: 63.30,
    dataQualityScore: 0.8950,
    dqsGrade: 'Grade A',
    contentHashSha256: '9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08',
    approvedBy: 'Kabir Ahmed (Managing Director)',
    approvedAt: '2026-09-05T14:30:00Z',
    readinessChecklist: {
      allExpectedDocsReceived: true,
      missingDocsCount: 0,
      criticalFlagsResolved: true,
      openCriticalFlagsCount: 0,
      dqsAboveThreshold: true,
      dqsScore: 0.8950,
      factorsUpToDate: true,
      isReadyForApproval: true,
    },
    activeShareLinks: [
      {
        token: 'share-tok-inditex-202608',
        shareUrl: `${typeof window !== 'undefined' ? window.location.origin : 'https://app.carbonbill.local'}/auditor?token=share-tok-inditex-202608`,
        expiresAt: '2026-11-05T23:59:59Z',
        redactPrices: true,
        viewCount: 4,
      },
    ],
  },
];

let inMemoryReports = [...mockReportsList];

export async function fetchReports(): Promise<ReportItem[]> {
  try {
    const data = await apiFetch<ReportItem[]>('/api/v1/reports');
    return data;
  } catch {
    return inMemoryReports;
  }
}

export async function submitReportForReview(id: string): Promise<boolean> {
  try {
    await apiFetch(`/api/v1/reports/${encodeURIComponent(id)}/submit-review`, { method: 'POST' });
  } catch {
    // fallback
  }
  inMemoryReports = inMemoryReports.map((r) =>
    r.id === id ? { ...r, status: 'ReadyForReview' } : r
  );
  return true;
}

export async function approveReport(id: string, approvedBy: string): Promise<{ success: boolean; hash: string }> {
  const hash = 'a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e';
  const now = new Date().toISOString();

  try {
    const res = await apiFetch<{ hash?: string }>(`/api/v1/reports/${encodeURIComponent(id)}/approve`, {
      method: 'POST',
      body: JSON.stringify({ approvedBy }),
    });
    if (res.hash) return { success: true, hash: res.hash };
  } catch {
    // fallback
  }

  inMemoryReports = inMemoryReports.map((r) =>
    r.id === id
      ? {
          ...r,
          status: 'Locked',
          contentHashSha256: hash,
          approvedBy,
          approvedAt: now,
        }
      : r
  );
  return { success: true, hash };
}

export async function createShareLink(
  reportId: string,
  expiryDays: number,
  redactPrices: boolean
): Promise<ShareLinkInfo> {
  const token = `share-${Math.random().toString(36).substring(2, 10)}`;
  const expiresAt = new Date(Date.now() + expiryDays * 86400000).toISOString();
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://app.carbonbill.local';
  const shareUrl = `${origin}/auditor?token=${token}`;

  try {
    const res = await apiFetch<ShareLinkInfo>(`/api/v1/reports/${encodeURIComponent(reportId)}/share`, {
      method: 'POST',
      body: JSON.stringify({ expiryDays, redactPrices }),
    });
    if (res && res.token) return res;
  } catch {
    // fallback
  }

  const newLink: ShareLinkInfo = {
    token,
    shareUrl,
    expiresAt,
    redactPrices,
    viewCount: 0,
  };

  inMemoryReports = inMemoryReports.map((r) =>
    r.id === reportId ? { ...r, activeShareLinks: [...r.activeShareLinks, newLink] } : r
  );

  return newLink;
}

export function getPdfDownloadUrl(id: string, lang: 'bn' | 'en' = 'en', redactPrices: boolean = false): string {
  return `/api/v1/reports/${encodeURIComponent(id)}/pdf?lang=${lang}&redactPrices=${redactPrices}`;
}

export function getExcelDownloadUrl(id: string, redactPrices: boolean = false): string {
  return `/api/v1/reports/${encodeURIComponent(id)}/excel?redactPrices=${redactPrices}`;
}

export const REPORT_DURATIONS = [
  {
    key: 'last_1_month' as const,
    labelEn: 'Last 1 Month',
    labelBn: 'বিগত ১ মাস',
    periodCode: '2026-09',
    dateRangeDescEn: 'Sep 1 – Sep 30, 2026 (Monthly Reconciliation)',
    dateRangeDescBn: '১ সেপ্টেম্বর – ৩০ সেপ্টেম্বর, ২০২৬ (মাসিক মিলকরণ)',
    normStandardEn: 'Monthly Utility & SREDA Reconciliation',
    normStandardBn: 'মাসিক ইউটিলিটি ও স্রেডা পর্যবেক্ষণ',
    monthsCount: 1,
  },
  {
    key: 'last_3_months' as const,
    labelEn: 'Last 3 Months / Q3',
    labelBn: 'বিগত ৩ মাস / ৩য় প্রান্তিক',
    periodCode: '2026-Q3',
    dateRangeDescEn: 'Jul 1 – Sep 30, 2026 (Quarterly Audit)',
    dateRangeDescBn: '১ জুলাই – ৩০ সেপ্টেম্বর, ২০২৬ (ত্রৈমাসিক অডিট)',
    normStandardEn: 'EU CBAM Quarterly Declaration & Higg FEM 4.0 Standard',
    normStandardBn: 'ইইউ সিবিএএম ও হিগ ফেম ৪.০ ত্রৈমাসিক মানদণ্ড',
    monthsCount: 3,
  },
  {
    key: 'last_6_months' as const,
    labelEn: 'Last 6 Months / H1',
    labelBn: 'বিগত ৬ মাস / অর্ধবার্ষিক',
    periodCode: '2026-H1',
    dateRangeDescEn: 'Apr 1 – Sep 30, 2026 (Semi-Annual Verification)',
    dateRangeDescBn: '১ এপ্রিল – ৩০ সেপ্টেম্বর, ২০২৬ (অর্ধবার্ষিক যাচাই)',
    normStandardEn: 'Inditex & H&M Half-Year Supplier ESG Review',
    normStandardBn: 'ইন্ডিটেক্স ও এইচঅ্যান্ডএম অর্ধবার্ষিক অডিট',
    monthsCount: 6,
  },
  {
    key: 'full_year_2026' as const,
    labelEn: 'Full Year 2026 (Annual)',
    labelBn: 'সম্পূর্ণ বছর ২০২৬ (বার্ষিক)',
    periodCode: '2026-FY',
    dateRangeDescEn: 'Jan 1 – Dec 31, 2026 (Annual Corporate Inventory)',
    dateRangeDescBn: '১ জানুয়ারি – ৩১ ডিসেম্বর, ২০২৬ (বার্ষিক ইনভেন্টরি)',
    normStandardEn: 'GHG Protocol Corporate Standard & CDP Annual Disclosure',
    normStandardBn: 'জিএইচজি প্রোটোকল কর্পোরেট স্ট্যান্ডার্ড ও সিডিপি বার্ষিক প্রকাশ',
    monthsCount: 12,
  },
];

export function exportAuditLedgerToCsv(
  items: Array<{
    scope: string;
    sourceNameEn: string;
    division?: string;
    billCompany?: string;
    billNumber?: string;
    meterNumber?: string;
    activityQuantity: number;
    activityUnit: string;
    billedAmountBdt?: number;
    factorValue: number;
    factorSource: string;
    emissionsTco2e: number;
    calculationFormula?: string;
    ocrConfidence: number;
    confirmedBy: string;
    confirmedAt: string;
  }>,
  fileName: string = 'CarbonBill-Audit-Ledger.csv',
  redactPrices: boolean = false
): void {
  const headers = [
    'Scope',
    'Activity Source',
    'Division',
    'Utility / Supplier',
    'Bill / Invoice Number',
    'Meter Number',
    'Activity Quantity',
    'Activity Unit',
    'Billed Cost (BDT)',
    'Emission Factor (kgCO2e/unit)',
    'Factor Citation',
    'Emissions (tCO2e)',
    'Calculation Formula',
    'OCR Confidence (%)',
    'Verified By',
    'Timestamp (UTC)'
  ];

  const escapeField = (val: any) => {
    if (val === null || val === undefined) return '""';
    const str = String(val).replace(/"/g, '""');
    return `"${str}"`;
  };

  const rows = items.map((item) => [
    escapeField(item.scope),
    escapeField(item.sourceNameEn),
    escapeField(item.division || 'Dhaka Division'),
    escapeField(item.billCompany || 'N/A'),
    escapeField(item.billNumber || 'N/A'),
    escapeField(item.meterNumber || 'N/A'),
    escapeField(item.activityQuantity),
    escapeField(item.activityUnit),
    escapeField(redactPrices ? '[REDACTED FOR BUYER]' : item.billedAmountBdt ?? 'N/A'),
    escapeField(item.factorValue),
    escapeField(item.factorSource),
    escapeField(item.emissionsTco2e),
    escapeField(item.calculationFormula || `${item.activityQuantity} ${item.activityUnit} * ${item.factorValue} / 1000`),
    escapeField(item.ocrConfidence),
    escapeField(item.confirmedBy),
    escapeField(item.confirmedAt),
  ]);

  const csvContent = '\uFEFF' + [headers.map((h) => `"${h}"`).join(','), ...rows.map((r) => r.join(','))].join('\r\n');
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
