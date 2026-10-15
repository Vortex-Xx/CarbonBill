import React, { useState, useEffect, useCallback, lazy, Suspense } from 'react';
import { useTranslation } from 'react-i18next';
import { getStoredUser, AuthUser, clearSession } from '../shared';
import { ErrorBoundary } from '../shared/components/ErrorBoundary';
import LoginView from '../routes/main/LoginView';
import DashboardView from '../routes/main/DashboardView';

// Code-split floor route for ultra-fast loading on low-end Android mobile devices
const FloorStaffView = lazy(() => import('../routes/floor/FloorStaffView'));
const ReviewWorkspace = lazy(() => import('../features/review/ReviewWorkspace'));

export type AppView = 'login' | 'dashboard' | 'floor' | 'review';

export default function App() {
  const { t, i18n } = useTranslation();
  const [user, setUser] = useState<AuthUser | null>(null);
  const [view, setView] = useState<AppView>('login');

  // Synchronize view state from URL query parameters and stored user session
  const syncViewFromUrl = useCallback(() => {
    const storedUser = getStoredUser();
    setUser(storedUser);
    const params = new URLSearchParams(window.location.search);
    const mode = params.get('mode');

    if (mode === 'floor') {
      setView('floor');
    } else if (mode === 'review') {
      setView('review');
    } else {
      setView(storedUser ? 'dashboard' : 'login');
    }
  }, []);

  useEffect(() => {
    syncViewFromUrl();

    // Listen to browser & mouse Back and Forward navigation events
    window.addEventListener('popstate', syncViewFromUrl);
    return () => window.removeEventListener('popstate', syncViewFromUrl);
  }, [syncViewFromUrl]);

  // Navigate to a new view while pushing a history state entry
  const navigateToView = useCallback(
    (targetView: AppView, options?: { docId?: string; replace?: boolean }) => {
      const url = new URL(window.location.href);

      if (targetView === 'floor') {
        url.searchParams.set('mode', 'floor');
        url.searchParams.delete('docId');
      } else if (targetView === 'review') {
        url.searchParams.set('mode', 'review');
        if (options?.docId) {
          url.searchParams.set('docId', options.docId);
        }
      } else {
        url.searchParams.delete('mode');
        url.searchParams.delete('docId');
      }

      if (options?.replace) {
        window.history.replaceState({ mode: targetView }, '', url.toString());
      } else {
        window.history.pushState({ mode: targetView }, '', url.toString());
      }

      setView(targetView);
    },
    []
  );

  // Back handler for built-in back arrow buttons and esc/keyboard back
  const handleGoBack = useCallback(() => {
    // If the browser has history inside this session, use standard browser back
    if (window.history.state || window.history.length > 1) {
      window.history.back();
    } else {
      // Fallback if directly opened via URL/bookmark without prior history
      navigateToView(user ? 'dashboard' : 'login', { replace: true });
    }
  }, [navigateToView, user]);

  function handleLoginSuccess() {
    const updated = getStoredUser();
    setUser(updated);
    navigateToView('dashboard', { replace: true });
  }

  function handleLogout() {
    clearSession();
    setUser(null);
    navigateToView('login', { replace: true });
  }

  return (
    <ErrorBoundary>
      <div className="min-h-screen bg-slate-50 text-slate-900 font-sans">
        {view === 'floor' && (
          <Suspense
            fallback={
              <div className="min-h-screen flex items-center justify-center p-8 text-center text-sm font-semibold text-slate-500">
                {i18n.language === 'bn' ? 'ফ্লোর মোড লোড হচ্ছে...' : 'Loading Floor Mode...'}
              </div>
            }
          >
            <FloorStaffView
              onBack={handleGoBack}
              onGoToReview={(docId: string) => navigateToView('review', { docId })}
            />
          </Suspense>
        )}

        {view === 'review' && (
          <Suspense
            fallback={
              <div className="min-h-screen flex items-center justify-center p-8 text-center text-sm font-semibold text-slate-500">
                {i18n.language === 'bn' ? 'পর্যালোচনা ওয়ার্কস্পেস লোড হচ্ছে...' : 'Loading Review Workspace...'}
              </div>
            }
          >
            <ReviewWorkspace onBack={handleGoBack} />
          </Suspense>
        )}

        {view === 'login' && (
          <LoginView
            onLoginSuccess={handleLoginSuccess}
            onGoToFloor={() => navigateToView('floor')}
          />
        )}

        {view === 'dashboard' && user && (
          <DashboardView
            user={user}
            onGoToFloor={() => navigateToView('floor')}
            onGoToReview={() => navigateToView('review')}
            onLogout={handleLogout}
          />
        )}
      </div>
    </ErrorBoundary>
  );
}
