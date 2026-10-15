import { describe, it, expect } from 'vitest';
import { fetchAuditorSnapshot } from '../auditorApi';
import { getCompanyBadge } from '../companyBadge';

describe('Auditor Read-Only Portal & Click-to-Source Traceability', () => {
  it('loads immutable snapshot metadata with cryptographic SHA-256 seal', async () => {
    const snapshot = await fetchAuditorSnapshot('rep-2026-08');
    expect(snapshot).toBeDefined();
    expect(snapshot.organizationName).toBe('Apex Textiles Limited');
    expect(snapshot.contentHashSha256.length).toBe(64);
    expect(snapshot.totalEmissions).toBeCloseTo(
      snapshot.scope1Emissions + snapshot.scope2Emissions + snapshot.scope3Emissions,
      1
    );
  });

  it('guarantees click-to-source traceability for every activity line item', async () => {
    const snapshot = await fetchAuditorSnapshot('rep-2026-08');
    expect(snapshot.lineItems.length).toBeGreaterThan(0);

    for (const item of snapshot.lineItems) {
      // Must link to original document
      expect(item.documentId).toMatch(/^DOC-/);
      expect(item.documentDate.length).toBeGreaterThan(0);

      // Must have OCR confidence score
      expect(item.ocrConfidence).toBeGreaterThanOrEqual(0);
      expect(item.ocrConfidence).toBeLessThanOrEqual(100);

      // Must have confirming reviewer
      expect(item.confirmedBy.length).toBeGreaterThan(0);

      // Must have applied emission factor citation and year
      expect(item.factorSource.length).toBeGreaterThan(5);
      expect(item.factorValue).toBeGreaterThan(0);
      expect(item.factorCitationYear).toBeGreaterThanOrEqual(2020);
    }
  });

  it('provides bill company identification for each traceability item', async () => {
    const snapshot = await fetchAuditorSnapshot('rep-2026-08');
    for (const item of snapshot.lineItems) {
      expect(item.billCompany).toBeDefined();
      expect(item.billCompany!.length).toBeGreaterThan(2);

      const badge = getCompanyBadge(item.billCompany, item.documentTypeEn, item.sourceNameEn);
      expect(badge.tag).toBeDefined();
      expect(badge.name).toBeDefined();
      expect(badge.className).toBeDefined();
    }
  });

  it('provides step-by-step tCO2e calculation breakdown and arithmetic formula', async () => {
    const snapshot = await fetchAuditorSnapshot('rep-2026-08');
    for (const item of snapshot.lineItems) {
      // Must have gross kg CO2e
      expect(item.grossKgCo2e).toBeDefined();
      const expectedGross = item.activityQuantity * item.factorValue;
      expect(item.grossKgCo2e).toBeCloseTo(expectedGross, 1);

      // Must have net tCO2e equal to grossKg / 1000
      expect(item.emissionsTco2e).toBeCloseTo(item.grossKgCo2e! / 1000, 1);

      // Must have human-readable arithmetic calculation formula
      expect(item.calculationFormula).toBeDefined();
      expect(item.calculationFormula).toContain('×');
      expect(item.calculationFormula).toContain('1,000');
      expect(item.calculationFormula).toContain('tCO₂e');
    }
  });

  it('provides division origin and company name for every activity source', async () => {
    const snapshot = await fetchAuditorSnapshot('rep-2026-08');
    for (const item of snapshot.lineItems) {
      expect(item.division).toBeDefined();
      expect(item.division!.length).toBeGreaterThan(0);
      expect(item.division).toMatch(/(Dhaka|Chattogram|Sylhet|Khulna|Rajshahi) Division/);

      expect(item.divisionBn).toBeDefined();
      expect(item.divisionBn!.length).toBeGreaterThan(0);

      expect(item.companyName).toBeDefined();
      expect(item.companyName!.length).toBeGreaterThan(0);
    }
  });

  it('validates GHG Protocol scopes breakdown and dynamic DQS grading', async () => {
    const snapshot = await fetchAuditorSnapshot('rep-2026-08');
    // Scope arithmetic
    expect(snapshot.scope1Emissions).toBeGreaterThan(0);
    expect(snapshot.scope2Emissions).toBeGreaterThan(0);
    expect(snapshot.scope3Emissions).toBeGreaterThan(0);
    expect(snapshot.totalEmissions).toBeCloseTo(
      snapshot.scope1Emissions + snapshot.scope2Emissions + snapshot.scope3Emissions,
      1
    );

    // Dynamic DQS score & grade validation
    expect(snapshot.dataQualityScore).toBeGreaterThanOrEqual(0.6);
    expect(snapshot.dataQualityScore).toBeLessThanOrEqual(1.0);
    expect(['Grade A', 'Grade B', 'Grade C', 'Grade D']).toContain(snapshot.dqsGrade);

    if (snapshot.dataQualityScore >= 0.9) {
      expect(snapshot.dqsGrade).toBe('Grade A');
    } else if (snapshot.dataQualityScore >= 0.75) {
      expect(snapshot.dqsGrade).toBe('Grade B');
    }
  });

  it('verifies that price redaction flag is respected', async () => {
    const snapshot = await fetchAuditorSnapshot('rep-2026-08');
    expect(snapshot.redactPrices).toBe(true);
  });
});
