export type RoleType = 'Owner' | 'Accountant' | 'Compliance' | 'Consultant' | 'FloorStaff' | 'PlatformAdmin';

export interface DashboardSummary {
  period: string;
  scope1Emissions: number;
  scope2Emissions: number;
  scope3Emissions: number;
  totalEmissions: number;
  dataQualityScore: number;
  dqsGrade: string;
  verifiedPercentage: number;
  status: string;
  activeFlagsCount: number;
  missingDocsCount: number;
}

export interface TrendDataPoint {
  period: string;
  periodLabelBn: string;
  periodLabelEn: string;
  verifiedEmissions: number;
  estimatedEmissions: number;
  totalEmissions: number;
  hatchFlag: boolean;
}

export interface PeerBenchmark {
  p25: number;
  p50: number;
  p75: number;
  p90: number;
  n: number;
  source: string;
  year: number;
}

export interface IntensityData {
  metric: string;
  metricLabelBn: string;
  metricLabelEn: string;
  unit: string;
  productionQuantity: number;
  verifiedIntensity: number;
  inclEstimateIntensity: number;
  benchmark: PeerBenchmark | null; // null when n < 10 (triggers "no benchmark yet")
}

export interface ClientOrgSummary {
  orgId: string;
  orgName: string;
  sector: string;
  totalEmissions: number;
  dqsScore: number;
  dqsGrade: string;
  openFlagsCount: number;
  pendingOverridesCount: number;
  lastUpdated: string;
}
