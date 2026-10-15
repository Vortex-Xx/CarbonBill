import React, { useState } from 'react';
import { Recommendation, EvidenceGrade } from '../types';
import { toBanglaDigits, formatBdt } from '../../../shared';
import {
  TrendingDown,
  Award,
  ExternalLink,
  ChevronDown,
  ChevronUp,
  Banknote,
  DollarSign,
  Clock,
  Leaf,
  CheckCircle2,
  Sparkles,
} from 'lucide-react';

interface RecommendationCardProps {
  rec: Recommendation;
  isBangla: boolean;
  onUpdateStatus: (rec: Recommendation, newStatus: any) => void;
}

export const RecommendationCard: React.FC<RecommendationCardProps> = ({
  rec,
  isBangla,
  onUpdateStatus,
}) => {
  const [showWhy, setShowWhy] = useState<boolean>(false);

  const gradeColor: Record<EvidenceGrade, { bg: string; text: string; labelBn: string; labelEn: string }> = {
    A: {
      bg: 'bg-emerald-100 border-emerald-300',
      text: 'text-emerald-900',
      labelBn: 'গ্রেড A (বাস্তব কারখানার ডেটা)',
      labelEn: 'Grade A (Empirical Factory Data)',
    },
    B: {
      bg: 'bg-sky-100 border-sky-300',
      text: 'text-sky-900',
      labelBn: 'গ্রেড B (জাতীয় নির্দেশিকা)',
      labelEn: 'Grade B (National Sector Guide)',
    },
    C: {
      bg: 'bg-slate-100 border-slate-300',
      text: 'text-slate-700',
      labelBn: 'গ্রেড C (আন্তর্জাতিক মডেল)',
      labelEn: 'Grade C (International Model)',
    },
  };

  const currentGrade = gradeColor[rec.evidenceGrade];

  // Format ranges with BDT and Bangla numerals
  const savingsRangeFormatted = `${formatBdt(rec.savingsBdtRange.low, {
    useBanglaDigits: isBangla,
  })} – ${formatBdt(rec.savingsBdtRange.high, {
    useBanglaDigits: isBangla,
  })} (${isBangla ? 'গড়ে ' : 'avg '}${formatBdt(rec.savingsBdtRange.typical, {
    useBanglaDigits: isBangla,
  })})`;

  const capexRangeFormatted = `${formatBdt(rec.capexBdtRange.low, {
    useBanglaDigits: isBangla,
  })} – ${formatBdt(rec.capexBdtRange.high, {
    useBanglaDigits: isBangla,
  })}`;

  const pLow = Number(rec.paybackMonthsRange?.low ?? 0).toFixed(1);
  const pHigh = Number(rec.paybackMonthsRange?.high ?? 0).toFixed(1);
  const pTyp = Number(rec.paybackMonthsRange?.typical ?? 0).toFixed(1);
  const paybackRangeFormatted = isBangla
    ? `${toBanglaDigits(pLow)} – ${toBanglaDigits(pHigh)} মাস (গড়ে ${toBanglaDigits(pTyp)})`
    : `${pLow} – ${pHigh} mos (avg ${pTyp})`;

  const cLow = Number(rec.tco2eAvoidedRange?.low ?? 0).toFixed(1);
  const cHigh = Number(rec.tco2eAvoidedRange?.high ?? 0).toFixed(1);
  const carbonRangeFormatted = isBangla
    ? `${toBanglaDigits(cLow)} – ${toBanglaDigits(cHigh)} tCO₂e/বছর`
    : `${cLow} – ${cHigh} tCO₂e/yr`;

  return (
    <div className="bg-white rounded-2xl border border-slate-200/90 p-5 shadow-xs hover:shadow-md transition-all duration-200 space-y-4">
      {/* Top Header */}
      <div className="flex flex-col md:flex-row md:items-start justify-between gap-3">
        <div className="space-y-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span
              className={`text-[11px] font-bold px-2.5 py-0.5 rounded-md border flex items-center gap-1 ${currentGrade.bg} ${currentGrade.text}`}
            >
              <Award size={13} /> {isBangla ? currentGrade.labelBn : currentGrade.labelEn}
            </span>
            <span className="text-[11px] font-semibold text-slate-500 bg-slate-100 px-2 py-0.5 rounded-md">
              {rec.category}
            </span>
            <span className="text-[10px] font-mono text-slate-400 font-bold">
              #{rec.measureCode}
            </span>
          </div>
          <h3 className="font-bold text-slate-900 text-base md:text-lg">
            {isBangla ? rec.titleBn : rec.titleEn}
          </h3>
        </div>

        {/* Status Badge & Selector */}
        <div className="flex items-center gap-2 shrink-0">
          <select
            value={rec.status}
            onChange={(e) => onUpdateStatus(rec, e.target.value)}
            className={`text-xs font-bold px-2.5 py-1.5 rounded-xl border focus:outline-hidden ${
              rec.status === 'Done'
                ? 'bg-emerald-50 border-emerald-300 text-emerald-800'
                : rec.status === 'Planned'
                ? 'bg-indigo-50 border-indigo-300 text-indigo-800'
                : rec.status === 'NotFeasible'
                ? 'bg-rose-50 border-rose-300 text-rose-800'
                : 'bg-slate-50 border-slate-200 text-slate-700'
            }`}
          >
            <option value="Suggested">{isBangla ? 'প্রস্তাবিত (Suggested)' : 'Suggested'}</option>
            <option value="Planned">{isBangla ? 'পরিকল্পিত (Planned)' : 'Planned'}</option>
            <option value="Done">{isBangla ? 'সম্পন্ন (Done)' : 'Done'}</option>
            <option value="NotFeasible">{isBangla ? 'সম্ভব নয় (Not Feasible)' : 'Not Feasible'}</option>
          </select>
        </div>
      </div>

      {/* 4-Column Honest Range Matrix (Never single value!) */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        {/* 1. Annual Savings in BDT */}
        <div className="p-3 bg-emerald-50/60 border border-emerald-200/80 rounded-xl space-y-1">
          <span className="text-[11px] font-bold text-emerald-900 flex items-center gap-1">
            <Banknote size={14} className="text-emerald-700" />
            {isBangla ? 'বার্ষিক সাশ্রয় (BDT Range):' : 'Annual Savings (BDT):'}
          </span>
          <p className="text-xs font-black text-emerald-950 font-mono">
            {savingsRangeFormatted}
          </p>
          <span className="text-[10px] text-emerald-700">
            {isBangla ? 'কারখানার নিজস্ব ট্যারিফ ভিত্তিক' : 'Based on own bill tariff'}
          </span>
        </div>

        {/* 2. Capex Investment in BDT */}
        <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl space-y-1">
          <span className="text-[11px] font-bold text-slate-700 flex items-center gap-1">
            <DollarSign size={14} className="text-slate-600" />
            {isBangla ? 'প্রাক্কলিত বিনিয়োগ (Capex):' : 'Estimated Capex:'}
          </span>
          <p className="text-xs font-bold text-slate-900 font-mono">
            {capexRangeFormatted}
          </p>
          <span className="text-[10px] text-slate-500">
            {isBangla ? 'সরঞ্জাম ও ইনস্টলেশন খরচ' : 'Turnkey installation range'}
          </span>
        </div>

        {/* 3. Payback Period */}
        <div className="p-3 bg-teal-50/60 border border-teal-200/80 rounded-xl space-y-1">
          <span className="text-[11px] font-bold text-teal-900 flex items-center gap-1">
            <Clock size={14} className="text-teal-700" />
            {isBangla ? 'পে-ব্যাক সময়সীমা:' : 'Payback Period:'}
          </span>
          <p className="text-xs font-black text-teal-950 font-mono">
            {paybackRangeFormatted}
          </p>
          <span className="text-[10px] text-teal-700">
            {isBangla ? 'বিনিয়োগ ফেরত আসার সময়' : 'Simple capital recovery'}
          </span>
        </div>

        {/* 4. Carbon Avoidance */}
        <div className="p-3 bg-sky-50/60 border border-sky-200/80 rounded-xl space-y-1">
          <span className="text-[11px] font-bold text-sky-900 flex items-center gap-1">
            <Leaf size={14} className="text-sky-700" />
            {isBangla ? 'কার্বন হ্রাস (tCO₂e Avoided):' : 'CO₂e Avoided / Year:'}
          </span>
          <p className="text-xs font-black text-sky-950 font-mono">
            {carbonRangeFormatted}
          </p>
          <span className="text-[10px] text-sky-700">
            {isBangla ? 'GHG প্রোটোকল স্কোপ ২ হ্রাস' : 'Displaced grid/fuel emissions'}
          </span>
        </div>
      </div>

      {/* Financing Note Pill */}
      <div className="p-3 bg-indigo-50/60 border border-indigo-200/70 rounded-xl flex items-start gap-2 text-xs text-indigo-950">
        <Sparkles size={16} className="text-indigo-600 shrink-0 mt-0.5" />
        <div>
          <span className="font-bold">
            {isBangla ? 'অর্থায়ন সুযোগ (Green Financing Note): ' : 'Concessional Financing: '}
          </span>
          <span className="text-slate-700">
            {isBangla ? rec.financingNoteBn : rec.financingNoteEn}
          </span>
        </div>
      </div>

      {/* "Why this?" / "কেন এই পদক্ষেপ?" Dropdown Drawer */}
      {showWhy && (
        <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl text-xs space-y-3">
          <div>
            <h4 className="font-bold text-slate-800">
              {isBangla ? 'প্রযোজ্যতা ও উপযুক্ততা (Applicability):' : 'Facility Applicability:'}
            </h4>
            <p className="text-slate-600 mt-0.5">
              {isBangla ? rec.applicabilityBn : rec.applicabilityEn}
            </p>
          </div>

          <div>
            <h4 className="font-bold text-slate-800">
              {isBangla ? 'হিসাবের ভিত্তি ও অনুমান (Assumptions & Factory Baseline):' : 'Calculation Baseline & Assumptions:'}
            </h4>
            <p className="text-slate-600 mt-0.5">
              {isBangla ? rec.assumptionsBn : rec.assumptionsEn}
            </p>
          </div>

          <div className="pt-2 border-t border-slate-200 flex items-center justify-between flex-wrap gap-2">
            <span className="text-[11px] text-slate-500">
              {isBangla ? 'উৎস তথ্যসূত্র:' : 'Source Citation:'}{' '}
              <strong className="text-slate-700">{rec.sourceCitation}</strong>
            </span>
            {rec.sourceUrl && (
              <a
                href={rec.sourceUrl}
                target="_blank"
                rel="noreferrer"
                className="text-[11px] font-bold text-teal-700 hover:underline flex items-center gap-1"
              >
                {isBangla ? 'উৎস দেখুন' : 'View Source'} <ExternalLink size={12} />
              </a>
            )}
          </div>
        </div>
      )}

      {/* Footer Toggle */}
      <div className="flex items-center justify-between pt-1 border-t border-slate-100 text-xs">
        <button
          onClick={() => setShowWhy(!showWhy)}
          className="text-slate-600 hover:text-slate-900 font-bold flex items-center gap-1 py-1"
        >
          {showWhy ? (
            <>
              {isBangla ? 'হিসাবের ভিত্তি লুকান' : 'Hide Assumptions'} <ChevronUp size={14} />
            </>
          ) : (
            <>
              {isBangla ? 'কেন এই পদক্ষেপ? (Why this?)' : 'Why this recommendation?'} <ChevronDown size={14} />
            </>
          )}
        </button>

        <span className="text-[11px] text-slate-400">
          {isBangla ? 'প্রকৌশল হিসাব ও অনুমানের পরিসীমা' : 'Engineering estimate ranges (low/typ/high)'}
        </span>
      </div>
    </div>
  );
};
