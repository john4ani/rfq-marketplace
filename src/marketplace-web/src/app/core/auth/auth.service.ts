import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { finalize, Observable, tap } from 'rxjs';

export interface TokenResponse { accessToken: string; expiresAt: string; }
export interface AccountSession { userId: string; email: string; accountType: 'Buyer' | 'Supplier'; }
export interface LinkedProfile { userId: string; email: string; accountType: 'Buyer' | 'Supplier'; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly accessTokenState = signal<string | null>(null);
  private readonly profilesState = signal<LinkedProfile[]>([]);
  private readonly activeProfileState = signal<'Buyer' | 'Supplier' | null>(null);
  readonly isAuthenticated = computed(() => this.accessTokenState() !== null);
  readonly accessToken = this.accessTokenState.asReadonly();
  readonly session = computed(() => {
    const identity = this.decodeIdentity(this.accessTokenState());
    const accountType = this.activeProfileState();
    return identity && accountType ? { ...identity, accountType } : null;
  });
  readonly profiles = computed(() => this.profilesState().length > 0
    ? this.profilesState()
    : this.session() ? [this.session()!] : []);

  registerBuyer(email: string, password: string): Observable<void> {
    return this.http.post<void>('/api/v1/auth/register/buyer', { email, password });
  }

  registerSupplier(email: string, password: string): Observable<void> {
    return this.http.post<void>('/api/v1/auth/register/supplier', { email, password });
  }

  register(email: string, password: string, accountTypes: ('Buyer' | 'Supplier')[]): Observable<void> {
    return this.http.post<void>('/api/v1/auth/register', { email, password, accountTypes });
  }

  login(email: string, password: string): Observable<TokenResponse> {
    return this.http.post<TokenResponse>('/api/v1/auth/login', { email, password }, { withCredentials: true })
      .pipe(tap(response => this.acceptToken(response.accessToken)));
  }

  refresh(): Observable<TokenResponse> {
    return this.http.post<TokenResponse>('/api/v1/auth/refresh', {}, { withCredentials: true })
      .pipe(tap(response => this.acceptToken(response.accessToken)));
  }

  switchProfile(accountType: 'Buyer' | 'Supplier'): void {
    if (this.profiles().some(profile => profile.accountType === accountType))
      this.activeProfileState.set(accountType);
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/v1/auth/logout', {}, { withCredentials: true })
      .pipe(finalize(() => { this.accessTokenState.set(null); this.profilesState.set([]); this.activeProfileState.set(null); }));
  }

  private acceptToken(token: string): void {
    this.accessTokenState.set(token);
    this.http.get<LinkedProfile[]>('/api/v1/auth/profiles').subscribe({
      next: profiles => {
        this.profilesState.set(profiles);
        if (!profiles.some(profile => profile.accountType === this.activeProfileState()))
          this.activeProfileState.set(profiles[0]?.accountType ?? null);
      }
    });
  }

  private decodeIdentity(token: string | null): Omit<AccountSession, 'accountType'> | null {
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as Record<string, string | string[]>;
      const email = String(payload['email'] ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ?? '');
      return payload['sub'] ? { userId: String(payload['sub']), email } : null;
    } catch {
      return null;
    }
  }
}
