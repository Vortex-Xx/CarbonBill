export { ConsultantWorkspace } from './ConsultantWorkspace';

export const consultantRoutes = [
  {
    path: '/consultant',
    component: () => import('./ConsultantWorkspace').then(m => ({ default: m.ConsultantWorkspace })),
  },
];
