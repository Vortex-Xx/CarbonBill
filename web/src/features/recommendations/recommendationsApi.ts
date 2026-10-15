import { Recommendation, RecommendationStatus, EvidenceGrade } from './types';
import { apiFetch } from '../../lib/apiClient';

export const mockRecommendationsList: Recommendation[] = [
  {
    id: 'rec-apfc-001',
    measureCode: 'PFL-EE-002',
    titleBn: 'পাওয়ার ফ্যাক্সর অটোমেটিক ক্যাপাসিটর ব্যাংক (APFC) সংস্কার',
    titleEn: 'Automatic Power Factor Correction (APFC) Capacitor Bank Refurbishment',
    category: 'Electricity',
    evidenceGrade: 'A',
    sourceCitation: 'DESCO Industrial Tariff Penalty Schedule & IFC PaCT Textile Energy Audit Guide (2023)',
    sourceUrl: 'https://pact-bangladesh.org',
    applicabilityBn: 'ডেসকো বিলে পাওয়ার ফ্যাক্টর জরিমানা (PF < 0.90) আরোপিত কারখানাসমূহের জন্য প্রযোজ্য।',
    applicabilityEn: 'Applicable to industrial facilities incurring low power factor penalty surcharges.',
    assumptionsBn: 'কারখানার নিজস্ব সেপ্টেম্বর মাসের বিদ্যুৎ বিল থেকে ৩,৪৫০ টাকা/মাস জরিমানা এবং ১২.৫০ টাকা/kWh ট্যারিফ ব্যবহার করে গণনা করা হয়েছে।',
    assumptionsEn: 'Calculated using actual BDT 3,450/mo surcharge and factory tariff of BDT 12.50/kWh.',
    savingsBdtRange: { low: 100000, typical: 120000, high: 140000 },
    capexBdtRange: { low: 35000, typical: 45000, high: 55000 },
    paybackMonthsRange: { low: 3.2, typical: 4.5, high: 6.0 },
    tco2eAvoidedRange: { low: 4.2, typical: 5.5, high: 7.0 },
    costPerTco2e: -18000, // Negative cost: investment yields huge net profits!
    status: 'Suggested',
    financingNoteBn: 'SREDA গ্রিন ফান্ড এবং স্ট্যান্ডার্ড চার্টার্ড এসএমই সাসটেইনেবিলিটি ঋণে ৫.৫% সুদে অর্থায়নযোগ্য।',
    financingNoteEn: 'Eligible for SREDA Green Refinancing Facility at concessionary 5.5% interest.',
  },
  {
    id: 'rec-air-002',
    measureCode: 'AIR-EE-003',
    titleBn: 'কম্প্রেসড এয়ার পাইপলাইন আল্ট্রাসনিক লিক মেরামত',
    titleEn: 'Compressed Air Ultrasonic Leak Sealing & Pressure Tuning',
    category: 'Compressed Air',
    evidenceGrade: 'A',
    sourceCitation: 'GIZ Bangladesh Promotion of Social & Environmental Standards (PSES) Textile Audit Data',
    sourceUrl: 'https://giz.de',
    applicabilityBn: 'কম্প্রেসার মোটর ৩০ কিলোওয়াটের বেশি এবং ৫ বছরের পুরনো পাইপলাইনযুক্ত ইউনিটের জন্য প্রযোজ্য।',
    applicabilityEn: 'Facilities with compressors > 30 kW and air distribution piping > 3 years old.',
    assumptionsBn: 'গড় পাইপলাইন অপচয় ২০% থেকে কমিয়ে ৫%-এ আনার বাস্তব অভিজ্ঞতার ভিত্তিতে প্রাক্কলিত।',
    assumptionsEn: 'Estimated based on reducing typical 20% air leakage down to 5% optimal baseline.',
    savingsBdtRange: { low: 65000, typical: 85000, high: 110000 },
    capexBdtRange: { low: 10000, typical: 15000, high: 22000 },
    paybackMonthsRange: { low: 0.5, typical: 2.1, high: 3.5 },
    tco2eAvoidedRange: { low: 3.0, typical: 4.2, high: 5.8 },
    costPerTco2e: -16500,
    status: 'Planned',
    financingNoteBn: 'স্বল্প ব্যয়ের অভ্যন্তরীণ অপারেশনাল বাজেট থেকে তাৎক্ষণিক সম্পাদনযোগ্য।',
    financingNoteEn: 'Low capex; easily funded through standard factory monthly operational maintenance budget.',
  },
  {
    id: 'rec-boil-003',
    measureCode: 'BOIL-EE-001',
    titleBn: 'বয়লার ব্লো-ডাউন এবং ফ্লু-গ্যাস ইকোনোমাইজার স্থাপন',
    titleEn: 'Boiler Flue Gas Economizer & Blowdown Heat Recovery',
    category: 'Boiler & Steam',
    evidenceGrade: 'A',
    sourceCitation: 'IFC PaCT Resource Efficiency in Bangladesh Textile Sector Report (Case Study #14)',
    sourceUrl: 'https://pact-bangladesh.org',
    applicabilityBn: 'প্রাকৃতিক গ্যাসচালিত ৫ টন/ঘণ্টা বা ততোধিক ক্ষমতার বাষ্প বয়লারের জন্য প্রযোজ্য।',
    applicabilityEn: 'Steam boilers > 3 TPH operating on Titas gas network without existing economizer.',
    assumptionsBn: 'তিতাস গ্যাস বাণিজ্যিক ট্যারিফ ৩০.০০ টাকা/m³ এবং নিষ্কাশিত ফ্লু গ্যাসের তাপমাত্রা ১৬০°C থেকে ১২০°C এ নামিয়ে আনা।',
    assumptionsEn: 'Based on Titas gas tariff of BDT 30.00/m³ and flue gas heat capture from 160°C down to 120°C.',
    savingsBdtRange: { low: 140000, typical: 185000, high: 240000 },
    capexBdtRange: { low: 180000, typical: 220000, high: 270000 },
    paybackMonthsRange: { low: 9.0, typical: 14.2, high: 18.0 },
    tco2eAvoidedRange: { low: 11.0, typical: 15.5, high: 20.0 },
    costPerTco2e: -9800,
    status: 'Suggested',
    financingNoteBn: 'আইডিসিওএল (IDCOL) এনার্জি এফিসিয়েন্সি ঋণ প্যাকেজের আওতায় ৮০% পর্যন্ত ঋণ সহায়তা উপলব্ধ।',
    financingNoteEn: 'Up to 80% capex financing available via IDCOL Energy Efficiency Financing Scheme.',
  },
  {
    id: 'rec-led-004',
    measureCode: 'ELEC-EE-004',
    titleBn: 'হাই-বে এলইডি লাইটিং এবং ফ্লোর মোশন সেন্সর রূপান্তর',
    titleEn: 'High-Bay Industrial LED Retrofit with Floor Motion Sensors',
    category: 'Electricity',
    evidenceGrade: 'A',
    sourceCitation: 'SREDA Industrial Lighting Efficiency Standards & Peer RMG Field Measurements',
    sourceUrl: 'http://sreda.gov.bd',
    applicabilityBn: 'ফ্লোরে এখনও প্রচলিত T8/T5 ফ্লুরোসেন্ট বা হ্যালোজেন টিউবলাইট ব্যবহারকারী কারখানা।',
    applicabilityEn: 'Factory floors utilizing conventional fluorescent or mercury vapor high-bay fixtures.',
    assumptionsBn: 'প্রতিদিন ১৬ ঘণ্টা কর্মকালীন সময় এবং ৪০% বিদ্যুৎ ব্যবহারের সাশ্রয় প্রাক্কলন।',
    assumptionsEn: 'Based on 16 hrs/day shift operations yielding 40% lighting power reduction.',
    savingsBdtRange: { low: 45000, typical: 60000, high: 80000 },
    capexBdtRange: { low: 40000, typical: 55000, high: 70000 },
    paybackMonthsRange: { low: 8.0, typical: 11.0, high: 14.5 },
    tco2eAvoidedRange: { low: 2.5, typical: 3.4, high: 4.5 },
    costPerTco2e: -8500,
    status: 'Suggested',
    financingNoteBn: 'স্থানীয় ভেন্ডরদের কিস্তিভিত্তিক সরবরাহ ব্যবস্থার অধীনে বাস্তবায়নযোগ্য।',
    financingNoteEn: 'Vendor deferred installment payment schemes readily available in Dhaka.',
  },
  {
    id: 'rec-sol-005',
    measureCode: 'SOL-RE-005',
    titleBn: 'ছাদভিত্তিক সৌর বিদ্যুৎ প্রকল্প (Net-Metering Solar PV - 100 kWp)',
    titleEn: 'Rooftop Solar PV Installation under Net-Metering Scheme (100 kWp)',
    category: 'Renewable Solar',
    evidenceGrade: 'B',
    sourceCitation: 'IDCOL Renewable Energy Rooftop Solar Master Plan & BPDB Net Metering Guidelines',
    sourceUrl: 'http://idcol.org',
    applicabilityBn: 'ন্যূনতম ১০,০০০ বর্গফুট কংক্রিট বা শেড ছাদ এলাকা এবং কারখানার মালিকানাধীন ভবনের জন্য।',
    applicabilityEn: 'Industrial premises with at least 10,000 sq ft unshaded roof space under own title.',
    assumptionsBn: '১০,০০০ বর্গফুট ছাদ এলাকা, বাৎসরিক ১৪০,০০০ kWh সৌর বিদ্যুৎ উৎপাদন ও গ্রিড ট্যারিফ ১২.৫০ টাকা।',
    assumptionsEn: '10,000 sq ft roof yielding ~140,000 kWh/yr displacement of peak utility grid electricity.',
    savingsBdtRange: { low: 1400000, typical: 1750000, high: 2100000 },
    capexBdtRange: { low: 6500000, typical: 7500000, high: 8500000 },
    paybackMonthsRange: { low: 44.0, typical: 51.4, high: 60.0 },
    tco2eAvoidedRange: { low: 78.0, typical: 92.0, high: 108.0 },
    costPerTco2e: 4200,
    status: 'Suggested',
    financingNoteBn: 'আইডিসিওএল (IDCOL) হতে ১০ বছরের মেয়াদে ৬% সুদে ৮০% পর্যন্ত দীর্ঘমেয়াদী কনসেশনাল ঋণ।',
    financingNoteEn: 'IDCOL offers 10-year term financing at 6% interest covering up to 80% total capex.',
  },
];

