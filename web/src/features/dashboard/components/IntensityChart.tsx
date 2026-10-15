import React from 'react';
import { IntensityData } from '../types';
import { toBanglaDigits } from '../../../shared';
import { Award, Info, AlertTriangle } from 'lucide-react';

interface IntensityChartProps {
  data: IntensityData;
  isBangla: boolean;
}

export const IntensityChart: React.FC<IntensityChartProps> = ({ data, isBangla }) => {
  const { benchmark, unit } = data;
  const vInt = Number(data.verifiedIntensity ?? 0);
  const iInt = Number(data.inclEstimateIntensity ?? 0);
  const pQty = Number(data.productionQuantity ?? 0);
  const hasBenchmark = Boolean(benchmark && typeof benchmark.n === 'number' && benchmark.n >= 10);

  return (
    <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-4">
      <div className="flex items-start justify-between border-b pb-3">
        <div>
          <h3 className="font-bold text-slate-900 text-sm md:text-base">
            {isBangla ? data.metricLabelBn : data.metricLabelEn}
          </h3>
          <p className="text-xs text-slate-500 mt-0.5">
            {isBangla
              ? `মাসিক মোট উৎপাদন: ${toBanglaDigits(pQty.toLocaleString('en-US'))} পিস`
              : `Monthly Production Output: ${pQty.toLocaleString('en-US')} pcs`}
          </p>
        </div>
        <span className="text-[11px] font-semibold px-2 py-0.5 bg-teal-50 text-teal-800 rounded-md border border-teal-200">
          {unit}
        </span>
      </div>

      {/* Main Intensity Numbers */}
      <div className="grid grid-cols-2 gap-3">
        <div className="p-3.5 bg-slate-50 border border-slate-200/70 rounded-xl space-y-1">
          <span className="text-[11px] font-semibold text-slate-500">
            {isBangla ? 'যাচাইকৃত ঘনত্ব (Verified)' : 'Verified Intensity'}
          </span>
          <div className="flex items-baseline gap-1">
            <span className="text-2xl font-black text-teal-900">
              {isBangla ? toBanglaDigits(vInt.toFixed(2)) : vInt.toFixed(2)}
            </span>
            <span className="text-[10px] text-slate-500">kg CO₂e</span>
          </div>
          <p className="text-[10px] text-emerald-700 font-medium">
            ✓ {isBangla ? 'প্রমাণিত বিল ও চালানের ভিত্তিতে' : 'Strictly audited source documents'}
          </p>
        </div>

        <div className="p-3.5 bg-slate-50 border border-slate-200/70 rounded-xl space-y-1">
          <span className="text-[11px] font-semibold text-slate-500">
            {isBangla ? 'অনুমিতসহ মোট ঘনত্ব (Incl. Proxy)' : 'Incl. Estimates Intensity'}
          </span>
          <div className="flex items-baseline gap-1">
            <span className="text-2xl font-black text-slate-800">
              {isBangla ? toBanglaDigits(iInt.toFixed(2)) : iInt.toFixed(2)}
            </span>
            <span className="text-[10px] text-slate-500">kg CO₂e</span>
          </div>
          <p className="text-[10px] text-amber-700 font-medium">
            ⚠️ {isBangla ? 'অনুপস্থিত বিলের অনুমিত প্রক্সি যুক্ত' : 'Includes proxy fallback for missing slips'}
          </p>
        </div>
      </div>

      {/* Peer Benchmark Section */}
      <div className="pt-2">
        <h4 className="text-xs font-bold text-slate-700 mb-2 flex items-center gap-1.5">
          <Award size={15} className="text-teal-700" />
          {isBangla ? 'পোশাক খাতের তুলনামূলক বেঞ্চমার্ক (Peer Comparison)' : 'Industry Peer Benchmark Distribution'}
        </h4>

        {hasBenchmark && benchmark ? (
          <div className="space-y-3 p-3.5 bg-teal-50/50 border border-teal-200/60 rounded-xl">
            <div className="flex items-center justify-between text-xs">
              <span className="text-slate-600 font-medium">
                {isBangla ? 'ক্লাস্টার নমুনা সংখ্যা:' : 'Sample Size:'}{' '}
                <strong className="text-slate-900">
                  {isBangla ? toBanglaDigits(benchmark.n) : benchmark.n}
                </strong>{' '}
                {isBangla ? 'টি সমমানের কারখানা' : 'peer factories'}
              </span>
              <span className="text-[10px] text-teal-800 font-bold bg-white px-2 py-0.5 rounded border border-teal-200">
                {benchmark.year}
              </span>
            </div>

            {/* Visual Percentile Distribution Line */}
            <div className="space-y-1.5">
              <div className="relative h-6 bg-slate-200 rounded-lg overflow-hidden flex text-[10px] font-bold text-white text-center">
                <div style={{ width: '25%' }} className="bg-emerald-600 flex items-center justify-center" title="Top 25% Quartile (P25)">
                  P25
                </div>
                <div style={{ width: '25%' }} className="bg-teal-500 flex items-center justify-center" title="Median 50% (P50)">
                  P50
                </div>
                <div style={{ width: '25%' }} className="bg-amber-500 flex items-center justify-center" title="75th Percentile (P75)">
                  P75
                </div>
                <div style={{ width: '25%' }} className="bg-rose-500 flex items-center justify-center" title="90th Percentile (P90)">
                  P90
                </div>
              </div>

              <div className="flex justify-between text-[10px] text-slate-500 font-mono">
                <span>{isBangla ? toBanglaDigits(Number(benchmark.p25 ?? 0).toFixed(1)) : Number(benchmark.p25 ?? 0).toFixed(1)}</span>
                <span>{isBangla ? toBanglaDigits(Number(benchmark.p50 ?? 0).toFixed(1)) : Number(benchmark.p50 ?? 0).toFixed(1)} (Median)</span>
                <span>{isBangla ? toBanglaDigits(Number(benchmark.p75 ?? 0).toFixed(1)) : Number(benchmark.p75 ?? 0).toFixed(1)}</span>
                <span>{isBangla ? toBanglaDigits(Number(benchmark.p90 ?? 0).toFixed(1)) : Number(benchmark.p90 ?? 0).toFixed(1)}</span>
              </div>
            </div>

            <div className="text-[11px] text-slate-600 flex items-center gap-1.5">
              <Info size={14} className="text-teal-700 flex-shrink-0" />
              <span>
                {isBangla
                  ? `আপনার কারখানার স্কোর মধ্যবর্তী মান (P50: ${toBanglaDigits(Number(benchmark.p50 ?? 0).toFixed(1))}) এর কাছাকাছি।`
                  : `Your factory is operating near the median peer intensity (P50: ${Number(benchmark.p50 ?? 0).toFixed(1)}).`}
              </span>
            </div>
          </div>
        ) : (
          /* Gating Rule: If n < 10, display Honest "No Benchmark Yet" banner */
          <div className="p-4 bg-amber-50/70 border border-dashed border-amber-300 rounded-xl space-y-1.5 text-center">
            <div className="flex items-center justify-center gap-1.5 text-amber-900 font-bold text-xs">
              <AlertTriangle size={16} className="text-amber-600" />
              <span>
                {isBangla ? 'যথেষ্ট পিয়ার ডেটা নেই (No benchmark yet)' : 'Insufficient Peer Data (No benchmark yet)'}
              </span>
            </div>
            <p className="text-[11px] text-slate-600 max-w-sm mx-auto">
              {isBangla
                ? 'নির্ভুল ও দায়িত্বশীল হিসাবের জন্য ন্যূনতম ১০টি সমমানের কারখানার অডিট ডেটা আবশ্যক। পর্যাপ্ত ডেটা জমলে স্বয়ংক্রিয়ভাবে বেঞ্চমার্ক সক্রিয় হবে।'
                : 'Honest reporting requires at least 10 verified peer factories in your sector cluster before publishing a statistical benchmark.'}
            </p>
          </div>
        )}
      </div>
    </div>
  );
};
