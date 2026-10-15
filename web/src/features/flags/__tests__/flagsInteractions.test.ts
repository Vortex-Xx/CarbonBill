import { describe, it, expect } from 'vitest';
import {
  mockFlagsList,
  fetchFlags,
  acknowledgeFlag,
  dismissFlag,
  snoozeFlag,
} from '../flagsApi';

describe('Carbon Flags Lifecycle & Fatigue Control', () => {
  it('loads top priority flags with deterministic data quality rules', async () => {
    const flags = await fetchFlags();
    expect(flags.length).toBeGreaterThan(0);

    const hasCritical = flags.some((f) => f.severity === 'Critical');
    const hasWarning = flags.some((f) => f.severity === 'Warning');
    expect(hasCritical).toBe(true);
    expect(hasWarning).toBe(true);
  });

  it('allows acknowledging an open flag', async () => {
    const testFlagId = 'flag-pf-004';
    const success = await acknowledgeFlag(testFlagId);
    expect(success).toBe(true);

    const updatedList = await fetchFlags();
    const updated = updatedList.find((f) => f.id === testFlagId);
    expect(updated?.state).toBe('Acknowledged');
  });

  it('enforces 30-day expiration date when dismissing a flag with reason', async () => {
    const testFlagId = 'flag-spike-001';
    const reason = 'Generator maintenance caused higher consumption.';
    const success = await dismissFlag(testFlagId, reason);
    expect(success).toBe(true);

    const updatedList = await fetchFlags();
    const dismissed = updatedList.find((f) => f.id === testFlagId);
    expect(dismissed?.state).toBe('Dismissed');
    expect(dismissed?.dismissedReason).toBe(reason);
    expect(dismissed?.expiresAt).toBeDefined();

    const expiresDate = new Date(dismissed!.expiresAt!);
    const now = new Date();
    const diffDays = Math.round((expiresDate.getTime() - now.getTime()) / (1000 * 3600 * 24));
    expect(diffDays).toBeGreaterThanOrEqual(29);
    expect(diffDays).toBeLessThanOrEqual(31);
  });

  it('supports snooze with duration', async () => {
    const testFlagId = 'flag-missing-002';
    const success = await snoozeFlag(testFlagId, 14, 'Awaiting delivery receipt');
    expect(success).toBe(true);

    const updatedList = await fetchFlags();
    const snoozed = updatedList.find((f) => f.id === testFlagId);
    expect(snoozed?.snoozedUntil).toBeDefined();
  });
});
