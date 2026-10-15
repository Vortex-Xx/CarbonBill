import React, { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Recommendation, RecommendationStatus } from './types';
import { fetchRecommendations, updateRecommendationStatus } from './recommendationsApi';
import { RecommendationCard } from './components/RecommendationCard';
import { MaccChart } from './components/MaccChart';
import { StatusChangeModal } from './components/StatusChangeModal';
import { TrendingDown, BarChart2, Award, ShieldCheck, RefreshCw, Layers } from 'lucide-react';

export const RecommendationsView: React.FC = () => {
  const { i18n } = useTranslation();
  const isBangla = i18n.language === 'bn';

  const [recommendations, setRecommendations] = useState<Recommendation[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [activeTab, setActiveTab] = useState<'cards' | 'macc'>('cards');
  const [categoryFilter, setCategoryFilter] = useState<string>('all');

  // Status modal
  const [statusModalData, setStatusModalData] = useState<{
    rec: Recommendation;
    targetStatus: RecommendationStatus;
  } | null>(null);

  async function loadData() {
    setLoading(true);
    try {
      const data = await fetchRecommendations();
      setRecommendations(Array.isArray(data) ? data : []);
    } catch {
      setRecommendations([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  const filtered = useMemo(() => {
    const list = Array.isArray(recommendations) ? recommendations : [];
    if (categoryFilter === 'all') return list;
    return list.filter((r) => r.category === categoryFilter);
  }, [recommendations, categoryFilter]);

  function handleInitiateStatusChange(rec: Recommendation, newStatus: RecommendationStatus) {
    if (newStatus === 'NotFeasible' || newStatus === 'Done') {
      setStatusModalData({ rec, targetStatus: newStatus });
    } else {
      handleConfirmStatusChange(rec.id, newStatus, '');
    }
  }

  async function handleConfirmStatusChange(
    id: string,
    status: RecommendationStatus,
    reason: string
  ) {
    await updateRecommendationStatus(id, status, reason);
    setRecommendations((prev) =>
      prev.map((r) => (r.id === id ? { ...r, status, statusReason: reason } : r))
    );
  }

  return (
    <div className="space-y-6">
      {/* Header & Tabs */}
      <div className="bg-white p-5 rounded-2xl border border-slate-200/90 shadow-sm space-y-4">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-teal-800 text-white rounded-2xl shadow-sm">
              <TrendingDown size={22} />
            </div>
            <div>
              <h2 className="text-base md:text-lg font-black text-slate-900">
                {isBangla ? 'প্রমাণভিত্তিক সাশ্রয়ী সুপারিশমালা' : 'Evidence-Based Recommendations Engine'}
              </h2>
              <p className="text-xs text-slate-500 mt-0.5">
                {isBangla
                  ? 'কারখানার নিজস্ব বিদ্যুৎ ও গ্যাস ট্যারিফ এবং IFC PaCT বাস্তব গবেষণা ভিত্তিক'
                  : 'Derived from factory utility tariffs, actual energy bills and IFC PaCT empirical guides'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <div className="flex bg-slate-100 p-1 rounded-xl text-xs font-semibold">
              <button
                onClick={() => setActiveTab('cards')}
                className={`px-3 py-1.5 rounded-lg transition ${
                  activeTab === 'cards'
                    ? 'bg-white text-teal-900 shadow-xs font-bold'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                {isBangla ? 'পদক্ষেপ কার্ডসমূহ' : 'Action Cards'}
              </button>
              <button
                onClick={() => setActiveTab('macc')}
                className={`px-3 py-1.5 rounded-lg transition flex items-center gap-1 ${
                  activeTab === 'macc'
                    ? 'bg-white text-teal-900 shadow-xs font-bold'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                <BarChart2 size={13} /> {isBangla ? 'MACC চার্ট' : 'MACC Curve'}
              </button>
            </div>

            <button
              onClick={loadData}
              className="p-1.5 text-slate-500 hover:text-slate-800 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
              title="Refresh"
            >
              <RefreshCw size={14} className={loading ? 'animate-spin' : ''} />
            </button>
          </div>
        </div>

        {/* Honesty & Range Rules Notice */}
        <div className="p-3 bg-teal-50 border border-teal-200 rounded-xl text-xs text-teal-900 flex items-start gap-2.5">
          <ShieldCheck size={18} className="text-teal-700 shrink-0 mt-0.5" />
          <div>
            <span className="font-bold">
              {isBangla
                ? 'দায়িত্বশীল হিসাব ও পরিসীমা নীতি (Honest Ranges Protocol):'
                : 'Scientific Range Protocol:'}
            </span>
            <p className="text-slate-600 mt-0.5 text-[11px]">
              {isBangla
                ? 'কার্বনবিলে কখনো মিথ্যা একক সংখ্যা দেওয়া হয় না। সাশ্রয়, পে-ব্যাক ও বিনিয়োগ সর্বদা ন্যূনতম, গড় ও সর্বোচ্চ পরিসীমায় (Low/Typical/High) প্রদর্শিত হয়।'
                : 'Under CarbonBill engineering standards, financial savings and payback are always presented as calibrated ranges (Low, Typical, High), never deceptive single-point estimates.'}
            </p>
          </div>
        </div>

        {/* Category Filter Pills */}
        <div className="flex flex-wrap gap-1.5 pt-1">
          {[
            { id: 'all', bn: 'সকল বিভাগ', en: 'All Categories' },
            { id: 'Electricity', bn: 'বিদ্যুৎ ও মোটর', en: 'Electricity' },
            { id: 'Boiler & Steam', bn: 'বয়লার ও বাষ্প', en: 'Boiler & Steam' },
            { id: 'Compressed Air', bn: 'কম্প্রেসড এয়ার', en: 'Compressed Air' },
            { id: 'Renewable Solar', bn: 'সৌর বিদ্যুৎ (Solar)', en: 'Renewable Solar' },
          ].map((cat) => (
            <button
              key={cat.id}
              onClick={() => setCategoryFilter(cat.id)}
              className={`px-3 py-1 rounded-xl text-xs font-semibold transition ${
                categoryFilter === cat.id
                  ? 'bg-teal-900 text-white shadow-xs font-bold'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              }`}
            >
              {isBangla ? cat.bn : cat.en}
            </button>
          ))}
        </div>
      </div>

      {/* Main View Body */}
      {loading ? (
        <div className="p-12 text-center text-slate-500 text-xs font-semibold">
          {isBangla ? 'সুপারিশমালা প্রস্তুত হচ্ছে...' : 'Loading recommendations...'}
        </div>
      ) : activeTab === 'macc' ? (
        <MaccChart recommendations={filtered} isBangla={isBangla} />
      ) : filtered.length === 0 ? (
        <div className="bg-white border border-slate-200 rounded-2xl p-12 text-center text-slate-500 text-xs font-semibold shadow-xs">
          {isBangla ? 'এই ক্যাটাগরিতে কোনো সুপারিশ পাওয়া যায়নি।' : 'No recommendations found for this category.'}
        </div>
      ) : (
        <div className="space-y-4">
          {filtered.map((rec) => (
            <RecommendationCard
              key={rec.id}
              rec={rec}
              isBangla={isBangla}
              onUpdateStatus={handleInitiateStatusChange}
            />
          ))}
        </div>
      )}

      {/* Status Modal for Reason */}
      {statusModalData && (
        <StatusChangeModal
          rec={statusModalData.rec}
          targetStatus={statusModalData.targetStatus}
          isBangla={isBangla}
          onClose={() => setStatusModalData(null)}
          onConfirm={handleConfirmStatusChange}
        />
      )}
    </div>
  );
};
