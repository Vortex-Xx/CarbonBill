import React, { useMemo } from 'react';
import { Bar } from 'react-chartjs-2';
import { Recommendation } from '../types';
import { ensureChartRegistered } from '../../dashboard/components/ChartSetup';
import { toBanglaDigits, formatBdt } from '../../../shared';

ensureChartRegistered();

interface MaccChartProps {
  recommendations: Recommendation[];
  isBangla: boolean;
}

export const MaccChart: React.FC<MaccChartProps> = ({ recommendations, isBangla }) => {
  // Sort measures by costPerTco2e ascending (lowest cost / most negative first)
  const sorted = useMemo(() => {
    return [...recommendations].sort((a, b) => a.costPerTco2e - b.costPerTco2e);
  }, [recommendations]);

  const chartData = useMemo(() => {
    const labels = sorted.map((r) =>
      isBangla ? r.titleBn.split(' (')[0].slice(0, 22) + '...' : r.titleEn.split(' (')[0].slice(0, 24) + '...'
    );
    const costs = sorted.map((r) => r.costPerTco2e);

    const backgroundColors = sorted.map((r) =>
      r.costPerTco2e < 0 ? '#059669' : '#0284c7' // Emerald for net savings, Sky for positive cost
    );

    return {
      labels,
      datasets: [
        {
          label: isBangla ? 'প্রতি টন কার্বন হ্রাসে খরচ (BDT / tCO₂e)' : 'Abatement Cost (BDT / tCO₂e)',
          data: costs,
          backgroundColor: backgroundColors,
          borderRadius: 6,
        },
      ],
    };
  }, [sorted, isBangla]);

  const options = useMemo(() => {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label: (context: any) => {
              const val = context.parsed.y;
              const formatted = formatBdt(Math.abs(val), { useBanglaDigits: isBangla });
              const label =
                val < 0
                  ? isBangla
                    ? `সাশ্রয়ী মুনাফা: ${formatted} / tCO₂e (Net Profit)`
                    : `Net Savings: ${formatted} / tCO₂e`
                  : isBangla
                  ? `নিট খরচ: ${formatted} / tCO₂e`
                  : `Net Cost: ${formatted} / tCO₂e`;
              return ` ${label}`;
            },
          },
        },
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: { font: { family: 'inherit', size: 10, weight: 600 } },
        },
        y: {
          grid: { color: '#e2e8f0' },
          ticks: {
            callback: (val: any) => (isBangla ? toBanglaDigits(val) : val),
            font: { family: 'inherit', size: 10 },
          },
          title: {
            display: true,
            text: isBangla ? 'খরচ (+) বা সাশ্রয় (-) [BDT / tCO₂e]' : 'Cost (+) or Profit (-) [BDT / tCO₂e]',
            font: { size: 11, weight: 600 },
          },
        },
      },
    };
  }, [isBangla]);

  return (
    <div className="bg-white p-5 rounded-2xl border border-slate-200/90 shadow-sm space-y-4">
      <div className="border-b pb-3 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
        <div>
          <h3 className="font-bold text-slate-900 text-sm md:text-base">
            {isBangla
              ? 'মার্জিনাল অ্যাবেটমেন্ট কস্ট কার্ভ (MACC Chart)'
              : 'Marginal Abatement Cost Curve (MACC)'}
          </h3>
          <p className="text-xs text-slate-500 mt-0.5">
            {isBangla
              ? 'শূন্য রেখার নিচের সবুজ বারগুলো কারখানায় সরাসরি টাকা সাশ্রয়কারী পদক্ষেপ'
              : 'Bars below zero line represent negative-cost interventions (yield net financial profit)'}
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs font-semibold">
          <span className="flex items-center gap-1.5 text-emerald-700">
            <span className="w-3 h-3 bg-emerald-600 rounded-sm"></span>
            {isBangla ? 'মুনাফাজনক সাশ্রয় (Below 0)' : 'Net Positive ROI'}
          </span>
          <span className="flex items-center gap-1.5 text-sky-700">
            <span className="w-3 h-3 bg-sky-600 rounded-sm"></span>
            {isBangla ? 'মূলধনী বিনিয়োগ (Above 0)' : 'Net Capex Required'}
          </span>
        </div>
      </div>

      <div className="h-64 w-full">
        <Bar data={chartData} options={options} />
      </div>

      <p className="text-[11px] text-slate-500 pt-1 border-t border-slate-100">
        {isBangla
          ? '💡 লক্ষ্য করুন: পাওয়ার ফ্যাক্টর এবং এয়ার পাইপলাইন লিক মেরামত সবচেয়ে দ্রুত অর্থ সাশ্রয়কারী পদক্ষেপ।'
          : '💡 Notice: Power factor correction and compressed air leak sealing generate immediate net returns with zero long-term penalty.'}
      </p>
    </div>
  );
};
