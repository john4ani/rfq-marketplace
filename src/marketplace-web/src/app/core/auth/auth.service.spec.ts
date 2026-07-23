import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  it('clears the access token when signing out', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);

    service.login('buyer@example.test', 'password', 'Buyer').subscribe();
    http.expectOne('/api/v1/auth/login').flush({ accessToken: 'access-token', expiresAt: new Date().toISOString() });
    expect(service.isAuthenticated()).toBe(true);

    service.logout().subscribe();
    http.expectOne('/api/v1/auth/logout').flush(null);

    expect(service.isAuthenticated()).toBe(false);
    http.verify();
  });
});
