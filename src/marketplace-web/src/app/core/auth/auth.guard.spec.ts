import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { authGuard, guestOnlyGuard } from './auth.guard';

describe('authGuard', () => {
  it('redirects an anonymous user to sign in after session recovery', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { initialize: () => Promise.resolve(), isAuthenticated: () => false } }
      ]
    });
    const result = await TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));
    expect((result as ReturnType<Router['createUrlTree']>).toString()).toBe('/sign-in');
  });
});

describe('guestOnlyGuard', () => {
  it('redirects an authenticated user to the dashboard after session recovery', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { initialize: () => Promise.resolve(), isAuthenticated: () => true } }
      ]
    });
    const result = await TestBed.runInInjectionContext(() => guestOnlyGuard({} as never, {} as never));
    expect((result as ReturnType<Router['createUrlTree']>).toString()).toBe('/dashboard');
  });
});
