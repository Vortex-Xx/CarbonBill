/**
 * CarbonBill: High-Performance Bangla & BDT Localization Formatters
 * Strict adherence to Rule 9: Bangla numerals (১২৩৪৫৬৭৮৯০) and BDT currency standard.
 */

const BANGLA_DIGITS: readonly string[] = ['০', '১', '২', '৩', '৪', '৫', '৬', '৭', '৮', '৯'];
const LATIN_TO_BANGLA_MAP: Record<string, string> = {
  '0': '০', '1': '১', '2': '২', '3': '৩', '4': '৪',
  '5': '৫', '6': '৬', '7': '৭', '8': '৮', '9': '৯'
};
const BANGLA_TO_LATIN_MAP: Record<string, string> = {
  '০': '0', '১': '1', '২': '2', '৩': '3', '৪': '4',
  '৫': '5', '৬': '6', '৭': '7', '৮': '8', '৯': '9'
};

export const BANGLA_MONTHS = [
  'জানুয়ারি', 'ফেব্রুয়ারি', 'মার্চ', 'এপ্রিল',
  'মে', 'জুন', 'জুলাই', 'আগস্ট',
  'সেপ্টেম্বর', 'অক্টোবর', 'নভেম্বর', 'ডিসেম্বর'
] as const;

export const BANGLA_TRADITIONAL_MONTHS = [
  'বৈশাখ', 'জ্যৈষ্ঠ', 'আষাঢ়', 'শ্রাবণ',
  'ভাদ্র', 'আশ্বিন', 'কার্তিক', 'অগ্রহায়ণ',
  'পৌষ', 'মাঘ', 'ফাল্গুন', 'চৈত্র'
] as const;

/**
 * Converts any number or Latin numeric string to Bangla numerals.
 * Example: 12345.67 -> ১২৩৪৫.৬৭
 */
export function toBanglaDigits(input: number | string): string {
  if (input === null || input === undefined) return '';
  const str = input.toString();
  return str.replace(/[0-9]/g, (char) => LATIN_TO_BANGLA_MAP[char] || char);
}

/**
 * Normalizes Bangla numeral strings back to standard Latin digits for calculations.
 * Example: "১২৩৪৫.৬৭" -> "12345.67"
 */
export function toLatinDigits(input: string): string {
  if (!input) return '';
  return input.replace(/[০-৯]/g, (char) => BANGLA_TO_LATIN_MAP[char] || char);
}

/**
 * Formats a currency amount into Bangladeshi Taka (BDT) with proper separators.
 * Standard format: ৳ ১২,৩৪৫.০০ or BDT 12,345.00
 */
export function formatBdt(amount: number, options: { useBanglaDigits?: boolean; symbol?: string } = {}): string {
  const { useBanglaDigits = true, symbol = '৳' } = options;
  if (amount == null || isNaN(amount)) return `${symbol} 0.00`;

  // Format with standard Indian/South Asian grouping: Lakhs and Crores
  const parts = amount.toFixed(2).split('.');
  let integerPart = parts[0];
  const decimalPart = parts[1];

  // South Asian number formatting
  const isNegative = integerPart.startsWith('-');
  if (isNegative) integerPart = integerPart.slice(1);

  let lastThree = integerPart.slice(-3);
  const otherNumbers = integerPart.slice(0, -3);
  if (otherNumbers !== '') {
    lastThree = ',' + lastThree;
  }
  const formattedInteger = otherNumbers.replace(/\B(?=(\d{2})+(?!\d))/g, ',') + lastThree;
  const fullAmount = `${isNegative ? '-' : ''}${formattedInteger}.${decimalPart}`;

  if (useBanglaDigits) {
    return `${symbol} ${toBanglaDigits(fullAmount)}`;
  }
  return `${symbol} ${fullAmount}`;
}

/**
 * Formats a date using localized Bangla month names.
 * Example: Date(2026-10-08) -> "০৮ অক্টোবর ২০২৬"
 */
export function formatBanglaDate(dateInput: Date | string, options: { includeDay?: boolean; includeYear?: boolean } = {}): string {
  const { includeDay = true, includeYear = true } = options;
  const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
  if (isNaN(date.getTime())) return '';

  const day = date.getDate();
  const monthIndex = date.getMonth();
  const year = date.getFullYear();

  const monthName = BANGLA_MONTHS[monthIndex];
  const parts: string[] = [];

  if (includeDay) {
    const dayStr = day < 10 ? `0${day}` : `${day}`;
    parts.push(toBanglaDigits(dayStr));
  }
  parts.push(monthName);
  if (includeYear) {
    parts.push(toBanglaDigits(year));
  }

  return parts.join(' ');
}

export const formatBanglaNumber = toBanglaDigits;
