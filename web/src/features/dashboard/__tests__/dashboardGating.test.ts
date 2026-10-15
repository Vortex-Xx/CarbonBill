import { describe, it, expect } from 'vitest';
import { mockIntensityData, mockTrendData } from '../dashboardApi';
import { IntensityData } from '../types';

describe('Dashboard Gating & Hatched Trend Rules', () => {
  it('enforces peer benchmark gating rule: n < 10 returns no benchmark yet state', () => {
    // When peer sample size is thin (< 10), benchmark must be null
    const thinData: IntensityData = {
      ...mockIntensityData,
      benchmark: {
        p25: 12.0,
        p50: 15.0,
        p75: 19.0,
        p90: 24.0,
        n: 6, // Under threshold of 10!
        source: 'Small Cluster Test',
        year: 2026,
      },
    };

    const hasBenchmark = thinData.benchmark !== null && thinData.benchmark.n >= 10;
    expect(hasBenchmark).toBe(false);
  });

  it('allows benchmark display when peer sample size n >= 10', () => {
    const validData = mockIntensityData;
    expect(validData.benchmark).not.toBeNull();
    expect(validData.benchmark!.n).toBeGreaterThanOrEqual(10);

    const hasBenchmark = validData.benchmark !== null && validData.benchmark.n >= 10;
    expect(hasBenchmark).toBe(true);
    expect(validData.benchmark!.p25).toBeLessThanOrEqual(validData.benchmark!.p50);
    expect(validData.benchmark!.p50).toBeLessThanOrEqual(validData.benchmark!.p75);
    expect(validData.benchmark!.p75).toBeLessThanOrEqual(validData.benchmark!.p90);
  });

  it('correctly tags months with estimated share > 10% with hatchFlag', () => {
    // August in mock data has estimated share = 7.9 / 63.3 = 12.48% (> 10%)
    const aug = mockTrendData.find((d) => d.period === '2026-08');
    expect(aug).toBeDefined();
    const estShare = (aug!.estimatedEmissions / aug!.totalEmissions) * 100;
    expect(estShare).toBeGreaterThan(10.0);
    expect(aug!.hatchFlag).toBe(true);

    // September has estimated share = 5.0 / 65.75 = 7.6% (< 10%)
    const sep = mockTrendData.find((d) => d.period === '2026-09');
    expect(sep).toBeDefined();
    const sepEstShare = (sep!.estimatedEmissions / sep!.totalEmissions) * 100;
    expect(sepEstShare).toBeLessThan(10.0);
    expect(sep!.hatchFlag).toBe(false);
  });
});
