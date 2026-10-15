export { OnboardingWizard } from './OnboardingWizard';

export const onboardingRoutes = [
  {
    path: '/onboarding',
    component: () => import('./OnboardingWizard').then(m => ({ default: m.OnboardingWizard })),
  },
];
