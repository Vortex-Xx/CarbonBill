import React from 'react';
import { AuditorLineItem } from '../types';
import { toBanglaDigits, formatBdt } from '../../../shared';
import { getCompanyBadge } from '../companyBadge';
import {
  X,
  ShieldCheck,
  Award,
  Lock,
  Building,
  Building2,
  MapPin,
  Calculator,
} from 'lucide-react';

interface ProvenanceInspectorModalProps {
  item: AuditorLineItem;
  redactPrices: boolean;
  isBangla: boolean;
  onClose: () => void;
}

export const ProvenanceInspectorModal: React.FC<ProvenanceInspectorModalProps> = ({
  item,
  redactPrices,
  isBangla,
  onClose,
}) => {
  const badge = getCompanyBadge(item.billCompany, item.documentTypeEn, item.sourceNameEn);
  const grossKg = item.grossKgCo2e ?? (item.activityQuantity * item.factorValue);
  const netTco2e = item.emissionsTco2e;

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 overflow-y-auto">
      <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl border border-slate-200 space-y-5 my-8 max-h-[92vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-start justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-teal-100 text-teal-800 rounded-xl">
              <ShieldCheck size={24} />
            </div>
            <div>
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-[10px] font-bold px-2 py-0.5 bg-slate-100 rounded text-slate-700">
                  {item.scope}
                </span>
                <span className="text-[10px] font-mono text-slate-400">
                  ID: {item.id}
                </span>
                <span className={`text-[10px] font-bold px-2 py-0.5 rounded border ${badge.className}`}>
                  {badge.tag}
                </span>
                <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-teal-50 text-teal-800 border border-teal-200 flex items-center gap-1">
                  <MapPin size={10} /> {isBangla ? (item.divisionBn || 'ঢাকা বিভাগ') : (item.division || 'Dhaka Division')}
                </span>
              </div>
              <h3 className="font-bold text-slate-900 text-lg mt-0.5">
                {isBangla ? item.sourceNameBn : item.sourceNameEn}
              </h3>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg transition cursor-pointer"
            aria-label="Close"
          >
            <X size={20} />
          </button>
        </div>

        {/* Traceability Grid */}
        <div className="space-y-4 text-xs">
          {/* 1. Bill & Provider Identification */}
          <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl space-y-3">
            <div className="flex items-center justify-between">
              <span className="font-bold text-slate-800 flex items-center gap-2 text-sm">
                <Building size={16} className="text-teal-700" />
                {isBangla ? 'উৎস, কোম্পানি ও সরবরাহকারী পরিচিতি' : 'Activity Source, Company & Facility Identification:'}
              </span>
              <span className={`text-xs font-bold px-2.5 py-0.5 rounded border ${badge.className}`}>
                {badge.tag}
              </span>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs pt-1">
              <div className="space-y-1">
                <span className="text-slate-500 font-medium flex items-center gap-1">
                  <Building2 size={12} className="text-slate-400" />
                  {isBangla ? 'কোম্পানি / প্রতিষ্ঠান:' : 'Enterprise Company:'}
                </span>
                <p className="font-bold text-slate-900 text-[13px]">
                  {item.companyName || 'Apex Textile & Garments Ltd.'}
                </p>
              </div>

              <div className="space-y-1">
                <span className="text-slate-500 font-medium flex items-center gap-1">
                  <MapPin size={12} className="text-teal-600" />
                  {isBangla ? 'বিভাগ / অবস্থান (Division):' : 'Administrative Division:'}
                </span>
                <p className="font-bold text-teal-900 text-[13px]">
                  {isBangla ? (item.divisionBn || 'ঢাকা বিভাগ') : (item.division || 'Dhaka Division')}
                </p>
              </div>

              <div className="space-y-1">
                <span className="text-slate-500 font-medium">
                  {isBangla ? 'বিল প্রদানকারী ইউটিলিটি / বিক্রেতা:' : 'Utility Provider / Vendor:'}
                </span>
                <p className="font-bold text-slate-900 text-[13px]">
                  {item.billCompany || badge.name}
                </p>
              </div>

              <div className="space-y-1">
                <span className="text-slate-500 font-medium">
                  {isBangla ? 'বিল / চালান নম্বর:' : 'Bill / Invoice Number:'}
                </span>
                <div>
                  <span className="font-mono font-bold text-teal-900 bg-teal-50 px-2.5 py-1 rounded border border-teal-200 inline-block">
                    #{item.billNumber || item.documentId}
                  </span>
                </div>
              </div>

              {item.meterNumber && (
                <div className="space-y-1">
                  <span className="text-slate-500 font-medium">
                    {isBangla ? 'মিটার / গ্রাহক নম্বর:' : 'Meter / Consumer Number:'}
                  </span>
                  <p className="font-mono font-bold text-slate-800">
                    {item.meterNumber}
                  </p>
                </div>
              )}

              {item.fileName && (
                <div className="space-y-1">
                  <span className="text-slate-500 font-medium">
                    {isBangla ? 'সংযুক্ত ফাইল / নথি:' : 'Attached File / Scan:'}
                  </span>
                  <p className="font-mono text-slate-700 truncate" title={item.fileName}>
                    {item.fileName}
                  </p>
                </div>
              )}

              <div className="space-y-1">
                <span className="text-slate-500 font-medium">
                  {isBangla ? 'চালানের ধরন ও তারিখ:' : 'Document Type & Date:'}
                </span>
                <p className="text-slate-800 font-medium">
                  {isBangla ? item.documentTypeBn : item.documentTypeEn} • {item.documentDate}
                </p>
              </div>

              <div className="space-y-1">
                <span className="text-slate-500 font-medium">
                  {isBangla ? 'OCR নির্ভরযোগ্যতা ও যাচাইকারী:' : 'OCR Confidence & Verifier:'}
                </span>
                <p className="text-slate-800 font-medium flex items-center gap-2">
                  <span className="text-emerald-700 font-mono font-bold">
                    {isBangla ? toBanglaDigits(item.ocrConfidence.toFixed(1)) : item.ocrConfidence.toFixed(1)}%
                  </span>
                  <span>• {item.confirmedBy}</span>
                </p>
              </div>
            </div>
          </div>

          {/* 2. Step-by-Step tCO₂e Calculation Breakdown */}
          <div className="p-4 bg-teal-50/70 border border-teal-200 rounded-xl space-y-3">
            <div className="flex items-center justify-between">
              <span className="font-bold text-teal-950 flex items-center gap-2 text-sm">
                <Calculator size={16} className="text-teal-700" />
                {isBangla ? 'tCO₂e নির্গমন গণনা বিশ্লেষণ (Calculation Breakdown)' : 'tCO₂e Emission Calculation Breakdown:'}
              </span>
              <span className="text-[10px] font-bold text-teal-800 bg-teal-100/80 px-2 py-0.5 rounded border border-teal-300">
                GHG Protocol Scope {item.scope.replace('Scope ', '')}
              </span>
            </div>

            {/* Formula banner */}
            <div className="p-2.5 bg-white/90 border border-teal-200 rounded-lg text-slate-800 font-mono text-center text-xs">
              <span className="text-teal-700 font-bold">{isBangla ? 'সূত্র (Formula): ' : 'Formula: '}</span>
              <span className="font-bold text-slate-900">
                {isBangla
                  ? 'নির্গমন (tCO₂e) = (কার্যক্রমের পরিমাণ × নির্গমন ফ্যাক্টর) ÷ ১,০০০'
                  : 'Emissions (tCO₂e) = (Activity Quantity × Emission Factor) ÷ 1,000 kg/t'}
              </span>
            </div>

            {/* Arithmetic Steps Grid */}
            <div className="grid grid-cols-1 sm:grid-cols-4 gap-2 pt-1 text-center">
              {/* Step 1 */}
              <div className="p-3 bg-white rounded-lg border border-teal-100 shadow-xs flex flex-col justify-between">
                <div className="text-[10px] font-bold text-teal-700 uppercase tracking-wider mb-1">
                  {isBangla ? '১. পরিমাণ (Quantity)' : '1. Activity'}
                </div>
                <div className="font-mono font-black text-slate-900 text-sm">
                  {isBangla
                    ? toBanglaDigits(item.activityQuantity.toLocaleString('en-US'))
                    : item.activityQuantity.toLocaleString('en-US')}
                </div>
                <div className="text-[11px] font-semibold text-slate-500 mt-0.5">
                  {item.activityUnit}
                </div>
              </div>

              {/* Step 2 */}
              <div className="p-3 bg-white rounded-lg border border-teal-100 shadow-xs flex flex-col justify-between">
                <div className="text-[10px] font-bold text-teal-700 uppercase tracking-wider mb-1">
                  {isBangla ? '২. গুণক (Factor)' : '2. Emission Factor'}
                </div>
                <div className="font-mono font-black text-emerald-800 text-sm">
                  × {isBangla ? toBanglaDigits(item.factorValue.toString()) : item.factorValue}
                </div>
                <div className="text-[10px] text-slate-500 mt-0.5 truncate" title={`kgCO₂e / ${item.activityUnit}`}>
                  kgCO₂e/{item.activityUnit}
                </div>
              </div>

              {/* Step 3 */}
              <div className="p-3 bg-white rounded-lg border border-teal-100 shadow-xs flex flex-col justify-between">
                <div className="text-[10px] font-bold text-teal-700 uppercase tracking-wider mb-1">
                  {isBangla ? '৩. মোট কেজি (Gross kg)' : '3. Gross Carbon'}
                </div>
                <div className="font-mono font-black text-slate-900 text-sm">
                  = {isBangla
                    ? toBanglaDigits(grossKg.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 2 }))
                    : grossKg.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 2 })}
                </div>
                <div className="text-[10px] text-slate-500 mt-0.5">
                  kg CO₂e
                </div>
              </div>

              {/* Step 4 */}
              <div className="p-3 bg-emerald-50 rounded-lg border border-emerald-300 shadow-xs flex flex-col justify-between">
                <div className="text-[10px] font-bold text-emerald-800 uppercase tracking-wider mb-1">
                  {isBangla ? '৪. মেট্রিক টন (tCO₂e)' : '4. Metric Tonnes'}
                </div>
                <div className="font-mono font-black text-emerald-950 text-base">
                  {isBangla ? toBanglaDigits(netTco2e.toFixed(2)) : netTco2e.toFixed(2)}
                </div>
                <div className="text-[10px] font-bold text-emerald-700 mt-0.5">
                  tCO₂e (÷ 1,000)
                </div>
              </div>
            </div>

            {/* Full Formula String Badge */}
            <div className="p-2 bg-teal-100/60 rounded-md text-[11px] text-teal-900 font-mono text-center border border-teal-200">
              <span className="font-bold">{isBangla ? 'গণনা সংক্ষেপ:' : 'Equation: '}</span>{' '}
              {item.calculationFormula ||
                `${item.activityQuantity.toLocaleString()} ${item.activityUnit} × ${item.factorValue} kgCO₂e/${item.activityUnit} ÷ 1,000 = ${item.emissionsTco2e.toFixed(2)} tCO₂e`}
            </div>

            <p className="text-[11px] text-slate-600 leading-relaxed pt-1">
              <span className="font-semibold text-slate-700">{isBangla ? 'গণনা ব্যাখ্যা:' : 'Note: '}</span>
              {isBangla
                ? `জিএইচজি প্রোটোকল অনুযায়ী মোট ${item.activityUnit} ব্যবহারকে ফ্যাক্টর (${item.factorValue} kgCO₂e/${item.activityUnit}) দিয়ে গুণ করে মোট ${grossKg.toLocaleString('en-US', { maximumFractionDigits: 1 })} kg CO₂e পাওয়া গেছে, যা ১,০০০ দিয়ে ভাগ করে মেট্রিক টন ${netTco2e.toFixed(2)} tCO₂e হিসেবে নির্ধারণ করা হয়েছে।`
                : `Under GHG Protocol standards, ${item.activityQuantity.toLocaleString()} ${item.activityUnit} multiplied by the emission factor (${item.factorValue} kgCO₂e/${item.activityUnit}) yields ${grossKg.toLocaleString('en-US', { maximumFractionDigits: 1 })} gross kg CO₂e. Dividing by 1,000 converts this to ${netTco2e.toFixed(2)} metric tonnes of CO₂e.`}
            </p>
          </div>

          {/* 3. Applied Factor Source & Citation */}
          <div className="p-3.5 bg-emerald-50/50 border border-emerald-200/70 rounded-xl space-y-1.5">
            <div className="flex items-center justify-between">
              <span className="font-bold text-emerald-950 flex items-center gap-1.5">
                <Award size={14} className="text-emerald-700" />
                {isBangla ? 'অনুমোদিত নির্গমন ফ্যাক্টর ও রেফারেন্স:' : 'Emission Factor Citation & Authority:'}
              </span>
              <span className="font-mono font-bold text-emerald-900">
                {item.factorValue} kg CO₂e / {item.activityUnit}
              </span>
            </div>
            <p className="text-[11px] text-slate-600 leading-relaxed">
              <strong>{isBangla ? 'উৎস ও বছর: ' : 'Source & Year: '}</strong>
              {item.factorSource} ({item.factorCitationYear})
            </p>
          </div>

          {/* 4. Commercial BDT Amount (Price Redaction Enforcement) */}
          <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl flex items-center justify-between">
            <span className="font-semibold text-slate-600">
              {isBangla ? 'চালানের মোট আর্থিক পরিমাণ (Billed Amount):' : 'Billed Invoice Amount:'}
            </span>
            {redactPrices ? (
              <span className="font-bold text-rose-700 bg-rose-50 px-2 py-0.5 rounded border border-rose-200 flex items-center gap-1 font-mono text-[11px]">
                <Lock size={12} /> {isBangla ? '[গোপনীয় - বাণিজ্যিক নিরাপত্তা]' : '[Confidential - Redacted]'}
              </span>
            ) : (
              <span className="font-mono font-bold text-slate-900">
                {formatBdt(item.billedAmountBdt || 0, { useBanglaDigits: isBangla })}
              </span>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="pt-3 border-t border-slate-200 flex items-center justify-between text-xs">
          <span className="text-[11px] text-slate-500">
            {isBangla ? 'যাচাই সময়: ' : 'Confirmed: '}
            {new Date(item.confirmedAt).toLocaleDateString()} • {new Date(item.confirmedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
          </span>
          <button
            onClick={onClose}
            className="px-5 py-2 bg-teal-800 hover:bg-teal-900 text-white font-bold rounded-xl transition shadow-xs cursor-pointer"
          >
            {isBangla ? 'বন্ধ করুন' : 'Close Inspector'}
          </button>
        </div>
      </div>
    </div>
  );
};
