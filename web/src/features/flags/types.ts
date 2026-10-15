export type FlagSeverity = 'Critical' | 'Warning' | 'Info';
export type FlagState = 'Open' | 'Acknowledged' | 'Dismissed' | 'Resolved';

export interface FlagEvidence {
  documentId?: string;
  documentType?: string;
  assetName?: string;
  measuredValue?: number;
  expectedValue?: number;
  unit?: string;
  ratio?: number;
}

export interface Flag {
  id: string;
  ruleId: string;
  ruleCode: string;
  severity: FlagSeverity;
  period: string;
  titleBn: string;
  titleEn: string;
  explanationBn: string;
  explanationEn: string;
  suggestedActionBn: string;
  suggestedActionEn: string;
  state: FlagState;
  evidence: FlagEvidence;
  dismissedReason?: string;
  dismissedAt?: string;
  expiresAt?: string;
  snoozedUntil?: string;
}
