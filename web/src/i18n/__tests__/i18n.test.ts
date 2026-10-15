import { describe, it, expect, beforeEach } from 'vitest';
import i18n from '../index';

describe('i18n Localization Engine', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('defaults to English language and provides high quality English copy', () => {
    expect(i18n.language).toBe('en');
    expect(i18n.t('appName')).toBe('CarbonBill');
    expect(i18n.t('tagline')).toBe('Defensible SME Carbon Accounting & Buyer Assurance');
    expect(i18n.t('nav.auditorPortal')).toBe('Auditor Portal');
    expect(i18n.t('dashboard.scope1')).toBe('Scope 1 (Direct Fuels & Gas)');
    expect(i18n.t('dashboard.scope2')).toBe('Scope 2 (Grid Electricity)');
    expect(i18n.t('dashboard.scope3')).toBe('Scope 3 (Freight & Transport)');
    expect(i18n.t('dashboard.dataQualityScore')).toBe('Data Quality Score (DQS)');
  });

  it('switches to Bangla accurately without hallucination', async () => {
    await i18n.changeLanguage('bn');
    expect(i18n.language).toBe('bn');
    expect(i18n.t('appName')).toBe('কার্বনবিল');
    expect(i18n.t('tagline')).toContain('কার্বন অ্যাকাউন্টিং');
    expect(i18n.t('nav.auditorPortal')).toBe('অডিটর পোর্টাল');
    expect(i18n.t('dashboard.scope1')).toBe('স্কোপ ১ (প্রত্যক্ষ জ্বালানি ও গ্যাস দহন)');
    expect(i18n.t('dashboard.scope2')).toBe('স্কোপ ২ (ক্রয়কৃত গ্রিড বিদ্যুৎ)');
    expect(i18n.t('dashboard.scope3')).toBe('স্কোপ ৩ (সরবরাহ চেইন ও পরিবহন)');
    expect(i18n.t('dashboard.dataQualityScore')).toBe('ডেটা কোয়ালিটি স্কোর (DQS)');
  });

  it('handles Review and Floor module translations in both languages', async () => {
    // English
    await i18n.changeLanguage('en');
    expect(i18n.t('review.title')).toBe('Review & Verification Workspace');
    expect(i18n.t('floor.title')).toBe('Capture Bill & Challan');

    // Bangla
    await i18n.changeLanguage('bn');
    expect(i18n.t('review.title')).toBe('চালান ও বিল পর্যালোচনা ওয়ার্কস্পেস');
    expect(i18n.t('floor.title')).toBe('রসিদ ও বিল আপলোড');
  });
});
