export interface AuditorLineItem {
  id: string;
  scope: string; // 'Scope 1' | 'Scope 2' | 'Scope 3'
  sourceNameBn: string;
  sourceNameEn: string;
  activityQuantity: number;
  activityUnit: string;
  billedAmountBdt?: number; // masked if redactPrices is true
  emissionsTco2e: number;
  documentId: string;
  documentTypeBn: string;
  documentTypeEn: string;
  documentDate: string;
  ocrConfidence: number; // e.g. 98.4
  confirmedBy: string;
  confirmedAt: string;
  factorSource: string;
  factorValue: number;
  factorCitationYear: number;
  billCompany?: string;
  billNumber?: string;
  fileName?: string;
  meterNumber?: string;
  grossKgCo2e?: number;
  calculationFormula?: string;
  division?: string;
  divisionBn?: string;
  companyName?: string;
}

export interface AuditorSnapshot {
  reportId: string;
  period: string;
  organizationName: string;
  facilityLocation: string;
  contentHashSha256: string;
  lockedAt: string;
  approvedBy: string;
  totalEmissions: number;
  scope1Emissions: number;
  scope2Emissions: number;
  scope3Emissions: number;
  dataQualityScore: number;
  dqsGrade: string;
  redactPrices: boolean;
  token?: string;
  lineItems: AuditorLineItem[];
}
