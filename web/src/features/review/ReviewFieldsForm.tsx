import React, { useState, useEffect, useRef } from 'react';
import {
  Check,
  AlertTriangle,
  RotateCcw,
  Sparkles,
  ShieldCheck,
  Calendar,
  DollarSign,
  Copy,
  Keyboard,
} from 'lucide-react';
import { ReviewField, ReviewQueueItem, ConfirmRequest } from './reviewApi';
import { toBanglaDigits } from '../../shared/formatters';

interface ReviewFieldsFormProps {
  document: ReviewQueueItem;
  activeField: ReviewField | null;
  onSelectField: (field: ReviewField) => void;
  onConfirm: (payload: ConfirmRequest) => Promise<void>;
  onRejectRetake: (reason: string) => Promise<void>;
  onNextDocument: () => void;
  isBangla?: boolean;
}

export function ReviewFieldsForm({
  document,
  activeField,
  onSelectField,
  onConfirm,
  onRejectRetake,
  onNextDocument,
  isBangla = false,
}: ReviewFieldsFormProps) {
  const [corrections, setCorrections] = useState<Record<string, string>>({});
  const [isEstimated, setIsEstimated] = useState<boolean>(document.isEstimated);
  const [duplicateResolutionId, setDuplicateResolutionId] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  const firstLowConfidenceInputRef = useRef<HTMLInputElement>(null);

  // Canonical bill fields that must always appear in review
  const canonicalFields = ['Vendor', 'BillingPeriod', 'BillNumber', 'Quantity', 'Unit', 'AmountBdt'];

  // Initialize corrections and auto-focus low-confidence field
  useEffect(() => {
    const initial: Record<string, string> = {
      Vendor: '',
      BillingPeriod: '',
      BillNumber: '',
      Quantity: '',
      Unit: document.docType === 'GasBill' ? 'm3' : document.docType === 'DieselSlip' ? 'litre' : 'kWh',
      AmountBdt: '',
    };

    document.fields.forEach((f) => {
      const val = f.correctedValue || f.normalizedValue || f.rawValue || '';
      initial[f.fieldName] = val;
    });

    // Check common aliases
    if (!initial['Vendor']) {
      initial['Vendor'] = initial['Organization'] || initial['Provider'] || initial['Company'] || '';
    }
    if (!initial['AmountBdt'] || initial['AmountBdt'] === '0' || initial['AmountBdt'] === '0.00') {
      const altAmt = initial['Amount'] || initial['TotalAmount'] || initial['TotalPayable'] || initial['NetPayable'];
      if (altAmt && altAmt !== '0' && altAmt !== '0.00') initial['AmountBdt'] = altAmt;
    }
    if (!initial['Quantity'] || initial['Quantity'] === '0' || initial['Quantity'] === '0.00') {
      const altQty = initial['Consumption'] || initial['TotalUnits'] || initial['UsedUnits'] || initial['Units'];
      if (altQty && altQty !== '0' && altQty !== '0.00') initial['Quantity'] = altQty;
    }
    if (!initial['BillingPeriod']) {
      initial['BillingPeriod'] = initial['BillMonth'] || initial['Period'] || initial['Month'] || '';
    }
    if (!initial['BillNumber']) {
      initial['BillNumber'] = initial['BillNo'] || initial['InvoiceNo'] || initial['AccountNumber'] || '';
    }

    setCorrections(initial);
    setIsEstimated(document.isEstimated);

    // Auto-focus lowest confidence field
    const lowest = [...document.fields].sort((a, b) => a.confidence - b.confidence)[0];
    if (lowest && lowest.confidence < 0.85) {
      onSelectField(lowest);
      setTimeout(() => {
        firstLowConfidenceInputRef.current?.focus();
      }, 100);
    }
  }, [document]);

  function handleFieldChange(fieldName: string, value: string) {
    setCorrections((prev) => ({ ...prev, [fieldName]: value }));
  }

  async function handleConfirmSubmit(e: React.FormEvent) {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      const qtyNum = parseFloat(corrections['Quantity'] || '0');
      const amountNum = parseFloat(corrections['AmountBdt'] || '0');

      const payload: ConfirmRequest = {
        activityType: document.docType,
        overrideQuantity: qtyNum > 0 ? qtyNum : undefined,
        overrideUnit: corrections['Unit'] || 'kWh',
        overrideAmountBdt: amountNum > 0 ? amountNum : undefined,
        overridePeriod: corrections['BillingPeriod'] || undefined,
        isEstimated,
        resolveDuplicateWithDocId: duplicateResolutionId || undefined,
      };

      await onConfirm(payload);
    } finally {
      setIsSubmitting(false);
    }
  }

  // Build sorted list of fields guaranteeing standard fields + any extras
  const existingFieldMap = new Map<string, ReviewField>(
    document.fields.map((f) => [f.fieldName, f])
  );

  const displayFields: Array<{
    id: string;
    fieldName: string;
    confidence: number;
    sourceTier: number;
    rawValue?: string;
  }> = [];

  // Add canonical fields in logical order
  canonicalFields.forEach((fieldName) => {
    const existing = existingFieldMap.get(fieldName);
    displayFields.push({
      id: existing?.id || `field_${fieldName}`,
      fieldName,
      confidence: existing?.confidence ?? 0.90,
      sourceTier: existing?.sourceTier ?? 3,
      rawValue: existing?.rawValue,
    });
  });

  // Add any extra extracted fields (MeterNumber, PowerFactorPenaltyBdt, etc.)
  document.fields.forEach((f) => {
    if (!canonicalFields.includes(f.fieldName)) {
      displayFields.push({
        id: f.id,
        fieldName: f.fieldName,
        confidence: f.confidence,
        sourceTier: f.sourceTier,
        rawValue: f.rawValue,
      });
    }
  });

  const confidencePct = Math.round(document.overallConfidence * 100);
  const isHighConfidence = document.overallConfidence >= 0.9;
  const isMediumConfidence = document.overallConfidence >= 0.75 && document.overallConfidence < 0.9;

  return (
    <form onSubmit={handleConfirmSubmit} className="flex flex-col h-full bg-white rounded-2xl border border-slate-200 shadow-md p-6">
      {/* Header Info */}
      <div className="flex items-start justify-between border-b border-slate-100 pb-4 mb-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2.5 py-0.5 text-xs font-black rounded-full bg-slate-100 text-slate-800 uppercase tracking-wider">
              {document.docType || (isBangla ? 'ডকুমেন্ট' : 'Document')}
            </span>
            <span
              className={`inline-flex items-center gap-1 px-2.5 py-0.5 text-xs font-bold rounded-full ${
                isHighConfidence
                  ? 'bg-emerald-100 text-emerald-800'
                  : isMediumConfidence
                  ? 'bg-amber-100 text-amber-800'
                  : 'bg-rose-100 text-rose-800'
              }`}
            >
              <Sparkles size={12} />
              {isBangla ? 'আত্মবিশ্বাস' : 'Confidence'}: {isBangla ? toBanglaDigits(confidencePct) : confidencePct}%
            </span>
          </div>
          <h2 className="text-base font-black text-slate-900 mt-1 truncate">
            {corrections['Vendor'] || document.fileName}
          </h2>
        </div>

        {/* Keyboard Quick Guide */}
        <div className="hidden lg:flex items-center gap-1.5 text-[11px] text-slate-400 bg-slate-50 border border-slate-200 px-2 py-1 rounded-lg">
          <Keyboard size={13} />
          <span>{isBangla ? 'Enter = অনুমোদন | N = পরবর্তী' : 'Enter = Confirm | N = Next'}</span>
        </div>
      </div>

      {/* Quick Summary Pill Banner for Consumption & Amount */}
      <div className="flex items-center justify-between bg-emerald-50/70 border border-emerald-200 rounded-xl px-3.5 py-2 mb-3.5">
        <div className="flex items-center gap-1.5 text-xs font-bold text-emerald-950">
          <span className="text-emerald-700">{isBangla ? 'ব্যবহার:' : 'Consumption:'}</span>
          <span className="font-mono font-black text-slate-900">
            {corrections['Quantity'] || '—'} {corrections['Unit'] || ''}
          </span>
        </div>
        <div className="flex items-center gap-1.5 text-xs font-bold text-emerald-950">
          <span className="text-emerald-700">{isBangla ? 'মোট টাকা:' : 'Total Amount:'}</span>
          <span className="font-mono font-black text-emerald-800 text-sm">
            ৳ {corrections['AmountBdt'] || '—'}
          </span>
        </div>
      </div>

      {/* Field List */}
      <div className="flex-1 overflow-y-auto space-y-3.5 pr-1">
        {displayFields.map((field, idx) => {
          const val = corrections[field.fieldName] ?? '';
          const isLow = field.confidence < 0.85;
          const isFocused = activeField?.id === field.id;

          return (
            <div
              key={field.id}
              onClick={() => onSelectField(field as any)}
              className={`p-3 rounded-xl border-2 transition cursor-pointer ${
                isFocused
                  ? 'border-emerald-600 bg-emerald-50/30 shadow-xs'
                  : isLow
                  ? 'border-amber-300 bg-amber-50/20'
                  : 'border-slate-200 hover:border-slate-300'
              }`}
            >
              <div className="flex items-center justify-between mb-1.5">
                <span className="text-xs font-black text-slate-700 uppercase tracking-wider">
                  {translateField(field.fieldName, isBangla)}
                </span>

                <div className="flex items-center gap-1.5">
                  <span
                    className={`text-[10px] font-bold px-1.5 py-0.5 rounded ${
                      isLow ? 'bg-amber-200 text-amber-900' : 'bg-slate-100 text-slate-600'
                    }`}
                  >
                    {isBangla ? toBanglaDigits(Math.round(field.confidence * 100)) : Math.round(field.confidence * 100)}%
                  </span>
                  {field.sourceTier === 3 && (
                    <span className="text-[10px] font-bold bg-purple-100 text-purple-800 px-1 rounded">
                      LLM
                    </span>
                  )}
                </div>
              </div>

              <input
                ref={isLow && idx === 0 ? firstLowConfidenceInputRef : null}
                type="text"
                value={val}
                placeholder={getFieldPlaceholder(field.fieldName, isBangla)}
                onFocus={() => onSelectField(field as any)}
                onChange={(e) => handleFieldChange(field.fieldName, e.target.value)}
                className="w-full px-3 py-1.5 rounded-lg border border-slate-300 text-sm font-semibold text-slate-900 focus:outline-hidden focus:border-emerald-600 bg-white"
              />

              {field.rawValue && field.rawValue !== val && (
                <p className="text-[10px] text-emerald-700 font-medium mt-1">
                  {isBangla ? 'মূল ও সি আর:' : 'Raw OCR:'} <span className="font-mono">{field.rawValue}</span>
                </p>
              )}
            </div>
          );
        })}

        {/* Missing Month Estimation Checkbox */}
        <div className="pt-2 border-t border-slate-100">
          <label className="flex items-center gap-2 text-xs font-bold text-slate-700 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={isEstimated}
              onChange={(e) => setIsEstimated(e.target.checked)}
              className="rounded text-emerald-600 focus:ring-emerald-500 w-4 h-4"
            />
            {isBangla
              ? 'অনুপস্থিত মাসের আনুমানিক হিসাব (Is Estimated Data)'
              : 'Estimated Data / Proxy Calculation'}
          </label>
        </div>
      </div>

      {/* Action Footer */}
      <div className="pt-4 border-t border-slate-200 mt-4 space-y-2">
        <div className="flex items-center gap-2">
          <button
            type="submit"
            disabled={isSubmitting}
            className="flex-1 py-3 bg-emerald-700 hover:bg-emerald-800 text-white font-black text-sm rounded-xl shadow-md transition flex items-center justify-center gap-2 disabled:opacity-50"
          >
            <ShieldCheck size={18} />
            {isSubmitting
              ? isBangla ? 'অনুমোদন হচ্ছে...' : 'Confirming...'
              : isBangla ? 'অনুমোদন করুন (Enter)' : 'Confirm & Save (Enter)'}
          </button>

          <button
            type="button"
            onClick={onNextDocument}
            className="px-4 py-3 bg-slate-100 hover:bg-slate-200 text-slate-800 font-bold text-sm rounded-xl transition"
          >
            {isBangla ? 'পরবর্তী (N)' : 'Next (N)'}
          </button>
        </div>

        <button
          type="button"
          onClick={() =>
            onRejectRetake(
              isBangla
                ? 'ছবি অস্পষ্ট বা কাটা পড়েছে'
                : 'Image is blurry or damaged; please retake'
            )
          }
          className="w-full py-2 bg-rose-50 hover:bg-rose-100 text-rose-700 font-bold text-xs rounded-lg transition flex items-center justify-center gap-1.5"
        >
          <RotateCcw size={14} />
          {isBangla
            ? 'ফ্লোরে পুনরায় তোলার অনুরোধ পাঠান (Retake Request)'
            : 'Request Photo Retake from Floor'}
        </button>
      </div>
    </form>
  );
}

