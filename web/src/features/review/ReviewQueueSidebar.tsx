import React from 'react';
import { Layers, AlertCircle, CheckCircle2, FileText, CheckCheck } from 'lucide-react';
import { ReviewQueueItem } from './reviewApi';
import { toBanglaDigits, formatBanglaDate } from '../../shared/formatters';

interface ReviewQueueSidebarProps {
  queue: ReviewQueueItem[];
  selectedDocumentId: string | null;
  onSelectDocument: (doc: ReviewQueueItem) => void;
  selectedFilter: string;
  onFilterChange: (filter: string) => void;
  onBulkConfirm: () => void;
  isBulkEnabled: boolean;
  isBangla?: boolean;
}

export function ReviewQueueSidebar({
  queue,
  selectedDocumentId,
  onSelectDocument,
  selectedFilter,
  onFilterChange,
  onBulkConfirm,
  isBulkEnabled,
  isBangla = false,
}: ReviewQueueSidebarProps) {
  const filters = [
    { key: '', label: isBangla ? 'সবগুলো' : 'All' },
    { key: 'GasBill', label: isBangla ? 'গ্যাস' : 'Gas' },
    { key: 'ElectricityBill', label: isBangla ? 'বিদ্যুৎ' : 'Electricity' },
    { key: 'DieselSlip', label: isBangla ? 'ডিজেল' : 'Diesel' },
    { key: 'ShippingChallan', label: isBangla ? 'চালান' : 'Challan' },
    { key: 'GeneralDocument', label: isBangla ? 'অন্যান্য' : 'Other' },
  ];

  const getDocTypeLabel = (docType?: string) => {
    switch (docType) {
      case 'GasBill':
        return isBangla ? 'গ্যাস বিল' : 'Gas Bill';
      case 'ElectricityBill':
        return isBangla ? 'বিদ্যুৎ বিল' : 'Electricity Bill';
      case 'DieselSlip':
        return isBangla ? 'ডিজেল মেমো' : 'Diesel Memo';
      case 'ShippingChallan':
        return isBangla ? 'পরিবহন চালান' : 'Transport Challan';
      default:
        return isBangla ? 'সাধারণ চালান' : 'General Slip';
    }
  };

  const formatDate = (dateUtc: string) => {
    if (isBangla) return formatBanglaDate(dateUtc);
    const d = new Date(dateUtc);
    return isNaN(d.getTime())
      ? ''
      : d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });
  };

  const highConfCount = queue.filter((q) => q.overallConfidence >= 0.9).length;

  return (
    <div className="flex flex-col h-full bg-white rounded-2xl border border-slate-200 shadow-md p-4">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3 mb-3">
        <div>
          <h3 className="text-sm font-black text-slate-900">
            {isBangla ? 'পর্যালোচনা কিউ' : 'Review Queue'}
          </h3>
          <p className="text-[11px] text-slate-500 font-medium">
            {isBangla ? 'কম আত্মবিশ্বাসী বিল সবার আগে' : 'Low confidence items prioritized'}
          </p>
        </div>

        <span className="bg-emerald-100 text-emerald-800 text-xs font-black px-2 py-0.5 rounded-full font-mono">
          {isBangla ? toBanglaDigits(queue.length) : queue.length}
        </span>
      </div>

      {/* Filter Tabs */}
      <div className="flex items-center gap-1 mb-3 overflow-x-auto pb-1">
        {filters.map((f) => (
          <button
            key={f.key}
            onClick={() => onFilterChange(f.key)}
            className={`px-2.5 py-1 rounded-lg text-xs font-bold transition whitespace-nowrap ${
              selectedFilter === f.key
                ? 'bg-slate-900 text-white'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            {f.label}
          </button>
        ))}
      </div>

      {/* Queue Items */}
      <div className="flex-1 overflow-y-auto space-y-2 pr-1">
        {queue.length === 0 ? (
          <div className="text-center py-12 text-slate-400 text-xs font-medium">
            {isBangla ? 'কিউতে কোনো বিল বাকি নেই! 🎉' : 'All documents reviewed! 🎉'}
          </div>
        ) : (
          queue.map((item) => {
            const isSelected = selectedDocumentId === item.documentId;
            const confPct = Math.round(item.overallConfidence * 100);
            const isLow = item.overallConfidence < 0.85;

            return (
              <div
                key={item.documentId}
                onClick={() => onSelectDocument(item)}
                className={`p-3 rounded-xl border-2 transition cursor-pointer ${
                  isSelected
                    ? 'border-emerald-600 bg-emerald-50/50 shadow-xs'
                    : isLow
                    ? 'border-amber-200 hover:border-amber-400 bg-amber-50/10'
                    : 'border-slate-100 hover:border-slate-300'
                }`}
              >
                <div className="flex items-center justify-between mb-1">
                  <span className="text-xs font-bold text-slate-900 truncate max-w-[140px]">
                    {item.fileName}
                  </span>
                  <span
                    className={`text-[10px] font-bold px-1.5 py-0.5 rounded ${
                      isLow ? 'bg-amber-100 text-amber-900' : 'bg-emerald-100 text-emerald-800'
                    }`}
                  >
                    {isBangla ? toBanglaDigits(confPct) : confPct}%
                  </span>
                </div>

                <div className="flex items-center justify-between text-[11px] text-slate-500">
                  <span className="font-medium text-slate-700">
                    {getDocTypeLabel(item.docType)}
                  </span>
                  <span>{formatDate(item.capturedAtUtc)}</span>
                </div>

                {/* Extracted Amount and Quantity Badge */}
                {(() => {
                  const getVal = (name: string) => {
                    const f = item.fields?.find(
                      (x) => x.fieldName?.toLowerCase() === name.toLowerCase()
                    );
                    return f?.correctedValue || f?.normalizedValue || f?.rawValue;
                  };
                  const amt = getVal('AmountBdt') || getVal('Amount');
                  const qty = getVal('Quantity') || getVal('Consumption');
                  const unit = getVal('Unit') || '';
                  const hasAmt = amt && amt !== '0.00' && amt !== '0';
                  const hasQty = qty && qty !== '0.00' && qty !== '0';

                  if (hasAmt || hasQty) {
                    return (
                      <div className="mt-1.5 pt-1.5 border-t border-slate-100 flex items-center justify-between text-[11px] font-bold">
                        <span className="text-emerald-700 font-mono">
                          {hasAmt ? `৳ ${amt}` : ''}
                        </span>
                        <span className="text-slate-600 font-mono text-[10px]">
                          {hasQty ? `${qty} ${unit}` : ''}
                        </span>
                      </div>
                    );
                  }
                  return null;
                })()}
              </div>
            );
          })
        )}
      </div>

      {/* Bulk Confirm Button */}
      {isBulkEnabled && queue.length > 0 && highConfCount > 0 && (
        <div className="pt-3 border-t border-slate-100 mt-2">
          <button
            onClick={onBulkConfirm}
            className="w-full py-2 bg-slate-900 hover:bg-slate-800 text-white font-bold text-xs rounded-xl shadow-xs transition flex items-center justify-center gap-1.5"
          >
            <CheckCheck size={15} />
            {isBangla
              ? `উচ্চ আত্মবিশ্বাসী বিলসমূহ একযোগে অনুমোদন (${toBanglaDigits(highConfCount)})`
              : `Bulk Approve High Confidence (${highConfCount})`}
          </button>
        </div>
      )}
    </div>
  );
}
