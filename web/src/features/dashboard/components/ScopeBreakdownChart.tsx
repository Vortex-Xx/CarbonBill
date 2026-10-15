import React, { useMemo } from 'react';
import { Doughnut } from 'react-chartjs-2';
import { ensureChartRegistered } from './ChartSetup';
import { toBanglaDigits } from '../../../shared';

ensureChartRegistered();

interface ScopeBreakdownChartProps {
  scope1?: number;
  scope2?: number;
  scope3?: number;
  isBangla: boolean;
}

export const ScopeBreakdownChart: React.FC<ScopeBreakdownChartProps> = ({
  scope1 = 0,
  scope2 = 0,
  scope3 = 0,
  isBangla,
}) => {
  const s1 = Number(scope1 || 0);
  const s2 = Number(scope2 || 0);
  const s3 = Number(scope3 || 0);
  const total = s1 + s2 + s3;

  const chartData = useMemo(() => {
    return {
      labels: [
        isBangla ? 'স্কোপ ১ (ডিজেল ও গ্যাস)' : 'Scope 1 (Fuels & Gas)',
        isBangla ? 'স্কোপ ২ (গ্রিড বিদ্যুৎ)' : 'Scope 2 (Grid Electricity)',
        isBangla ? 'স্কোপ ৩ (পরিবহন চালান)' : 'Scope 3 (Freight & Transport)',
      ],
      datasets: [
        {
          data: [s1, s2, s3],
          backgroundColor: ['#f97316', '#0284c7', '#8b5cf6'], // Orange, Blue, Violet
          borderWidth: 2,
          borderColor: '#ffffff',
          hoverOffset: 4,
        },
      ],
    };
  }, [s1, s2, s3, isBangla]);

  const options = useMemo(() => {
    return {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '72%',
      plugins: {
        legend: {
          display: false,
        },
        tooltip: {
          callbacks: {
            label: (context: any) => {
              const val = Number(context.parsed ?? 0);
              const pct = total > 0 ? ((val / total) * 100).toFixed(1) : '0';
              const formattedVal = isBangla ? toBanglaDigits(val.toFixed(2)) : val.toFixed(2);
              const formattedPct = isBangla ? toBanglaDigits(pct) : pct;
              return ` ${context.label}: ${formattedVal} tCO₂e (${formattedPct}%)`;
            },
          },
        },
      },
    };
  }, [total, isBangla]);

  const s1Pct = total > 0 ? ((s1 / total) * 100).toFixed(1) : '0';
  const s2Pct = total > 0 ? ((s2 / total) * 100).toFixed(1) : '0';
  const s3Pct = total > 0 ? ((s3 / total) * 100).toFixed(1) : '0';

  return (
    <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-4">
      <div className="border-b pb-3">
        <h3 className="font-bold text-slate-900 text-sm md:text-base">
          {isBangla ? 'স্কোপভিত্তিক নির্গমন বিভাজন' : 'Scope Emission Breakdown'}
        </h3>
        <p className="text-xs text-slate-500 mt-0.5">
          {isBangla ? 'GHG প্রোটোকল কর্পোরেট স্ট্যান্ডার্ড অনুযায়ী' : 'Aligned with GHG Protocol Corporate Standard'}
        </p>
      </div>

      <div className="relative h-52 w-full flex items-center justify-center">
        <Doughnut data={chartData} options={options} />
        {/* Center Readout */}
        <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
          <span className="text-2xl font-black text-slate-900">
            {isBangla ? toBanglaDigits(total.toFixed(2)) : total.toFixed(2)}
          </span>
          <span className="text-[11px] font-semibold text-slate-400">tCO₂e Total</span>
        </div>
      </div>

      <div className="space-y-2 pt-2 border-t border-slate-100 text-xs">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="w-2.5 h-2.5 rounded-full bg-orange-500"></span>
            <span className="font-semibold text-slate-700">
              {isBangla ? 'স্কোপ ১ (জ্বালানি/গ্যাস)' : 'Scope 1 (Fuels)'}
            </span>
          </div>
          <div className="font-mono text-slate-800">
            {isBangla ? toBanglaDigits(s1.toFixed(2)) : s1.toFixed(2)} tCO₂e (
            {isBangla ? toBanglaDigits(s1Pct) : s1Pct}%)
          </div>
        </div>

        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="w-2.5 h-2.5 rounded-full bg-sky-600"></span>
            <span className="font-semibold text-slate-700">
              {isBangla ? 'স্কোপ ২ (গ্রিড বিদ্যুৎ)' : 'Scope 2 (Electricity)'}
            </span>
          </div>
          <div className="font-mono text-slate-800">
            {isBangla ? toBanglaDigits(s2.toFixed(2)) : s2.toFixed(2)} tCO₂e (
            {isBangla ? toBanglaDigits(s2Pct) : s2Pct}%)
          </div>
        </div>

        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="w-2.5 h-2.5 rounded-full bg-purple-500"></span>
            <span className="font-semibold text-slate-700">
              {isBangla ? 'স্কোপ ৩ (পরিবহন)' : 'Scope 3 (Freight)'}
            </span>
          </div>
          <div className="font-mono text-slate-800">
            {isBangla ? toBanglaDigits(s3.toFixed(2)) : s3.toFixed(2)} tCO₂e (
            {isBangla ? toBanglaDigits(s3Pct) : s3Pct}%)
          </div>
        </div>
      </div>
    </div>
  );
};
