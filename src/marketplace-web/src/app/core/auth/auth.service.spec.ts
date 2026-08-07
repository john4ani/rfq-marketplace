import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  function token(payload: object): string {
    return `header.${btoa(JSON.stringify(payload)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_')}.signature`;
  }

  it('clears the access token when signing out', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);

    service.login('buyer@example.test', 'password').subscribe();
    http.expectOne('/api/v1/auth/login').flush({ accessToken: 'access-token', expiresAt: new Date().toISOString() });
    http.expectOne('/api/v1/piata-live/presence').flush(null);
    http.expectOne('/api/v1/auth/profiles').flush([]);
    expect(service.isAuthenticated()).toBe(true);

    service.logout().subscribe();
    http.expectOne('/api/v1/piata-live/presence').flush(null);
    http.expectOne('/api/v1/auth/logout').flush(null);

    expect(service.isAuthenticated()).toBe(false);
    http.verify();
  });

  it('switches locally between the profiles available to one identity', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const accessToken = token({ sub: '11111111-1111-1111-1111-111111111111', email: 'same@example.test', role: 'Supplier' });

    service.login('same@example.test', 'password').subscribe();
    http.expectOne('/api/v1/auth/login').flush({ accessToken, expiresAt: new Date().toISOString() });
    http.expectOne('/api/v1/piata-live/presence').flush(null);
    http.expectOne('/api/v1/auth/profiles').flush([
      { userId: '11111111-1111-1111-1111-111111111111', email: 'same@example.test', accountType: 'Buyer' },
      { userId: '11111111-1111-1111-1111-111111111111', email: 'same@example.test', accountType: 'Supplier' }
    ]);

    expect(service.session()?.accountType).toBe('Buyer');
    service.switchProfile('Supplier');
    expect(service.session()?.accountType).toBe('Supplier');
    http.expectNone('/api/v1/auth/switch-profile');
    http.verify();
  });

  it('adds and activates a missing profile with the refreshed access token', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const buyerToken = token({ sub: '11111111-1111-1111-1111-111111111111', email: 'same@example.test', role: 'Buyer' });
    const dualToken = token({ sub: '11111111-1111-1111-1111-111111111111', email: 'same@example.test', role: ['Buyer', 'Supplier'] });

    service.login('same@example.test', 'password').subscribe();
    http.expectOne('/api/v1/auth/login').flush({ accessToken: buyerToken, expiresAt: new Date().toISOString() });
    http.expectOne('/api/v1/piata-live/presence').flush(null);
    http.expectOne('/api/v1/auth/profiles').flush([
      { userId: '11111111-1111-1111-1111-111111111111', email: 'same@example.test', accountType: 'Buyer' }
    ]);

    service.addProfile('Supplier').subscribe();
    const addition = http.expectOne('/api/v1/auth/profiles/Supplier');
    expect(addition.request.method).toBe('POST');
    expect(addition.request.withCredentials).toBe(true);
    addition.flush({ accessToken: dualToken, expiresAt: new Date().toISOString() });
    http.expectOne('/api/v1/piata-live/presence').flush(null);
    http.expectOne('/api/v1/auth/profiles').flush([
      { userId: '11111111-1111-1111-1111-111111111111', email: 'same@example.test', accountType: 'Buyer' },
      { userId: '11111111-1111-1111-1111-111111111111', email: 'same@example.test', accountType: 'Supplier' }
    ]);

    expect(service.session()?.accountType).toBe('Supplier');
    expect(service.profiles().map(profile => profile.accountType)).toEqual(['Buyer', 'Supplier']);
    http.verify();
  });

  it('requests and completes password recovery without changing session state', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);

    service.requestPasswordReset('buyer@example.test').subscribe(response =>
      expect(response?.token).toBe('development-token'));
    const request = http.expectOne('/api/v1/auth/request-password-reset');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'buyer@example.test' });
    request.flush({ token: 'development-token' });

    service.resetPassword('buyer@example.test', 'development-token', 'new-password').subscribe();
    const reset = http.expectOne('/api/v1/auth/reset-password');
    expect(reset.request.method).toBe('POST');
    expect(reset.request.body).toEqual({
      email: 'buyer@example.test',
      token: 'development-token',
      newPassword: 'new-password'
    });
    reset.flush(null);

    expect(service.isAuthenticated()).toBe(false);
    http.verify();
  });

  it('recognizes the Administrator role from the access token', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const accessToken = token({
      sub: '11111111-1111-1111-1111-111111111111',
      email: 'admin@example.test',
      role: ['Buyer', 'Administrator']
    });

    service.login('admin@example.test', 'password').subscribe();
    http.expectOne('/api/v1/auth/login').flush({ accessToken, expiresAt: new Date().toISOString() });
    http.expectOne('/api/v1/piata-live/presence').flush(null);
    http.expectOne('/api/v1/auth/profiles').flush([
      { userId: '11111111-1111-1111-1111-111111111111', email: 'admin@example.test', accountType: 'Buyer' }
    ]);

    expect(service.isAdministrator()).toBe(true);
    http.verify();
  });
});
