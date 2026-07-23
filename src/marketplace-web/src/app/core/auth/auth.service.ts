import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { finalize, Observable, tap } from 'rxjs';

export interface TokenResponse { accessToken: string; expiresAt: string; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly accessTokenState = signal<string | null>(null);
  readonly isAuthenticated = computed(() => this.accessTokenState() !== null);
  readonly accessToken = this.accessTokenState.asReadonly();

  registerBuyer(email: string, password: string): Observable<void> {
    return this.http.post<void>('/api/v1/auth/register/buyer', { email, password });
  }

  registerSupplier(email: string, password: string): Observable<void> {
    return this.http.post<void>('/api/v1/auth/register/supplier', { email, password });
  }

  login(email: string, password: string, accountType: 'Buyer' | 'Supplier'): Observable<TokenResponse> {
    return this.http.post<TokenResponse>('/api/v1/auth/login', { email, password, accountType }, { withCredentials: true })
      .pipe(tap(response => this.accessTokenState.set(response.accessToken)));
  }

  refresh(): Observable<TokenResponse> {
    return this.http.post<TokenResponse>('/api/v1/auth/refresh', {}, { withCredentials: true })
      .pipe(tap(response => this.accessTokenState.set(response.accessToken)));
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/v1/auth/logout', {}, { withCredentials: true })
      .pipe(finalize(() => this.accessTokenState.set(null)));
  }
}
