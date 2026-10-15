import React, { useState } from 'react';
import { Recommendation, RecommendationStatus } from '../types';
import { X, CheckCircle2, AlertOctagon } from 'lucide-react';

interface StatusChangeModalProps {
  rec: Recommendation;
  targetStatus: RecommendationStatus;
  isBangla: boolean;
  onClose: () => void;
  onConfirm: (id: string, status: RecommendationStatus, reason: string) => void;
}

export const StatusChangeModal: React.FC<StatusChangeModalProps> = ({
  rec,
  targetStatus,
  isBangla,
  onClose,
  onConfirm,
}) => {
  const [reason, setReason] = useState<string>('');
  const [error, setError] = useState<boolean>(false);

  const isNotFeasible = targetStatus === 'NotFeasible';

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (isNotFeasible && !reason.trim()) {
      setError(true);
      return;
    }
    onConfirm(rec.id, targetStatus, reason.trim());
    onClose();
  }

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200 space-y-4">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-2">
            <div
              className={`p-2 rounded-xl ${
                isNotFeasible ? 'bg-rose-100 text-rose-700' : 'bg-emerald-100 text-emerald-700'
              }`}
            >
              {isNotFeasible ? <AlertOctagon size={20} /> : <CheckCircle2 size={20} />}
            </div>
            <div>
              <h3 className="font-bold text-slate-900 text-base">
                {isNotFeasible
                  ? isBangla
                    ? 'পদক্ষেপটি অনুপযোগী হিসেবে চিহ্নিত করুন'
                    : 'Mark as Not Feasible'
                  : isBangla
                  ? 'বাস্তবায়ন সম্পন্ন চিহ্নিত করুন'
                  : 'Mark as Completed'}
              </h3>
              <p className="text-xs text-slate-500">{isBangla ? rec.titleBn : rec.titleEn}</p>
            </div>
          </div>
          <button onClick={onClose} className="p-1 text-slate-400 hover:text-slate-600 rounded-lg">
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">
              {isNotFeasible
                ? isBangla
                  ? 'অসম্ভব হওয়ার কারণ (বাধ্যতামূলক):'
                  : 'Mandatory Reason for Infeasibility:'
                : isBangla
                ? 'বাস্তবায়নের বিবরণ বা নোট (ঐচ্ছিক):'
                : 'Implementation Outcome Notes (Optional):'}
            </label>
            <textarea
              rows={3}
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
                if (error) setError(false);
              }}
              placeholder={
                isNotFeasible
                  ? isBangla
                    ? 'যেমন: ভবনটি ভাড়ায় চালিত হওয়ায় বাড়িওয়ালা ছাদ ব্যবহারে অনুমতি দেননি...'
                    : 'e.g. Factory is in leased shed; landlord declined roof access...'
                  : isBangla
                  ? 'যেমন: নতুন ক্যাপাসিটর ব্যাংক সংযোগ করা হয়েছে...'
                  : 'e.g. New capacitor bank connected; power factor restored to 0.96...'
              }
              className={`w-full p-2.5 text-xs border rounded-xl focus:outline-hidden focus:ring-2 focus:ring-teal-600 ${
                error ? 'border-rose-500 bg-rose-50' : 'border-slate-300 bg-slate-50'
              }`}
            />
            {error && (
              <p className="text-[11px] text-rose-600 mt-1 font-semibold">
                ⚠️ {isBangla ? 'কারণ উল্লেখ করা বাধ্যতামূলক।' : 'Reason is mandatory.'}
              </p>
            )}
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl transition"
            >
              {isBangla ? 'ফিরে যান' : 'Cancel'}
            </button>
            <button
              type="submit"
              className={`px-4 py-2 text-xs font-bold text-white rounded-xl shadow-xs transition ${
                isNotFeasible
                  ? 'bg-rose-600 hover:bg-rose-700'
                  : 'bg-emerald-600 hover:bg-emerald-700'
              }`}
            >
              {isBangla ? 'নিশ্চিত করুন' : 'Confirm'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
