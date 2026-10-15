export interface CompanyBadgeInfo {
  tag: string;
  name: string;
  className: string;
}

export const getCompanyBadge = (
  company?: string,
  docType?: string,
  sourceName?: string
): CompanyBadgeInfo => {
  const c = `${company || ''} ${docType || ''} ${sourceName || ''}`.toLowerCase();
  if (c.includes('bpdb') || c.includes('বিপিডিবি') || c.includes('power development')) {
    return {
      tag: 'BPDB',
      name: company || 'Bangladesh Power Development Board (BPDB)',
      className: 'bg-amber-100 text-amber-900 border-amber-300',
    };
  }
  if (c.includes('desco') || c.includes('ডেসকো')) {
    return {
      tag: 'DESCO',
      name: company || 'Dhaka Electric Supply Company (DESCO)',
      className: 'bg-sky-100 text-sky-900 border-sky-300',
    };
  }
  if (c.includes('dpdc') || c.includes('ডিপিডিসি')) {
    return {
      tag: 'DPDC',
      name: company || 'Dhaka Power Distribution Company (DPDC)',
      className: 'bg-indigo-100 text-indigo-900 border-indigo-300',
    };
  }
  if (c.includes('reb') || c.includes('পল্লী বিদ্যুৎ') || c.includes('breb')) {
    return {
      tag: 'BREB',
      name: company || 'Bangladesh Rural Electrification Board (BREB)',
      className: 'bg-teal-100 text-teal-900 border-teal-300',
    };
  }
  if (c.includes('titas') || c.includes('তিতাস') || c.includes('gas')) {
    return {
      tag: 'Titas Gas',
      name: company || 'Titas Gas Transmission & Distribution Co. Ltd.',
      className: 'bg-emerald-100 text-emerald-900 border-emerald-300',
    };
  }
  if (c.includes('padma') || c.includes('পদ্মা') || c.includes('diesel') || c.includes('ডিজেল')) {
    return {
      tag: 'Padma Oil',
      name: company || 'Padma Oil Company Limited',
      className: 'bg-purple-100 text-purple-900 border-purple-300',
    };
  }
  if (c.includes('phed')) {
    return {
      tag: 'PHED',
      name: company || 'PHED Electricity Utility',
      className: 'bg-cyan-100 text-cyan-900 border-cyan-300',
    };
  }
  if (c.includes('meralco')) {
    return {
      tag: 'Meralco',
      name: company || 'Meralco Power',
      className: 'bg-orange-100 text-orange-900 border-orange-300',
    };
  }
  return {
    tag: company ? company.substring(0, 10).toUpperCase() : 'UTILITY',
    name: company || 'Utility Provider',
    className: 'bg-slate-100 text-slate-800 border-slate-300',
  };
};
