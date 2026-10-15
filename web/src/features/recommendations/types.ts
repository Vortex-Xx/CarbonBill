export type EvidenceGrade = 'A' | 'B' | 'C';
export type RecommendationStatus = 'Suggested' | 'Planned' | 'Done' | 'NotFeasible';

export interface NumberRange {
  low: number;
  typical: number;
  high: number;
}

export interface Recommendation {
  id: string;
  measureCode: string;
  titleBn: string;
  titleEn: string;
  category: string;
  evidenceGrade: EvidenceGrade;
  sourceCitation: string;
  sourceUrl?: string;
  applicabilityBn: string;
  applicabilityEn: string;
  assumptionsBn: string;
  assumptionsEn: string;
  savingsBdtRange: NumberRange;
  capexBdtRange: NumberRange;
  paybackMonthsRange: NumberRange;
  tco2eAvoidedRange: NumberRange;
  costPerTco2e: number; // In BDT / tCO2e. Can be negative!
  status: RecommendationStatus;
  statusReason?: string;
  financingNoteBn: string;
  financingNoteEn: string;
  realisedSavingBdt?: number;
}
