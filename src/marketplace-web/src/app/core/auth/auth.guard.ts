import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

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
  await auth.waitForProfiles();
  return !auth.isAuthenticated()
    ? router.createUrlTree(['/sign-in'])
    : auth.profiles().some(profile => profile.accountType === 'Buyer')
      ? true
      : router.createUrlTree(['/dashboard']);
};
