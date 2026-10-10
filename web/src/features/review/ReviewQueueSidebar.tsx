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
}

export function ReviewQueueSidebar({
  queue,
  selectedDocumentId,
  onSelectDocument,
  selectedFilter,
  onFilterChange,
  onBulkConfirm,
  isBulkEnabled,
}: ReviewQueueSidebarProps) {
  const filters = [
    { key: '', label: 'সবগুলো' },
    { key: 'GasBill', label: 'গ্যাস' },
    { key: 'ElectricityBill', label: 'বিদ্যুৎ' },
    { key: 'DieselSlip', label: 'ডিজেল' },
    { key: 'ShippingChallan', label: 'চালান' },
    { key: 'GeneralDocument', label: 'অন্যান্য' },
  ];

  return (
    <div className="flex flex-col h-full bg-white rounded-2xl border border-slate-200 shadow-md p-4">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3 mb-3">
        <div>
          <h3 className="text-sm font-black text-slate-900">পর্যালোচনা কিউ</h3>
          <p className="text-[11px] text-slate-500 font-medium">কম আত্মবিশ্বাসী বিল সবার আগে</p>
        </div>

        <span className="bg-emerald-100 text-emerald-800 text-xs font-black px-2 py-0.5 rounded-full font-mono">
          {toBanglaDigits(queue.length)}
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
            কিউতে কোনো বিল বাকি নেই! 🎉
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
                    {toBanglaDigits(confPct)}%
                  </span>
                </div>

                <div className="flex items-center justify-between text-[11px] text-slate-500">
                  <span className="font-medium text-slate-700">
                    {item.docType === 'GasBill'
                      ? 'গ্যাস বিল'
                      : item.docType === 'ElectricityBill'
                      ? 'বিদ্যুৎ বিল'
                      : item.docType === 'DieselSlip'
                      ? 'ডিজেল মেমো'
                      : item.docType === 'ShippingChallan'
                      ? 'পরিবহন চালান'
                      : 'সাধারণ চালান'}
                  </span>
                  <span>{formatBanglaDate(item.capturedAtUtc)}</span>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Bulk Confirm Button */}
      {isBulkEnabled && queue.length > 0 && (
        <div className="pt-3 border-t border-slate-100 mt-2">
          <button
            onClick={onBulkConfirm}
            className="w-full py-2 bg-slate-900 hover:bg-slate-800 text-white font-bold text-xs rounded-xl shadow-xs transition flex items-center justify-center gap-1.5"
          >
            <CheckCheck size={15} />
            উচ্চ আত্মবিশ্বাসী বিলসমূহ একযোগে অনুমোদন ({toBanglaDigits(queue.filter(q => q.overallConfidence >= 0.9).length)})
          </button>
        </div>
      )}
    </div>
  );
}
