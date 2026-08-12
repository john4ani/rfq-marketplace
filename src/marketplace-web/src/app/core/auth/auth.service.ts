import { HttpClient } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, firstValueFrom, Observable, of, switchMap, tap, timeout } from 'rxjs';

export interface TokenResponse { accessToken: string; expiresAt: string; }
export interface PasswordResetRequestResponse { token: string | null; }
export interface AccountSession { userId: string; email: string; accountType: 'Buyer' | 'Supplier'; roles: string[]; }
export interface LinkedProfile { userId: string; email: string; accountType: 'Buyer' | 'Supplier'; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly accessTokenState = signal<string | null>(null);
  private readonly profilesState = signal<LinkedProfile[]>([]);
  private readonly activeProfileState = signal<'Buyer' | 'Supplier' | null>(null);
  private initialization: Promise<void> | null = null;
  private profilesLoading: Promise<void> = Promise.resolve();
  private presenceTimer: ReturnType<typeof setInterval> | null = null;
  readonly isAuthenticated = computed(() => this.accessTokenState() !== null);
  readonly isAdministrator = computed(() => this.decodeIdentity(this.accessTokenState())?.roles.includes('Administrator') ?? false);
  readonly accessToken = this.accessTokenState.asReadonly();
  readonly identity = computed(() => this.decodeIdentity(this.accessTokenState()));
  readonly session = computed(() => {
    const identity = this.decodeIdentity(this.accessTokenState());
    const accountType = this.activeProfileState();
    return identity && accountType ? { ...identity, accountType } : null;
  });
  readonly profiles = computed(() => this.profilesState().length > 0
    ? this.profilesState()
    : this.session() ? [this.session()!] : []);

  constructor() { this.destroyRef.onDestroy(() => this.stopPresence()); }

  initialize(): Promise<void> {
    this.initialization ??= firstValueFrom(this.refresh().pipe(
      timeout(2000),
      catchError(() => of(null))
    )).then(async () => this.profilesLoading);
    return this.initialization;
  }

  waitForProfiles(): Promise<void> { return this.profilesLoading; }

  registerBuyer(email: string, password: string): Observable<void> {
    return this.http.post<void>('/api/v1/auth/register/buyer', { email, password });
  }

  registerSupplier(email: string, password: string): Observable<void> {
    return this.http.post<void>('/api/v1/auth/register/supplier', { email, password });
  }

  register(email: string, password: string): Observable<void> {
    return this.http.post<void>('/api/v1/auth/register', { email, password });
  }

  addProfile(accountType: 'Buyer' | 'Supplier'): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`/api/v1/auth/profiles/${accountType}`, {}, { withCredentials: true })
      .pipe(tap(response => {
        this.activeProfileState.set(accountType);
        this.acceptToken(response.accessToken);
      }));
  }

  requestPasswordReset(email: string): Observable<PasswordResetRequestResponse | null> {
    return this.http.post<PasswordResetRequestResponse | null>('/api/v1/auth/request-password-reset', { email });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.http.post<void>('/api/v1/auth/reset-password', { email, token, newPassword });
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
    this.stopPresence();
    return this.http.delete<void>('/api/v1/piata-live/presence')
      .pipe(
        catchError(() => of(undefined)),
        switchMap(() => this.http.post<void>('/api/v1/auth/logout', {}, { withCredentials: true })),
        finalize(() => { this.accessTokenState.set(null); this.profilesState.set([]); this.activeProfileState.set(null); })
      );
  }

  private acceptToken(token: string): void {
    this.accessTokenState.set(token);
    this.startPresence();
    this.profilesLoading = firstValueFrom(this.http.get<LinkedProfile[]>('/api/v1/auth/profiles').pipe(
      tap(profiles => {
        this.profilesState.set(profiles);
        if (!profiles.some(profile => profile.accountType === this.activeProfileState()))
          this.activeProfileState.set(profiles[0]?.accountType ?? null);
      }),
      catchError(() => of<LinkedProfile[]>([]))
    )).then(() => undefined);
  }

  private startPresence(): void {
    this.markOnline();
    if (this.presenceTimer === null) this.presenceTimer = setInterval(() => this.markOnline(), 30_000);
  }
  private stopPresence(): void {
    if (this.presenceTimer !== null) clearInterval(this.presenceTimer);
    this.presenceTimer = null;
  }
  private markOnline(): void {
    this.http.post<void>('/api/v1/piata-live/presence', {}).subscribe({ error: () => undefined });
  }

  private decodeIdentity(token: string | null): Omit<AccountSession, 'accountType'> | null {
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as Record<string, string | string[]>;
      const email = String(payload['email'] ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ?? '');
      const rawRoles=payload['role']??payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      const roles=Array.isArray(rawRoles)?rawRoles:rawRoles?[String(rawRoles)]:[];
      return payload['sub'] ? { userId: String(payload['sub']), email, roles } : null;
    } catch {
      return null;
    }
  }
}
