import React, { useState } from 'react';
import { ReportItem, ShareLinkInfo } from '../types';
import { toBanglaDigits } from '../../../shared';
import { Share2, X, Copy, Check, Lock, Calendar, Eye, Shield } from 'lucide-react';

interface ShareLinkModalProps {
  report: ReportItem;
  isBangla: boolean;
  onClose: () => void;
  onCreateShareLink: (expiryDays: number, redactPrices: boolean) => Promise<ShareLinkInfo>;
}

export const ShareLinkModal: React.FC<ShareLinkModalProps> = ({
  report,
  isBangla,
  onClose,
  onCreateShareLink,
}) => {
  const [expiryDays, setExpiryDays] = useState<number>(30);
  const [redactPrices, setRedactPrices] = useState<boolean>(true);
  const [generatedLink, setGeneratedLink] = useState<ShareLinkInfo | null>(null);
  const [copied, setCopied] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(false);

  async function handleGenerate(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      const link = await onCreateShareLink(expiryDays, redactPrices);
      setGeneratedLink(link);
    } finally {
      setLoading(false);
    }
  }

  function handleCopy(url: string) {
    navigator.clipboard.writeText(url);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-slate-200 space-y-5">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-2.5">
            <div className="p-2 bg-teal-100 text-teal-800 rounded-xl">
              <Share2 size={20} />
            </div>
            <div>
              <h3 className="font-bold text-slate-900 text-base">
                {isBangla ? 'সুরক্ষিত অডিটর শেয়ার লিঙ্ক তৈরি করুন' : 'Generate Secure Auditor Share Link'}
              </h3>
              <p className="text-xs text-slate-500">
                {isBangla
                  ? `${report.period} হিসাবকালের অডিটর পোর্টাল লিঙ্ক`
                  : `Expiring read-only access for Period ${report.period}`}
              </p>
            </div>
          </div>
          <button onClick={onClose} className="p-1 text-slate-400 hover:text-slate-600 rounded-lg">
            <X size={18} />
          </button>
        </div>

        {/* Form Controls */}
        <form onSubmit={handleGenerate} className="space-y-4">
          {/* Expiry Selector */}
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1.5 flex items-center gap-1.5">
              <Calendar size={14} className="text-slate-500" />
              {isBangla ? 'লিঙ্কের মেয়াদ (Expiry Duration):' : 'Link Expiry Duration:'}
            </label>
            <div className="grid grid-cols-4 gap-2">
              {[7, 14, 30, 90].map((days) => (
                <button
                  key={days}
                  type="button"
                  onClick={() => setExpiryDays(days)}
                  className={`py-2 text-xs font-bold rounded-xl border transition ${
                    expiryDays === days
                      ? 'bg-teal-50 border-teal-600 text-teal-900 shadow-xs'
                      : 'border-slate-200 bg-slate-50 text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  {isBangla ? `${toBanglaDigits(days)} দিন` : `${days} Days`}
                </button>
              ))}
            </div>
          </div>

          {/* Redact Prices Toggle */}
          <div className="p-3.5 bg-slate-50 border border-slate-200 rounded-xl space-y-2">
            <label className="flex items-center justify-between cursor-pointer">
              <div className="flex items-center gap-2">
                <Shield size={16} className="text-teal-700" />
                <span className="text-xs font-bold text-slate-800">
                  {isBangla
                    ? 'বাণিজ্যিক মূল্য গোপন রাখুন (Redact Prices)'
                    : 'Redact Commercial Prices'}
                </span>
              </div>
              <input
                type="checkbox"
                checked={redactPrices}
                onChange={(e) => setRedactPrices(e.target.checked)}
                className="w-4 h-4 text-teal-600 rounded-sm focus:ring-teal-500"
              />
            </label>
            <p className="text-[11px] text-slate-500 leading-relaxed">
              {isBangla
                ? 'অনুমোদন থাকলে অডিটর প্রতিটি চালানের ফিজিক্যাল পরিমাণ (kWh, লিটার) ও নির্গমন ফ্যাক্টর দেখতে পাবেন, কিন্তু টাকার পরিমাণ [গোপনীয়] হিসেবে মুখোশিত থাকবে।'
                : 'Protects commercial sensitivity. Auditors will see physical quantities and emission factors, while monetary BDT amounts are masked with [Confidential].'}
            </p>
          </div>

          {!generatedLink && (
            <button
              type="submit"
              disabled={loading}
              className="w-full py-2.5 bg-teal-800 hover:bg-teal-900 text-white rounded-xl text-xs font-bold shadow-xs transition"
            >
              {loading
                ? isBangla
                  ? 'লিঙ্ক তৈরি হচ্ছে...'
                  : 'Generating Link...'
                : isBangla
                ? 'লিঙ্ক তৈরি করুন'
                : 'Create Share Link'}
            </button>
          )}
        </form>

        {/* Generated Link Display */}
        {generatedLink && (
          <div className="p-3.5 bg-teal-50/70 border border-teal-200 rounded-xl space-y-2">
            <span className="text-xs font-bold text-teal-950 flex items-center gap-1.5">
              ✓ {isBangla ? 'শেয়ার লিঙ্ক প্রস্তুত হয়েছে:' : 'Shareable Link Ready:'}
            </span>
            <div className="flex items-center gap-2">
              <input
                type="text"
                readOnly
                value={generatedLink.shareUrl}
                className="w-full p-2 bg-white text-xs border border-teal-300 rounded-lg font-mono text-slate-700"
              />
              <button
                type="button"
                onClick={() => handleCopy(generatedLink.shareUrl)}
                className="px-3 py-2 bg-teal-800 hover:bg-teal-900 text-white text-xs font-bold rounded-lg flex items-center gap-1 shrink-0 transition"
              >
                {copied ? <Check size={14} /> : <Copy size={14} />}
                {copied ? (isBangla ? 'কপি হয়েছে' : 'Copied') : isBangla ? 'কপি' : 'Copy'}
              </button>
            </div>
            <p className="text-[10px] text-teal-800">
              {isBangla
                ? `মেয়াদ শেষ: ${new Date(generatedLink.expiresAt).toLocaleDateString()} | মূল্য ফিল্টার: ${
                    generatedLink.redactPrices ? 'গোপন' : 'প্রকাশিত'
                  }`
                : `Expires: ${new Date(generatedLink.expiresAt).toLocaleDateString()} | Prices: ${
                    generatedLink.redactPrices ? 'Redacted' : 'Visible'
                  }`}
            </p>
          </div>
        )}

        {/* Active Share Links List */}
        {report.activeShareLinks.length > 0 && (
          <div className="pt-2 border-t border-slate-100 space-y-2">
            <h4 className="text-xs font-bold text-slate-700 flex items-center gap-1.5">
              <Eye size={14} className="text-slate-500" />
              {isBangla ? 'ইতিমধ্যে সক্রিয় শেয়ার লিঙ্কসমূহ:' : 'Existing Active Links:'}
            </h4>
            <div className="space-y-1.5 max-h-32 overflow-y-auto">
              {report.activeShareLinks.map((link, idx) => (
                <div
                  key={idx}
                  className="p-2 bg-slate-50 border border-slate-200 rounded-lg flex items-center justify-between text-xs"
                >
                  <div className="font-mono text-[11px] text-slate-600 truncate max-w-[240px]">
                    {link.token}
                  </div>
                  <div className="flex items-center gap-2 text-[10px] text-slate-500">
                    <span>
                      {isBangla ? 'ভিউ:' : 'Views:'}{' '}
                      <strong>{isBangla ? toBanglaDigits(link.viewCount) : link.viewCount}</strong>
                    </span>
                    <button
                      onClick={() => handleCopy(link.shareUrl)}
                      className="text-teal-700 font-bold hover:underline"
                    >
                      {isBangla ? 'কপি' : 'Copy'}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
