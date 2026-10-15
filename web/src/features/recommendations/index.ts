export * from './types';
export * from './recommendationsApi';
export * from './RecommendationsView';
export * from './components/RecommendationCard';
export * from './components/MaccChart';
export * from './components/StatusChangeModal';

import { RecommendationsView } from './RecommendationsView';

export const recommendationsRoutes = [
  {
    path: '/recommendations',
    component: RecommendationsView,
  },
];
