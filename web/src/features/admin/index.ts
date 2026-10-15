export { AdminDashboard } from './AdminDashboard';

export const adminRoutes = [
  {
    path: '/admin',
    component: () => import('./AdminDashboard').then(m => ({ default: m.AdminDashboard })),
  },
];
