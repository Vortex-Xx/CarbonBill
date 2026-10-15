export { AuthScreen } from './AuthScreen';

export const authRoutes = [
  {
    path: '/login',
    component: () => import('./AuthScreen').then(m => ({ default: m.AuthScreen })),
  },
];
