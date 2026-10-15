import React, { useState } from 'react';
import { Flag } from '../types';
import { AlertTriangle, X } from 'lucide-react';

interface DismissModalProps {
  flag: Flag;
  isBangla: boolean;
  onClose: () => void;
  onConfirm: (id: string, reason: string) => void;
}

export const DismissModal: React.FC<DismissModalProps> = ({
  flag,
  isBangla,
  onClose,
  onConfirm,
}) => {
  const [reason, setReason] = useState('');
  const [error, setError] = useState(false);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!reason.trim()) {
      setError(true);
      return;
    }
    onConfirm(flag.id, reason.trim());
    onClose();
  }

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200 space-y-4">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-2">
            <div className="p-2 bg-amber-100 text-amber-800 rounded-xl">
              <AlertTriangle size={20} />
            </div>
            <div>
              <h3 className="font-bold text-slate-900 text-base">
                {isBangla ? 'কার্বন ফ্ল্যাগ বাতিল করুন (Dismiss Flag)' : 'Dismiss Carbon Flag'}
              </h3>
              <p className="text-xs text-slate-500">
                {isBangla ? flag.titleBn : flag.titleEn}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1 text-slate-400 hover:text-slate-600 rounded-lg"
          >
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">
              {isBangla ? 'বাতিলের সুনির্দিষ্ট কারণ (বাধ্যতামূলক):' : 'Mandatory Justification Reason:'}
            </label>
            <textarea
              rows={3}
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
                if (error) setError(false);
              }}
              placeholder={
                isBangla
                  ? 'যেমন: জেনারেটর সার্ভিসিংয়ের কারণে বিকল্প লোড নেওয়া হয়েছিল...'
                  : 'e.g. Unusual surge due to bi-annual generator engine overhaul...'
              }
              className={`w-full p-2.5 text-xs border rounded-xl focus:outline-hidden focus:ring-2 focus:ring-teal-600 ${
                error ? 'border-rose-500 bg-rose-50/50' : 'border-slate-300 bg-slate-50'
              }`}
            />
            {error && (
              <p className="text-[11px] text-rose-600 mt-1 font-semibold">
                ⚠️ {isBangla ? 'কারণ উল্লেখ করা বাধ্যতামূলক।' : 'Reason is required to dismiss a compliance flag.'}
              </p>
            )}
          </div>

          <div className="p-3 bg-amber-50 border border-amber-200 rounded-xl text-[11px] text-amber-900 space-y-1">
            <p className="font-bold">
              ℹ️ {isBangla ? '৩০ দিনের সাময়িক মেয়াদ:' : '30-Day Expiry Notice:'}
            </p>
            <p className="text-slate-600">
              {isBangla
                ? 'নিয়ম অনুযায়ী এই বাতিলকরণ ৩০ দিন পর্যন্ত কার্যকর থাকবে। ৩০ দিন পর কারণ পর্যালোচনা সাপেক্ষে পুনরায় যাচাই করা হবে।'
                : 'Under audit governance rules, flag dismissals automatically expire in 30 days unless conditions are permanently resolved.'}
            </p>
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
              className="px-4 py-2 text-xs font-bold text-white bg-amber-600 hover:bg-amber-700 rounded-xl shadow-xs transition"
            >
              {isBangla ? 'বাতিল নিশ্চিত করুন' : 'Confirm Dismissal'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
