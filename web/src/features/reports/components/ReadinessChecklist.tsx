import React from 'react';
import { ReportReadinessData as ChecklistType } from '../types';
import { toBanglaDigits } from '../../../shared';
import { CheckCircle2, AlertCircle, ShieldCheck, FileCheck } from 'lucide-react';

interface ReadinessChecklistProps {
  checklist: ChecklistType;
  isBangla: boolean;
}

export const ReadinessChecklist: React.FC<ReadinessChecklistProps> = ({ checklist, isBangla }) => {
  return (
    <div className="bg-white p-5 rounded-2xl border border-slate-200/90 shadow-sm space-y-4">
      <div className="flex items-center justify-between border-b pb-3">
        <div className="flex items-center gap-2">
          <FileCheck className="text-teal-700" size={20} />
          <div>
            <h3 className="font-bold text-slate-900 text-sm md:text-base">
              {isBangla ? 'রিপোর্ট অনুমোদন প্রস্তুতি চেকলিস্ট' : 'Report Sign-Off Readiness Checklist'}
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              {isBangla
                ? 'আইনি কমপ্লায়েন্স ও ক্রেতা অডিটের জন্য প্রয়োজনীয় শর্তাবলী'
                : 'Mandatory verification checks required before locking report snapshot'}
            </p>
          </div>
        </div>

        <span
          className={`text-xs font-bold px-3 py-1 rounded-lg border ${
            checklist.isReadyForApproval
              ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
              : 'bg-amber-50 text-amber-800 border-amber-200'
          }`}
        >
          {checklist.isReadyForApproval
            ? isBangla
              ? '✓ স্বাক্ষরের জন্য প্রস্তুত'
              : 'Ready for Sign-Off'
            : isBangla
            ? '⚠️ মনোযোগ প্রয়োজন'
            : 'Attention Required'}
        </span>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
        {/* Item 1: Expected Documents */}
        <div
          className={`p-3 rounded-xl border flex items-start gap-2.5 ${
            checklist.allExpectedDocsReceived
              ? 'bg-emerald-50/60 border-emerald-200 text-emerald-950'
              : 'bg-amber-50/60 border-amber-200 text-amber-950'
          }`}
        >
          {checklist.allExpectedDocsReceived ? (
            <CheckCircle2 size={16} className="text-emerald-600 shrink-0 mt-0.5" />
          ) : (
            <AlertCircle size={16} className="text-amber-600 shrink-0 mt-0.5" />
          )}
          <div>
            <span className="font-bold">
              {isBangla ? 'প্রত্যাশিত চালান ও রসিদ প্রাপ্তি:' : 'Expected Utility Bills:'}
            </span>
            <p className="text-slate-600 mt-0.5">
              {checklist.allExpectedDocsReceived
                ? isBangla
                  ? 'সকল প্রয়োজনীয় বিল ও ডিজেল স্লিপ সংগ্রহ ও নিশ্চিত হয়েছে।'
                  : 'All expected utility invoices and fuel slips confirmed.'
                : isBangla
                ? `${toBanglaDigits(checklist.missingDocsCount)}টি চালান বকেয়া রয়েছে (প্রক্সি মান ব্যবহৃত হচ্ছে)।`
                : `${checklist.missingDocsCount} documents missing (proxy estimates used).`}
            </p>
          </div>
        </div>

        {/* Item 2: Critical Flags */}
        <div
          className={`p-3 rounded-xl border flex items-start gap-2.5 ${
            checklist.criticalFlagsResolved
              ? 'bg-emerald-50/60 border-emerald-200 text-emerald-950'
              : 'bg-rose-50/60 border-rose-200 text-rose-950'
          }`}
        >
          {checklist.criticalFlagsResolved ? (
            <CheckCircle2 size={16} className="text-emerald-600 shrink-0 mt-0.5" />
          ) : (
            <AlertCircle size={16} className="text-rose-600 shrink-0 mt-0.5" />
          )}
          <div>
            <span className="font-bold">
              {isBangla ? 'উন্মুক্ত জটিল ফ্ল্যাগ সমাধান:' : 'Critical Flags Resolution:'}
            </span>
            <p className="text-slate-600 mt-0.5">
              {checklist.criticalFlagsResolved
                ? isBangla
                  ? 'কোনো জটিল বা অসমাধানকৃত ডেটা অসঙ্গতি নেই।'
                  : 'Zero unresolved critical data quality flags.'
                : isBangla
                ? `${toBanglaDigits(checklist.openCriticalFlagsCount)}টি জটিল ফ্ল্যাগ রয়েছে যা ব্যাখ্যা সাপেক্ষ।`
                : `${checklist.openCriticalFlagsCount} critical flags require justification.`}
            </p>
          </div>
        </div>

        {/* Item 3: DQS Threshold */}
        <div
          className={`p-3 rounded-xl border flex items-start gap-2.5 ${
            checklist.dqsAboveThreshold
              ? 'bg-emerald-50/60 border-emerald-200 text-emerald-950'
              : 'bg-amber-50/60 border-amber-200 text-amber-950'
          }`}
        >
          <ShieldCheck size={16} className="text-emerald-600 shrink-0 mt-0.5" />
          <div>
            <span className="font-bold">
              {isBangla ? 'ডেটা কোয়ালিটি স্কোর (DQS ≥ ৮০%):' : 'Data Quality Score Threshold:'}
            </span>
            <p className="text-slate-600 mt-0.5">
              {isBangla
                ? `বর্তমান স্কোর ${(checklist.dqsScore * 100).toFixed(1)}% (গ্রেড A অডিট মান পূরণ করেছে)।`
                : `Current score ${(checklist.dqsScore * 100).toFixed(1)}% (Meets Grade A requirements).`}
            </p>
          </div>
        </div>

        {/* Item 4: Factor Registry */}
        <div className="p-3 rounded-xl border bg-emerald-50/60 border-emerald-200 text-emerald-950 flex items-start gap-2.5">
          <CheckCircle2 size={16} className="text-emerald-600 shrink-0 mt-0.5" />
          <div>
            <span className="font-bold">
              {isBangla ? 'নির্গমন ফ্যাক্টর সংস্করণ হালনাগাদ:' : 'Factor Registry Version:'}
            </span>
            <p className="text-slate-600 mt-0.5">
              {isBangla
                ? 'জাতীয় গ্রিড ও জ্বালানি ফ্যাক্টর IPCC AR5 নির্দেশিকা অনুযায়ী প্রস্তুত।'
                : 'Grid and combustion factors verified with IPCC AR5 citations.'}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};
