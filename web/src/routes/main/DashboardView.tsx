import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { AuthUser, clearSession } from '../../lib/apiClient';
import {
  Leaf,
  LayoutDashboard,
  AlertCircle,
  TrendingDown,
  FileText,
  ShieldCheck,
  LogOut,
  Camera,
  ClipboardList,
} from 'lucide-react';
import { DashboardFeature } from '../../features/dashboard';
import { FlagsFeed } from '../../features/flags';
import { RecommendationsView } from '../../features/recommendations';
import { ReportsView } from '../../features/reports';
import { AuditorView } from '../../features/auditor';

export type MainTab = 'dashboard' | 'flags' | 'recommendations' | 'reports' | 'auditor';

interface DashboardViewProps {
  user: AuthUser;
  onGoToFloor: () => void;
  onGoToReview?: () => void;
  onLogout: () => void;
}

export default function DashboardView({ user, onGoToFloor, onGoToReview, onLogout }: DashboardViewProps) {
  const { t, i18n } = useTranslation();
  const isBangla = i18n.language === 'bn';

  const [activeTab, setActiveTab] = useState<MainTab>('dashboard');

  useEffect(() => {
    function syncTabFromUrl() {
      const params = new URLSearchParams(window.location.search);
      const tabParam = params.get('tab') as MainTab;
      if (tabParam && ['dashboard', 'flags', 'recommendations', 'reports', 'auditor'].includes(tabParam)) {
        setActiveTab(tabParam);
      } else {
        setActiveTab('dashboard');
      }
    }

    syncTabFromUrl();
    window.addEventListener('popstate', syncTabFromUrl);
    return () => window.removeEventListener('popstate', syncTabFromUrl);
  }, []);

  function handleTabChange(tab: MainTab) {
    if (tab === activeTab) return;
    setActiveTab(tab);
    const url = new URL(window.location.href);
    if (tab === 'dashboard') {
      url.searchParams.delete('tab');
    } else {
      url.searchParams.set('tab', tab);
    }
    window.history.pushState({ tab }, '', url.toString());
  }

  return (
    <div className="min-h-screen bg-slate-100 font-sans text-slate-900">
      {/* Top Main Navbar */}
      <header className="bg-white border-b border-slate-200 sticky top-0 z-20 shadow-2xs">
        <div className="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-teal-800 rounded-xl text-white shadow-xs">
              <Leaf size={22} />
            </div>
            <div>
              <h1 className="text-lg font-black text-teal-900">{t('appName')}</h1>
              <p className="text-xs text-slate-500 font-medium">{user.activeOrgName}</p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {/* Language Switcher */}
            <button
              onClick={() => i18n.changeLanguage(i18n.language === 'bn' ? 'en' : 'bn')}
              className="text-xs font-semibold px-2 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg border border-slate-200 transition"
            >
              {i18n.language === 'bn' ? 'English' : 'বাংলা'}
            </button>

            {/* Quick Mode Links */}
            <button
              onClick={onGoToFloor}
              className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 bg-teal-50 text-teal-800 border border-teal-200 text-xs font-bold rounded-xl hover:bg-teal-100 transition"
            >
              <Camera size={14} />
              {isBangla ? 'ফ্লোর মোড' : 'Floor PWA'}
            </button>

            {onGoToReview && (
              <button
                onClick={onGoToReview}
                className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 bg-indigo-50 text-indigo-800 border border-indigo-200 text-xs font-bold rounded-xl hover:bg-indigo-100 transition"
              >
                <ClipboardList size={14} />
                {isBangla ? 'রিভিউ কিউ' : 'Review Queue'}
              </button>
            )}

            {/* User Profile & Logout */}
            <div className="flex items-center gap-2 border-l pl-3">
              <div className="text-right">
                <p className="text-xs font-bold text-slate-800">{user.fullName}</p>
                <span className="text-[10px] text-teal-700 font-semibold px-1.5 py-0.5 bg-teal-50 rounded">
                  {user.activeOrgName || 'Apex Textiles'}
                </span>
              </div>
              <button
                onClick={() => {
                  clearSession();
                  onLogout();
                }}
                className="p-1.5 text-slate-400 hover:text-rose-600 rounded-lg transition"
                title={t('nav.logout')}
              >
                <LogOut size={18} />
              </button>
            </div>
          </div>
        </div>

        {/* Feature Navigation Tabs */}
        <div className="border-t border-slate-100 bg-white">
          <div className="max-w-6xl mx-auto px-4 flex items-center gap-1 overflow-x-auto py-1">
            <button
              onClick={() => handleTabChange('dashboard')}
              className={`px-3.5 py-2 text-xs font-bold rounded-xl transition flex items-center gap-1.5 whitespace-nowrap ${
                activeTab === 'dashboard'
                  ? 'bg-teal-900 text-white shadow-xs'
                  : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
              }`}
            >
              <LayoutDashboard size={14} />
              {isBangla ? 'ড্যাশবোর্ড' : 'Dashboard'}
            </button>

            <button
              onClick={() => handleTabChange('flags')}
              className={`px-3.5 py-2 text-xs font-bold rounded-xl transition flex items-center gap-1.5 whitespace-nowrap ${
                activeTab === 'flags'
                  ? 'bg-teal-900 text-white shadow-xs'
                  : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
              }`}
            >
              <AlertCircle size={14} />
              {isBangla ? 'কার্বন ফ্ল্যাগস' : 'Carbon Flags'}
              <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-amber-500 text-white font-bold ml-0.5">
                {isBangla ? '৩' : '3'}
              </span>
            </button>

            <button
              onClick={() => handleTabChange('recommendations')}
              className={`px-3.5 py-2 text-xs font-bold rounded-xl transition flex items-center gap-1.5 whitespace-nowrap ${
                activeTab === 'recommendations'
                  ? 'bg-teal-900 text-white shadow-xs'
                  : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
              }`}
            >
              <TrendingDown size={14} />
              {isBangla ? 'সাশ্রয়ী পদক্ষেপ (MACC)' : 'Recommendations (MACC)'}
            </button>

            <button
              onClick={() => handleTabChange('reports')}
              className={`px-3.5 py-2 text-xs font-bold rounded-xl transition flex items-center gap-1.5 whitespace-nowrap ${
                activeTab === 'reports'
                  ? 'bg-teal-900 text-white shadow-xs'
                  : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
              }`}
            >
              <FileText size={14} />
              {isBangla ? 'প্রতিবেদন ও এক্সপোর্ট' : 'Buyer Reports'}
            </button>

            <button
              onClick={() => handleTabChange('auditor')}
              className={`px-3.5 py-2 text-xs font-bold rounded-xl transition flex items-center gap-1.5 whitespace-nowrap ${
                activeTab === 'auditor'
                  ? 'bg-teal-900 text-white shadow-xs'
                  : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
              }`}
            >
              <ShieldCheck size={14} />
              {isBangla ? 'অডিটর পোর্টাল' : 'Auditor Portal'}
            </button>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="max-w-6xl mx-auto px-4 py-6">
        {activeTab === 'dashboard' && (
          <DashboardFeature
            user={user}
            onNavigateTab={handleTabChange}
            onGoToFloor={onGoToFloor}
            onGoToReview={onGoToReview}
          />
        )}

        {activeTab === 'flags' && <FlagsFeed onGoToReview={onGoToReview} />}

        {activeTab === 'recommendations' && <RecommendationsView />}

        {activeTab === 'reports' && <ReportsView />}

        {activeTab === 'auditor' && <AuditorView />}
      </main>
    </div>
  );
}
