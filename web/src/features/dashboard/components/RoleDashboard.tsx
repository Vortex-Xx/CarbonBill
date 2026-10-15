import React from 'react';
import { RoleType, DashboardSummary, ClientOrgSummary } from '../types';
import { toBanglaDigits, formatBdt } from '../../../shared';
import {
  TrendingDown,
  AlertCircle,
  FileCheck2,
  FileQuestion,
  Users,
  Building2,
  CheckCircle2,
  ArrowRight,
  ShieldCheck,
  Zap,
} from 'lucide-react';

interface RoleDashboardProps {
  role: RoleType;
  summary: DashboardSummary;
  clientOrgs: ClientOrgSummary[];
  isBangla: boolean;
  onNavigateTab: (tab: 'dashboard' | 'flags' | 'recommendations' | 'reports' | 'auditor') => void;
  onGoToReview?: () => void;
  onSelectClientOrg?: (orgId: string) => void;
}

export const RoleDashboard: React.FC<RoleDashboardProps> = ({
  role,
  summary,
  clientOrgs,
  isBangla,
  onNavigateTab,
  onGoToReview,
  onSelectClientOrg,
}) => {
  // 1. OWNER DASHBOARD
  if (role === 'Owner') {
    return (
      <div className="space-y-6">
        {/* Top Highlight Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-gradient-to-br from-teal-900 to-slate-900 text-white p-5 rounded-2xl shadow-sm space-y-2">
            <span className="text-xs font-semibold text-teal-200">
              {isBangla ? 'সেপ্টেম্বর ২০২৬ মোট কার্বন পদচিহ্ন' : 'Total Carbon Footprint (Sep 2026)'}
            </span>
            <div className="flex items-baseline justify-between">
              <span className="text-3xl font-black">
                {isBangla ? toBanglaDigits((summary?.totalEmissions ?? 0).toFixed(2)) : (summary?.totalEmissions ?? 0).toFixed(2)}
              </span>
              <span className="text-sm font-semibold text-teal-300">tCO₂e</span>
            </div>
            <div className="text-[11px] text-teal-300 flex items-center gap-1 pt-1">
              <span>📉 {isBangla ? 'গত কোয়ার্টারের চেয়ে ৩.২% কম' : '3.2% lower than Q2'}</span>
            </div>
          </div>

          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-2">
            <span className="text-xs font-semibold text-slate-500">
              {isBangla ? 'ডেটা কোয়ালিটি স্কোর (DQS)' : 'Data Quality Score'}
            </span>
            <div className="flex items-baseline justify-between">
              <span className="text-3xl font-black text-slate-900">
                {isBangla ? toBanglaDigits(((summary?.dataQualityScore ?? 0) * 100).toFixed(0)) : ((summary?.dataQualityScore ?? 0) * 100).toFixed(0)}%
              </span>
              <span className="text-xs font-bold px-2 py-0.5 bg-emerald-100 text-emerald-800 rounded-md">
                {summary?.dqsGrade || 'Grade A'}
              </span>
            </div>
            <div className="text-[11px] text-emerald-700 font-medium pt-1 flex items-center gap-1">
              <ShieldCheck size={14} />
              {isBangla ? 'ক্রেতা অডিট ও কমপ্লায়েন্সের জন্য অনুমোদিত' : 'Approved for buyer audit disclosure'}
            </div>
          </div>

          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-2">
            <span className="text-xs font-semibold text-slate-500">
              {isBangla ? 'টাকায় মোট সম্ভাব্য বার্ষিক সাশ্রয়' : 'Potential Annual Savings in BDT'}
            </span>
            <div className="flex items-baseline justify-between">
              <span className="text-2xl font-black text-teal-700">
                {formatBdt(265000, { useBanglaDigits: isBangla })}
              </span>
              <span className="text-[11px] text-slate-400">/ {isBangla ? 'বছর' : 'year'}</span>
            </div>
            <button
              onClick={() => onNavigateTab('recommendations')}
              className="text-[11px] text-teal-800 font-bold hover:underline flex items-center gap-1 pt-1"
            >
              {isBangla ? 'সাশ্রয়ী পদক্ষেপ দেখুন' : 'Explore Action Plans'} <ArrowRight size={12} />
            </button>
          </div>
        </div>

        {/* Owner Split: Top 3 Flags & Top 3 BDT Actions */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Top 3 Flags */}
          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-3">
            <div className="flex items-center justify-between border-b pb-3">
              <div className="flex items-center gap-2">
                <AlertCircle className="text-amber-600" size={18} />
                <h3 className="font-bold text-slate-900 text-sm md:text-base">
                  {isBangla ? 'শীর্ষ ৩টি জরুরি সতর্কতা' : 'Top 3 Attention Notices'}
                </h3>
              </div>
              <button
                onClick={() => onNavigateTab('flags')}
                className="text-xs font-bold text-teal-700 hover:underline"
              >
                {isBangla ? 'সবগুলো দেখুন' : 'View All'} ({isBangla ? toBanglaDigits(summary.activeFlagsCount) : summary.activeFlagsCount})
              </button>
            </div>

            <div className="space-y-2">
              <div className="p-3 bg-rose-50 border border-rose-200 rounded-xl text-xs space-y-1">
                <div className="flex items-center justify-between font-bold text-rose-900">
                  <span>{isBangla ? 'ডিজেল ব্যবহার স্পাইক: ১.৪৫ গুণ বৃদ্ধি' : 'Diesel Fuel Surge: 1.45x increase'}</span>
                  <span className="px-1.5 py-0.5 bg-rose-200 text-rose-900 rounded text-[10px]">Critical</span>
                </div>
                <p className="text-slate-600">
                  {isBangla
                    ? 'জেনারেটর ১-এ আগস্ট মাসের তুলনায় ডিজেল খরচ ৪৫% বেশি হয়েছে।'
                    : 'Generator 1 consumed 45% more diesel than trailing median.'}
                </p>
              </div>

              <div className="p-3 bg-amber-50 border border-amber-200 rounded-xl text-xs space-y-1">
                <div className="flex items-center justify-between font-bold text-amber-900">
                  <span>{isBangla ? 'অনুপস্থিত ডিজেল স্লিপ (২টি বকেয়া)' : 'Missing Diesel Slips (2 pending)'}</span>
                  <span className="px-1.5 py-0.5 bg-amber-200 text-amber-900 rounded text-[10px]">Warning</span>
                </div>
                <p className="text-slate-600">
                  {isBangla
                    ? 'সেপ্টেম্বর মাসের বিল হিসাব চূড়ান্ত করতে ২টি চালান এখনও জমা পড়েনি।'
                    : '2 slips pending from floor operator for full reconciliation.'}
                </p>
              </div>

              <div className="p-3 bg-blue-50 border border-blue-200 rounded-xl text-xs space-y-1">
                <div className="flex items-center justify-between font-bold text-blue-900">
                  <span>{isBangla ? 'পাওয়ার ফ্যাক্টর সারচার্জ পেনাল্টি' : 'Low Power Factor Surcharge'}</span>
                  <span className="px-1.5 py-0.5 bg-blue-200 text-blue-900 rounded text-[10px]">Info</span>
                </div>
                <p className="text-slate-600">
                  {isBangla
                    ? 'ডেসকো বিলে ৩,৪৫০ টাকা অতিরিক্ত জরিমানা কাটা হয়েছে।'
                    : 'DESCO electricity invoice incurred BDT 3,450 penalty surcharge.'}
                </p>
              </div>
            </div>
          </div>

          {/* Top 3 Actions in BDT */}
          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-3">
            <div className="flex items-center justify-between border-b pb-3">
              <div className="flex items-center gap-2">
                <TrendingDown className="text-teal-700" size={18} />
                <h3 className="font-bold text-slate-900 text-sm md:text-base">
                  {isBangla ? 'টাকায় শীর্ষ ৩টি সাশ্রয়ী পদক্ষেপ' : 'Top 3 Interventions in BDT'}
                </h3>
              </div>
              <button
                onClick={() => onNavigateTab('recommendations')}
                className="text-xs font-bold text-teal-700 hover:underline"
              >
                {isBangla ? 'মেজার লাইব্রেরি' : 'Measure Library'}
              </button>
            </div>

            <div className="space-y-2">
              <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl text-xs space-y-1">
                <div className="flex items-center justify-between">
                  <span className="font-bold text-slate-900">
                    {isBangla ? 'পাওয়ার ফ্যাক্টর ক্যাপাসিটর ব্যাংক মেরামত' : 'APFC Capacitor Bank Refurbishment'}
                  </span>
                  <span className="text-emerald-700 font-black">
                    {formatBdt(120000, { useBanglaDigits: isBangla })} / {isBangla ? 'বছর' : 'yr'}
                  </span>
                </div>
                <p className="text-slate-500">
                  {isBangla ? 'পে-ব্যাক: ৩ – ৫ মাস | বিনিয়োগ: ৳ ৩৫,০০০ – ৳ ৫০,০০০' : 'Payback: 3-5 mos | Capex: BDT 35k-50k'}
                </p>
              </div>

              <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl text-xs space-y-1">
                <div className="flex items-center justify-between">
                  <span className="font-bold text-slate-900">
                    {isBangla ? 'কম্প্রেসড এয়ার পাইপলাইন লিক অডিট' : 'Compressed Air Ultrasonic Leak Sealing'}
                  </span>
                  <span className="text-emerald-700 font-black">
                    {formatBdt(85000, { useBanglaDigits: isBangla })} / {isBangla ? 'বছর' : 'yr'}
                  </span>
                </div>
                <p className="text-slate-500">
                  {isBangla ? 'পে-ব্যাক: ২ – ৪ সপ্তাহ | বিনিয়োগ: ৳ ১২,০০০' : 'Payback: 2-4 wks | Capex: BDT 12k'}
                </p>
              </div>

              <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl text-xs space-y-1">
                <div className="flex items-center justify-between">
                  <span className="font-bold text-slate-900">
                    {isBangla ? 'বয়লার ব্লো-ডাউন তাপ পুনরুদ্ধার ইউনিট' : 'Boiler Blowdown Heat Exchanger'}
                  </span>
                  <span className="text-emerald-700 font-black">
                    {formatBdt(60000, { useBanglaDigits: isBangla })} / {isBangla ? 'বছর' : 'yr'}
                  </span>
                </div>
                <p className="text-slate-500">
                  {isBangla ? 'পে-ব্যাক: ৮ – ১২ মাস | বিনিয়োগ: ৳ ৬০,০০০' : 'Payback: 8-12 mos | Capex: BDT 60k'}
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // 2. ACCOUNTANT DASHBOARD
  if (role === 'Accountant') {
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-indigo-50 border border-indigo-200 p-5 rounded-2xl shadow-sm space-y-2">
            <span className="text-xs font-semibold text-indigo-900">
              {isBangla ? 'পর্যালোচনা কিউতে পেন্ডিং নথি' : 'Pending Review Queue Documents'}
            </span>
            <div className="flex items-baseline justify-between">
              <span className="text-3xl font-black text-indigo-950">
                {isBangla ? toBanglaDigits(4) : '4'}
              </span>
              <span className="text-xs font-bold text-indigo-700">
                {isBangla ? 'যাচাই প্রয়োজন' : 'Requires verification'}
              </span>
            </div>
            {onGoToReview && (
              <button
                onClick={onGoToReview}
                className="text-xs font-bold text-indigo-800 bg-white px-3 py-1.5 rounded-lg border border-indigo-200 hover:bg-indigo-50 inline-flex items-center gap-1 mt-2"
              >
                {isBangla ? 'রিভিউ কিউ খুলুন' : 'Open Review Queue'} <ArrowRight size={14} />
              </button>
            )}
          </div>

          <div className="bg-amber-50 border border-amber-200 p-5 rounded-2xl shadow-sm space-y-2">
            <span className="text-xs font-semibold text-amber-900">
              {isBangla ? 'অনুপস্থিত বিল ও রসিদ (Gap Detection)' : 'Missing Bills & Slips (Gaps)'}
            </span>
            <div className="flex items-baseline justify-between">
              <span className="text-3xl font-black text-amber-950">
                {isBangla ? toBanglaDigits(summary.missingDocsCount) : summary.missingDocsCount}
              </span>
              <span className="text-xs font-bold text-amber-800">
                {isBangla ? 'তাগিদ পাঠানো হয়েছে' : 'Escalations sent'}
              </span>
            </div>
            <p className="text-[11px] text-amber-800 pt-1">
              {isBangla ? 'ফ্লোর স্টাফ জাহিদকে ডিজেল স্লিপের তাগিদ পাঠানো হয়েছে।' : 'Nudge sent to floor operator Jahid.'}
            </p>
          </div>

          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-2">
            <span className="text-xs font-semibold text-slate-500">
              {isBangla ? 'সেপ্টেম্বর মাসের বিল মিলকরণ (Reconciliation)' : 'Invoice Reconciliation'}
            </span>
            <div className="flex items-baseline justify-between">
              <span className="text-3xl font-black text-slate-900">
                {isBangla ? toBanglaDigits('৯৪.৮%') : '94.8%'}
              </span>
              <span className="text-xs font-semibold text-emerald-700">✓ Audited</span>
            </div>
            <p className="text-[11px] text-slate-500 pt-1">
              {isBangla ? 'অ্যাকাউন্টিং লেজারের সাথে চালান যাচাইকৃত।' : 'General Ledger reconciled with OCR bills.'}
            </p>
          </div>
        </div>

        {/* Missing Expected Documents Checklist */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-3">
          <div className="flex items-center justify-between border-b pb-3">
            <div className="flex items-center gap-2">
              <FileQuestion className="text-amber-600" size={18} />
              <h3 className="font-bold text-slate-900 text-sm md:text-base">
                {isBangla ? 'প্রত্যাশিত নথিপত্র চেকলিস্ট (Expected Document Calendar)' : 'Expected Document Calendar'}
              </h3>
            </div>
            <span className="text-xs text-slate-400">
              {isBangla ? 'আইনি ও কমপ্লায়েন্স ক্যালেন্ডার' : 'Statutory & Compliance Cycle'}
            </span>
          </div>

          <div className="divide-y divide-slate-100 text-xs">
            <div className="py-2.5 flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <CheckCircle2 size={16} className="text-emerald-600" />
                <div>
                  <p className="font-bold text-slate-800">
                    {isBangla ? 'ডেসকো (DESCO) গ্রিড বিদ্যুৎ বিল — সেপ্টেম্বর ২০২৬' : 'DESCO Grid Electricity Bill — Sep 2026'}
                  </p>
                  <p className="text-[11px] text-slate-500">{isBangla ? 'মিটার নং: #E-8821 | পরিমাণ: ৪২,৪০০ kWh' : 'Meter #E-8821 | 42,400 kWh'}</p>
                </div>
              </div>
              <span className="px-2 py-0.5 bg-emerald-100 text-emerald-800 font-semibold rounded">
                {isBangla ? 'যাচাইকৃত' : 'Confirmed'}
              </span>
            </div>

            <div className="py-2.5 flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <AlertCircle size={16} className="text-amber-600" />
                <div>
                  <p className="font-bold text-slate-800">
                    {isBangla ? 'জেনারেটর ২ — ডিজেল সরবরাহ স্লিপ (বকেয়া)' : 'Generator 2 — Diesel Slip (Pending)'}
                  </p>
                  <p className="text-[11px] text-slate-500">{isBangla ? 'প্রত্যাশিত সময়সীমা: ০৩ অক্টোবর (Day 0 Escalation)' : 'Due: Oct 3 (Day 0 Escalation)'}</p>
                </div>
              </div>
              <span className="px-2 py-0.5 bg-amber-100 text-amber-800 font-semibold rounded">
                {isBangla ? 'অনুপস্থিত' : 'Missing'}
              </span>
            </div>

            <div className="py-2.5 flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <CheckCircle2 size={16} className="text-emerald-600" />
                <div>
                  <p className="font-bold text-slate-800">
                    {isBangla ? 'তিতাস গ্যাস ট্রান্সমিশন বিল — সেপ্টেম্বর ২০২৬' : 'Titas Gas Transmission Bill — Sep 2026'}
                  </p>
                  <p className="text-[11px] text-slate-500">{isBangla ? 'বয়লার সংযোগ | পরিমাণ: ৫,২০০ m³' : 'Boiler connection | 5,200 m³'}</p>
                </div>
              </div>
              <span className="px-2 py-0.5 bg-emerald-100 text-emerald-800 font-semibold rounded">
                {isBangla ? 'যাচাইকৃত' : 'Confirmed'}
              </span>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // 3. COMPLIANCE DASHBOARD
  if (role === 'Compliance') {
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-teal-900 text-white p-5 rounded-2xl shadow-sm space-y-2">
            <span className="text-xs font-semibold text-teal-200">
              {isBangla ? 'রিপোর্ট প্রস্তুতি সূচক (Readiness Index)' : 'Report Readiness Index'}
            </span>
            <div className="flex items-baseline justify-between">
              <span className="text-3xl font-black">
                {isBangla ? toBanglaDigits('৮৮%') : '88%'}
              </span>
              <span className="text-xs font-bold text-teal-300">
                {isBangla ? 'অনুমোদনের উপযোগী' : 'Ready for review'}
              </span>
            </div>
            <button
              onClick={() => onNavigateTab('reports')}
              className="text-xs font-bold text-teal-200 underline flex items-center gap-1 pt-1"
            >
              {isBangla ? 'প্রতিবেদন প্রিভিউ ও স্বাক্ষর' : 'Preview & Sign Report'} <ArrowRight size={12} />
            </button>
          </div>

          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-2">
            <span className="text-xs font-semibold text-slate-500">
              {isBangla ? 'ডেটা কোয়ালিটি গ্রেড (DQS)' : 'Data Quality Score Grade'}
            </span>
            <div className="flex items-baseline justify-between">
              <span className="text-3xl font-black text-slate-900">
                {summary.dqsGrade}
              </span>
              <span className="text-xs font-mono text-slate-500">
                {isBangla ? toBanglaDigits((summary?.dataQualityScore ?? 0).toFixed(3)) : (summary?.dataQualityScore ?? 0).toFixed(3)}
              </span>
            </div>
            <p className="text-[11px] text-emerald-700 font-medium pt-1">
              ✓ {isBangla ? 'Higg FEM ও Inditex অডিট স্ট্যান্ডার্ড পূরণ' : 'Meets Higg FEM & Inditex verification standard'}
            </p>
          </div>

          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-2">
            <span className="text-xs font-semibold text-slate-500">
              {isBangla ? 'নির্গমন ফ্যাক্টর সংস্করণ (Factor Basis)' : 'Emission Factor Basis'}
            </span>
            <div className="flex items-baseline justify-between">
              <span className="text-2xl font-black text-slate-900">IPCC AR5</span>
              <span className="text-xs font-bold text-teal-800 bg-teal-50 px-2 py-0.5 rounded border border-teal-200">
                2025/2026 Grid
              </span>
            </div>
            <p className="text-[11px] text-slate-500 pt-1">
              {isBangla ? 'জাতীয় গ্রিড ও জ্বালানি ফ্যাক্টর হালনাগাদ।' : 'National grid & fuel factor registry up-to-date.'}
            </p>
          </div>
        </div>

        {/* Readiness Checklist Card */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-3">
          <div className="flex items-center justify-between border-b pb-3">
            <div className="flex items-center gap-2">
              <FileCheck2 className="text-teal-700" size={18} />
              <h3 className="font-bold text-slate-900 text-sm md:text-base">
                {isBangla ? 'ক্রেতা অডিট চেকলিস্ট (Audit Preparedness Check)' : 'Buyer Audit Preparedness'}
              </h3>
            </div>
            <span className="text-xs text-slate-500">GHG Protocol Corporate Standard</span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl space-y-1">
              <span className="font-bold text-emerald-950">✓ {isBangla ? 'সাংগঠনিক সীমানা নির্ধারণ' : 'Organizational Boundary'}</span>
              <p className="text-slate-600">{isBangla ? 'অপারেশনাল কন্ট্রোল (১০০% কারখানা ভবন ও সাবস্টেশন)' : 'Operational Control approach (100% factory facility)'}</p>
            </div>

            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl space-y-1">
              <span className="font-bold text-emerald-950">✓ {isBangla ? 'ক্যালকুলেশন মেথডোলজি' : 'Calculation Methodology'}</span>
              <p className="text-slate-600">{isBangla ? 'লোকেশন-বেসড গ্রিড হিসাব এবং প্রমাণপত্র ট্র্যাকিং' : 'Location-based grid method with full document trail'}</p>
            </div>

            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl space-y-1">
              <span className="font-bold text-emerald-950">✓ {isBangla ? 'তথ্যসূত্র ও ফ্যাক্টর রেজিস্ট্রি' : 'Factor Traceability'}</span>
              <p className="text-slate-600">{isBangla ? 'প্রতিটি সংখ্যার জন্য সরকারি ও আন্তর্জাতিক উৎস রেকর্ড' : 'Peer-reviewed national grid factors with citation'}</p>
            </div>

            <div className="p-3 bg-amber-50 border border-amber-200 rounded-xl space-y-1">
              <span className="font-bold text-amber-950">⚠️ {isBangla ? 'বকেয়া ফ্ল্যাগ পর্যালোচনা' : 'Flag Resolution Review'}</span>
              <p className="text-slate-600">{isBangla ? '১টি জটিল ফ্ল্যাগ (ডিজেল স্পাইক) অনুমোদনের আগে ব্যাখ্যা দরকার।' : '1 critical flag requires explanation before locking snapshot.'}</p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // 4. CONSULTANT DASHBOARD
  return (
    <div className="space-y-6">
      <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-3">
        <div className="flex items-center justify-between border-b pb-3">
          <div className="flex items-center gap-2">
            <Building2 className="text-teal-700" size={20} />
            <div>
              <h3 className="font-bold text-slate-900 text-sm md:text-base">
                {isBangla ? 'ক্লায়েন্ট কারখানা পোর্টফোলিও' : 'Client Factory Portfolio Overview'}
              </h3>
              <p className="text-xs text-slate-500 mt-0.5">
                {isBangla
                  ? 'আপনার পরামর্শাধীন সকল কারখানার কার্বন অবস্থা ও ফ্ল্যাগ তালিকা'
                  : 'Manage multiple SME textile clients in one workspace'}
              </p>
            </div>
          </div>
          <span className="text-xs font-bold text-teal-800 bg-teal-50 px-3 py-1 rounded-lg border border-teal-200">
            {isBangla ? `${toBanglaDigits(clientOrgs.length)}টি কারখানা` : `${clientOrgs.length} Factories`}
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-500 font-bold uppercase tracking-wider text-[10px]">
                <th className="py-2.5 px-3">{isBangla ? 'কারখানার নাম' : 'Factory Name'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'খাত / সেক্টর' : 'Sector'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'মোট নির্গমন (tCO₂e)' : 'Emissions (tCO₂e)'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'DQS স্কোর' : 'DQS Grade'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'খোলা ফ্ল্যাগ' : 'Open Flags'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'ফ্যাক্টর ওভাররাইড' : 'Overrides'}</th>
                <th className="py-2.5 px-3 text-right">{isBangla ? 'পদক্ষেপ' : 'Action'}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {clientOrgs.map((org) => (
                <tr key={org.orgId} className="hover:bg-slate-50 transition">
                  <td className="py-3 px-3 font-bold text-slate-900">{org.orgName}</td>
                  <td className="py-3 px-3 text-slate-600">{org.sector}</td>
                  <td className="py-3 px-3 font-mono font-semibold text-slate-800">
                    {isBangla ? toBanglaDigits((org.totalEmissions ?? 0).toFixed(2)) : (org.totalEmissions ?? 0).toFixed(2)}
                  </td>
                  <td className="py-3 px-3">
                    <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                      org.dqsGrade === 'Grade A' ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'
                    }`}>
                      {org.dqsGrade} ({((org.dqsScore ?? 0) * 100).toFixed(0)}%)
                    </span>
                  </td>
                  <td className="py-3 px-3">
                    {org.openFlagsCount > 0 ? (
                      <span className="text-amber-800 font-bold bg-amber-50 px-2 py-0.5 rounded border border-amber-200">
                        {isBangla ? toBanglaDigits(org.openFlagsCount) : org.openFlagsCount} {isBangla ? 'টি' : 'flags'}
                      </span>
                    ) : (
                      <span className="text-emerald-700 font-semibold">✓ {isBangla ? 'স্বচ্ছ' : 'Clear'}</span>
                    )}
                  </td>
                  <td className="py-3 px-3">
                    {org.pendingOverridesCount > 0 ? (
                      <span className="text-indigo-800 font-semibold bg-indigo-50 px-2 py-0.5 rounded border border-indigo-200">
                        {isBangla ? toBanglaDigits(org.pendingOverridesCount) : org.pendingOverridesCount} {isBangla ? 'পেন্ডিং' : 'pending'}
                      </span>
                    ) : (
                      <span className="text-slate-400">০</span>
                    )}
                  </td>
                  <td className="py-3 px-3 text-right">
                    <button
                      onClick={() => onSelectClientOrg && onSelectClientOrg(org.orgId)}
                      className="px-2.5 py-1 text-xs font-bold text-teal-800 bg-teal-50 border border-teal-200 hover:bg-teal-100 rounded-lg transition"
                    >
                      {isBangla ? 'সুইচ করুন' : 'Switch'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
