import React, { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Flag, FlagSeverity } from './types';
import { fetchFlags, acknowledgeFlag, dismissFlag, snoozeFlag } from './flagsApi';
import { FlagCard } from './components/FlagCard';
import { DismissModal } from './components/DismissModal';
import { SnoozeModal } from './components/SnoozeModal';
import { AlertCircle, CheckCircle2, Filter, ShieldCheck, RefreshCw } from 'lucide-react';

interface FlagsFeedProps {
  onGoToReview?: () => void;
}

export const FlagsFeed: React.FC<FlagsFeedProps> = ({ onGoToReview }) => {
  const { i18n } = useTranslation();
  const isBangla = i18n.language === 'bn';

  const [flags, setFlags] = useState<Flag[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [filter, setFilter] = useState<string>('top5');

  // Modals state
  const [dismissingFlag, setDismissingFlag] = useState<Flag | null>(null);
  const [snoozingFlag, setSnoozingFlag] = useState<Flag | null>(null);

  async function loadData() {
    setLoading(true);
    try {
      const data = await fetchFlags();
      setFlags(data);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  const filteredFlags = useMemo(() => {
    if (filter === 'top5') {
      // Top 5 by severity then open state
      return [...flags]
        .sort((a, b) => {
          const score = (f: Flag) => (f.severity === 'Critical' ? 3 : f.severity === 'Warning' ? 2 : 1);
          return score(b) - score(a);
        })
        .slice(0, 5);
    }
    if (filter === 'open') return flags.filter((f) => f.state === 'Open');
    if (filter === 'critical') return flags.filter((f) => f.severity === 'Critical');
    if (filter === 'warning') return flags.filter((f) => f.severity === 'Warning');
    if (filter === 'acknowledged') return flags.filter((f) => f.state === 'Acknowledged');
    if (filter === 'dismissed') return flags.filter((f) => f.state === 'Dismissed');
    return flags;
  }, [flags, filter]);

  async function handleAcknowledge(id: string) {
    await acknowledgeFlag(id);
    setFlags((prev) => prev.map((f) => (f.id === id ? { ...f, state: 'Acknowledged' } : f)));
  }

  async function handleConfirmDismiss(id: string, reason: string) {
    await dismissFlag(id, reason);
    setFlags((prev) =>
      prev.map((f) => (f.id === id ? { ...f, state: 'Dismissed', dismissedReason: reason } : f))
    );
  }

  async function handleConfirmSnooze(id: string, days: number, reason: string) {
    await snoozeFlag(id, days, reason);
    setFlags((prev) => prev.map((f) => (f.id === id ? { ...f, state: 'Acknowledged' } : f)));
  }

  return (
    <div className="space-y-6">
      {/* Header & Fatigue Controls Notice */}
      <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-3">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-amber-500 text-white rounded-2xl shadow-sm">
              <AlertCircle size={22} />
            </div>
            <div>
              <h2 className="text-base md:text-lg font-black text-slate-900">
                {isBangla ? 'কার্বন ফ্ল্যাগস ও অডিট সতর্কতা' : 'Carbon Flags & Audit Readiness Feed'}
              </h2>
              <p className="text-xs text-slate-500 mt-0.5">
                {isBangla
                  ? 'ডেটা কোয়ালিটি, অস্বাভাবিক ওঠানামা এবং ক্রেতা অডিট ঝুঁকির স্বয়ংক্রিয় নোটিশ'
                  : 'Automated deterministic rules monitoring data anomalies and buyer audit risks'}
              </p>
            </div>
          </div>

          <button
            onClick={loadData}
            className="self-start md:self-auto px-3 py-1.5 text-xs font-bold text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-xl transition flex items-center gap-1.5"
          >
            <RefreshCw size={13} className={loading ? 'animate-spin' : ''} />
            {isBangla ? 'রিফ্রেশ' : 'Refresh'}
          </button>
        </div>

        {/* Fatigue Control Banner */}
        <div className="p-3 bg-teal-50 border border-teal-200/80 rounded-xl text-xs text-teal-900 flex items-start gap-2.5">
          <ShieldCheck size={18} className="text-teal-700 shrink-0 mt-0.5" />
          <div>
            <span className="font-bold">
              {isBangla
                ? 'সতর্কতা ক্লান্তি নিয়ন্ত্রণ (Alert Fatigue Control):'
                : 'Fatigue Protection Protocol:'}
            </span>
            <p className="text-slate-600 mt-0.5 text-[11px]">
              {isBangla
                ? 'অতিরিক্ত নোটিফিকেশনের ক্লান্তি এড়াতে কেবল শীর্ষ ৫টি সবচেয়ে গুরুত্বপূর্ণ ফ্ল্যাগ সামনে রাখা হয়। অপ্রয়োজনীয় ফ্ল্যাগ ৩০ দিনের জন্য স্থগিত বা বাতিল করা যায়।'
                : 'To prevent notification overload, CarbonBill restricts high-priority views to the top 5 most actionable items. Dismissed items expire in 30 days.'}
            </p>
          </div>
        </div>

        {/* Filter Pills */}
        <div className="flex flex-wrap gap-1.5 pt-1">
          {[
            { id: 'top5', bn: 'শীর্ষ ৫ অগ্রাধিকার', en: 'Top 5 Priority' },
            { id: 'open', bn: 'উন্মুক্ত (Open)', en: 'Open' },
            { id: 'critical', bn: 'জটিল (Critical)', en: 'Critical' },
            { id: 'warning', bn: 'সতর্কতা (Warning)', en: 'Warning' },
            { id: 'acknowledged', bn: 'স্বীকৃত', en: 'Acknowledged' },
            { id: 'dismissed', bn: 'বাতিলকৃত', en: 'Dismissed' },
          ].map((tab) => (
            <button
              key={tab.id}
              onClick={() => setFilter(tab.id)}
              className={`px-3 py-1.5 rounded-xl text-xs font-bold transition ${
                filter === tab.id
                  ? 'bg-teal-900 text-white shadow-xs'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              }`}
            >
              {isBangla ? tab.bn : tab.en}
            </button>
          ))}
        </div>
      </div>

      {/* Flags Feed List */}
      <div className="space-y-3">
        {loading ? (
          <div className="p-12 text-center text-slate-500 text-xs font-semibold">
            {isBangla ? 'ফ্ল্যাগ তালিকা লোড হচ্ছে...' : 'Loading flags...'}
          </div>
        ) : filteredFlags.length === 0 ? (
          /* Empty State */
          <div className="bg-white p-12 rounded-2xl border border-slate-200/80 shadow-sm text-center space-y-3">
            <div className="w-12 h-12 bg-emerald-100 text-emerald-700 rounded-full flex items-center justify-center mx-auto">
              <CheckCircle2 size={24} />
            </div>
            <h3 className="font-bold text-slate-900 text-sm md:text-base">
              {isBangla ? 'সব ঠিক আছে! কোনো সক্রিয় কার্বন ফ্ল্যাগ নেই' : 'All Clear! No Open Carbon Flags'}
            </h3>
            <p className="text-xs text-slate-500 max-w-sm mx-auto">
              {isBangla
                ? 'আপনার কারখানার সকল জ্বালানি ও বিদ্যুৎ ডেটা স্বাভাবিক পরিসীমায় রয়েছে এবং অডিট স্ট্যান্ডার্ড পূরণ করেছে।'
                : 'All utility meters and fuel logs are operating within normal statistical limits and meet audit requirements.'}
            </p>
          </div>
        ) : (
          filteredFlags.map((flag) => (
            <FlagCard
              key={flag.id}
              flag={flag}
              isBangla={isBangla}
              onAcknowledge={handleAcknowledge}
              onOpenDismiss={(f) => setDismissingFlag(f)}
              onOpenSnooze={(f) => setSnoozingFlag(f)}
              onGoToEvidence={() => onGoToReview && onGoToReview()}
            />
          ))
        )}
      </div>

      {/* Dismiss Reason Modal */}
      {dismissingFlag && (
        <DismissModal
          flag={dismissingFlag}
          isBangla={isBangla}
          onClose={() => setDismissingFlag(null)}
          onConfirm={handleConfirmDismiss}
        />
      )}

      {/* Snooze Modal */}
      {snoozingFlag && (
        <SnoozeModal
          flag={snoozingFlag}
          isBangla={isBangla}
          onClose={() => setSnoozingFlag(null)}
          onConfirm={handleConfirmSnooze}
        />
      )}
    </div>
  );
};
