import React, { useState } from 'react';
import { Flag, FlagSeverity } from '../types';
import { toBanglaDigits } from '../../../shared';
import {
  AlertCircle,
  AlertTriangle,
  Info,
  CheckCircle2,
  Clock,
  ChevronDown,
  ChevronUp,
  FileText,
  ExternalLink,
  ShieldAlert,
} from 'lucide-react';

interface FlagCardProps {
  flag: Flag;
  isBangla: boolean;
  onAcknowledge: (id: string) => void;
  onOpenDismiss: (flag: Flag) => void;
  onOpenSnooze: (flag: Flag) => void;
  onGoToEvidence?: (documentId?: string) => void;
}

export const FlagCard: React.FC<FlagCardProps> = ({
  flag,
  isBangla,
  onAcknowledge,
  onOpenDismiss,
  onOpenSnooze,
  onGoToEvidence,
}) => {
  const [showDetails, setShowDetails] = useState<boolean>(false);

  const severityConfig: Record<
    FlagSeverity,
    { bg: string; border: string; text: string; badge: string; icon: React.ReactNode }
  > = {
    Critical: {
      bg: 'bg-rose-50/70',
      border: 'border-rose-200',
      text: 'text-rose-900',
      badge: 'bg-rose-100 text-rose-800 border-rose-300',
      icon: <ShieldAlert size={18} className="text-rose-600" />,
    },
    Warning: {
      bg: 'bg-amber-50/70',
      border: 'border-amber-200',
      text: 'text-amber-900',
      badge: 'bg-amber-100 text-amber-800 border-amber-300',
      icon: <AlertTriangle size={18} className="text-amber-600" />,
    },
    Info: {
      bg: 'bg-sky-50/70',
      border: 'border-sky-200',
      text: 'text-sky-900',
      badge: 'bg-sky-100 text-sky-800 border-sky-300',
      icon: <Info size={18} className="text-sky-600" />,
    },
  };

  const currentCfg = severityConfig[flag.severity];

  return (
    <div
      className={`rounded-2xl border p-4 transition-all duration-200 ${currentCfg.bg} ${currentCfg.border} shadow-xs space-y-3`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-2.5">
          <div className="p-1.5 bg-white rounded-xl shadow-xs mt-0.5">
            {currentCfg.icon}
          </div>
          <div>
            <div className="flex items-center gap-2 flex-wrap">
              <span
                className={`text-[10px] font-black uppercase tracking-wider px-2 py-0.5 rounded-md border ${currentCfg.badge}`}
              >
                {flag.severity}
              </span>
              <span className="text-[11px] font-mono text-slate-500 font-semibold">
                {flag.ruleCode}
              </span>
              {flag.state !== 'Open' && (
                <span className="text-[10px] font-bold px-2 py-0.5 bg-slate-200 text-slate-700 rounded-md">
                  {flag.state}
                </span>
              )}
            </div>
            <h4 className="font-bold text-slate-900 text-sm mt-1">
              {isBangla ? flag.titleBn : flag.titleEn}
            </h4>
          </div>
        </div>

        <span className="text-[11px] font-mono text-slate-500 font-semibold shrink-0">
          {flag.period}
        </span>
      </div>

      {/* Explanation */}
      <p className="text-xs text-slate-700 leading-relaxed">
        {isBangla ? flag.explanationBn : flag.explanationEn}
      </p>

      {/* Evidence Pills */}
      {flag.evidence && (
        <div className="flex flex-wrap gap-2 text-[11px]">
          {flag.evidence.assetName && (
            <span className="bg-white/80 border border-slate-200/80 px-2.5 py-1 rounded-lg text-slate-700 font-medium">
              🏭 {flag.evidence.assetName}
            </span>
          )}
          {flag.evidence.measuredValue !== undefined && (
            <span className="bg-white/80 border border-slate-200/80 px-2.5 py-1 rounded-lg text-slate-700 font-medium font-mono">
              📊 {isBangla ? toBanglaDigits(flag.evidence.measuredValue) : flag.evidence.measuredValue}{' '}
              {flag.evidence.unit || ''}
            </span>
          )}
          {flag.evidence.documentId && (
            <span className="bg-white/80 border border-slate-200/80 px-2.5 py-1 rounded-lg text-slate-700 font-medium font-mono flex items-center gap-1">
              <FileText size={12} /> {flag.evidence.documentId}
            </span>
          )}
        </div>
      )}

      {/* Action / "What to do" drawer */}
      {showDetails && (
        <div className="p-3 bg-white rounded-xl border border-slate-200 text-xs space-y-2">
          <div>
            <span className="font-bold text-slate-800">
              💡 {isBangla ? 'কী করতে হবে (Suggested Action):' : 'Suggested Action:'}
            </span>
            <p className="text-slate-600 mt-0.5">
              {isBangla ? flag.suggestedActionBn : flag.suggestedActionEn}
            </p>
          </div>

          {flag.evidence.documentId && (
            <div className="pt-2 border-t border-slate-100 flex items-center justify-between">
              <span className="text-[11px] text-slate-500">
                {isBangla ? 'চালান সরাসরি যাচাই করুন:' : 'Inspect original document:'}
              </span>
              <button
                onClick={() => onGoToEvidence && onGoToEvidence(flag.evidence.documentId)}
                className="text-xs font-bold text-teal-700 hover:text-teal-900 inline-flex items-center gap-1"
              >
                {isBangla ? 'রিভিউ কিউ খুলুন' : 'Open in Review Queue'} <ExternalLink size={12} />
              </button>
            </div>
          )}
        </div>
      )}

      {/* Footer Buttons */}
      <div className="flex items-center justify-between pt-1 border-t border-slate-200/60 text-xs">
        <button
          onClick={() => setShowDetails(!showDetails)}
          className="text-slate-600 hover:text-slate-900 font-bold flex items-center gap-1 py-1"
        >
          {showDetails ? (
            <>
              {isBangla ? 'সংক্ষিপ্ত করুন' : 'Hide Action'} <ChevronUp size={14} />
            </>
          ) : (
            <>
              {isBangla ? 'কী করতে হবে?' : 'What to do?'} <ChevronDown size={14} />
            </>
          )}
        </button>

        <div className="flex items-center gap-2">
          {flag.state === 'Open' && (
            <>
              <button
                onClick={() => onAcknowledge(flag.id)}
                className="px-2.5 py-1 text-slate-700 hover:bg-white rounded-lg border border-transparent hover:border-slate-300 font-semibold transition"
                title="Acknowledge Flag"
              >
                ✓ {isBangla ? 'স্বীকৃতি' : 'Acknowledge'}
              </button>
              <button
                onClick={() => onOpenSnooze(flag)}
                className="px-2.5 py-1 text-slate-700 hover:bg-white rounded-lg border border-transparent hover:border-slate-300 font-semibold transition flex items-center gap-1"
                title="Snooze"
              >
                <Clock size={12} /> {isBangla ? 'স্থগিত' : 'Snooze'}
              </button>
              <button
                onClick={() => onOpenDismiss(flag)}
                className="px-2.5 py-1 text-rose-700 hover:bg-rose-100/80 rounded-lg font-semibold transition"
                title="Dismiss with reason"
              >
                ✕ {isBangla ? 'বাতিল' : 'Dismiss'}
              </button>
            </>
          )}

          {flag.state === 'Acknowledged' && (
            <span className="text-[11px] text-teal-700 font-bold flex items-center gap-1">
              <CheckCircle2 size={13} /> {isBangla ? 'স্বীকৃত' : 'Acknowledged'}
            </span>
          )}

          {flag.state === 'Dismissed' && (
            <span className="text-[11px] text-slate-500 italic">
              {isBangla ? '৩০ দিনের জন্য স্থগিত' : 'Dismissed (30-day expiry)'}
            </span>
          )}
        </div>
      </div>
    </div>
  );
};
