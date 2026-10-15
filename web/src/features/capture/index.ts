import { lazy } from 'react';

export const CaptureScreen = lazy(() => import('./CaptureScreen'));
export * from './imageProcessing';
export * from './offlineCaptureQueue';
export * from './pushSubscription';

export const captureRoutes = [
  {
    path: '/floor',
    component: CaptureScreen,
  },
  {
    path: '/capture',
    component: CaptureScreen,
  },
];
