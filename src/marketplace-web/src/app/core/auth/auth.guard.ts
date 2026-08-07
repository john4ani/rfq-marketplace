import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/sign-in']);

export const guestOnlyGuard: CanActivateFn = () =>
  !inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/dashboard']);

export const administratorGuard: CanActivateFn = () => {
  const auth=inject(AuthService);
  const router=inject(Router);
  return !auth.isAuthenticated()
    ? router.createUrlTree(['/sign-in'])
    : auth.isAdministrator() || router.createUrlTree(['/dashboard']);
};
