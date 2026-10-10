import { DashboardSummary, TrendDataPoint, IntensityData, ClientOrgSummary } from './types';
import { apiFetch } from '../../lib/apiClient';

export const mockDashboardSummary: DashboardSummary = {
  period: '2026-09',
  scope1Emissions: 12.45,
  scope2Emissions: 48.20,
  scope3Emissions: 5.10,
  totalEmissions: 65.75,
  dataQualityScore: 0.9125,
  dqsGrade: 'Grade A',
  verifiedPercentage: 92.4,
  status: 'ReadyForReview',
  activeFlagsCount: 3,
  missingDocsCount: 2,
};

export const mockTrendData: TrendDataPoint[] = [
  {
    period: '2026-04',
    periodLabelBn: 'এপ্রিল',
    periodLabelEn: 'Apr',
    verifiedEmissions: 58.2,
    estimatedEmissions: 4.1,
    totalEmissions: 62.3,
    hatchFlag: false,
  },
  {
    period: '2026-05',
    periodLabelBn: 'মে',
    periodLabelEn: 'May',
    verifiedEmissions: 61.5,
    estimatedEmissions: 3.8,
    totalEmissions: 65.3,
    hatchFlag: false,
  },
  {
    period: '2026-06',
    periodLabelBn: 'জুন',
    periodLabelEn: 'Jun',
    verifiedEmissions: 64.1,
    estimatedEmissions: 4.5,
    totalEmissions: 68.6,
    hatchFlag: false,
  },
  {
    period: '2026-07',
    periodLabelBn: 'জুলাই',
    periodLabelEn: 'Jul',
    verifiedEmissions: 59.8,
    estimatedEmissions: 5.2,
    totalEmissions: 65.0,
    hatchFlag: false,
  },
  {
    period: '2026-08',
    periodLabelBn: 'আগস্ট',
    periodLabelEn: 'Aug',
    verifiedEmissions: 55.4,
    estimatedEmissions: 7.9,
    totalEmissions: 63.3,
    hatchFlag: true, // > 10% estimated
  },
  {
    period: '2026-09',
    periodLabelBn: 'সেপ্টেম্বর',
    periodLabelEn: 'Sep',
    verifiedEmissions: 60.75,
    estimatedEmissions: 5.0,
    totalEmissions: 65.75,
    hatchFlag: false,
  },
];

export const mockIntensityData: IntensityData = {
  metric: 'intensity_garments',
  metricLabelBn: 'পোশাক উৎপাদন প্রতি কার্বন ঘনত্ব',
  metricLabelEn: 'Garment Output Emission Intensity',
  unit: 'kg CO₂e / 1,000 pcs',
  productionQuantity: 35000,
  verifiedIntensity: 18.78,
  inclEstimateIntensity: 19.45,
  // When peer data is thin (n < 10), benchmark is null, triggering "no benchmark yet"
  benchmark: {
    p25: 14.5,
    p50: 18.2,
    p75: 22.8,
    p90: 27.5,
    n: 14, // > 10, so benchmark is valid
    source: 'Bangladesh Textile RMG Cluster Benchmark (BGMEA/IFC)',
    year: 2025,
  },
};

export const mockConsultantClientOrgs: ClientOrgSummary[] = [
  {
    orgId: 'org-apex',
    orgName: 'Apex Textiles Ltd.',
    sector: 'Knit Dyeing & Finishing',
    totalEmissions: 65.75,
    dqsScore: 0.9125,
    dqsGrade: 'Grade A',
    openFlagsCount: 3,
    pendingOverridesCount: 0,
    lastUpdated: '2026-10-08',
  },
  {
    orgId: 'org-square',
    orgName: 'Square Fashions (Unit 2)',
    sector: 'Woven Garments',
    totalEmissions: 142.30,
    dqsScore: 0.8850,
    dqsGrade: 'Grade A',
    openFlagsCount: 1,
    pendingOverridesCount: 1,
    lastUpdated: '2026-10-07',
  },
  {
    orgId: 'org-hameem',
    orgName: 'Ha-Meem Denim Mill',
    sector: 'Denim Spinning & Weaving',
    totalEmissions: 310.80,
    dqsScore: 0.7420,
    dqsGrade: 'Grade B',
    openFlagsCount: 6,
    pendingOverridesCount: 2,
    lastUpdated: '2026-10-06',
  },
  {
    orgId: 'org-beximco',
    orgName: 'Beximco Apparels Industrial Park',
    sector: 'Composite Textile',
    totalEmissions: 495.10,
    dqsScore: 0.9410,
    dqsGrade: 'Grade A',
    openFlagsCount: 0,
    pendingOverridesCount: 0,
    lastUpdated: '2026-10-09',
  },
];

