import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { authGuard, guestOnlyGuard } from './auth.guard';

describe('authGuard', () => {
  it('redirects an anonymous user to sign in', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isAuthenticated: () => false } }
      ]
    });
    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));
    expect((result as ReturnType<Router['createUrlTree']>).toString()).toBe('/sign-in');
  });
});

describe('guestOnlyGuard', () => {
  it('redirects an authenticated user to the dashboard', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isAuthenticated: () => true } }
      ]
    });
    const result = TestBed.runInInjectionContext(() => guestOnlyGuard({} as never, {} as never));
    expect((result as ReturnType<Router['createUrlTree']>).toString()).toBe('/dashboard');
  });
});
