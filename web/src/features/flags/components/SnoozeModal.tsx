import React, { useState } from 'react';
import { Flag } from '../types';
import { Clock, X } from 'lucide-react';

interface SnoozeModalProps {
  flag: Flag;
  isBangla: boolean;
  onClose: () => void;
  onConfirm: (id: string, days: number, reason: string) => void;
}

export const SnoozeModal: React.FC<SnoozeModalProps> = ({
  flag,
  isBangla,
  onClose,
  onConfirm,
}) => {
  const [days, setDays] = useState<number>(7);
  const [reason, setReason] = useState<string>('');

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    onConfirm(flag.id, days, reason.trim() || 'Deferred review');
    onClose();
  }

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200 space-y-4">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-2">
            <div className="p-2 bg-indigo-100 text-indigo-800 rounded-xl">
              <Clock size={20} />
            </div>
            <div>
              <h3 className="font-bold text-slate-900 text-base">
                {isBangla ? 'সতর্কতা স্থগিত করুন (Snooze Alert)' : 'Snooze Carbon Alert'}
              </h3>
              <p className="text-xs text-slate-500">
                {isBangla ? flag.titleBn : flag.titleEn}
              </p>
            </div>
          </div>
          <button onClick={onClose} className="p-1 text-slate-400 hover:text-slate-600 rounded-lg">
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">
              {isBangla ? 'স্থগিত রাখার সময়কাল:' : 'Snooze Duration:'}
            </label>
            <div className="grid grid-cols-3 gap-2">
              {[7, 14, 30].map((d) => (
                <button
                  key={d}
                  type="button"
                  onClick={() => setDays(d)}
                  className={`py-2 text-xs font-bold rounded-xl border transition ${
                    days === d
                      ? 'bg-indigo-50 border-indigo-500 text-indigo-900 shadow-xs'
                      : 'border-slate-200 bg-slate-50 text-slate-700 hover:bg-slate-100'
                  }`}
                >
                  {isBangla ? `${d} দিন` : `${d} Days`}
                </button>
              ))}
            </div>
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">
              {isBangla ? 'স্থগিতের কারণ (ঐচ্ছিক):' : 'Reason / Note (Optional):'}
            </label>
            <input
              type="text"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder={isBangla ? 'যেমন: বিল সংগ্রহের কাজ চলছে...' : 'e.g. Invoices being gathered...'}
              className="w-full p-2.5 text-xs border border-slate-300 bg-slate-50 rounded-xl focus:outline-hidden focus:ring-2 focus:ring-indigo-600"
            />
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
              className="px-4 py-2 text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-xs transition"
            >
              {isBangla ? 'স্থগিত করুন' : 'Confirm Snooze'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