export async function fetchDashboardSummary(period: string = '2026-09'): Promise<DashboardSummary> {
  try {
    const raw = await apiFetch<any>(`/api/v1/dashboard/summary?period=${encodeURIComponent(period)}`);
    if (!raw) return mockDashboardSummary;

    const s1 = Number(raw.scope1Emissions ?? raw.scope1Tco2e ?? mockDashboardSummary.scope1Emissions);
    const s2 = Number(raw.scope2Emissions ?? raw.scope2Tco2e ?? mockDashboardSummary.scope2Emissions);
    const s3 = Number(raw.scope3Emissions ?? raw.scope3Tco2e ?? mockDashboardSummary.scope3Emissions);
    const total = Number(raw.totalEmissions ?? raw.totalTco2e ?? (s1 + s2 + s3));
    const dqsRaw = raw.dataQualityScore ?? mockDashboardSummary.dataQualityScore;
    const dqs = typeof dqsRaw === 'number' ? (dqsRaw > 1 ? dqsRaw / 100 : dqsRaw) : 0.9125;

    return {
      period: raw.reportingPeriod || raw.period || period,
      scope1Emissions: s1,
      scope2Emissions: s2,
      scope3Emissions: s3,
      totalEmissions: total,
      dataQualityScore: dqs,
      dqsGrade: raw.dqsGrade || (dqs >= 0.85 ? 'Grade A' : dqs >= 0.70 ? 'Grade B' : 'Grade C'),
      verifiedPercentage: Number(raw.verifiedPercentage ?? raw.verifiedSharePercent ?? 92.4),
      status: raw.status || 'ReadyForReview',
      activeFlagsCount: Number(raw.activeFlagsCount ?? mockDashboardSummary.activeFlagsCount),
      missingDocsCount: Number(raw.missingDocsCount ?? mockDashboardSummary.missingDocsCount),
    };
  } catch {
    return mockDashboardSummary;
  }
}

export async function fetchDashboardTrend(start: string = '2026-04', end: string = '2026-09'): Promise<TrendDataPoint[]> {
  try {
    const rawList = await apiFetch<any[]>(`/api/v1/dashboard/trend?start=${encodeURIComponent(start)}&end=${encodeURIComponent(end)}`);
    if (Array.isArray(rawList) && rawList.length > 0) {
      const monthNamesEn: Record<string, string> = { '01': 'Jan', '02': 'Feb', '03': 'Mar', '04': 'Apr', '05': 'May', '06': 'Jun', '07': 'Jul', '08': 'Aug', '09': 'Sep', '10': 'Oct', '11': 'Nov', '12': 'Dec' };
      const monthNamesBn: Record<string, string> = { '01': 'জানু', '02': 'ফেব্রু', '03': 'মার্চ', '04': 'এপ্রিল', '05': 'মে', '06': 'জুন', '07': 'জুলাই', '08': 'আগস্ট', '09': 'সেপ্টে', '10': 'অক্টো', '11': 'নভে', '12': 'ডিসে' };
      return rawList.map((p) => {
        const periodStr = p.period || p.reportingPeriod || '';
        const monthNum = periodStr.split('-')[1] || '';
        const v = Number(p.verifiedEmissions ?? (p.verifiedKgCo2e != null ? p.verifiedKgCo2e / 1000 : 0));
        const e = Number(p.estimatedEmissions ?? (p.estimatedKgCo2e != null ? p.estimatedKgCo2e / 1000 : 0));
        const tot = Number(p.totalEmissions ?? (p.totalKgCo2e != null ? p.totalKgCo2e / 1000 : v + e));
        return {
          period: periodStr,
          periodLabelBn: p.periodLabelBn || monthNamesBn[monthNum] || periodStr || 'মাস',
          periodLabelEn: p.periodLabelEn || monthNamesEn[monthNum] || periodStr || 'Month',
          verifiedEmissions: v,
          estimatedEmissions: e,
          totalEmissions: tot,
          hatchFlag: Boolean(p.hatchFlag),
        };
      });
    }
    return mockTrendData;
  } catch {
    return mockTrendData;
  }
}

export async function fetchIntensityData(period: string = '2026-09'): Promise<IntensityData> {
  try {
    const raw = await apiFetch<any>(`/api/v1/dashboard/intensity?period=${encodeURIComponent(period)}`);
    if (!raw) return mockIntensityData;
    return {
      metric: raw.metric || 'intensity_garments',
      metricLabelBn: raw.metricLabelBn || 'পোশাক উৎপাদন প্রতি কার্বন ঘনত্ব',
      metricLabelEn: raw.metricLabelEn || 'Garment Output Emission Intensity',
      unit: raw.unit || 'kg CO₂e / 1,000 pcs',
      productionQuantity: Number(raw.productionQuantity ?? 35000),
      verifiedIntensity: Number(raw.verifiedIntensity ?? raw.verifiedFactoryValue ?? raw.factoryValue ?? 18.78),
      inclEstimateIntensity: Number(raw.inclEstimateIntensity ?? raw.factoryValue ?? 19.45),
      benchmark: raw.peerBenchmark?.hasBenchmark ? {
        p25: Number(raw.peerBenchmark.p25 ?? 0),
        p50: Number(raw.peerBenchmark.p50 ?? 0),
        p75: Number(raw.peerBenchmark.p75 ?? 0),
        p90: Number(raw.peerBenchmark.p90 ?? 0),
        n: Number(raw.peerBenchmark.n ?? 0),
        source: raw.peerBenchmark.source ?? '',
        year: Number(raw.peerBenchmark.year ?? 2025),
      } : (raw.benchmark ?? null),
    };
  } catch {
    return mockIntensityData;
  }
}

export async function fetchConsultantClients(): Promise<ClientOrgSummary[]> {
  try {
    const data = await apiFetch<ClientOrgSummary[]>('/api/v1/consultant/clients');
    if (Array.isArray(data) && data.length > 0) return data;
    return mockConsultantClientOrgs;
  } catch {
    return mockConsultantClientOrgs;
  }
}
