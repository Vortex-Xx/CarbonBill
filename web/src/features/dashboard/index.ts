export * from './types';
export * from './dashboardApi';
export * from './DashboardFeature';
export * from './components/RoleDashboard';
export * from './components/TrendChart';
export * from './components/ScopeBreakdownChart';
export * from './components/IntensityChart';

import { DashboardFeature } from './DashboardFeature';

export const dashboardRoutes = [
  {
    path: '/dashboard',
    component: DashboardFeature,
  },
];
