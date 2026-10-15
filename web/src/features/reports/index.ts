export * from './types';
export * from './reportsApi';
export * from './ReportsView';
export * from './components/ReadinessChecklist';
export * from './components/ReportPreview';
export * from './components/ShareLinkModal';

import { ReportsView } from './ReportsView';

export const reportsRoutes = [
  {
    path: '/reports',
    component: ReportsView,
  },
];
