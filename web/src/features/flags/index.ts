export * from './types';
export * from './flagsApi';
export * from './FlagsFeed';
export * from './components/FlagCard';
export * from './components/DismissModal';
export * from './components/SnoozeModal';

import { FlagsFeed } from './FlagsFeed';

export const flagsRoutes = [
  {
    path: '/flags',
    component: FlagsFeed,
  },
];