let inMemoryRecommendations = [...mockRecommendationsList];

function mapDtoToRecommendation(dto: any): Recommendation {
  if (dto.titleBn && dto.savingsBdtRange) {
    return dto;
  }

  const capexLow = Number(dto.capexLow ?? dto.capexBdtRange?.low ?? 0);
  const capexHigh = Number(dto.capexHigh ?? dto.capexBdtRange?.high ?? 0);
  const capexTypical = Number(dto.capexBdtRange?.typical ?? (capexLow + capexHigh) / 2);

  const savingsLow = Number(dto.savingsBdtLow ?? dto.savingsBdtRange?.low ?? 0);
  const savingsTypical = Number(dto.savingsBdtTypical ?? dto.savingsBdtRange?.typical ?? 0);
  const savingsHigh = Number(dto.savingsBdtHigh ?? dto.savingsBdtRange?.high ?? 0);

  const tco2eLow = Number(dto.tco2eAvoidedLow ?? dto.tco2eAvoidedRange?.low ?? 0);
  const tco2eTypical = Number(dto.tco2eAvoidedTypical ?? dto.tco2eAvoidedRange?.typical ?? 0);
  const tco2eHigh = Number(dto.tco2eAvoidedHigh ?? dto.tco2eAvoidedRange?.high ?? 0);

  const paybackMonths = Number(dto.paybackYears ?? 0) * 12;

  return {
    id: String(dto.id),
    measureCode: dto.measureCode || '',
    titleBn: dto.nameBn || dto.titleBn || '',
    titleEn: dto.nameEn || dto.titleEn || '',
    category: dto.category || 'Electricity',
    evidenceGrade: (dto.evidenceGrade || 'A') as EvidenceGrade,
    sourceCitation: dto.sourceCitation || '',
    sourceUrl: dto.sourceUrl || '',
    applicabilityBn: dto.explanationBn || dto.applicabilityBn || '',
    applicabilityEn: dto.explanationEn || dto.applicabilityEn || '',
    assumptionsBn: dto.assumptionsBn || '',
    assumptionsEn: dto.assumptionsEn || '',
    savingsBdtRange: {
      low: savingsLow,
      typical: savingsTypical,
      high: savingsHigh,
    },
    capexBdtRange: {
      low: capexLow,
      typical: capexTypical,
      high: capexHigh,
    },
    paybackMonthsRange: dto.paybackMonthsRange ?? {
      low: Math.max(0.1, Number((paybackMonths * 0.8).toFixed(1))),
      typical: Math.max(0.1, Number(paybackMonths.toFixed(1))),
      high: Math.max(0.1, Number((paybackMonths * 1.2).toFixed(1))),
    },
    tco2eAvoidedRange: {
      low: tco2eLow,
      typical: tco2eTypical,
      high: tco2eHigh,
    },
    costPerTco2e: Number(dto.costPerTco2e ?? 0),
    status: (dto.status || 'Suggested') as RecommendationStatus,
    statusReason: dto.statusReason || undefined,
    financingNoteBn: dto.financingNoteBn || '',
    financingNoteEn: dto.financingNoteEn || '',
    realisedSavingBdt: dto.realisedSavingBdt ? Number(dto.realisedSavingBdt) : undefined,
  };
}

export async function fetchRecommendations(): Promise<Recommendation[]> {
  try {
    const data = await apiFetch<any>('/api/v1/recommendations');
    let rawList: any[] = [];
    if (Array.isArray(data)) {
      rawList = data;
    } else if (data && Array.isArray(data.all)) {
      rawList = data.all;
    } else if (data && Array.isArray(data.top5)) {
      rawList = data.top5;
    }

    if (rawList.length > 0) {
      return rawList.map(mapDtoToRecommendation);
    }
    return inMemoryRecommendations;
  } catch {
    return inMemoryRecommendations;
  }
}

export async function updateRecommendationStatus(
  id: string,
  status: RecommendationStatus,
  reason?: string
): Promise<boolean> {
  try {
    await apiFetch(`/api/v1/recommendations/${encodeURIComponent(id)}/status`, {
      method: 'PUT',
      body: JSON.stringify({ status, reason }),
    });
  } catch {
    // fallback
  }

  inMemoryRecommendations = inMemoryRecommendations.map((r) =>
    r.id === id ? { ...r, status, statusReason: reason } : r
  );
  return true;
}
