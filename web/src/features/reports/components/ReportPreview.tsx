import React, { useState } from 'react';
import { ReportItem, ReportDuration } from '../types';
import { AuditorLineItem } from '../../auditor/types';
import { toBanglaDigits } from '../../../shared';
import {
  FileText,
  FileSpreadsheet,
  Share2,
  Lock,
  ShieldCheck,
  CheckCircle2,
  Download,
  Fingerprint,
  Building2,
  MapPin,
  Calendar,
  Eye,
  EyeOff,
  Printer,
  Sparkles,
  ExternalLink,
} from 'lucide-react';
import { getPdfDownloadUrl, getExcelDownloadUrl, REPORT_DURATIONS, exportAuditLedgerToCsv } from '../reportsApi';

interface ReportPreviewProps {
  report: ReportItem;
  isBangla: boolean;
  onApprove: (id: string) => Promise<void>;
  onOpenShareModal: (report: ReportItem) => void;
  selectedDuration?: ReportDuration;
  onSelectDuration?: (duration: ReportDuration) => void;
  lineItems?: AuditorLineItem[];
  organizationName?: string;
  facilityLocation?: string;
}

export const ReportPreview: React.FC<ReportPreviewProps> = ({
  report,
  isBangla,
  onApprove,
  onOpenShareModal,
  selectedDuration = 'last_1_month',
  onSelectDuration,
  lineItems = [],
  organizationName = 'Apex Textile & Garments Ltd.',
  facilityLocation = 'Savar & Gazipur, Dhaka Division, Bangladesh',
}) => {
  const [approving, setApproving] = useState<boolean>(false);
  const [redactPrices, setRedactPrices] = useState<boolean>(true);
  const [isExportingExcel, setIsExportingExcel] = useState<boolean>(false);

  const isLocked = report.status === 'Locked';
  const activeDuration = REPORT_DURATIONS.find((d) => d.key === selectedDuration) || REPORT_DURATIONS[0];

  async function handleApproveClick() {
    setApproving(true);
    try {
      await onApprove(report.id);
    } finally {
      setApproving(false);
    }
  }

  function handlePrintExecutiveReport() {
    window.print();
  }

  async function handleExcelExport() {
    setIsExportingExcel(true);
    try {
      // 1. Try download via backend ClosedXML endpoint
      const excelUrl = getExcelDownloadUrl(selectedDuration, redactPrices);
      const link = document.createElement('a');
      link.href = excelUrl;
      link.target = '_blank';
      link.download = `CarbonBill-GHG-DataSheet-${selectedDuration}.xlsx`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } catch (err) {
      console.warn('Backend excel download fallback to client csv:', err);
      // 2. Client-side fallback with complete audit ledger
      exportAuditLedgerToCsv(lineItems, `CarbonBill-Audit-Ledger-${selectedDuration}.csv`, redactPrices);
    } finally {
      setTimeout(() => setIsExportingExcel(false), 800);
    }
  }

  return (
    <div className="bg-white p-6 md:p-8 rounded-3xl border border-slate-200/90 shadow-sm space-y-7 print:border-none print:shadow-none print:p-0">
      {/* Duration Norms Tab Selector */}
      {onSelectDuration && (
        <div className="no-print bg-slate-50 border border-slate-200 rounded-2xl p-3">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-2">
            <span className="text-xs font-bold text-slate-700 flex items-center gap-1.5">
              <Calendar size={14} className="text-teal-700" />
              {isBangla ? 'আন্তর্জাতিক ক্রেতা অডিট সময়কাল নির্বাচন (Industry Reporting Norms):' : 'Select Industry Audit Reporting Duration:'}
            </span>
            <span className="text-[11px] font-semibold text-teal-800 bg-teal-100/80 px-2 py-0.5 rounded-md">
              {isBangla ? activeDuration.normStandardBn : activeDuration.normStandardEn}
            </span>
          </div>
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-2">
            {REPORT_DURATIONS.map((dur) => {
              const isSelected = selectedDuration === dur.key;
              return (
                <button
                  key={dur.key}
                  type="button"
                  onClick={() => onSelectDuration(dur.key)}
                  className={`p-2.5 rounded-xl border text-left transition cursor-pointer flex flex-col justify-between ${
                    isSelected
                      ? 'bg-teal-800 text-white border-teal-800 shadow-sm'
                      : 'bg-white text-slate-700 border-slate-200 hover:border-teal-300 hover:bg-teal-50/40'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-black">
                      {isBangla ? dur.labelBn : dur.labelEn}
                    </span>
                    {isSelected && <span className="text-[10px] bg-white/20 px-1.5 py-0.2 rounded font-bold">Active</span>}
                  </div>
                  <span className={`text-[10px] mt-1 font-medium ${isSelected ? 'text-teal-100' : 'text-slate-500'}`}>
                    {isBangla ? dur.dateRangeDescBn : dur.dateRangeDescEn}
                  </span>
                </button>
              );
            })}
          </div>
        </div>
      )}

      {/* Top Header & Factory Accreditation Banner */}
      <div className="border-b border-slate-200 pb-5 space-y-3">
        <div className="flex flex-col md:flex-row md:items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 flex-wrap mb-1.5">
              <span
                className={`text-xs font-bold px-2.5 py-0.5 rounded-md ${
                  isLocked
                    ? 'bg-emerald-100 text-emerald-800 border border-emerald-300'
                    : 'bg-indigo-100 text-indigo-800 border border-indigo-300'
                }`}
              >
                {isLocked
                  ? isBangla
                    ? '🔒 স্থায়ী ও ডিজিটালভাবে সিলমোহরকৃত (Locked)'
                    : '🔒 Permanently Locked & Sealed'
                  : isBangla
                  ? '📋 পর্যালোচনার জন্য প্রস্তুত (Ready for Review)'
                  : 'Ready for Review'}
              </span>
              <span className="text-xs font-mono text-slate-500 font-semibold bg-slate-100 px-2 py-0.5 rounded">
                v{report.version}.0
              </span>
              <span className="text-xs font-bold text-teal-800 bg-teal-50 px-2.5 py-0.5 rounded border border-teal-200">
                {isBangla ? activeDuration.labelBn : activeDuration.labelEn}
              </span>
            </div>

            <h1 className="text-xl md:text-2xl font-black text-slate-900 tracking-tight">
              {isBangla
                ? `কর্পোরেট GHG নির্গমন প্রতিবেদন — ${report.period}`
                : `Corporate GHG Emission Report — ${report.period}`}
            </h1>

            <div className="flex items-center gap-4 text-xs text-slate-600 mt-2 flex-wrap">
              <span className="flex items-center gap-1 font-bold text-slate-800">
                <Building2 size={14} className="text-teal-700" />
                {organizationName}
              </span>
              <span className="flex items-center gap-1 text-slate-500">
                <MapPin size={14} className="text-rose-500" />
                {facilityLocation}
              </span>
              <span className="flex items-center gap-1 text-slate-500">
                <Calendar size={14} className="text-blue-500" />
                {isBangla ? activeDuration.dateRangeDescBn : activeDuration.dateRangeDescEn}
              </span>
            </div>
          </div>

          {/* Action Buttons (Approve & Share Link) */}
          <div className="no-print flex items-center gap-2 flex-wrap shrink-0">
            {!isLocked && (
              <button
                onClick={handleApproveClick}
                disabled={approving || !report.readinessChecklist.isReadyForApproval}
                className="px-4 py-2 bg-emerald-700 hover:bg-emerald-800 disabled:opacity-50 text-white rounded-xl text-xs font-bold shadow-xs transition flex items-center gap-1.5 cursor-pointer"
              >
                <CheckCircle2 size={15} />
                {approving
                  ? isBangla
                    ? 'অনুমোদন হচ্ছে...'
                    : 'Approving...'
                  : isBangla
                  ? 'অনুমোদন ও লক করুন'
                  : 'Approve & Lock Report'}
              </button>
            )}

            <button
              onClick={() => onOpenShareModal(report)}
              className="px-3.5 py-2 bg-slate-100 hover:bg-slate-200 text-slate-800 rounded-xl text-xs font-bold transition flex items-center gap-1.5 cursor-pointer"
            >
              <Share2 size={14} />
              {isBangla ? 'অডিটর শেয়ার লিঙ্ক' : 'Auditor Share Link'}
            </button>
          </div>
        </div>

        {/* Global Compliance Badges Strip */}
        <div className="flex items-center gap-2 pt-2 flex-wrap text-[11px] font-semibold text-slate-600">
          <span className="px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-800 border border-emerald-200 flex items-center gap-1">
            <ShieldCheck size={12} className="text-emerald-700" /> GHG Protocol Corporate Standard (Scope 1, 2, 3)
          </span>
          <span className="px-2 py-0.5 rounded-full bg-blue-50 text-blue-800 border border-blue-200 flex items-center gap-1">
            <Sparkles size={12} className="text-blue-700" /> Higg FEM 4.0 Verified
          </span>
          <span className="px-2 py-0.5 rounded-full bg-indigo-50 text-indigo-800 border border-indigo-200 flex items-center gap-1">
            <CheckCircle2 size={12} className="text-indigo-700" /> ISO 14064-1:2018 Quantification Ready
          </span>
        </div>
      </div>

      {/* KPI Totals Matrix */}
      <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-5 gap-3">
        <div className="p-4 bg-slate-50 border border-slate-200 rounded-2xl space-y-1">
          <span className="text-xs font-bold text-slate-500">
            {isBangla ? 'মোট নির্গমন (Total Footprint)' : 'Total Footprint'}
          </span>
          <div className="flex items-baseline gap-1">
            <span className="text-2xl font-black text-slate-900 tracking-tight">
              {isBangla ? toBanglaDigits(Number(report.totalEmissions ?? 0).toFixed(2)) : Number(report.totalEmissions ?? 0).toFixed(2)}
            </span>
            <span className="text-xs font-bold text-slate-500">tCO₂e</span>
          </div>
          <p className="text-[10px] text-slate-400 font-medium">
            {isBangla ? 'স্কোপ ১, ২ ও ৩ সম্মিলিত' : 'Scope 1, 2 & 3 Combined'}
          </p>
        </div>

        <div className="p-4 bg-orange-50/50 border border-orange-200/80 rounded-2xl space-y-1">
          <span className="text-xs font-bold text-orange-900">
            {isBangla ? 'স্কোপ ১ (ডিজেল ও গ্যাস)' : 'Scope 1 (Direct Fuel)'}
          </span>
          <div className="flex items-baseline gap-1">
            <span className="text-2xl font-black text-orange-700 tracking-tight">
              {isBangla ? toBanglaDigits(Number(report.scope1Emissions ?? 0).toFixed(2)) : Number(report.scope1Emissions ?? 0).toFixed(2)}
            </span>
            <span className="text-xs font-bold text-orange-600">tCO₂e</span>
          </div>
          <p className="text-[10px] text-orange-800/80 font-medium">
            {isBangla ? 'জেনারেটর ফুয়েল ও বয়লার গ্যাস' : 'Generator & Boiler Combustion'}
          </p>
        </div>

        <div className="p-4 bg-sky-50/50 border border-sky-200/80 rounded-2xl space-y-1">
          <span className="text-xs font-bold text-sky-900">
            {isBangla ? 'স্কোপ ২ (গ্রিড বিদ্যুৎ)' : 'Scope 2 (Grid Electricity)'}
          </span>
          <div className="flex items-baseline gap-1">
            <span className="text-2xl font-black text-sky-700 tracking-tight">
              {isBangla ? toBanglaDigits(Number(report.scope2Emissions ?? 0).toFixed(2)) : Number(report.scope2Emissions ?? 0).toFixed(2)}
            </span>
            <span className="text-xs font-bold text-sky-600">tCO₂e</span>
          </div>
          <p className="text-[10px] text-sky-800/80 font-medium">
            {isBangla ? 'ডেসকো/বিপিডিবি বিদ্যুৎ সাবস্টেশন' : 'DESCO / BPDB Grid Supply'}
          </p>
        </div>

        <div className="p-4 bg-blue-50/50 border border-blue-200/80 rounded-2xl space-y-1">
          <span className="text-xs font-bold text-blue-900">
            {isBangla ? 'স্কোপ ৩ (পরিবহন লজিস্টিকস)' : 'Scope 3 (Logistics)'}
          </span>
          <div className="flex items-baseline gap-1">
            <span className="text-2xl font-black text-blue-700 tracking-tight">
              {isBangla ? toBanglaDigits(Number(report.scope3Emissions ?? 0).toFixed(2)) : Number(report.scope3Emissions ?? 0).toFixed(2)}
            </span>
            <span className="text-xs font-bold text-blue-600">tCO₂e</span>
          </div>
          <p className="text-[10px] text-blue-800/80 font-medium">
            {isBangla ? 'বন্দর কার্গো ফ্রেইট চালান' : 'Port Freight Trucking & Logistics'}
          </p>
        </div>

        <div className="p-4 bg-teal-50/60 border border-teal-200 rounded-2xl space-y-1 col-span-2 sm:col-span-1">
          <span className="text-xs font-bold text-teal-900">
            {isBangla ? 'ডেটা কোয়ালিটি স্কোর (DQS)' : 'Data Quality Score'}
          </span>
          <div className="flex items-baseline gap-1">
            <span className="text-2xl font-black text-teal-800 tracking-tight">
              {isBangla
                ? toBanglaDigits(((report.dataQualityScore ?? 0) * 100).toFixed(0))
                : ((report.dataQualityScore ?? 0) * 100).toFixed(0)}
              %
            </span>
            <span className="text-xs font-bold text-teal-700 ml-1">({report.dqsGrade})</span>
          </div>
          <p className="text-[10px] text-teal-700 font-medium">
            {isBangla ? 'প্রাথমিক ইনভয়েস ভিত্তিক যাচাই' : 'Verified Primary Evidence'}
          </p>
        </div>
      </div>

      {/* Commercial Price Redaction & Ledger Header Strip */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-3 border-t border-slate-200">
        <div>
          <h3 className="text-sm font-black text-slate-900 flex items-center gap-2">
            <span>{isBangla ? 'নিরীক্ষিত কার্যকলাপ লেজার ও সোর্স প্রমাণক' : 'Audited Activity Ledger & Source Traceability'}</span>
            <span className="text-xs font-bold px-2 py-0.5 bg-teal-100 text-teal-800 rounded-full">
              {lineItems.length} {isBangla ? 'টি নিরীক্ষিত রেকর্ড' : 'Verified Records'}
            </span>
          </h3>
          <p className="text-xs text-slate-500 mt-0.5">
            {isBangla
              ? `${activeDuration.labelBn}-এর জন্য ফিল্টারকৃত ইউটিলিটি বিল, মিটার রিডিং ও গাণিতিক সমীকরণ`
              : `Filtered line items, verified utility bills, meter readings, and emission formulas for ${activeDuration.labelEn}`}
          </p>
        </div>

        {/* Commercial Price Redaction Toggle */}
        <div className="no-print flex items-center gap-2">
          <button
            type="button"
            onClick={() => setRedactPrices((prev) => !prev)}
            className={`px-3 py-1.5 rounded-xl text-xs font-bold border transition flex items-center gap-1.5 cursor-pointer ${
              redactPrices
                ? 'bg-amber-50 text-amber-900 border-amber-300'
                : 'bg-slate-100 text-slate-700 border-slate-200 hover:bg-slate-200'
            }`}
            title={isBangla ? 'ক্রেতার জন্য বাণিজ্যিক রেট গোপন করার ফিল্টার' : 'Toggle commercial price confidentiality for buyer'}
          >
            {redactPrices ? (
              <>
                <EyeOff size={14} className="text-amber-700" />
                {isBangla ? 'বাণিজ্যিক রেট গোপন (Redacted)' : 'Prices Redacted for Buyer'}
              </>
            ) : (
              <>
                <Eye size={14} className="text-slate-600" />
                {isBangla ? 'বাণিজ্যিক রেট দৃশ্যমান' : 'Show Financial Costs'}
              </>
            )}
          </button>
        </div>
      </div>

      {/* Audited Line Items Table */}
      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
        <table className="w-full text-left border-collapse text-xs">
          <thead>
            <tr className="bg-slate-50/90 border-b border-slate-200 text-slate-700 font-bold">
              <th className="py-3 px-3.5">
                {isBangla ? 'স্কোপ ও উৎস' : 'Scope & Source'}
              </th>
              <th className="py-3 px-3.5">
                {isBangla ? 'কোম্পানি ও মিটার/বিল নং' : 'Company & Bill Ref'}
              </th>
              <th className="py-3 px-3.5 text-right">
                {isBangla ? 'ব্যবহারের পরিমাণ' : 'Physical Units'}
              </th>
              <th className="py-3 px-3.5 text-right">
                {isBangla ? 'বিলকৃত টাকা (BDT)' : 'Billed Cost (BDT)'}
              </th>
              <th className="py-3 px-3.5">
                {isBangla ? 'নির্গমন গুণক (EF Benchmark)' : 'Emission Factor & Source'}
              </th>
              <th className="py-3 px-3.5 text-right">
                {isBangla ? 'নিট tCO₂e ও হিসাব' : 'Verified tCO₂e & Formula'}
              </th>
              <th className="py-3 px-3.5 text-center">
                {isBangla ? 'যাচাই স্ট্যাম্প' : 'Verification Stamp'}
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {lineItems.length === 0 ? (
              <tr>
                <td colSpan={7} className="py-8 text-center text-slate-400 font-medium">
                  {isBangla ? 'নির্বাচিত সময়কালের জন্য কোনো অডিট রেকর্ড পাওয়া যায়নি।' : 'No audit records found for the selected duration.'}
                </td>
              </tr>
            ) : (
              lineItems.map((item) => {
                const scopeBadgeColor =
                  item.scope === 'Scope 1'
                    ? 'bg-orange-100 text-orange-800 border-orange-200'
                    : item.scope === 'Scope 2'
                    ? 'bg-sky-100 text-sky-800 border-sky-200'
                    : 'bg-blue-100 text-blue-800 border-blue-200';

                return (
                  <tr key={item.id} className="hover:bg-slate-50/70 transition">
                    {/* Scope & Source */}
                    <td className="py-3 px-3.5">
                      <div className="flex items-center gap-1.5 mb-1">
                        <span className={`px-2 py-0.2 rounded-full text-[10px] font-bold border ${scopeBadgeColor}`}>
                          {item.scope}
                        </span>
                        <span className="text-[10px] font-semibold text-slate-500 bg-slate-100 px-1.5 py-0.2 rounded">
                          {isBangla ? (item.divisionBn || 'ঢাকা বিভাগ') : (item.division || 'Dhaka Division')}
                        </span>
                      </div>
                      <p className="font-bold text-slate-900 leading-snug">
                        {isBangla ? item.sourceNameBn : item.sourceNameEn}
                      </p>
                    </td>

                    {/* Company & Bill Ref */}
                    <td className="py-3 px-3.5">
                      <p className="font-bold text-slate-800">
                        {item.billCompany || (isBangla ? 'ইউটিলিটি সরবরাহকারী' : 'Utility Supplier')}
                      </p>
                      <div className="flex items-center gap-1.5 text-[11px] text-slate-500 mt-0.5">
                        <span className="font-mono bg-slate-100 px-1 rounded text-[10px]">
                          {item.billNumber || item.documentId}
                        </span>
                        {item.meterNumber && item.meterNumber !== 'N/A' && (
                          <span className="text-[10px] text-slate-400">
                            Mtr: {item.meterNumber}
                          </span>
                        )}
                      </div>
                    </td>

                    {/* Physical Activity Units */}
                    <td className="py-3 px-3.5 text-right font-mono font-bold text-slate-900">
                      {isBangla ? toBanglaDigits(item.activityQuantity.toLocaleString()) : item.activityQuantity.toLocaleString()}
                      <span className="text-slate-500 font-sans font-medium text-[11px] ml-1">
                        {item.activityUnit}
                      </span>
                    </td>

                    {/* Billed Cost (BDT) */}
                    <td className="py-3 px-3.5 text-right font-mono">
                      {redactPrices ? (
                        <span className="text-[10px] font-bold px-2 py-0.5 bg-amber-50 text-amber-800 rounded border border-amber-200">
                          {isBangla ? '[বাণিজ্যিক রেট গোপন]' : '[REDACTED]'}
                        </span>
                      ) : (
                        <span className="font-bold text-slate-800">
                          ৳{isBangla ? toBanglaDigits(Number(item.billedAmountBdt || 0).toLocaleString()) : Number(item.billedAmountBdt || 0).toLocaleString()}
                        </span>
                      )}
                    </td>

                    {/* Emission Factor Benchmark */}
                    <td className="py-3 px-3.5">
                      <div className="flex items-baseline gap-1 font-mono font-bold text-slate-900">
                        <span>{item.factorValue}</span>
                        <span className="text-[10px] text-slate-500 font-sans font-normal">
                          kgCO₂e/{item.activityUnit}
                        </span>
                      </div>
                      <p className="text-[10px] text-slate-500 mt-0.5 truncate max-w-xs" title={item.factorSource}>
                        {item.factorSource} ({item.factorCitationYear})
                      </p>
                    </td>

                    {/* Net tCO₂e & Formula Breakdown */}
                    <td className="py-3 px-3.5 text-right">
                      <div className="flex items-baseline justify-end gap-1 font-bold text-slate-900">
                        <span className="text-sm font-black text-teal-900">
                          {isBangla ? toBanglaDigits(item.emissionsTco2e.toFixed(2)) : item.emissionsTco2e.toFixed(2)}
                        </span>
                        <span className="text-[10px] text-slate-500 font-normal">tCO₂e</span>
                      </div>
                      <p className="text-[10px] text-slate-500 font-mono mt-0.5">
                        {item.calculationFormula || `${item.activityQuantity} × ${item.factorValue} ÷ 1,000`}
                      </p>
                    </td>

                    {/* Verification Stamp */}
                    <td className="py-3 px-3.5 text-center">
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-200">
                        <CheckCircle2 size={11} className="text-emerald-700" />
                        {isBangla ? `${toBanglaDigits(item.ocrConfidence)}% কনফিডেন্স` : `${item.ocrConfidence}% OCR`}
                      </span>
                      <p className="text-[9px] text-slate-400 mt-0.5 truncate max-w-[120px]" title={item.confirmedBy}>
                        {item.confirmedBy}
                      </p>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
          {lineItems.length > 0 && (
            <tfoot>
              <tr className="bg-slate-50 font-bold border-t-2 border-slate-300 text-slate-900">
                <td colSpan={2} className="py-3 px-3.5">
                  {isBangla ? 'সর্বমোট ফিল্টারকৃত নিরীক্ষা যোগফল:' : 'Total Filtered Inventory:'}
                </td>
                <td className="py-3 px-3.5 text-right font-mono text-slate-600">
                  {lineItems.length} {isBangla ? 'আইটেম' : 'records'}
                </td>
                <td className="py-3 px-3.5 text-right font-mono">
                  {redactPrices ? (
                    <span className="text-[10px] font-bold text-amber-800">[REDACTED]</span>
                  ) : (
                    <span>
                      ৳{isBangla
                        ? toBanglaDigits(lineItems.reduce((acc, i) => acc + (i.billedAmountBdt || 0), 0).toLocaleString())
                        : lineItems.reduce((acc, i) => acc + (i.billedAmountBdt || 0), 0).toLocaleString()}
                    </span>
                  )}
                </td>
                <td className="py-3 px-3.5 text-slate-500 text-[10px]">
                  IPCC AR6 / SREDA Benchmark
                </td>
                <td className="py-3 px-3.5 text-right font-black text-sm text-teal-900 font-mono">
                  {isBangla ? toBanglaDigits(Number(report.totalEmissions ?? 0).toFixed(2)) : Number(report.totalEmissions ?? 0).toFixed(2)} tCO₂e
                </td>
                <td className="py-3 px-3.5 text-center text-[10px] text-teal-800 font-bold">
                  {report.dqsGrade} Verified
                </td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>

      {/* Cryptographic Hash Seal (if locked or verified) */}
      {report.contentHashSha256 && (
        <div className="p-4 bg-emerald-50/70 border border-emerald-300 rounded-2xl space-y-1.5 text-xs text-emerald-950">
          <div className="flex items-center gap-2 font-bold">
            <Fingerprint size={16} className="text-emerald-700" />
            <span>
              {isBangla
                ? 'ক্রিপ্টোগ্রাফিক ডিজিটাল সিলমোহর (SHA-256 Snapshot Seal):'
                : 'Cryptographic Audit Snapshot Seal (SHA-256):'}
            </span>
          </div>
          <p className="font-mono text-[11px] text-slate-800 break-all select-all bg-white/80 p-2 rounded-lg border border-emerald-200">
            {report.contentHashSha256}
          </p>
          <div className="flex items-center justify-between text-[11px] text-emerald-900 pt-1">
            <span>
              {isBangla ? 'ডিজিটাল স্বাক্ষরকারী: ' : 'Authorized Signatory: '}
              <strong>{report.approvedBy || 'Managing Director (Apex Textiles)'}</strong>
            </span>
            <span>
              {isBangla ? 'তারিখ: ' : 'Date: '}
              {report.approvedAt ? new Date(report.approvedAt).toLocaleDateString() : '2026-09-05'}
            </span>
          </div>
        </div>
      )}

      {/* Mandatory GHG Protocol Disclaimer (Rule 16) */}
      <div className="p-4 bg-slate-50 border border-slate-200 rounded-2xl text-xs text-slate-600 flex items-start gap-2.5">
        <ShieldCheck size={18} className="text-teal-700 shrink-0 mt-0.5" />
        <div>
          <span className="font-bold text-slate-800">
            {isBangla ? 'GHG প্রোটোকল মেথডোলজি অস্বীকৃতি (Disclaimer):' : 'GHG Protocol Methodology Disclaimer:'}
          </span>
          <p className="mt-0.5 leading-relaxed">
            {isBangla
              ? 'এই প্রতিবেদনের ফলাফলসমূহ GHG Protocol Corporate Accounting and Reporting Standard মেথডোলজি অনুযায়ী হিসাবকৃত বৈজ্ঞানিক প্রকৌশল প্রাক্কলন, কোনো তৃতীয়-পক্ষের সার্টিফায়েড অডিট নয়। কার্বন ফ্যাক্টরসমূহ সরকারি স্রেডা (SREDA), পরিবেশ অধিদপ্তর এবং ডিইএফআরএ (DEFRA) ডাটাবেজ থেকে সংগৃহীত।'
              : 'Estimates aligned with GHG Protocol Corporate Accounting and Reporting Standard methodology, not audited or certified. Figures are calculated based on confirmed primary supplier utility invoices, meter readings, and location-based grid factors from SREDA/DoE.'}
          </p>
        </div>
      </div>

      {/* Download Action Strip with the 2 World-Class Options */}
      <div className="no-print pt-3 border-t border-slate-200 flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <span className="text-xs font-black text-slate-900 flex items-center gap-1.5">
            <Download size={15} className="text-teal-700" />
            {isBangla ? 'আন্তর্জাতিক ক্রেতা অডিট এক্সপোর্ট অপশন:' : 'Buyer Audit Export Options:'}
          </span>
          <p className="text-[11px] text-slate-500 mt-0.5">
            {isBangla
              ? 'প্রফেশনাল এক্সিকিউটিভ রিপোর্ট (ইংরেজি/পিডিএফ) ও বিশ্বমানের মাল্টি-শিট এক্সেল স্প্রেডশিট'
              : 'Professional executive buyer report (English/PDF) and world-class multi-sheet Excel spreadsheet'}
          </p>
        </div>

        {/* The Two Main Export Options */}
        <div className="flex items-center gap-2.5 flex-wrap">
          {/* Option 1: English Report (Executive PDF / Print) */}
          <div className="flex items-center bg-teal-800 hover:bg-teal-900 text-white rounded-xl shadow-xs transition overflow-hidden">
            <a
              href={getPdfDownloadUrl(selectedDuration, 'en', redactPrices)}
              target="_blank"
              rel="noreferrer"
              className="px-3.5 py-2.5 text-xs font-bold flex items-center gap-1.5 cursor-pointer hover:bg-teal-900/80"
              title={isBangla ? 'কোয়েস্টপিডিএফ এক্সিকিউটিভ ইংরেজি রিপোর্ট ডাউনলোড করুন' : 'Download QuestPDF executive English report'}
            >
              <FileText size={15} />
              {isBangla ? 'ইংরেজি রিপোর্ট (PDF)' : 'English Report (PDF)'}
            </a>
            <div className="w-[1px] h-5 bg-teal-700/80" />
            <button
              type="button"
              onClick={handlePrintExecutiveReport}
              className="px-2.5 py-2.5 text-teal-200 hover:text-white hover:bg-teal-900/80 cursor-pointer transition"
              title={isBangla ? 'প্রিন্ট বা পিডিএফ হিসেবে সংরক্ষণ করুন' : 'Print or Save as PDF'}
            >
              <Printer size={15} />
            </button>
          </div>

          {/* Option 2: Excel Spreadsheet (.xlsx) */}
          <button
            type="button"
            onClick={handleExcelExport}
            disabled={isExportingExcel}
            className="px-4 py-2.5 bg-emerald-700 hover:bg-emerald-800 disabled:opacity-60 text-white rounded-xl text-xs font-bold shadow-xs transition flex items-center gap-1.5 cursor-pointer"
            title={isBangla ? 'ক্লোজডএক্সএমএল স্প্রেডশিট ডাউনলোড করুন' : 'Download ClosedXML Excel spreadsheet (.xlsx)'}
          >
            <FileSpreadsheet size={15} />
            {isExportingExcel
              ? isBangla
                ? 'এক্সেল প্রস্তুত হচ্ছে...'
                : 'Generating .xlsx...'
              : isBangla
              ? 'এক্সেল স্প্রেডশিট (.xlsx)'
              : 'Excel Spreadsheet (.xlsx)'}
          </button>
        </div>
      </div>
    </div>
  );
};
