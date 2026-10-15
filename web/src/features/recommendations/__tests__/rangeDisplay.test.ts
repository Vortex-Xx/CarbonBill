import { describe, it, expect } from 'vitest';
import { mockRecommendationsList } from '../recommendationsApi';
import { formatBdt, toBanglaDigits } from '../../../shared';

describe('Recommendations Range Display & Science Rules', () => {
  it('strictly enforces range ordering: low <= typical <= high for all measures', () => {
    expect(mockRecommendationsList.length).toBeGreaterThan(0);

    for (const rec of mockRecommendationsList) {
      // Savings range
      expect(rec.savingsBdtRange.low).toBeLessThanOrEqual(rec.savingsBdtRange.typical);
      expect(rec.savingsBdtRange.typical).toBeLessThanOrEqual(rec.savingsBdtRange.high);

      // Capex range
      expect(rec.capexBdtRange.low).toBeLessThanOrEqual(rec.capexBdtRange.typical);
      expect(rec.capexBdtRange.typical).toBeLessThanOrEqual(rec.capexBdtRange.high);

      // Payback months range
      expect(rec.paybackMonthsRange.low).toBeLessThanOrEqual(rec.paybackMonthsRange.typical);
      expect(rec.paybackMonthsRange.typical).toBeLessThanOrEqual(rec.paybackMonthsRange.high);

      // CO2e avoided range
      expect(rec.tco2eAvoidedRange.low).toBeLessThanOrEqual(rec.tco2eAvoidedRange.typical);
      expect(rec.tco2eAvoidedRange.typical).toBeLessThanOrEqual(rec.tco2eAvoidedRange.high);
    }
  });

  it('formats BDT ranges in both English and Bangla numerals without NaN', () => {
    const sample = mockRecommendationsList[0];

    const bnFormattedLow = formatBdt(sample.savingsBdtRange.low, { useBanglaDigits: true });
    const bnFormattedHigh = formatBdt(sample.savingsBdtRange.high, { useBanglaDigits: true });
    const enFormattedLow = formatBdt(sample.savingsBdtRange.low, { useBanglaDigits: false });

    expect(bnFormattedLow).toContain('৳');
    expect(bnFormattedLow).not.toContain('NaN');
    expect(enFormattedLow).toContain('৳');
    expect(enFormattedLow).not.toContain('NaN');
    expect(toBanglaDigits('12345')).toBe('১২৩৪৫');
  });

  it('handles negative cost-per-tonne for profitable energy efficiency interventions', () => {
    // APFC capacitor bank has negative cost per tonne because avoided penalties exceed capital expenditure
    const apfc = mockRecommendationsList.find((r) => r.measureCode === 'PFL-EE-002');
    expect(apfc).toBeDefined();
    expect(apfc!.costPerTco2e).toBeLessThan(0);

    // Compressed air leak repair is also negative cost
    const air = mockRecommendationsList.find((r) => r.measureCode === 'AIR-EE-003');
    expect(air).toBeDefined();
    expect(air!.costPerTco2e).toBeLessThan(0);
  });

  it('ensures every measure has verifiable source citation and evidence grade', () => {
    for (const rec of mockRecommendationsList) {
      expect(['A', 'B', 'C']).toContain(rec.evidenceGrade);
      expect(rec.sourceCitation.length).toBeGreaterThan(10);
      expect(rec.financingNoteBn.length).toBeGreaterThan(5);
      expect(rec.financingNoteEn.length).toBeGreaterThan(5);
    }
  });
});
