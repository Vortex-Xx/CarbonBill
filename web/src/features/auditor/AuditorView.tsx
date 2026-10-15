import React, { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { AuditorSnapshot, AuditorLineItem } from './types';
import { fetchAuditorSnapshot } from './auditorApi';
import { ProvenanceInspectorModal } from './components/ProvenanceInspectorModal';
import { AuditMethodologyModal } from './components/AuditMethodologyModal';
import { getCompanyBadge } from './companyBadge';
import { toBanglaDigits, formatBdt } from '../../shared';
import {
  ShieldCheck,
  Fingerprint,
  FileSearch,
  Lock,
  Search,
  Building,
  Building2,
  MapPin,
  RefreshCw,
  Info,
  BookOpen,
  Download,
  Filter,
  CheckCircle2,
  HelpCircle,
} from 'lucide-react';

interface AuditorViewProps {
  token?: string;
}

export const AuditorView: React.FC<AuditorViewProps> = ({ token }) => {
  const { i18n } = useTranslation();
  const isBangla = i18n.language === 'bn';

  const [snapshot, setSnapshot] = useState<AuditorSnapshot | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [selectedItem, setSelectedItem] = useState<AuditorLineItem | null>(null);
  const [selectedScopeFilter, setSelectedScopeFilter] = useState<'ALL' | 'Scope 1' | 'Scope 2' | 'Scope 3'>('ALL');
  const [methodologyOpen, setMethodologyOpen] = useState<boolean>(false);
  const [methodologyTab, setMethodologyTab] = useState<'scopes' | 'grading'>('scopes');

  // Extract token from prop or URL query params
  const activeToken = useMemo(() => {
    if (token) return token;
    if (typeof window !== 'undefined') {
      const p = new URLSearchParams(window.location.search);
      return p.get('token') || undefined;
    }
    return undefined;
  }, [token]);

  async function loadData() {
    setLoading(true);
    try {
      const data = await fetchAuditorSnapshot(undefined, activeToken);
      setSnapshot(data);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, [activeToken]);

  const filteredItems = useMemo(() => {
    if (!snapshot) return [];
    let items = snapshot.lineItems;
    if (selectedScopeFilter !== 'ALL') {
      items = items.filter((item) => item.scope === selectedScopeFilter);
    }
    if (!searchTerm.trim()) return items;
    const term = searchTerm.toLowerCase();
    return items.filter(
      (item) =>
        item.sourceNameEn.toLowerCase().includes(term) ||
        item.sourceNameBn.includes(term) ||
        item.documentId.toLowerCase().includes(term) ||
        item.scope.toLowerCase().includes(term) ||
        (item.billCompany && item.billCompany.toLowerCase().includes(term)) ||
        (item.billNumber && item.billNumber.toLowerCase().includes(term)) ||
        (item.fileName && item.fileName.toLowerCase().includes(term)) ||
        (item.division && item.division.toLowerCase().includes(term)) ||
        (item.divisionBn && item.divisionBn.includes(term)) ||
        (item.companyName && item.companyName.toLowerCase().includes(term))
    );
  }, [snapshot, searchTerm, selectedScopeFilter]);

  const handleExportCsv = () => {
    if (!snapshot || !filteredItems.length) return;
    const headers = [
      'Scope',
      'Bill Company',
      'Bill Number',
      'Meter Number',
      'Activity Source',
      'Company',
      'Division',
      'Quantity',
      'Unit',
      'Billed BDT',
      'Emission Factor (kgCO2e/unit)',
      'Gross kgCO2e',
      'Net tCO2e',
      'Document ID',
      'Document Date',
      'OCR Score %',
      'Confirmed By',
      'Calculation Formula',
    ];

    const rows = filteredItems.map((item) => [
      `"${item.scope}"`,
      `"${(item.billCompany || '').replace(/"/g, '""')}"`,
      `"${(item.billNumber || '').replace(/"/g, '""')}"`,
      `"${(item.meterNumber || '').replace(/"/g, '""')}"`,
      `"${(isBangla ? item.sourceNameBn : item.sourceNameEn).replace(/"/g, '""')}"`,
      `"${(item.companyName || snapshot.organizationName).replace(/"/g, '""')}"`,
      `"${(isBangla ? item.divisionBn || 'ঢাকা বিভাগ' : item.division || 'Dhaka Division').replace(/"/g, '""')}"`,
      item.activityQuantity,
      `"${item.activityUnit}"`,
      snapshot.redactPrices ? '"[REDACTED]"' : (item.billedAmountBdt ?? 0),
      item.factorValue,
      item.grossKgCo2e ?? (item.activityQuantity * item.factorValue),
      item.emissionsTco2e,
      `"${item.documentId}"`,
      `"${item.documentDate}"`,
      item.ocrConfidence,
      `"${(item.confirmedBy || '').replace(/"/g, '""')}"`,
      `"${(item.calculationFormula || '').replace(/"/g, '""')}"`,
    ]);

    const csvContent = '\uFEFF' + [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `CarbonBill_Auditor_Ledger_${snapshot.organizationName.replace(/\s+/g, '_')}_${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  if (loading || !snapshot) {
    return (
      <div className="p-12 text-center text-slate-500 text-xs font-semibold">
        {isBangla ? 'অডিটর পোর্টাল যাচাইকরণ লোড হচ্ছে...' : 'Validating auditor verification snapshot...'}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Top Auditor Verified Header Banner */}
      <div className="bg-white p-6 rounded-2xl border border-slate-200/90 shadow-sm space-y-4">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="p-3 bg-teal-900 text-white rounded-2xl shadow-sm">
              <ShieldCheck size={28} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-bold px-2 py-0.5 bg-emerald-100 text-emerald-800 rounded-md">
                  ✓ {isBangla ? 'যাচাইকৃত অডিট ভিউ (Read-Only)' : 'Verified Auditor Access'}
                </span>
                <span className="text-xs font-semibold text-slate-500">
                  {isBangla ? `হিসাবকাল: ${snapshot.period}` : `Period: ${snapshot.period}`}
                </span>
              </div>
              <h2 className="text-lg md:text-xl font-black text-slate-900 mt-1">
                {snapshot.organizationName}
              </h2>
              <p className="text-xs text-slate-500 flex items-center gap-1 mt-0.5">
                <Building size={13} className="text-slate-400" />
                {snapshot.facilityLocation}
              </p>
            </div>
          </div>

          <div className="flex flex-col md:items-end gap-1.5">
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold text-slate-400">
                {isBangla ? 'ডিজিটাল অডিট সম্মতি:' : 'Audit Preparedness:'}
              </span>
              <button
                onClick={() => {
                  setMethodologyTab('grading');
                  setMethodologyOpen(true);
                }}
                className="text-xs font-bold text-teal-700 hover:text-teal-900 underline flex items-center gap-1 cursor-pointer"
                title={isBangla ? 'গ্রেড নির্ণয় পদ্ধতি ও মানদণ্ড দেখুন' : 'View Grade Calculation Engine'}
              >
                <HelpCircle size={13} />
                {isBangla ? 'গ্রেড পদ্ধতি' : 'How Calculated?'}
              </button>
            </div>

            <button
              onClick={() => {
                setMethodologyTab('grading');
                setMethodologyOpen(true);
              }}
              className="text-xl font-black text-teal-800 hover:text-teal-950 transition cursor-pointer flex items-center gap-1.5"
            >
              <span>{snapshot.dqsGrade} ({((snapshot.dataQualityScore ?? 0) * 100).toFixed(0)}% DQS)</span>
              <span className="text-xs font-bold px-2 py-0.5 bg-teal-100 text-teal-900 rounded-md">
                {isBangla ? 'বিস্তারিত' : 'View'}
              </span>
            </button>

            <button
              onClick={() => {
                setMethodologyTab('scopes');
                setMethodologyOpen(true);
              }}
              className="mt-1 px-3 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-lg text-xs transition cursor-pointer flex items-center gap-1.5"
            >
              <BookOpen size={13} className="text-teal-700" />
              {isBangla ? 'স্কোপ ও গ্রেড গাইডলাইন' : 'Scopes & Grading Guide'}
            </button>
          </div>
        </div>

        {/* Cryptographic SHA-256 Stamp */}
        <div className="p-3 bg-slate-50 border border-slate-200 rounded-xl space-y-1 text-xs">
          <div className="flex items-center justify-between text-[11px] text-slate-500">
            <span className="font-bold text-slate-700 flex items-center gap-1.5">
              <Fingerprint size={14} className="text-teal-700" />
              {isBangla ? 'ক্রিপ্টোগ্রাফিক স্ন্যাপশট হ্যাশ (SHA-256):' : 'Cryptographic Hash Stamp (SHA-256):'}
            </span>
            <span>
              {isBangla ? 'অনুমোদনকারী: ' : 'Signed by: '}
              <strong>{snapshot.approvedBy}</strong>
            </span>
          </div>
          <p className="font-mono text-[11px] text-slate-800 break-all select-all bg-white p-1.5 rounded border border-slate-200">
            {snapshot.contentHashSha256}
          </p>
        </div>

        {/* Commercial Price Redaction Status Badge */}
        {snapshot.redactPrices && (
          <div className="p-2.5 bg-amber-50 border border-amber-200 rounded-xl text-xs text-amber-900 flex items-center gap-2">
            <Lock size={15} className="text-amber-700 shrink-0" />
            <span>
              {isBangla
                ? 'বাণিজ্যিক গোপনীয়তা নীতি সক্রিয়: চালানের আর্থিক টাকা গোপন রাখা হয়েছে। ফিজিক্যাল ব্যবহার (লিটার, kWh) ও ফ্যাক্টরসমূহ সম্পূর্ণ অডিটযোগ্য।'
                : 'Commercial Price Redaction Active: Financial currency amounts are masked. Physical activity metrics (litres, kWh, m³) and emission factors remain fully auditable.'}
            </span>
          </div>
        )}
      </div>

      {/* Scope Totals Bar - Interactive Filter Cards */}
      <div className="space-y-1.5">
        <div className="flex items-center justify-between text-xs text-slate-500 px-1">
          <span className="font-medium">
            {isBangla
              ? 'স্কোপ অনুযায়ী ফিল্টার করতে যেকোনো কার্ডে ক্লিক করুন:'
              : 'Click any scope card below to filter the ledger table:'}
          </span>
          {selectedScopeFilter !== 'ALL' && (
            <button
              onClick={() => setSelectedScopeFilter('ALL')}
              className="text-xs font-bold text-teal-700 hover:text-teal-900 underline cursor-pointer"
            >
              {isBangla ? 'সব স্কোপ দেখুন (ফিল্টার রিসেট)' : 'Show All Scopes (Reset Filter)'}
            </button>
          )}
        </div>

        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          {/* Total Card */}
          <div
            onClick={() => setSelectedScopeFilter('ALL')}
            className={`p-4 rounded-xl border transition cursor-pointer shadow-2xs space-y-1 ${
              selectedScopeFilter === 'ALL'
                ? 'bg-teal-50/40 border-teal-600 ring-2 ring-teal-600/30'
                : 'bg-white border-slate-200 hover:border-teal-300'
            }`}
          >
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">
                {isBangla ? 'মোট নির্গমন (Total)' : 'Total Footprint'}
              </span>
              {selectedScopeFilter === 'ALL' && (
                <span className="text-[10px] font-bold text-teal-800 bg-teal-100 px-1.5 py-0.5 rounded">
                  {isBangla ? 'সক্রিয়' : 'All'}
                </span>
              )}
            </div>
            <p className="text-2xl font-black text-slate-900">
              {isBangla ? toBanglaDigits(Number(snapshot.totalEmissions ?? 0).toFixed(2)) : Number(snapshot.totalEmissions ?? 0).toFixed(2)}{' '}
              <span className="text-xs font-semibold text-slate-400">tCO₂e</span>
            </p>
          </div>

          {/* Scope 1 Card */}
          <div
            onClick={() => setSelectedScopeFilter(selectedScopeFilter === 'Scope 1' ? 'ALL' : 'Scope 1')}
            className={`p-4 rounded-xl border transition cursor-pointer shadow-2xs space-y-1 ${
              selectedScopeFilter === 'Scope 1'
                ? 'bg-orange-50/50 border-orange-500 ring-2 ring-orange-500/30'
                : 'bg-white border-slate-200 hover:border-orange-300'
            }`}
          >
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">
                {isBangla ? 'স্কোপ ১ (জ্বালানি/গ্যাস)' : 'Scope 1 (Direct)'}
              </span>
              <span className="text-[10px] font-bold text-orange-800 bg-orange-100 px-1.5 py-0.5 rounded">
                {selectedScopeFilter === 'Scope 1' ? (isBangla ? 'ফিল্টারড' : 'Active') : 'Scope 1'}
              </span>
            </div>
            <p className="text-2xl font-black text-orange-600">
              {isBangla ? toBanglaDigits(Number(snapshot.scope1Emissions ?? 0).toFixed(2)) : Number(snapshot.scope1Emissions ?? 0).toFixed(2)}{' '}
              <span className="text-xs font-semibold text-slate-400">tCO₂e</span>
            </p>
          </div>

          {/* Scope 2 Card */}
          <div
            onClick={() => setSelectedScopeFilter(selectedScopeFilter === 'Scope 2' ? 'ALL' : 'Scope 2')}
            className={`p-4 rounded-xl border transition cursor-pointer shadow-2xs space-y-1 ${
              selectedScopeFilter === 'Scope 2'
                ? 'bg-sky-50/50 border-sky-500 ring-2 ring-sky-500/30'
                : 'bg-white border-slate-200 hover:border-sky-300'
            }`}
          >
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">
                {isBangla ? 'স্কোপ ২ (গ্রিড বিদ্যুৎ)' : 'Scope 2 (Electricity)'}
              </span>
              <span className="text-[10px] font-bold text-sky-800 bg-sky-100 px-1.5 py-0.5 rounded">
                {selectedScopeFilter === 'Scope 2' ? (isBangla ? 'ফিল্টারড' : 'Active') : 'Scope 2'}
              </span>
            </div>
            <p className="text-2xl font-black text-sky-600">
              {isBangla ? toBanglaDigits(Number(snapshot.scope2Emissions ?? 0).toFixed(2)) : Number(snapshot.scope2Emissions ?? 0).toFixed(2)}{' '}
              <span className="text-xs font-semibold text-slate-400">tCO₂e</span>
            </p>
          </div>

          {/* Scope 3 Card */}
          <div
            onClick={() => setSelectedScopeFilter(selectedScopeFilter === 'Scope 3' ? 'ALL' : 'Scope 3')}
            className={`p-4 rounded-xl border transition cursor-pointer shadow-2xs space-y-1 ${
              selectedScopeFilter === 'Scope 3'
                ? 'bg-purple-50/50 border-purple-500 ring-2 ring-purple-500/30'
                : 'bg-white border-slate-200 hover:border-purple-300'
            }`}
          >
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">
                {isBangla ? 'স্কোপ ৩ (পরিবহন)' : 'Scope 3 (Freight)'}
              </span>
              <span className="text-[10px] font-bold text-purple-800 bg-purple-100 px-1.5 py-0.5 rounded">
                {selectedScopeFilter === 'Scope 3' ? (isBangla ? 'ফিল্টারড' : 'Active') : 'Scope 3'}
              </span>
            </div>
            <p className="text-2xl font-black text-purple-600">
              {isBangla ? toBanglaDigits(Number(snapshot.scope3Emissions ?? 0).toFixed(2)) : Number(snapshot.scope3Emissions ?? 0).toFixed(2)}{' '}
              <span className="text-xs font-semibold text-slate-400">tCO₂e</span>
            </p>
          </div>
        </div>
      </div>

      {/* Click-to-Source Traceability Ledger */}
      <div className="bg-white p-5 rounded-2xl border border-slate-200/90 shadow-sm space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b pb-3">
          <div>
            <div className="flex items-center gap-2">
              <h3 className="font-bold text-slate-900 text-sm md:text-base">
                {isBangla
                  ? 'ক্লিক-টু-সোর্স প্রমাণপত্র লেজার (Click-to-Source Traceability Ledger)'
                  : 'Click-to-Source Traceability Ledger'}
              </h3>
              {selectedScopeFilter !== 'ALL' && (
                <span className="text-xs font-bold px-2 py-0.5 rounded bg-teal-100 text-teal-800">
                  {selectedScopeFilter}
                </span>
              )}
            </div>
            <p className="text-xs text-slate-500 mt-0.5">
              {isBangla
                ? 'যেকোনো সারিতে ক্লিক করে মূল চালান, OCR কনফিডেন্স ও প্রয়োগকৃত ফ্যাক্টর যাচাই করুন'
                : 'Click any row to inspect original supplier invoice, OCR score, and emission factor citation'}
            </p>
          </div>

          <div className="flex items-center gap-2 flex-wrap">
            <div className="relative">
              <Search size={14} className="absolute left-3 top-2.5 text-slate-400" />
              <input
                type="text"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder={isBangla ? 'কোম্পানি, বিল বা বিভাগ খুঁজুন...' : 'Filter by bill, company, division...'}
                className="pl-8 pr-3 py-1.5 text-xs border border-slate-300 rounded-xl bg-slate-50 focus:outline-hidden focus:ring-2 focus:ring-teal-600"
              />
            </div>

            <button
              onClick={handleExportCsv}
              title={isBangla ? 'লেজার ডেটা CSV আকারে ডাউনলোড করুন' : 'Export Auditor Ledger to CSV'}
              className="px-3 py-1.5 border border-slate-200 hover:bg-slate-100 rounded-xl text-slate-700 font-bold text-xs transition cursor-pointer flex items-center gap-1.5"
            >
              <Download size={13} className="text-teal-700" />
              <span>{isBangla ? 'CSV ডাউনলোড' : 'Export CSV'}</span>
            </button>

            <button
              onClick={loadData}
              title={isBangla ? 'লেজার রিফ্রেশ করুন' : 'Refresh Ledger'}
              className="p-2 border border-slate-200 hover:bg-slate-100 rounded-xl text-slate-600 transition cursor-pointer"
            >
              <RefreshCw size={14} />
            </button>
          </div>
        </div>

        {/* Ledger Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-500 font-bold uppercase tracking-wider text-[10px]">
                <th className="py-2.5 px-3">{isBangla ? 'স্কোপ' : 'Scope'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'বিল প্রদানকারী ও চালান' : 'Bill & Company'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'নির্গমন উৎস ও বিভাগ' : 'Activity Source & Division'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'ব্যবহারের পরিমাণ' : 'Quantity'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'আর্থিক মূল্য' : 'Billed (BDT)'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'নির্গমন ও গণনা (tCO₂e)' : 'tCO₂e & Breakdown'}</th>
                <th className="py-2.5 px-3">{isBangla ? 'চালান আইডি' : 'Document ID'}</th>
                <th className="py-2.5 px-3 text-right">{isBangla ? 'যাচাই' : 'Inspect'}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filteredItems.length === 0 ? (
                <tr>
                  <td colSpan={8} className="py-12 text-center text-slate-500">
                    <p className="font-semibold text-sm">
                      {isBangla ? 'কোন তথ্য পাওয়া যায়নি' : 'No matching activity records found'}
                    </p>
                    <p className="text-xs text-slate-400 mt-1">
                      {isBangla
                        ? 'অনুগ্রহ করে ভিন্ন কীওয়ার্ড দিয়ে অনুসন্ধান করুন অথবা ফিল্টার রিসেট করুন'
                        : 'Try adjusting your search query or clearing active scope filter'}
                    </p>
                    <button
                      onClick={() => {
                        setSearchTerm('');
                        setSelectedScopeFilter('ALL');
                      }}
                      className="mt-3 px-3 py-1.5 text-xs bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-lg transition cursor-pointer"
                    >
                      {isBangla ? 'ফিল্টার রিসেট করুন' : 'Reset Filters'}
                    </button>
                  </td>
                </tr>
              ) : (
                filteredItems.map((item) => {
                  const badge = getCompanyBadge(item.billCompany, item.documentTypeEn, item.sourceNameEn);
                  return (
                    <tr
                      key={item.id}
                      onClick={() => setSelectedItem(item)}
                      className="hover:bg-teal-50/50 cursor-pointer transition group"
                    >
                      <td className="py-3 px-3 font-bold text-slate-700">
                        <span className="px-2 py-0.5 bg-slate-100 rounded text-[10px]">
                          {item.scope}
                        </span>
                      </td>
                      <td className="py-3 px-3">
                        <div className="space-y-1">
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <span className={`px-2 py-0.5 rounded text-[10px] font-bold border ${badge.className}`}>
                              {badge.tag}
                            </span>
                            <span
                              className="font-bold text-slate-900 text-xs truncate max-w-[170px]"
                              title={item.billCompany || badge.name}
                            >
                              {item.billCompany || badge.name}
                            </span>
                          </div>
                          <div className="flex items-center gap-1.5 text-[10px] text-slate-500 font-mono">
                            <span
                              className="bg-slate-100 text-slate-700 px-1.5 py-0.5 rounded font-bold border border-slate-200/80"
                              title={`Bill/Invoice #: ${item.billNumber || item.documentId}`}
                            >
                              #{item.billNumber ? (item.billNumber.length > 18 ? item.billNumber.slice(0, 16) + '..' : item.billNumber) : item.documentId}
                            </span>
                            {item.meterNumber && (
                              <span className="text-slate-400" title={`Meter: ${item.meterNumber}`}>
                                Mtr: {item.meterNumber}
                              </span>
                            )}
                          </div>
                        </div>
                      </td>
                      <td className="py-3 px-3">
                        <div className="space-y-1">
                          <div className="font-semibold text-slate-900 text-xs">
                            {isBangla ? item.sourceNameBn : item.sourceNameEn}
                          </div>
                          <div className="flex items-center gap-1.5 flex-wrap text-[10px]">
                            <span
                              className="inline-flex items-center gap-1 text-slate-700 font-medium bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200"
                              title={isBangla ? `কোম্পানি: ${item.companyName || snapshot.organizationName}` : `Company: ${item.companyName || snapshot.organizationName}`}
                            >
                              <Building2 size={11} className="text-slate-500 shrink-0" />
                              <span className="truncate max-w-[130px]">{item.companyName || snapshot.organizationName}</span>
                            </span>
                            <span
                              className="inline-flex items-center gap-1 text-teal-800 font-medium bg-teal-50 px-1.5 py-0.5 rounded border border-teal-200"
                              title={isBangla ? `বিভাগ: ${item.divisionBn || 'ঢাকা বিভাগ'}` : `Division: ${item.division || 'Dhaka Division'}`}
                            >
                              <MapPin size={11} className="text-teal-600 shrink-0" />
                              <span>{isBangla ? (item.divisionBn || 'ঢাকা বিভাগ') : (item.division || 'Dhaka Division')}</span>
                            </span>
                          </div>
                        </div>
                      </td>
                      <td className="py-3 px-3 font-mono">
                        {isBangla
                          ? toBanglaDigits(item.activityQuantity.toLocaleString('en-US'))
                          : item.activityQuantity.toLocaleString('en-US')}{' '}
                        {item.activityUnit}
                      </td>
                      <td className="py-3 px-3">
                        {snapshot.redactPrices ? (
                          <span className="font-mono text-[10px] text-slate-400 font-bold bg-slate-100 px-1.5 py-0.5 rounded">
                            {isBangla ? '[গোপনীয়]' : '[Redacted]'}
                          </span>
                        ) : (
                          <span className="font-mono font-bold text-slate-800">
                            {formatBdt(item.billedAmountBdt || 0, { useBanglaDigits: isBangla })}
                          </span>
                        )}
                      </td>
                      <td className="py-3 px-3 font-mono">
                        <div className="space-y-1">
                          <div className="flex items-baseline gap-1">
                            <span className="text-sm font-black text-slate-900">
                              {isBangla ? toBanglaDigits(Number(item.emissionsTco2e ?? 0).toFixed(2)) : Number(item.emissionsTco2e ?? 0).toFixed(2)}
                            </span>
                            <span className="text-[10px] font-bold text-slate-400">tCO₂e</span>
                          </div>
                          <div
                            className="text-[10px] text-teal-900 bg-teal-50/90 px-1.5 py-0.5 rounded border border-teal-200/80 inline-flex items-center gap-1 whitespace-nowrap font-medium"
                            title={item.calculationFormula || `${item.activityQuantity} ${item.activityUnit} × ${item.factorValue} ÷ 1,000 = ${item.emissionsTco2e} tCO₂e`}
                          >
                            <span className="text-teal-600 font-bold">∑</span>
                            <span>
                              {isBangla
                                ? `${toBanglaDigits(item.activityQuantity.toLocaleString('en-US'))} ${item.activityUnit} × ${toBanglaDigits(item.factorValue.toString())} ÷ ১,০০০`
                                : `${item.activityQuantity.toLocaleString('en-US')} ${item.activityUnit} × ${item.factorValue} ÷ 1k`}
                            </span>
                          </div>
                        </div>
                      </td>
                      <td className="py-3 px-3 font-mono text-[11px] text-teal-800 font-bold">
                        {item.documentId}
                      </td>
                      <td className="py-3 px-3 text-right">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setSelectedItem(item);
                          }}
                          className="px-2.5 py-1 text-xs font-bold text-teal-800 bg-teal-50 border border-teal-200 rounded-lg hover:bg-teal-100 transition inline-flex items-center gap-1 group-hover:bg-teal-600 group-hover:text-white group-hover:border-teal-600 cursor-pointer"
                        >
                          <FileSearch size={12} />
                          {isBangla ? 'উৎস দেখুন' : 'Trace'}
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Mandatory GHG Protocol Disclaimer */}
      <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-600 flex items-start gap-2.5">
        <Info size={16} className="text-teal-700 shrink-0 mt-0.5" />
        <p className="leading-relaxed">
          {isBangla
            ? 'এই অডিট প্রতিবেদনটি GHG Protocol Corporate Standard পদ্ধতি অনুসারে যাচাইকৃত প্রাইমারি ইউটিলিটি ইনভয়েসের ভিত্তিতে প্রস্তুতকৃত। এটি তৃতীয়-পক্ষের প্রত্যয়িত সনদ নয়।'
            : 'This audit summary reflects verified primary utility records calculated in alignment with GHG Protocol Corporate Accounting Standard. Not an accredited third-party certification.'}
        </p>
      </div>

      {/* Click-to-Source Inspector Modal */}
      {selectedItem && (
        <ProvenanceInspectorModal
          item={selectedItem}
          redactPrices={snapshot.redactPrices}
          isBangla={isBangla}
          onClose={() => setSelectedItem(null)}
        />
      )}

      {/* Scopes & Audit Methodology Guide Modal */}
      {methodologyOpen && (
        <AuditMethodologyModal
          snapshot={snapshot}
          isBangla={isBangla}
          initialTab={methodologyTab}
          onClose={() => setMethodologyOpen(false)}
        />
      )}
    </div>
  );
};