function translateField(fieldName: string, isBangla: boolean): string {
  if (isBangla) {
    switch (fieldName) {
      case 'Vendor':
        return 'সরবরাহকারী প্রতিষ্ঠান (Vendor)';
      case 'BillNumber':
        return 'বিল / চালান নম্বর (Bill No)';
      case 'BillingPeriod':
        return 'বিলিং পিরিয়ড / মাস (Period)';
      case 'Quantity':
        return 'ব্যবহারের পরিমাণ (Quantity)';
      case 'Unit':
        return 'একক (Unit)';
      case 'AmountBdt':
        return 'মোট প্রদেয় টাকা (Amount BDT)';
      case 'MeterNumber':
        return 'মিটার নম্বর (Meter No)';
      case 'PowerFactorPenalty':
        return 'পাওয়ার ফ্যাক্টর জরিমানা (Penalty)';
      default:
        return fieldName;
    }
  }

  switch (fieldName) {
    case 'Vendor':
      return 'Vendor / Utility Provider';
    case 'BillNumber':
      return 'Bill / Invoice Number';
    case 'BillingPeriod':
      return 'Billing Period / Month';
    case 'Quantity':
      return 'Consumption Quantity';
    case 'Unit':
      return 'Unit of Measure';
    case 'AmountBdt':
      return 'Billed Amount (BDT)';
    case 'MeterNumber':
      return 'Meter Serial Number';
    case 'PowerFactorPenalty':
      return 'Power Factor Surcharge (BDT)';
    default:
      return fieldName;
  }
}

function getFieldPlaceholder(fieldName: string, isBangla: boolean): string {
  if (isBangla) {
    switch (fieldName) {
      case 'Vendor':
        return 'যেমন: DPDC, DESCO, Titas Gas';
      case 'BillNumber':
        return 'যেমন: 25128075';
      case 'BillingPeriod':
        return 'যেমন: 2026-09';
      case 'Quantity':
        return 'যেমন: 439.00';
      case 'Unit':
        return 'যেমন: kWh, m3, litre';
      case 'AmountBdt':
        return 'যেমন: 4445.00';
      case 'MeterNumber':
        return 'যেমন: 167564';
      default:
        return 'মান লিখুন...';
    }
  }

  switch (fieldName) {
    case 'Vendor':
      return 'e.g. DPDC, DESCO, Titas Gas';
    case 'BillNumber':
      return 'e.g. 25128075';
    case 'BillingPeriod':
      return 'e.g. 2026-09';
    case 'Quantity':
      return 'e.g. 439.00';
    case 'Unit':
      return 'e.g. kWh, m3, litre';
    case 'AmountBdt':
      return 'e.g. 4445.00';
    case 'MeterNumber':
      return 'e.g. 167564';
    default:
      return 'Enter value...';
  }
}
