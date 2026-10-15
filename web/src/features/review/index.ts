import { ReviewWorkspace } from './ReviewWorkspace';

export { ReviewWorkspace };
export * from './reviewApi';
export * from './DocumentImageViewer';
export * from './ReviewFieldsForm';
export * from './ReviewQueueSidebar';

export const reviewRoutes = [
  {
    path: '/review',
    component: ReviewWorkspace,
  },
];
