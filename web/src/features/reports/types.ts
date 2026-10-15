export type ReportStatus = 'Draft' | 'ReadyForReview' | 'Approved' | 'Locked' | 'Superseded';

export interface ReportReadinessData {
  allExpectedDocsReceived: boolean;
  missingDocsCount: number;
  criticalFlagsResolved: boolean;
  openCriticalFlagsCount: number;
  dqsAboveThreshold: boolean;
  dqsScore: number;
  factorsUpToDate: boolean;
  isReadyForApproval: boolean;
}

export interface ShareLinkInfo {
  token: string;
  shareUrl: string;
  expiresAt: string;
  redactPrices: boolean;
  viewCount: number;
}

export interface ReportItem {
  id: string;
  period: string;
  status: ReportStatus;
  version: number;
  scope1Emissions: number;
  scope2Emissions: number;
  scope3Emissions: number;
  totalEmissions: number;
  dataQualityScore: number;
  dqsGrade: string;
  contentHashSha256?: string;
  approvedBy?: string;
  approvedAt?: string;
  readinessChecklist: ReportReadinessData;
  activeShareLinks: ShareLinkInfo[];
}

export type ReportDuration = 'last_1_month' | 'last_3_months' | 'last_6_months' | 'full_year_2026';

export interface DurationOption {
  key: ReportDuration;
  labelEn: string;
  labelBn: string;
  periodCode: string;
  dateRangeDescEn: string;
  dateRangeDescBn: string;
  normStandardEn: string;
  normStandardBn: string;
  monthsCount: number;
}
