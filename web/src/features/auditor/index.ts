export * from './types';
export * from './auditorApi';
export * from './AuditorView';
export * from './components/ProvenanceInspectorModal';

import { AuditorView } from './AuditorView';

export const auditorRoutes = [
  {
    path: '/auditor',
    component: AuditorView,
  },
];
