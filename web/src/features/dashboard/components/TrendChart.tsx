import React, { useMemo } from 'react';
import { Bar } from 'react-chartjs-2';
import { TrendDataPoint } from '../types';
import { ensureChartRegistered, createHatchPattern } from './ChartSetup';
import { toBanglaDigits } from '../../../shared';

ensureChartRegistered();

interface TrendChartProps {
  data: TrendDataPoint[];
  isBangla: boolean;
}

export const TrendChart: React.FC<TrendChartProps> = ({ data, isBangla }) => {
  const chartData = useMemo(() => {
    const labels = data.map((d) => (isBangla ? d.periodLabelBn : d.periodLabelEn));
    const verifiedValues = data.map((d) => d.verifiedEmissions);
    const estimatedValues = data.map((d) => d.estimatedEmissions);

    const hatch = createHatchPattern('#d97706', '#fef3c7');

    return {
      labels,
      datasets: [
        {
          label: isBangla ? 'যাচাইকৃত (Verified)' : 'Verified Activity',
          data: verifiedValues,
          backgroundColor: '#0f766e', // Teal-700
          borderRadius: 4,
          stack: 'emissions',
        },
        {
          label: isBangla ? 'আনুমানিক (Estimated - হ্যাচড)' : 'Estimated (Hatched)',
          data: estimatedValues,
          backgroundColor: hatch,
          borderColor: '#b45309',
          borderWidth: 1,
          borderRadius: 4,
          stack: 'emissions',
        },
      ],
    };
  }, [data, isBangla]);

  const options = useMemo(() => {
    return {
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        mode: 'index' as const,
        intersect: false,
      },
      plugins: {
        legend: {
          position: 'top' as const,
          labels: {
            font: { family: 'inherit', size: 12, weight: 600 },
            usePointStyle: true,
            boxWidth: 10,
          },
        },
        tooltip: {
          callbacks: {
            label: (context: any) => {
              const val = context.parsed.y;
              const formatted = isBangla ? toBanglaDigits(val.toFixed(1)) : val.toFixed(1);
              return ` ${context.dataset.label}: ${formatted} tCO₂e`;
            },
          },
        },
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: { font: { family: 'inherit', size: 11, weight: 600 } },
        },
        y: {
          stacked: true,
          grid: { color: '#f1f5f9' },
          ticks: {
            callback: (val: any) => (isBangla ? toBanglaDigits(val) : val),
            font: { family: 'inherit', size: 11 },
          },
          title: {
            display: true,
            text: isBangla ? 'নির্গমন (tCO₂e)' : 'Emissions (tCO₂e)',
            font: { size: 11, weight: 600 },
          },
        },
      },
    };
  }, [isBangla]);

  const hasHatchedPoint = data.some((d) => d.hatchFlag);

  return (
    <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-sm space-y-3">
      <div className="flex items-center justify-between border-b pb-3">
        <div>
          <h3 className="font-bold text-slate-900 text-sm md:text-base">
            {isBangla ? 'মাসিক নির্গমন ট্রেন্ড (৬ মাস)' : 'Monthly Footprint Trend (6 Months)'}
          </h3>
          <p className="text-xs text-slate-500 mt-0.5">
            {isBangla
              ? 'স্কোপ ১, ২ ও ৩ এর সম্মিলিত হিসাব (যাচাইকৃত ও অনুমিত বিভাজন)'
              : 'Combined Scope 1, 2 & 3 (Verified vs Estimated split)'}
          </p>
        </div>
        {hasHatchedPoint && (
          <span className="text-[11px] font-semibold text-amber-800 bg-amber-50 border border-amber-200 px-2.5 py-1 rounded-lg">
            ⚠️ {isBangla ? 'আগস্ট মাসে অনুমিত অংশ > ১০%' : 'Aug has > 10% estimated share'}
          </span>
        )}
      </div>

      <div className="h-64 w-full">
        <Bar data={chartData} options={options} />
      </div>

      <div className="pt-2 flex items-center justify-between text-[11px] text-slate-500 border-t border-slate-100">
        <span>
          {isBangla
            ? '💡 হ্যাচড (ডোরাকাটা) অংশটি অনুপস্থিত বিলের কারণে অনুমোদিত অ্যালগরিদমে আনুমানিক ধরা হয়েছে।'
            : '💡 Hatched bar indicates portions calculated using standard proxy estimates due to missing bills.'}
        </span>
      </div>
    </div>
  );
};
