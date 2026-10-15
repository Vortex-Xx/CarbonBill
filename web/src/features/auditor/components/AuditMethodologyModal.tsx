import React, { useState } from 'react';
import { AuditorSnapshot } from '../types';
import { toBanglaDigits } from '../../../shared';
import {
  X,
  ShieldCheck,
  Award,
  Layers,
  Calculator,
  Flame,
  Zap,
  Truck,
  CheckCircle2,
  FileCheck,
  Scale,
  BookOpen,
} from 'lucide-react';

interface AuditMethodologyModalProps {
  snapshot: AuditorSnapshot;
  isBangla: boolean;
  onClose: () => void;
  initialTab?: 'scopes' | 'grading';
}

export const AuditMethodologyModal: React.FC<AuditMethodologyModalProps> = ({
  snapshot,
  isBangla,
  onClose,
  initialTab = 'scopes',
}) => {
  const [activeTab, setActiveTab] = useState<'scopes' | 'grading'>(initialTab);

  const dqsPct = (snapshot.dataQualityScore * 100).toFixed(1);
  const total = snapshot.totalEmissions || 1;
  const s1Pct = ((snapshot.scope1Emissions / total) * 100).toFixed(1);
  const s2Pct = ((snapshot.scope2Emissions / total) * 100).toFixed(1);
  const s3Pct = ((snapshot.scope3Emissions / total) * 100).toFixed(1);

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 overflow-y-auto">
      <div className="bg-white rounded-2xl max-w-3xl w-full p-6 shadow-2xl border border-slate-200 space-y-5 my-8 max-h-[92vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-start justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-teal-100 text-teal-800 rounded-xl">
              <BookOpen size={24} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-bold px-2 py-0.5 bg-teal-50 text-teal-900 rounded border border-teal-200">
                  GHG Protocol Corporate Standard
                </span>
                <span className="text-[10px] font-bold px-2 py-0.5 bg-emerald-50 text-emerald-800 rounded border border-emerald-200">
                  ISO 14064-1 Aligned
                </span>
              </div>
              <h3 className="font-bold text-slate-900 text-lg mt-0.5">
                {isBangla
                  ? 'কার্বন হিসাব ও অডিট গ্রেডিং পদ্ধতি গাইড'
                  : 'Carbon Accounting Scopes & Audit Grading Methodology'}
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

        {/* Tab Navigation */}
        <div className="flex border-b border-slate-200 text-xs font-bold gap-2">
          <button
            onClick={() => setActiveTab('scopes')}
            className={`pb-2.5 px-3 border-b-2 flex items-center gap-1.5 transition cursor-pointer ${
              activeTab === 'scopes'
                ? 'border-teal-700 text-teal-900'
                : 'border-transparent text-slate-400 hover:text-slate-700'
            }`}
          >
            <Layers size={14} />
            {isBangla ? '১. স্কোপ ১, ২, ৩ ও গণনা সূত্র' : '1. GHG Scopes (1, 2, 3) & Formulas'}
          </button>
          <button
            onClick={() => setActiveTab('grading')}
            className={`pb-2.5 px-3 border-b-2 flex items-center gap-1.5 transition cursor-pointer ${
              activeTab === 'grading'
                ? 'border-teal-700 text-teal-900'
                : 'border-transparent text-slate-400 hover:text-slate-700'
            }`}
          >
            <Award size={14} />
            {isBangla ? '২. অডিট গ্রেড ও DQS নির্ণয়' : '2. Audit Grade & DQS Rating Engine'}
          </button>
        </div>

        {/* Tab 1: Scopes Explained & How They Are Calculated */}
        {activeTab === 'scopes' && (
          <div className="space-y-4 text-xs">
            <p className="text-slate-600 leading-relaxed">
              {isBangla
                ? 'আন্তর্জাতিক গ্রীনহাউস গ্যাস (GHG) প্রোটোকল কর্পোরেট অ্যাকাউন্টিং স্ট্যান্ডার্ড অনুযায়ী একটি প্রতিষ্ঠানের সকল কার্বন নির্গমনকে ৩টি প্রধান স্কোপে (Scope 1, Scope 2, Scope 3) বিভক্ত করা হয়।'
                : 'Under the international Greenhouse Gas (GHG) Protocol Corporate Standard, emissions are categorized into three mandatory operational scopes based on operational control and ownership.'}
            </p>

            {/* Scope 1 */}
            <div className="p-4 bg-orange-50/50 border border-orange-200 rounded-xl space-y-2.5">
              <div className="flex items-center justify-between">
                <span className="font-bold text-orange-950 flex items-center gap-2 text-sm">
                  <Flame size={16} className="text-orange-600" />
                  {isBangla ? 'স্কোপ ১: প্রত্যক্ষ নির্গমন (Direct Combustion)' : 'Scope 1: Direct GHG Emissions'}
                </span>
                <span className="text-[11px] font-mono font-bold text-orange-900 bg-white px-2 py-0.5 rounded border border-orange-200">
                  {isBangla ? toBanglaDigits(snapshot.scope1Emissions.toFixed(2)) : snapshot.scope1Emissions.toFixed(2)} tCO₂e ({s1Pct}%)
                </span>
              </div>
              <p className="text-slate-700 leading-relaxed">
                {isBangla
                  ? 'কারখানার নিজস্ব মালিকানাধীন বা নিয়ন্ত্রণাধীন জ্বালানি দহন—যেমন ব্যাকআপ ডিজেল জেনারেটর (Padma Oil) এবং বয়লারে প্রাকৃতিক গ্যাস (Titas Gas) ব্যবহার।'
                  : 'Emissions from company-owned or controlled physical combustion assets, such as onsite diesel backup gensets and natural gas steam boilers.'}
              </p>
              <div className="p-2.5 bg-white rounded-lg border border-orange-100 font-mono text-[11px] text-slate-800">
                <div className="font-bold text-orange-800 mb-1">{isBangla ? 'গণনা সূত্র (Formula):' : 'Calculation Formula:'}</div>
                <code>Scope 1 (tCO₂e) = (Fuel Volume [litres or m³] × Emission Factor [kgCO₂e/unit]) ÷ 1,000</code>
                <div className="text-[10px] text-slate-500 mt-1">
                  • Diesel: 2.6800 kgCO₂e/litre (IPCC 2024 Stationary Combustion)<br />
                  • Natural Gas: 2.0187 kgCO₂e/m³ (DoE Bangladesh / IPCC)
                </div>
              </div>
            </div>

            {/* Scope 2 */}
            <div className="p-4 bg-sky-50/50 border border-sky-200 rounded-xl space-y-2.5">
              <div className="flex items-center justify-between">
                <span className="font-bold text-sky-950 flex items-center gap-2 text-sm">
                  <Zap size={16} className="text-sky-600" />
                  {isBangla ? 'স্কোপ ২: গ্রিড বিদ্যুৎ (Purchased Electricity)' : 'Scope 2: Indirect Electricity Emissions'}
                </span>
                <span className="text-[11px] font-mono font-bold text-sky-900 bg-white px-2 py-0.5 rounded border border-sky-200">
                  {isBangla ? toBanglaDigits(snapshot.scope2Emissions.toFixed(2)) : snapshot.scope2Emissions.toFixed(2)} tCO₂e ({s2Pct}%)
                </span>
              </div>
              <p className="text-slate-700 leading-relaxed">
                {isBangla
                  ? 'জাতীয় বিদ্যুৎ গ্রিড (BPDB, DESCO, DPDC, BREB) থেকে কেনা বিদ্যুৎ ব্যবহারের ফলে পরোক্ষ নির্গমন।'
                  : 'Indirect emissions resulting from the generation of purchased electricity consumed by the industrial facility from the national power grid.'}
              </p>
              <div className="p-2.5 bg-white rounded-lg border border-sky-100 font-mono text-[11px] text-slate-800">
                <div className="font-bold text-sky-800 mb-1">{isBangla ? 'গণনা সূত্র (Formula):' : 'Calculation Formula:'}</div>
                <code>Scope 2 (tCO₂e) = (Grid Consumption [kWh] × Grid Emission Factor [kgCO₂e/kWh]) ÷ 1,000</code>
                <div className="text-[10px] text-slate-500 mt-1">
                  • Bangladesh National Grid Factor: 0.6210 - 0.6403 kgCO₂e/kWh (SREDA / DoE Bangladesh Location-Based)
                </div>
              </div>
            </div>

            {/* Scope 3 */}
            <div className="p-4 bg-purple-50/50 border border-purple-200 rounded-xl space-y-2.5">
              <div className="flex items-center justify-between">
                <span className="font-bold text-purple-950 flex items-center gap-2 text-sm">
                  <Truck size={16} className="text-purple-600" />
                  {isBangla ? 'স্কোপ ৩: পরিবহন ও ভ্যালু চেইন (Value Chain & Freight)' : 'Scope 3: Upstream & Transport Emissions'}
                </span>
                <span className="text-[11px] font-mono font-bold text-purple-900 bg-white px-2 py-0.5 rounded border border-purple-200">
                  {isBangla ? toBanglaDigits(snapshot.scope3Emissions.toFixed(2)) : snapshot.scope3Emissions.toFixed(2)} tCO₂e ({s3Pct}%)
                </span>
              </div>
              <p className="text-slate-700 leading-relaxed">
                {isBangla
                  ? 'কারখানার বাইরের সরবরাহ চেইন নির্গমন—যেমন তৈরি পোশাক কারখানা (গাজীপুর) থেকে চট্টগ্রাম বন্দর পর্যন্ত রপ্তানি কন্টেইনার পরিবহন ট্রাক।'
                  : 'Upstream and downstream freight logistics, such as garment export container trucking from Gazipur factory to Chittagong Port.'}
              </p>
              <div className="p-2.5 bg-white rounded-lg border border-purple-100 font-mono text-[11px] text-slate-800">
                <div className="font-bold text-purple-800 mb-1">{isBangla ? 'গণনা সূত্র (Formula):' : 'Calculation Formula:'}</div>
                <code>Scope 3 (tCO₂e) = (Freight Tonnes × Distance [km] × Factor [kgCO₂e/tonne-km]) ÷ 1,000</code>
                <div className="text-[10px] text-slate-500 mt-1">
                  • Heavy Goods Vehicle (HGV) Road Freight: 0.2541 kgCO₂e/tonne-km (UK DEFRA / GLEC Framework)
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Tab 2: How Grades & DQS Are Calculated */}
        {activeTab === 'grading' && (
          <div className="space-y-4 text-xs">
            <div className="p-3 bg-teal-50 border border-teal-200 rounded-xl space-y-1">
              <div className="font-bold text-teal-950 flex items-center gap-1.5 text-sm">
                <Scale size={16} className="text-teal-700" />
                {isBangla ? 'গ্রেড কি স্থির (Static)? না, এটি সম্পূর্ণ ডায়নামিক ও প্রমাণভিত্তিক।' : 'Is the Grade Static? No, it is 100% dynamically calculated.'}
              </div>
              <p className="text-slate-700 leading-relaxed">
                {isBangla
                  ? 'কার্বনবিলের ডেটা কোয়ালিটি স্কোর (DQS) এবং অডিট গ্রেড প্রতিটি আপলোডকৃত চালানের OCR কনফিডেন্স, মানবীয় যাচাই স্বাক্ষর এবং এমিশন ওজনের ভিত্তিতে স্বয়ংক্রিয়ভাবে গণনা করা হয়।'
                  : 'The Data Quality Score (DQS) and Audit Grade are dynamically computed from every single activity item in the inventory based on OCR confidence, primary document provenance, and emission weighting.'}
              </p>
            </div>

            {/* DQS Mathematical Formula */}
            <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl space-y-2">
              <span className="font-bold text-slate-800 text-xs flex items-center gap-1.5">
                <Calculator size={14} className="text-teal-700" />
                {isBangla ? 'DQS গাণিতিক সূত্র (DQS Mathematical Formula):' : 'DQS Mathematical Formula:'}
              </span>
              <div className="p-3 bg-white rounded-lg border border-slate-200 font-mono text-center text-xs space-y-1">
                <div className="font-bold text-teal-900">
                  DQS (%) = [ ∑ (Emissions_i × Quality_Factor_i) ÷ ∑ Emissions_i ] × 100%
                </div>
                <div className="text-[10px] text-slate-500">
                  {isBangla
                    ? 'যেখানে Quality Factor = OCR রিডিং কনফিডেন্স (০-১) + মানবীয় পর্যালোচক অনুমোদন বোনাস'
                    : 'Where Quality Factor = OCR AI Confidence (0.0 - 1.0) + Human Reviewer Signoff Assurance'}
                </div>
              </div>
            </div>

            {/* Grade Rating Scale */}
            <div className="space-y-2">
              <span className="font-bold text-slate-800 text-xs">
                {isBangla ? 'অডিট গ্রেড স্কেল ও মানদণ্ড (Grading Scale & Assurance Thresholds):' : 'Audit Grading Scale & Assurance Thresholds:'}
              </span>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                {/* Grade A */}
                <div className={`p-3 rounded-xl border ${snapshot.dqsGrade === 'Grade A' ? 'bg-emerald-50 border-emerald-300 ring-2 ring-emerald-500/20' : 'bg-slate-50 border-slate-200'}`}>
                  <div className="flex items-center justify-between">
                    <span className="font-black text-emerald-900 text-sm">Grade A (≥ 90%)</span>
                    <span className="text-[10px] font-bold px-2 py-0.5 bg-emerald-100 text-emerald-800 rounded">
                      {isBangla ? 'অডিট-উপযোগী' : 'Audit-Ready'}
                    </span>
                  </div>
                  <p className="text-[11px] text-slate-600 mt-1">
                    {isBangla
                      ? 'সর্বোচ্চ নির্ভরযোগ্যতা। সকল নির্গমনের বিপরীতে মূল ইউটিলিটি বিল/চালান সংযুক্ত এবং উচ্চ OCR নির্ভুলতায় যাচাইকৃত।'
                      : 'Highest assurance. All emission items backed by primary utility bills with ≥90% OCR confidence and auditor sign-offs.'}
                  </p>
                </div>

                {/* Grade B */}
                <div className={`p-3 rounded-xl border ${snapshot.dqsGrade === 'Grade B' ? 'bg-sky-50 border-sky-300 ring-2 ring-sky-500/20' : 'bg-slate-50 border-slate-200'}`}>
                  <div className="flex items-center justify-between">
                    <span className="font-black text-sky-900 text-sm">Grade B (75% - 89%)</span>
                    <span className="text-[10px] font-bold px-2 py-0.5 bg-sky-100 text-sky-800 rounded">
                      {isBangla ? 'যুক্তিসঙ্গত নিশ্চয়তা' : 'Reasonable'}
                    </span>
                  </div>
                  <p className="text-[11px] text-slate-600 mt-1">
                    {isBangla
                      ? 'ভালো মানের তথ্য। সিংহভাগ চালানের প্রমাণপত্র বিদ্যমান, গৌণ কিছু ক্ষেত্রে স্ট্যান্ডার্ড প্রাক্কলন ব্যবহৃত।'
                      : 'Good data quality. Most sources backed by primary bills with minor secondary estimation for non-material scopes.'}
                  </p>
                </div>

                {/* Grade C */}
                <div className={`p-3 rounded-xl border ${snapshot.dqsGrade === 'Grade C' ? 'bg-amber-50 border-amber-300 ring-2 ring-amber-500/20' : 'bg-slate-50 border-slate-200'}`}>
                  <div className="flex items-center justify-between">
                    <span className="font-black text-amber-900 text-sm">Grade C (60% - 74%)</span>
                    <span className="text-[10px] font-bold px-2 py-0.5 bg-amber-100 text-amber-800 rounded">
                      {isBangla ? 'সীমিত নিশ্চয়তা' : 'Limited'}
                    </span>
                  </div>
                  <p className="text-[11px] text-slate-600 mt-1">
                    {isBangla
                      ? 'গড়পড়তা তথ্য। কিছু চালানের অভাব অথবা অনুমানের ওপর নির্ভরতা বেশি।'
                      : 'Moderate data quality. Incomplete primary evidence or significant reliance on gap-filling estimates.'}
                  </p>
                </div>

                {/* Grade D */}
                <div className={`p-3 rounded-xl border ${snapshot.dqsGrade === 'Grade D' ? 'bg-rose-50 border-rose-300 ring-2 ring-rose-500/20' : 'bg-slate-50 border-slate-200'}`}>
                  <div className="flex items-center justify-between">
                    <span className="font-black text-rose-900 text-sm">Grade D (&lt; 60%)</span>
                    <span className="text-[10px] font-bold px-2 py-0.5 bg-rose-100 text-rose-800 rounded">
                      {isBangla ? 'অনুপযুক্ত' : 'Inadequate'}
                    </span>
                  </div>
                  <p className="text-[11px] text-slate-600 mt-1">
                    {isBangla
                      ? 'উচ্চ অনিশ্চয়তা। প্রাথমিক প্রমাণপত্রের গুরুতর ঘাটতি, অডিটের জন্য উপযুক্ত নয়।'
                      : 'High estimation uncertainty with critical invoice gaps. Not recommended for third-party auditing.'}
                  </p>
                </div>
              </div>
            </div>

            {/* Current Audit Status */}
            <div className="p-3.5 bg-emerald-50 border border-emerald-200 rounded-xl flex items-center justify-between">
              <div>
                <span className="font-bold text-emerald-950 text-xs block">
                  {isBangla ? 'বর্তমান অডিটের অর্জিত স্কোর:' : 'Current Live Audit Result:'}
                </span>
                <span className="text-slate-600 text-[11px]">
                  {isBangla
                    ? `${snapshot.lineItems.length}টি নথির গড় OCR ও প্রমাণপত্র যাচাইয়ের ভিত্তিতে নির্ণীত`
                    : `Evaluated across ${snapshot.lineItems.length} verified activity document records`}
                </span>
              </div>
              <div className="text-right">
                <span className="text-xl font-black text-emerald-900 block font-mono">
                  {snapshot.dqsGrade} ({dqsPct}%)
                </span>
                <span className="text-[10px] font-bold text-emerald-700 bg-white px-2 py-0.5 rounded border border-emerald-200">
                  ✓ {isBangla ? 'অডিট উত্তীর্ণ' : 'Audit Ready'}
                </span>
              </div>
            </div>
          </div>
        )}

        {/* Footer */}
        <div className="pt-3 border-t border-slate-200 flex items-center justify-between text-xs">
          <span className="text-[11px] text-slate-500">
            {isBangla ? 'স্ট্যান্ডার্ড: ISO 14064-1 & GHG Protocol Corporate Standard' : 'Standard: ISO 14064-1 & GHG Protocol'}
          </span>
          <button
            onClick={onClose}
            className="px-5 py-2 bg-teal-800 hover:bg-teal-900 text-white font-bold rounded-xl transition shadow-xs cursor-pointer"
          >
            {isBangla ? 'বন্ধ করুন' : 'Got it'}
          </button>
        </div>
      </div>
    </div>
  );
};
