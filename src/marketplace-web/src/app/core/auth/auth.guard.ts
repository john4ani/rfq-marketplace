import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { FeaturesService } from '../features/features.service';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.initialize();
  return auth.isAuthenticated() || router.createUrlTree(['/sign-in']);
};

export const guestOnlyGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.initialize();
  return !auth.isAuthenticated() || router.createUrlTree(['/dashboard']);
};

/**
 * Blocks every marketplace route (live market, RFQs, offers, profiles, dashboard) when the
 * MarketPlace:Enabled feature flag is false (MVP: only Anunțuri SEAP + Admin remain). The route
 * owner decides the fallback: marketplace routes redirect to Anunțuri SEAP; the public landing stays.
 */
export const marketplaceGuard: CanActivateFn = async () => {
  const features = inject(FeaturesService);
  const router = inject(Router);
  await features.initialize();
  return features.marketplaceEnabled() || router.createUrlTree(['/seap/tenders']);
};

export const administratorGuard: CanActivateFn = async () => {
  const auth=inject(AuthService);
  const router=inject(Router);
  await auth.initialize();
  return !auth.isAuthenticated()
    ? router.createUrlTree(['/sign-in'])
    : auth.isAdministrator() || router.createUrlTree(['/dashboard']);
};

export const buyerGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.initialize();
  return auth.isAuthenticated() || router.createUrlTree(['/sign-in']);
};
